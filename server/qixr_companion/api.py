import asyncio
from contextlib import asynccontextmanager
import io
import os
from pathlib import Path
import secrets
import time
import wave

from fastapi import FastAPI, Header, HTTPException, Request, WebSocket, WebSocketDisconnect
from fastapi.responses import JSONResponse, Response
from fastapi.exceptions import RequestValidationError
from fastapi.exception_handlers import request_validation_exception_handler

from .contracts import EpochChange, Join, PasswordLogin, PasswordChange, Question, QuantumEvent, RoomOptions, Snapshot
from .passwords import LoginLimiter, hash_password, verify_password
from .dialogue import Planner
from .service import Service
from .store import Store, authorized, digest
from .voice import Voice


def create_app(runtime=None, planner=None, voice=None):
    root = Path(runtime or os.environ.get('QIXR_RUNTIME', str(Path(__file__).resolve().parents[2]/'.runtime'))).resolve()
    root.mkdir(parents=True, exist_ok=True, mode=0o700)
    secret_path = root/'admin.token'
    if not secret_path.exists():
        fd = os.open(secret_path, os.O_CREAT|os.O_WRONLY|os.O_EXCL, 0o600)
        with os.fdopen(fd,'w') as f: f.write(secrets.token_urlsafe(32))
    admin_hash = digest(secret_path.read_text().strip())
    store = Store(root/'rooms.sqlite3', max_clients=int(os.getenv('QIXR_MAX_CLIENTS','8')))
    planner = planner or Planner(os.getenv('QIXR_OLLAMA_URL','http://127.0.0.1:11434'), os.getenv('QIXR_MODEL','qwen3:8b'))
    voice = voice or Voice(root/'models', os.getenv('QIXR_VOICE','1') == '1')
    service = Service(store, planner, voice, int(os.getenv('QIXR_PARALLEL','2')), int(os.getenv('QIXR_QUEUE','8')))
    asr_semaphore, asr_pending = asyncio.Semaphore(1), set()
    password_semaphore, password_limiter = asyncio.Semaphore(2), LoginLimiter()
    password_pending = 0

    @asynccontextmanager
    async def lifespan(app):
        await asyncio.to_thread(voice.load)
        yield
        await service.close()
        store.db.close()

    app = FastAPI(title='QIXR Quantum Companion', version='0.1.0', lifespan=lifespan)
    app.state.store, app.state.service, app.state.voice = store, service, voice

    def fail(e):
        code = 429 if isinstance(e, OverflowError) else 403 if isinstance(e, PermissionError) else 404 if isinstance(e, LookupError) else 409
        raise HTTPException(code, str(e))

    def bearer(value): return value[7:] if value and value.startswith('Bearer ') else ''

    def room_auth(room_id, value, role='authority'):
        room = store.rooms.get(room_id)
        if room is None: raise HTTPException(404, 'Room not found')
        hashed = room.authorityHash if role == 'authority' else room.joinHash
        if not authorized(bearer(value), hashed): raise HTTPException(401, 'Invalid room credential')
        return room

    def conv_auth(cid, value):
        c = store.conversations.get(cid)
        if c is None: raise HTTPException(404, 'Conversation not found')
        if c.passwordHashAtJoin is not None and c.passwordHashAtJoin != store.rooms[c.roomId].passwordHash:
            raise HTTPException(401, 'Password changed; sign in again')
        if time.monotonic()-c.lastActive > 120 or not authorized(bearer(value), c.tokenHash):
            raise HTTPException(401, 'Invalid or expired conversation credential')
        c.lastActive = time.monotonic()
        return c

    def job_auth(cid, rid, value):
        conv_auth(cid, value)
        job = service.jobs.get((cid,rid))
        if job is None: raise HTTPException(404, 'Request not found')
        return job

    @app.exception_handler(ValueError)
    async def invalid(_, exc): return JSONResponse(status_code=422, content={'detail':str(exc)[:300]})

    @app.exception_handler(RequestValidationError)
    async def invalid_request(request, exc):
        if request.url.path.endswith(('/password-conversations', '/password')):
            return JSONResponse(status_code=422, content={'detail':'Invalid password request'})
        return await request_validation_exception_handler(request, exc)

    @app.middleware('http')
    async def limit_body(request: Request, call_next):
        # JSON and WAV bodies are both bounded, including chunked uploads.
        if request.method in ['POST','PUT','PATCH']:
            size = 0
            chunks = []
            async for chunk in request.stream():
                size += len(chunk)
                if size > 1_048_576: return JSONResponse(status_code=413, content={'detail':'Payload limit is 1 MiB'})
                chunks.append(chunk)
            request._body = b''.join(chunks)
        return await call_next(request)

    @app.get('/healthz')
    async def health(): return {'status':'ok','protocolVersion':1}

    @app.get('/readyz')
    async def ready():
        model = await planner.ready()
        return {'status':'ready' if model else 'degraded', 'model':planner.model, 'modelReady':model,
                'asrReady':voice.asr is not None, 'ttsLanguages':list(voice.tts),
                'voiceError':voice.error, 'parallel':service.parallel, 'maxClientsPerRoom':store.max_clients}

    @app.get('/v1/admin/metrics')
    async def metrics(authorization: str=Header(default='')):
        if not authorized(bearer(authorization), admin_hash): raise HTTPException(401)
        return dict(**service.metrics, active=sum(j.state in ['queued','running','speaking'] for j in service.jobs.values()), conversations=len(store.conversations))

    @app.post('/v1/rooms')
    async def create_room(options: RoomOptions, authorization: str=Header(default='')):
        if not authorized(bearer(authorization), admin_hash): raise HTTPException(401)
        try: room, authority, join = store.create_room(options.name, options.allowShared)
        except OverflowError as e: fail(e)
        return {'roomId':room.id,'authorityToken':authority,'joinToken':join,'authorityEpoch':room.epoch}

    @app.post('/v1/rooms/{rid}/epoch')
    async def epoch(rid: str, data: EpochChange, authorization: str=Header(default='')):
        room = room_auth(rid, authorization)
        if data.expectedEpoch != room.epoch: raise HTTPException(409,'Epoch changed; fetch room status before resetting')
        room.epoch += 1; room.snapshots.clear(); room.events.clear(); room.latestId=None; room.eventSequence=0
        store.save(room)
        for c in list(store.conversations.values()):
            if c.roomId == rid:
                service.cancel(c)
                c.history.clear()
                await service.notify(c, {'type':'authority_reset','authorityEpoch':room.epoch})
        return {'authorityEpoch':room.epoch}

    @app.delete('/v1/rooms/{rid}')
    async def delete_room(rid: str, authorization: str=Header(default='')):
        if not authorized(bearer(authorization), admin_hash): raise HTTPException(401)
        if rid not in store.rooms: raise HTTPException(404)
        for c in list(store.conversations.values()):
            if c.roomId==rid:
                service.cancel(c)
                await service.notify(c, {'type':'room_closed'})
                store.conversations.pop(c.id,None)
        store.rooms.pop(rid)
        store.db.execute('DELETE FROM rooms WHERE id=?',(rid,)); store.db.commit()
        return {'status':'deleted'}

    @app.get('/v1/rooms/{rid}')
    async def room_status(rid: str, authorization: str=Header(default='')):
        room = room_auth(rid, authorization)
        return {'roomId':rid, 'authorityEpoch':room.epoch, 'latestSnapshotId':room.latestId, 'eventSequence':room.eventSequence}

    @app.put('/v1/rooms/{rid}/password')
    async def change_password(rid: str, data: PasswordChange, authorization: str=Header(default='')):
        if not authorized(bearer(authorization), admin_hash): raise HTTPException(401)
        room = store.rooms.get(rid)
        if room is None: raise HTTPException(404)
        room.passwordHash = await asyncio.to_thread(hash_password, data.password.get_secret_value())
        store.save(room)
        for c in list(store.conversations.values()):
            if c.roomId == rid and c.passwordHashAtJoin is not None:
                service.cancel(c)
                store.conversations.pop(c.id, None)
        return {'status':'password_updated'}

    @app.post('/v1/rooms/{rid}/password-conversations')
    async def password_login(rid: str, data: PasswordLogin, request: Request):
        nonlocal password_pending
        peer = request.client.host if request.client else 'unknown'
        if not password_limiter.allow(peer) or password_pending >= 8:
            raise HTTPException(429, 'Too many sign-in attempts; wait before retrying', headers={'Retry-After':'60'})
        password_pending += 1
        try:
            room = store.rooms.get(rid)
            encoded = room.passwordHash if room else None
            async with password_semaphore:
                accepted = await asyncio.to_thread(verify_password, data.password.get_secret_value(), encoded)
            if not accepted or room is None or room.passwordHash != encoded:
                raise HTTPException(401, 'Incorrect password or unavailable room')
            try: c, token = store.join(room, data.participantId)
            except OverflowError as e: fail(e)
            c.passwordHashAtJoin = encoded
            return {'conversationId':c.id, 'conversationToken':token,'authorityEpoch':room.epoch}
        finally:
            password_pending -= 1

    @app.post('/v1/rooms/{rid}/snapshots')
    async def snapshot(rid: str, data: Snapshot, authorization: str=Header(default='')):
        room = room_auth(rid, authorization)
        try: room.put_snapshot(data)
        except ValueError as e: fail(e)
        return {'snapshotId':data.snapshotId, 'authorityEpoch':room.epoch}

    @app.post('/v1/rooms/{rid}/events')
    async def event(rid: str, data: QuantumEvent, authorization: str=Header(default='')):
        room = room_auth(rid, authorization)
        try: room.put_event(data)
        except ValueError as e: fail(e)
        return {'eventId':data.eventId,'sequence':data.sequence}

    @app.post('/v1/rooms/{rid}/conversations')
    async def join_room(rid: str, data: Join, authorization: str=Header(default='')):
        room = room_auth(rid, authorization, 'join')
        try: c, token = store.join(room, data.participantId)
        except OverflowError as e: fail(e)
        return {'conversationId':c.id,'conversationToken':token,'authorityEpoch':room.epoch}

    @app.get('/v1/conversations/{cid}/context')
    async def context(cid: str, authorization: str=Header(default='')):
        c = conv_auth(cid, authorization); room=store.rooms[c.roomId]
        if room.latestId is None: raise HTTPException(409,'Waiting for state authority')
        return {'snapshot':room.snapshots[room.latestId], 'lastEventId':next(reversed(room.events),None)}

    @app.get('/v1/conversations/{cid}/messages')
    async def messages(cid: str, after: int=0, authorization: str=Header(default='')):
        c=conv_auth(cid,authorization)
        return {'messages':[m for m in c.messages if m['messageSequence']>after], 'cursor':c.messageSequence}

    @app.get('/v1/conversations/{cid}/shared/{speaker}/{qid}/audio/{index}')
    async def shared_audio(cid: str, speaker: str, qid: str, index: int, authorization: str=Header(default='')):
        c=conv_auth(cid,authorization)
        owner=store.conversations.get(speaker); job=service.jobs.get((speaker,qid))
        if owner is None or owner.roomId!=c.roomId or job is None or job.question.visibility!='room': raise HTTPException(404)
        if index<0 or index>=len(job.audio): raise HTTPException(404)
        return Response(job.audio[index],media_type='audio/wav',headers={'Cache-Control':'no-store'})

    @app.post('/v1/conversations/{cid}/questions', status_code=202)
    async def question(cid: str, data: Question, authorization: str=Header(default='')):
        c=conv_auth(cid, authorization)
        try: return service.submit(c,data).public()
        except (ValueError,LookupError,OverflowError,PermissionError) as e: fail(e)

    @app.get('/v1/conversations/{cid}/requests/{qid}')
    async def get_request(cid: str, qid: str, authorization: str=Header(default='')):
        return job_auth(cid,qid,authorization).public()

    @app.get('/v1/conversations/{cid}/requests/{qid}/audio/{index}')
    async def get_audio(cid: str, qid: str, index: int, authorization: str=Header(default='')):
        job=job_auth(cid,qid,authorization)
        if index < 0 or index >= len(job.audio): raise HTTPException(404)
        return Response(job.audio[index], media_type='audio/wav', headers={'Cache-Control':'no-store'})

    @app.post('/v1/conversations/{cid}/cancel')
    async def cancel(cid: str, authorization: str=Header(default='')):
        c=conv_auth(cid,authorization); service.cancel(c)
        await service.notify(c, {'type':'cancelled','generation':c.generation})
        return {'generation':c.generation}

    @app.delete('/v1/conversations/{cid}')
    async def leave(cid: str, authorization: str=Header(default='')):
        c=conv_auth(cid,authorization); service.cancel(c)
        store.conversations.pop(cid,None)
        return {'status':'left'}

    @app.post('/v1/conversations/{cid}/transcribe')
    async def transcribe(cid: str, request: Request, language: str='en', authorization: str=Header(default='')):
        conv_auth(cid,authorization)
        if language not in ['ko','en']: raise HTTPException(422,'Unsupported language')
        # One native ASR call at a time, with one pending utterance per participant.
        if cid in asr_pending or len(asr_pending)>=8: raise HTTPException(429,'ASR queue is full')
        asr_pending.add(cid)
        try:
            data=await request.body()
            async with asr_semaphore:
                text=await voice.run(voice.transcribe_sync,data,language)
            conv_auth(cid,authorization)
            return {'text':text,'language':language}
        except RuntimeError as e: raise HTTPException(503,str(e))
        except (wave.Error,EOFError,ValueError) as e: raise HTTPException(422,str(e))
        finally: asr_pending.discard(cid)

    @app.websocket('/v1/conversations/{cid}/stream')
    async def stream(ws: WebSocket, cid: str):
        await ws.accept()
        queue=asyncio.Queue(maxsize=32)
        c=None; sender=None
        async def send():
            while True: await ws.send_json(await queue.get())
        try:
            first=await asyncio.wait_for(ws.receive_json(),5)
            if first.get('type') != 'auth' or first.get('protocolVersion') != 1: raise ValueError('Protocol 1 authentication required')
            c=conv_auth(cid,'Bearer '+first.get('token',''))
            if len(service.listeners.get(cid,[])) >= 2: raise ValueError('Stream limit reached')
            service.listeners.setdefault(cid,[]).append(queue)
            sender=asyncio.create_task(send())
            await queue.put({'type':'ready','protocolVersion':1,'conversationId':cid})
            while True:
                message=await asyncio.wait_for(ws.receive_json(),timeout=300)
                conv_auth(cid,'Bearer '+first.get('token',''))
                if message.get('type') == 'question':
                    try: job=service.submit(c,Question.model_validate(message['question'])); await queue.put({'type':'accepted',**job.public()})
                    except (ValueError,LookupError,OverflowError,PermissionError) as e: await queue.put({'type':'rejected','message':str(e)})
                elif message.get('type') == 'cancel':
                    service.cancel(c); await queue.put({'type':'cancelled','generation':c.generation})
                elif message.get('type') == 'ping': await queue.put({'type':'pong'})
                else: await queue.put({'type':'rejected','message':'Unsupported message type'})
        except (WebSocketDisconnect, asyncio.TimeoutError): pass
        except (ValueError,HTTPException): await ws.close(code=1008)
        finally:
            if sender: sender.cancel(); await asyncio.gather(sender,return_exceptions=True)
            if c:
                if queue in service.listeners.get(cid,[]): service.listeners[cid].remove(queue)

    return app


app = create_app()

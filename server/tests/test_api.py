import asyncio
from contextlib import asynccontextmanager
from pathlib import Path

import httpx
import pytest
from fastapi.testclient import TestClient

from qixr_companion.api import create_app
from qixr_companion.contracts import Plan
from qixr_companion.dialogue import quick_plan
from qixr_companion.voice import Voice
from conftest import state


class TestPlanner:
    model='test-planner'
    __test__=False
    async def ready(self): return True
    async def plan(self,q,history):
        if q.text=='slow': await asyncio.sleep(.1)
        return Plan(intent='probability'), 'test', {}


@asynccontextmanager
async def client_at(path, planner=None, voice=None):
    app=create_app(path,planner or TestPlanner(),voice or Voice(path,False))
    async with app.router.lifespan_context(app):
        async with httpx.AsyncClient(transport=httpx.ASGITransport(app=app),base_url='http://test') as c:
            yield c,app


def auth(token): return {'Authorization':'Bearer '+token}


async def setup(c,path):
    r=await c.post('/v1/rooms',headers=auth((path/'admin.token').read_text()),json={'allowShared':True})
    assert r.status_code==200,r.text
    room=r.json(); rid=room['roomId']; ah=auth(room['authorityToken'])
    r=await c.post(f'/v1/rooms/{rid}/epoch',headers=ah,json={'expectedEpoch':0}); assert r.status_code==200
    s=state('bell').model_copy(update={'roomId':rid})
    r=await c.post(f'/v1/rooms/{rid}/snapshots',headers=ah,json=s.model_dump()); assert r.status_code==200,r.text
    joined=(await c.post(f'/v1/rooms/{rid}/conversations',headers=auth(room['joinToken']),json={'participantId':'one'})).json()
    return room,joined,s


async def wait(c,path,headers):
    for _ in range(100):
        r=(await c.get(path,headers=headers)).json()
        if r['state'] in ['done','error','cancelled']: return r
        await asyncio.sleep(.005)
    raise AssertionError('Turn did not complete')


@pytest.mark.asyncio
async def test_auth_roles_isolation_and_real_turn(tmp_path):
    async with client_at(tmp_path) as (c,app):
        room,one,s=await setup(c,tmp_path)
        rid=room['roomId']; cid=one['conversationId']; h=auth(one['conversationToken'])
        assert (await c.post('/v1/rooms',json={})).status_code==401
        assert (await c.post(f'/v1/rooms/{rid}/snapshots',headers=auth(room['joinToken']),json=s.model_dump())).status_code==401
        two=(await c.post(f'/v1/rooms/{rid}/conversations',headers=auth(room['joinToken']),json={'participantId':'two'})).json()
        q=dict(requestId='r1',selectionId='sel',referenceSnapshotId=s.snapshotId,targetQubitIds=[0],text='probability',language='en')
        r=await c.post(f'/v1/conversations/{cid}/questions',headers=h,json=q); assert r.status_code==202,r.text
        result=await wait(c,f'/v1/conversations/{cid}/requests/r1',h)
        assert result['state']=='done'; assert '50.0%' in result['result']['text']
        assert result['result']['referenceSnapshotId']==s.snapshotId
        assert (await c.get(f'/v1/conversations/{cid}/requests/r1',headers=auth(two['conversationToken']))).status_code==401
        assert (await c.get(f'/v1/conversations/{two["conversationId"]}/requests/r1',headers=auth(two['conversationToken']))).status_code==404
        r=await c.post(f'/v1/conversations/{cid}/questions',headers=h,json=q); assert r.status_code==202
        q['text']='changed'; assert (await c.post(f'/v1/conversations/{cid}/questions',headers=h,json=q)).status_code==409


@pytest.mark.asyncio
async def test_epoch_reset_cancels_turn_and_rejects_old_snapshot(tmp_path):
    async with client_at(tmp_path) as (c,app):
        room,one,s=await setup(c,tmp_path); cid=one['conversationId']; h=auth(one['conversationToken']); rid=room['roomId']
        q=dict(requestId='r',selectionId='sel',referenceSnapshotId=s.snapshotId,targetQubitIds=[0],text='slow')
        await c.post(f'/v1/conversations/{cid}/questions',headers=h,json=q)
        await asyncio.sleep(.01)
        await c.post(f'/v1/rooms/{rid}/epoch',headers=auth(room['authorityToken']),json={'expectedEpoch':1})
        result=await wait(c,f'/v1/conversations/{cid}/requests/r',h)
        assert result['state']=='cancelled'
        assert (await c.post(f'/v1/rooms/{rid}/snapshots',headers=auth(room['authorityToken']),json=s.model_dump())).status_code==409


@pytest.mark.asyncio
async def test_new_question_supersedes_pending_answer(tmp_path):
    async with client_at(tmp_path) as (c,app):
        room,one,s=await setup(c,tmp_path); cid=one['conversationId']; h=auth(one['conversationToken'])
        for rid,text in [('old','slow'),('new','probability')]:
            q=dict(requestId=rid,selectionId=rid,referenceSnapshotId=s.snapshotId,targetQubitIds=[0],text=text)
            await c.post(f'/v1/conversations/{cid}/questions',headers=h,json=q)
            await asyncio.sleep(.01)
        old=await wait(c,f'/v1/conversations/{cid}/requests/old',h)
        new=await wait(c,f'/v1/conversations/{cid}/requests/new',h)
        assert old['state']=='cancelled' and new['state']=='done'
        assert new['result']['selectionId']=='new'


@pytest.mark.asyncio
async def test_immutable_snapshot_and_client_limit(tmp_path):
    async with client_at(tmp_path) as (c,app):
        room,one,s=await setup(c,tmp_path); rid=room['roomId']
        bad=s.model_copy(update={'stateVersion':2})
        assert (await c.post(f'/v1/rooms/{rid}/snapshots',headers=auth(room['authorityToken']),json=bad.model_dump())).status_code==409
        app.state.store.max_clients=1
        assert (await c.post(f'/v1/rooms/{rid}/conversations',headers=auth(room['joinToken']),json={'participantId':'two'})).status_code==429
        q=dict(requestId='r',selectionId='s',referenceSnapshotId='missing',text='x')
        assert (await c.post(f'/v1/conversations/{one["conversationId"]}/questions',headers=auth(one['conversationToken']),json=q)).status_code==404


def test_websocket_authentication(tmp_path):
    app=create_app(tmp_path,TestPlanner(),Voice(tmp_path,False))
    with TestClient(app) as c:
        room=c.post('/v1/rooms',headers=auth((tmp_path/'admin.token').read_text()),json={}).json()
        joined=c.post(f'/v1/rooms/{room["roomId"]}/conversations',headers=auth(room['joinToken']),json={'participantId':'ws'}).json()
        with c.websocket_connect(f'/v1/conversations/{joined["conversationId"]}/stream') as ws:
            ws.send_json({'type':'auth','protocolVersion':1,'token':joined['conversationToken']})
            assert ws.receive_json()['type']=='ready'
            ws.send_json({'type':'ping'}); assert ws.receive_json()['type']=='pong'


@pytest.mark.asyncio
async def test_shared_mode_is_scoped_and_private_messages_stay_private(tmp_path):
    async with client_at(tmp_path) as (c,app):
        room,one,s=await setup(c,tmp_path); cid=one['conversationId']; h=auth(one['conversationToken'])
        other=(await c.post(f'/v1/rooms/{room["roomId"]}/conversations',headers=auth(room['joinToken']),json={'participantId':'other'})).json()
        q=dict(requestId='private',selectionId='sel',referenceSnapshotId=s.snapshotId,targetQubitIds=[0],text='probability')
        await c.post(f'/v1/conversations/{cid}/questions',headers=h,json=q)
        await wait(c,f'/v1/conversations/{cid}/requests/private',h)
        def url(member): return '/v1/conversations/'+member['conversationId']+'/messages'
        assert (await c.get(url(other),headers=auth(other['conversationToken']))).json()['messages']==[]
        q.update(requestId='shared',visibility='room')
        await c.post(f'/v1/conversations/{cid}/questions',headers=h,json=q)
        await wait(c,f'/v1/conversations/{cid}/requests/shared',h)
        messages=(await c.get(url(other),headers=auth(other['conversationToken']))).json()['messages']
        assert any(m['type']=='answer' and m['result']['visibility']=='room' for m in messages)
        assert not any(m.get('requestId')=='private' for m in messages)
        job=app.state.service.jobs[(cid,'shared')]; job.audio.append(b'test-audio')
        endpoint=f'/v1/conversations/{other["conversationId"]}/shared/{cid}/shared/audio/0'
        assert (await c.get(endpoint,headers=auth(other['conversationToken']))).content==b'test-audio'


@pytest.mark.asyncio
async def test_bounded_queue_and_payload(tmp_path):
    async with client_at(tmp_path) as (c,app):
        room,one,s=await setup(c,tmp_path); cid=one['conversationId']; h=auth(one['conversationToken'])
        app.state.service.parallel=1; app.state.service.queue_size=0
        q=dict(requestId='one',selectionId='s',referenceSnapshotId=s.snapshotId,targetQubitIds=[0],text='slow')
        assert (await c.post(f'/v1/conversations/{cid}/questions',headers=h,json=q)).status_code==202
        other=(await c.post(f'/v1/rooms/{room["roomId"]}/conversations',headers=auth(room['joinToken']),json={'participantId':'other'})).json()
        response=await c.post(f'/v1/conversations/{other["conversationId"]}/questions',headers=auth(other['conversationToken']),json=q)
        assert response.status_code==429
        assert (await c.post('/v1/rooms',content=b'x'*1_048_577)).status_code==413


@pytest.mark.asyncio
async def test_bad_audio_and_expired_session(tmp_path):
    async with client_at(tmp_path) as (c,app):
        room,one,s=await setup(c,tmp_path); cid=one['conversationId']; h=auth(one['conversationToken'])
        response=await c.post(f'/v1/conversations/{cid}/transcribe',headers=h,content=b'not wav')
        assert response.status_code==503 # voice explicitly disabled in this test
        app.state.store.conversations[cid].lastActive-=121
        assert (await c.get(f'/v1/conversations/{cid}/context',headers=h)).status_code==401


class ExperiencePlanner(TestPlanner):
    async def plan(self, q, history):
        return quick_plan(q.text, len(q.targetQubitIds)) or Plan(intent='unsupported'), 'offline-rules', {}


@pytest.mark.asyncio
async def test_default_english_followup_and_private_shared_memory_boundary(tmp_path):
    async with client_at(tmp_path, ExperiencePlanner()) as (c, app):
        room, one, s = await setup(c, tmp_path)
        cid=one['conversationId']; h=auth(one['conversationToken'])
        q=dict(requestId='brief', selectionId='sel', referenceSnapshotId=s.snapshotId,
               targetQubitIds=[0], text='Explain this arrow')  # No language: English.
        path=f'/v1/conversations/{cid}'
        assert (await c.post(path+'/questions', headers=h, json=q)).status_code==202
        brief=(await wait(c,path+'/requests/brief',h))['result']
        assert brief['responseLanguage']=='en' and 'purity' not in brief['text']
        q.update(requestId='more',text='Tell me more')
        await c.post(path+'/questions', headers=h, json=q)
        more=(await wait(c,path+'/requests/more',h))['result']
        assert more['expanded'] and 'purity' in more['text']
        q.update(requestId='shared', visibility='room')
        await c.post(path+'/questions', headers=h, json=q)
        shared=(await wait(c,path+'/requests/shared',h))['result']
        assert shared['status']=='history_unavailable' and not shared.get('expanded')
        assert 'purity' not in shared['text']


@pytest.mark.asyncio
async def test_stop_produces_no_speech_segments_or_audio(tmp_path):
    voice=Voice(tmp_path,False)
    voice.tts['en']=object()
    def forbidden(*args): raise AssertionError('Quiet turn reached the speech engine')
    voice.speak_sync=forbidden
    async with client_at(tmp_path, ExperiencePlanner(), voice) as (c, app):
        room,one,s=await setup(c,tmp_path); cid=one['conversationId']; h=auth(one['conversationToken'])
        q=dict(requestId='quiet',selectionId='s',referenceSnapshotId=s.snapshotId,text='Let me try',speak=True)
        await c.post(f'/v1/conversations/{cid}/questions',headers=h,json=q)
        result=await wait(c,f'/v1/conversations/{cid}/requests/quiet',h)
        assert result['state']=='done' and result['result']['status']=='quiet'
        assert result['audioCount']==0 and result['result']['segments']==[]


@pytest.mark.asyncio
async def test_cancel_before_caption_does_not_commit_history(tmp_path):
    async with client_at(tmp_path) as (c,app):
        room,one,s=await setup(c,tmp_path); cid=one['conversationId']; h=auth(one['conversationToken'])
        q=dict(requestId='slow',selectionId='s',referenceSnapshotId=s.snapshotId,text='slow',targetQubitIds=[0])
        await c.post(f'/v1/conversations/{cid}/questions',headers=h,json=q)
        await asyncio.sleep(.01)
        await c.post(f'/v1/conversations/{cid}/cancel',headers=h,json={})
        await wait(c,f'/v1/conversations/{cid}/requests/slow',h)
        assert app.state.store.conversations[cid].history==[]

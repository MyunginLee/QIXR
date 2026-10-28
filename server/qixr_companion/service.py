import asyncio
import copy
from dataclasses import dataclass, field
import re
import time

from .agent import QuantumAgent, remember
from .physics import facts


@dataclass
class Job:
    question: object
    conversationId: str
    generation: int
    state: str = 'queued'
    result: dict | None = None
    task: object = None
    audio: list = field(default_factory=list)
    created: float = field(default_factory=time.monotonic)

    def public(self):
        return dict(requestId=self.question.requestId, generation=self.generation, state=self.state,
                    result=self.result, audioCount=len(self.audio))


class Service:
    def __init__(self, store, planner, voice, parallel=2, queue_size=8):
        self.store, self.planner, self.voice = store, planner, voice
        self.agent = QuantumAgent(planner)
        self.parallel, self.queue_size = parallel, queue_size
        self.semaphore = asyncio.Semaphore(parallel)
        self.jobs, self.listeners = {}, {}
        self.metrics = dict(submitted=0, completed=0, cancelled=0, rejected=0, errors=0)

    def cancel(self, c):
        c.generation += 1
        for job in self.jobs.values():
            if job.conversationId == c.id and job.state in ['queued','running','speaking']:
                job.state = 'cancelled'
                if job.task: job.task.cancel()

    async def notify(self, c, message, shared=False):
        recipients = [v for v in self.store.conversations.values() if v.roomId == c.roomId] if shared else [c]
        for recipient in recipients:
            recipient.messageSequence += 1
            delivery = copy.deepcopy(dict(message, messageSequence=recipient.messageSequence, speakerConversationId=c.id))
            recipient.messages.append(delivery)
            recipient.messages[:] = recipient.messages[-64:]
            for queue in list(self.listeners.get(recipient.id, [])):
                try: queue.put_nowait(delivery)
                except asyncio.QueueFull: pass  # Slow streams may resync via authenticated polling.

    def submit(self, c, q):
        key = (c.id, q.requestId)
        if key in self.jobs:
            job = self.jobs[key]
            if job.question != q: raise ValueError('Request ID reused with different content')
            return job
        # Prune completed transient content before enforcing bounds.
        for k in list(self.jobs):
            j = self.jobs[k]
            if j.state in ['done','cancelled','error'] and time.monotonic()-j.created > 300: del self.jobs[k]
        if len(self.jobs) >= 256 or sum(j.state in ['queued','running','speaking'] for j in self.jobs.values()) >= self.parallel+self.queue_size:
            self.metrics['rejected'] += 1
            raise OverflowError('Server busy; retry later')
        room = self.store.rooms[c.roomId]
        snapshot = room.snapshots.get(q.referenceSnapshotId)
        if snapshot is None: raise LookupError('Referenced snapshot expired or is missing')
        if snapshot.authorityEpoch != room.epoch: raise ValueError('Authority epoch changed')
        if q.visibility == 'room' and not room.allowShared: raise PermissionError('Shared presentation is disabled')
        if q.referenceEventId and q.referenceEventId not in room.events: raise LookupError('Referenced event is unavailable')
        if q.referenceEventId:
            event = room.events[q.referenceEventId]
            if event.afterSnapshotId not in room.snapshots: raise LookupError('Referenced event state has expired')
        self.cancel(c)
        c.selectionId = q.selectionId
        job = Job(q, c.id, c.generation)
        self.jobs[key] = job
        self.metrics['submitted'] += 1
        job.task = asyncio.create_task(self.run(c, job, snapshot))
        return job

    def valid(self, c, job, snapshot):
        return (self.store.conversations.get(c.id) is c and job.generation == c.generation and c.selectionId == job.question.selectionId
                and self.store.rooms[c.roomId].epoch == snapshot.authorityEpoch)

    async def run(self, c, job, snapshot):
        start = time.monotonic()
        q = job.question
        shared = q.visibility == 'room'
        # Shared speech is serialized per room in addition to global compute limits.
        room_lock = self.__dict__.setdefault('room_locks', {}).setdefault(c.roomId, asyncio.Lock()) if shared else asyncio.Lock()
        try:
            async with room_lock, self.semaphore:
                if not self.valid(c, job, snapshot): raise asyncio.CancelledError()
                job.state = 'running'
                room = self.store.rooms[c.roomId]
                event = room.events.get(q.referenceEventId)
                if event and snapshot.stateVersion < room.snapshots[event.afterSnapshotId].stateVersion:
                    raise ValueError('Question snapshot predates its event')
                result, plan, backend, timing = await self.agent.respond(
                    q, snapshot, c.history, event,
                    room.snapshots.get(event.beforeSnapshotId) if event else None,
                    room.snapshots.get(event.afterSnapshotId) if event else None,
                    current_snapshot=(lambda: room.snapshots[room.latestId]) if q.referenceMode == 'live' else None)
                snapshot = room.snapshots.get(result['referenceSnapshotId'], snapshot)
                if not self.valid(c, job, snapshot): raise asyncio.CancelledError()
                result.update(planner=backend, intent=plan.intent, modelTiming=timing,
                              captionSeconds=time.monotonic()-start, segments=[], visibility=q.visibility)
                job.result = result
                # Once committed to captions, an interrupted spoken answer can be repeated.
                c.history.append(remember(q, plan, result))
                c.history[:] = c.history[-8:]
                await self.notify(c, dict(type='answer', **job.public()), shared)
                sentences = [s for s in re.split(r'(?<=[.!?])\s+', result['text']) if s.strip()]
                job.state = 'speaking'
                for index, sentence in enumerate(sentences):
                    if not self.valid(c, job, snapshot): raise asyncio.CancelledError()
                    if result['referenceMode'] == 'live' and not result.get('repeated') and not result.get('expanded'):
                        latest = room.snapshots[room.latestId]
                        oldf, newf = result['facts'], facts(latest)
                        ids = q.targetQubitIds or list(range(snapshot.qubitCount))
                        changed = latest.eventSequence != snapshot.eventSequence or any(
                            abs(oldf['nodes'][i]['p0']-newf['nodes'][i]['p0']) > .001 or
                            abs(oldf['nodes'][i]['purity']-newf['nodes'][i]['purity']) > .001 or
                            any(abs(a-b) > .001 for a,b in zip(oldf['nodes'][i]['bloch'],newf['nodes'][i]['bloch'])) for i in ids)
                        changed |= abs(oldf['globalPurity']-newf['globalPurity']) > .001 or any(
                            abs(a['logarithmicNegativityBits']-b['logarithmicNegativityBits']) > .001 or
                            abs(a['mutualInformationBits']-b['mutualInformationBits']) > .001
                            for a,b in zip(oldf['pairs'],newf['pairs']))
                        if changed:
                            result['status'] = 'context_changed'
                            job.state = 'done'
                            await self.notify(c, dict(type='context_changed', **job.public()), shared)
                            return
                    result['segments'].append(dict(index=index, text=sentence, factIds=result['factIds']))
                    await self.notify(c, dict(type='segment', **job.public()), shared)
                    if q.speak and q.language in self.voice.tts:
                        audio = await self.voice.run(self.voice.speak_sync, sentence, q.language)
                        if not self.valid(c, job, snapshot): raise asyncio.CancelledError()
                        job.audio.append(audio)
                        if 'firstAudioSeconds' not in result: result['firstAudioSeconds'] = time.monotonic()-start
                        await self.notify(c, dict(type='audio_ready', audioIndex=len(job.audio)-1, **job.public()), shared)
                    elif q.speak:
                        result['voiceStatus'] = 'unavailable'
                job.state = 'done'
                result['totalSeconds'] = time.monotonic()-start
                self.metrics['completed'] += 1
                await self.notify(c, dict(type='done', **job.public()), shared)
        except asyncio.CancelledError:
            job.state = 'cancelled'
            job.audio.clear()
            self.metrics['cancelled'] += 1
            await self.notify(c, dict(type='cancelled', requestId=q.requestId, generation=job.generation), shared)
        except Exception as e:
            job.state = 'error'
            job.result = dict(status='error', code=type(e).__name__, message='The turn failed. Retry with a current snapshot.')
            self.metrics['errors'] += 1
            await self.notify(c, dict(type='error', **job.public()), shared)

    async def close(self):
        tasks = [j.task for j in self.jobs.values() if j.task and not j.task.done()]
        for task in tasks: task.cancel()
        await asyncio.gather(*tasks, return_exceptions=True)

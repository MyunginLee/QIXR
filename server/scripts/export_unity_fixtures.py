"""Export synthetic Unity wire fixtures offline. Never imports the gateway or voice."""
import asyncio
import hashlib
import json
from pathlib import Path

import numpy as np

from qixr_companion.agent import POLICY_VERSION, QuantumAgent, remember
from qixr_companion.contracts import Plan, Question, QuantumEvent, Snapshot
from qixr_companion.physics import check_event, pack, preview, validate

ROOT = Path(__file__).resolve().parents[2]
DESTINATION = ROOT/'docs/handoff/fixtures/protocol-v1.json'


class FixturePlanner:
    """Explicit fixture plans, without any inference or HTTP client."""
    def __init__(self, plan):
        self.plan_value = plan

    async def plan(self, question, history):
        return self.plan_value, 'offline-fixture', {}


async def export():
    psi = np.zeros(8, complex)
    psi[4] = 1  # Q1=1, Q2=0, Q3=0, with Q1 the most significant bit.
    initial = Snapshot(roomId='offline-fixture-room', authorityEpoch=1,
                       snapshotId='fixture-initial', stateVersion=0,
                       capturedAtUtc='2026-10-28T00:00:00Z', density=pack(np.outer(psi, psi.conj())))
    superposition = initial.model_copy(update={
        'snapshotId':'fixture-superposition', 'stateVersion':1, 'eventSequence':1,
        'density':preview(initial, 'H', 0)['density']})
    psi[:] = 0
    psi[4], psi[2] = 1/np.sqrt(2), -1j/np.sqrt(2)
    pair = superposition.model_copy(update={
        'snapshotId':'fixture-entangled-pair', 'stateVersion':2,
        'density':pack(np.outer(psi, psi.conj()))})
    mixed = pair.model_copy(update={
        'snapshotId':'fixture-mixed-global', 'stateVersion':3, 'density':pack(np.eye(8)/8)})
    event = QuantumEvent(eventId='fixture-real-H', sequence=1, authorityEpoch=1,
                         actor='fixture-authority', operation='H', target=0,
                         beforeSnapshotId=initial.snapshotId, afterSnapshotId=superposition.snapshotId)
    for snapshot in (initial, superposition, pair, mixed):
        validate(snapshot)
    check_event(event, initial, superposition)
    cases, memories = [], {}

    async def case(name, text, snapshot, targets, plan, history=(), recorded_event=None):
        q = Question(requestId='fixture-'+name, selectionId='fixture-selection',
                     referenceSnapshotId=snapshot.snapshotId, targetQubitIds=targets,
                     referenceEventId=recorded_event.eventId if recorded_event else None,
                     text=text, language='en', speak=False)
        result, resolved, backend, _ = await QuantumAgent(FixturePlanner(plan)).respond(
            q, snapshot, list(history), recorded_event,
            initial if recorded_event else None, superposition if recorded_event else None)
        result.update(planner=backend, intent=resolved.intent, visibility=q.visibility, segments=[])
        cases.append(dict(name=name, request=q.model_dump(),
                          job=dict(requestId=q.requestId, generation=1, state='done', result=result, audioCount=0)))
        memories[name] = remember(q, resolved, result)

    await case('greeting', 'Who are you?', superposition, [0], Plan(intent='identity'))
    await case('probability', 'What are the Z probabilities?', superposition, [0], Plan(intent='probability'))
    await case('preview-H', 'What if I use H again?', superposition, [0], Plan(intent='preview', gate='H'))
    await case('preview-measure', 'What if I measure?', pair, [0], Plan(intent='preview', gate='measureZ'))
    await case('pair', 'Are you two friends?', pair, [0,1], Plan(intent='pair'))
    await case('missing-pair-selection', 'Are you two friends?', pair, [0], Plan(intent='pair'))
    await case('quiet', 'Let me try', superposition, [0], Plan(intent='stop'))
    await case('vague-preview', 'What if I do that?', superposition, [0], Plan(intent='preview'))
    await case('comparison', 'Why is it shorter than before?', pair, [0], Plan(intent='compare'), [memories['probability']])
    await case('repeat', 'Say that again', pair, [0], Plan(intent='repeat'), [memories['probability']])
    await case('detail', 'Tell me more', pair, [0], Plan(intent='detail'), [memories['probability']])
    await case('event', 'What just happened?', superposition, [0], Plan(intent='event'), recorded_event=event)
    await case('mixed-guard', 'Explain this arrow', mixed, [0], Plan(intent='qubit'))
    sources = [f'server/qixr_companion/{name}.py' for name in ('agent','api','contracts','dialogue','physics','service','passwords')]
    sources.append('Assets/Scripts/Companion/QuantumCompanionContracts.cs')
    data = dict(fixtureOnly=True, protocolVersion=1, agentPolicy=POLICY_VERSION,
                notice='Synthetic analytical states and constructed job envelopes; not live traces, timing, or model evaluation. No usable credentials.',
                sourceSha256={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in sources},
                schemas={name:model.model_json_schema() for name,model in (
                    ('Snapshot', Snapshot), ('QuantumEvent', QuantumEvent), ('Question', Question))},
                joinResponse=dict(conversationId='fixture-conversation', conversationToken='FIXTURE_NOT_A_CREDENTIAL', authorityEpoch=1),
                contextResponse=dict(snapshot=superposition.model_dump(), lastEventId=event.eventId),
                snapshots=[s.model_dump() for s in (initial, superposition, pair, mixed)],
                events=[event.model_dump()], cases=cases)
    DESTINATION.parent.mkdir(parents=True, exist_ok=True)
    DESTINATION.write_text(json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False)+'\n')
    print(f'Exported {len(cases)} synthetic cases to {DESTINATION}; no network or model workers used.')


if __name__ == '__main__':
    asyncio.run(export())

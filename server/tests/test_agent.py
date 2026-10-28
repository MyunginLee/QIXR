"""Offline interaction checks: no inference, speech workers, or listening sockets."""
import json

import httpx
import numpy as np
import pytest

from conftest import state
from qixr_companion.agent import QuantumAgent, remember
from qixr_companion.contracts import Plan, Question, QuantumEvent
from qixr_companion.dialogue import Planner, quick_plan
from qixr_companion.physics import pack, preview


class RulesOnly:
    async def plan(self, question, history):
        return quick_plan(question.text, len(question.targetQubitIds)) or Plan(intent='unsupported'), 'offline-rules', {}


def question(snapshot, text, targets=(0,), **kwargs):
    return Question(requestId='turn', selectionId='selection', referenceSnapshotId=snapshot.snapshotId,
                    targetQubitIds=list(targets), text=text, **kwargs)


async def turn(text, snapshot=None, targets=(0,), history=(), **kwargs):
    snapshot = snapshot or state('product')
    q = question(snapshot, text, targets, **kwargs)
    result, plan, _, _ = await QuantumAgent(RulesOnly()).respond(q, snapshot, list(history))
    return result, remember(q, plan, result)


@pytest.mark.parametrize('text,intent,gate', [
    ('Hello!', 'identity', 'none'), ('Who are you?', 'identity', 'none'),
    ('Do you feel anything?', 'identity', 'none'),
    ('Are you two friends now?', 'pair', 'none'),
    ('Say that again', 'repeat', 'none'), ('again', 'repeat', 'none'),
    ('What if I use H again?', 'preview', 'H'),
    ('What if I do that?', 'preview', 'none'),
    ('What if I measure?', 'preview', 'measureZ'),
    ('What will happen if I measure?', 'probability', 'none'),
    ('Why is the ribbon still there after moving apart?', 'concept', 'none'),
    ('I did not expect a shorter arrow', 'compare', 'none'),
    ("I didn't understand", 'repair', 'none'), ('Nothing happened', 'repair', 'none'),
    ('Did I do it right?', 'repair', 'none'),
    ('Tell me more', 'detail', 'none'), ("I'll try it myself", 'stop', 'none'),
    ('Stop talking', 'stop', 'none'), ('Thanks', 'acknowledge', 'none'),
])
def test_conversational_routes(text, intent, gate):
    plan = quick_plan(text)
    assert plan and (plan.intent, plan.gate) == (intent, gate)


async def test_english_default_brief_answers_and_opt_in_detail():
    result, h = await turn('Explain this arrow')
    assert result['responseLanguage'] == 'en'
    assert len(result['text'].split()) <= 55
    assert 'purity' not in result['text']
    detailed, _ = await turn('Tell me more', history=[h])
    assert detailed['expanded'] and 'purity' in detailed['text']
    assert detailed['referenceSnapshotId'] == result['referenceSnapshotId']
    assert detailed['nextAction'] is None


async def test_preview_speaks_calculated_outcome_and_preserves_live_state():
    start = state()
    superposed = start.model_copy(update={'snapshotId':'superposition', 'density':preview(start, 'H', 0)['density']})
    before = list(superposed.density)
    result, _ = await turn('What if I use H again?', superposed)
    assert '0 becomes 100.0%' in result['text']
    assert result['preview']['facts']['nodes'][0]['p0'] == pytest.approx(1)
    assert result['referenceCue']['snapshotId'] == 'superposition'
    assert result['nextAction'] is None
    assert superposed.density == before
    vague, _ = await turn('What if I do that?', superposed)
    assert vague['status'] == 'needs_clarification' and vague['preview'] is None


async def test_measurement_preview_does_not_sample_or_mutate():
    s = state('bell')
    result, _ = await turn('What if I measure?', s)
    assert '0: 50.0%' in result['text'] and '1: 50.0%' in result['text']
    assert 'outcome' not in result  # Branches, never a chosen live outcome.
    assert s.density == state('bell').density


async def test_same_probabilities_different_arrow_uses_real_captures():
    psi = np.zeros(8, complex); psi[[0,4]] = 1/np.sqrt(2)
    pure = state().model_copy(update={'snapshotId':'earlier-plus', 'density':pack(np.outer(psi,psi.conj()))})
    _, h = await turn('What are the Z probabilities?', pure)
    result, _ = await turn('Why is it shorter than before?', state('bell'), history=[h])
    assert 'same Z probabilities' in result['text'] and 'entangled with the rest' in result['text']
    assert result['comparison']['snapshotId'] == pure.snapshotId
    assert result['comparison']['nodes'][0]['blochRadius'] == pytest.approx(1)
    assert result['facts']['nodes'][0]['blochRadius'] == pytest.approx(0)
    mixed, _ = await turn('Why is it shorter than before?', state('classical'), history=[h])
    assert 'entangled' not in mixed['text']


async def test_repeat_keeps_original_reference_and_does_not_reexecute_preview():
    first, h = await turn('What if I use H?', state())
    result, _ = await turn('Say that again', state('bell'), history=[h])
    assert result['text'] == first['text']
    assert result['referenceSnapshotId'] == 'product'
    assert result['referenceCue']['label'] == 'Earlier answer'
    assert result['preview'] == first['preview']
    assert result['nextAction'] is None


@pytest.mark.parametrize('change', [dict(visibility='room'), dict(targets=(1,)), dict(language='ko')])
async def test_private_history_is_not_reused_for_other_scopes(change):
    _, h = await turn('Explain this arrow')
    result, _ = await turn('Say that again', history=[h], **change)
    assert result['status'] == 'history_unavailable' and not result.get('repeated')


async def test_epoch_change_forgets_previous_explanations():
    _, h = await turn('Explain this arrow')
    result, _ = await turn('Say that again', state().model_copy(update={'authorityEpoch':2}), history=[h])
    assert result['status'] == 'history_unavailable'


async def test_missing_targets_and_missing_history_are_clarified():
    result, _ = await turn('Are you two friends?', state('bell'))
    assert result['status'] == 'needs_selection' and not result['referenceCue']['showState']
    assert 'entanglement in this' not in result['text']
    result, _ = await turn('Why is it shorter than before?', state('bell'))
    assert result['status'] == 'history_unavailable' and 'earlier state' in result['text']


async def test_no_entanglement_claim_for_mixed_joint_state_or_ghz_pair():
    result, _ = await turn('Explain this arrow', state('classical'))
    assert 'cannot establish entanglement' in result['text']
    pair, _ = await turn('Are you two friends?', state('ghz'), targets=(0,1))
    assert "don't detect" in pair['text'] and "all three" in pair['text']


async def test_repair_is_one_question_without_inventing_device_failure():
    result, _ = await turn("It didn't work")
    assert result['text'].count('?') == 1
    assert 'failed' not in result['text'] and 'broken' not in result['text']
    assert result['nextAction']['kind'] == 'ask'
    quiet, _ = await turn('Let me try')
    assert quiet['status'] == 'quiet' and quiet['text'] == '' and quiet['nextAction'] is None


async def test_ordinary_acknowledgement_leaves_room_for_exploration():
    result, _ = await turn('Wow')
    assert result['status'] == 'quiet' and result['text'] == '' and result['nextAction'] is None


async def test_single_qubit_entanglement_question_does_not_invent_a_neighbor():
    result, _ = await turn('Are you entangled?', state('bell'))
    assert result['status'] == 'answered' and 'with the rest' in result['text']
    assert result['targetQubitIds'] == [0]


async def test_detail_after_a_greeting_clarifies_instead_of_denying_history():
    _, h = await turn('Who are you?')
    result, _ = await turn('Tell me more', history=[h])
    assert result['status'] == 'needs_clarification'


async def test_no_matching_event_is_not_fabricated():
    result, _ = await turn('What just happened?')
    assert result['status'] == 'history_unavailable' and result['factIds'] == []


async def test_recorded_gate_uses_before_and_after_evidence():
    before = state()
    after = before.model_copy(update={'snapshotId':'after-H', 'stateVersion':1, 'eventSequence':1,
                                      'density':preview(before,'H',0)['density']})
    event = QuantumEvent(eventId='real-H', sequence=1, authorityEpoch=1, operation='H', target=0,
                         beforeSnapshotId=before.snapshotId, afterSnapshotId=after.snapshotId)
    q = question(after, 'What just happened?')
    result, _, _, _ = await QuantumAgent(RulesOnly()).respond(q, after, [], event, before, after)
    assert '100.0% to 50.0%' in result['text']
    assert result['referenceSnapshotId'] == after.snapshotId
    assert result['factIds'] == ['real-H']


async def test_korean_is_an_explicit_option():
    result, _ = await turn('너는 누구야?', language='ko')
    assert result['responseLanguage'] == 'ko' and '양자예요' in result['text']


async def test_live_mode_refreshes_state_after_planning():
    old, new = state(), state('bell')
    q = question(old, 'What are the Z probabilities?', referenceMode='live')
    result, _, _, _ = await QuantumAgent(RulesOnly()).respond(q, old, [], current_snapshot=lambda: new)
    assert result['referenceSnapshotId'] == 'bell' and '50.0%' in result['text']
    assert result['referenceCue']['label'] == 'At answer'


async def test_model_receives_policy_and_schema_but_no_private_history(monkeypatch):
    requests = []
    def handler(request):
        requests.append(json.loads(request.content))
        return httpx.Response(200, json={'message':{'content':'{"intent":"identity"}'}})
    original = httpx.AsyncClient
    monkeypatch.setattr('qixr_companion.dialogue.httpx.AsyncClient',
                        lambda **kwargs: original(transport=httpx.MockTransport(handler), **kwargs))
    planner = Planner('http://offline.invalid', 'test')
    _, h = await turn('Explain this arrow')
    q = question(state(), 'a novel greeting', visibility='room')
    result, _, backend, _ = await QuantumAgent(planner).respond(q, state(), [h])
    assert backend == 'ollama:test' and 'Quantum' in result['text']
    payload = requests[0]
    assert 'English is the default' in payload['messages'][0]['content']
    assert json.loads(payload['messages'][1]['content'])['history'] == []
    assert 'stop' in payload['format']['properties']['intent']['enum']


async def test_malformed_model_output_never_becomes_speech(monkeypatch):
    original = httpx.AsyncClient
    monkeypatch.setattr('qixr_companion.dialogue.httpx.AsyncClient', lambda **kwargs: original(
        transport=httpx.MockTransport(lambda _: httpx.Response(200, json={'message':{'content':'I can pick zero every time!'}})), **kwargs))
    q = question(state(), 'an unknown topic')
    result, _, backend, _ = await QuantumAgent(Planner('http://offline.invalid', 'test')).respond(q, state(), [])
    assert backend == 'fallback' and result['status'] == 'unsupported'
    assert 'pick zero' not in result['text']

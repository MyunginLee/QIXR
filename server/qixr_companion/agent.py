"""A reactive, state-grounded companion. It has no tools that mutate live qubits."""
import copy

from .contracts import Plan
from .dialogue import answer
from .physics import facts

POLICY_VERSION = "quantum-companion-en-1"
STATE_INTENTS = {"qubit", "pair", "probability", "event", "preview", "compare"}


def scoped_history(question, snapshot, history):
    # A room-visible answer must never use a participant's private dialogue.
    return [h for h in history if h.get('visibility') == question.visibility
            and h.get('authorityEpoch') == snapshot.authorityEpoch
            and sorted(h.get('targets', [])) == sorted(question.targetQubitIds)
            and h.get('language') == question.language][-8:]


def remember(question, plan, result):
    return dict(question=question.text, intent=plan.intent, concept=plan.concept, gate=plan.gate,
                targets=list(question.targetQubitIds), visibility=question.visibility,
                language=question.language, authorityEpoch=result['authorityEpoch'],
                result=copy.deepcopy({k: result[k] for k in (
                    'text', 'status', 'detailText', 'facts', 'factIds', 'sources', 'referenceSnapshotId',
                    'authorityEpoch', 'stateVersion', 'targetQubitIds', 'referenceMode', 'referenceCue',
                    'preview', 'comparison') if k in result}))


class QuantumAgent:
    def __init__(self, planner):
        self.planner = planner

    async def respond(self, question, snapshot, history, event=None, event_before=None, event_after=None, current_snapshot=None):
        history = scoped_history(question, snapshot, history)
        # Give the model dialogue subjects, never cached prose as physical evidence.
        routing_history = [{k: h[k] for k in ('question', 'intent', 'concept', 'gate')} for h in history]
        plan, backend, timing = await self.planner.plan(question, routing_history)
        if current_snapshot is not None:
            snapshot = current_snapshot()
            history = scoped_history(question, snapshot, history)
        result, resolved = self.render(question, snapshot, plan, history, event, event_before, event_after)
        result.update(agentPolicy=POLICY_VERSION, responseLanguage=question.language)
        return result, resolved, backend, timing

    def render(self, q, snapshot, plan, history, event=None, event_before=None, event_after=None):
        ko = q.language == 'ko'
        say = lambda en, kr: kr if ko else en
        previous = next((h for h in reversed(history) if h['result']['text']), None)
        if plan.intent == 'repeat' and previous:
            result = copy.deepcopy(previous['result'])
            result.update(selectionId=q.selectionId, repeated=True, nextAction=None, referenceMode='captured')
            result['referenceCue']['label'] = say('Earlier answer', '앞선 답변')
            return result, Plan(intent=previous['intent'], concept=previous['concept'], gate=previous['gate'])
        if plan.intent == 'detail' and previous and previous['result'].get('detailText'):
            result = copy.deepcopy(previous['result'])
            result.update(text=result['detailText'], selectionId=q.selectionId, nextAction=None, expanded=True, referenceMode='captured')
            result['referenceCue']['label'] = say('Earlier answer · details', '앞선 답변 · 자세히')
            return result, Plan(intent=previous['intent'], concept=previous['concept'], gate=previous['gate'])

        if plan.intent == 'event' and event and event_after:
            snapshot = event_after
        f = facts(snapshot)
        # Reuse validated scientific evidence; the agent owns the conversational layer.
        supported = plan.intent in ((STATE_INTENTS - {'compare'}) | {'presentation'}) or (plan.intent == 'concept' and plan.concept in (
            'superposition', 'entanglement', 'measurement', 'gates', 'sonification'))
        supported &= not (plan.intent == 'preview' and plan.gate == 'none')
        result = answer(q, snapshot, plan if supported else Plan(intent='unsupported'), event)
        detail = result['text'] if supported else ''
        result.update(status='answered', text='', detailText='', nextAction=None,
                      referenceCue=dict(label=say('At answer', '응답 당시') if q.referenceMode == 'live' else say('At your question', '질문 당시'), snapshotId=snapshot.snapshotId,
                                        stateVersion=snapshot.stateVersion, eventSequence=snapshot.eventSequence,
                                        targetQubitIds=list(q.targetQubitIds), showState=plan.intent in STATE_INTENTS))
        if supported:
            result['detailText'] = detail
        else:
            result.update(factIds=[], sources=[])

        def action(en, kr, question_en, question_ko):
            # Displayed silently. Clicking submits a question; it never operates a live gate.
            result['nextAction'] = dict(label=say(en, kr), question=say(question_en, question_ko), kind='ask')

        count = len(q.targetQubitIds)
        required = 2 if plan.intent == 'pair' else 1 if plan.intent in ('qubit', 'probability', 'preview', 'compare') else None
        if required is not None and count != required:
            result['referenceCue']['showState'] = False
            result.update(status='needs_selection', detailText='', factIds=[], text=say(
                'Select both qubits so I can check their relationship.' if required == 2 else 'Which qubit do you mean? Grab one or select its label.',
                '관계를 확인할 두 큐빗을 함께 선택해 주세요.' if required == 2 else '어느 큐빗인가요? 하나를 잡거나 이름을 선택해 주세요.'))
            return result, plan
        if any(i >= snapshot.qubitCount for i in q.targetQubitIds):
            raise ValueError('Selected qubit is absent from the referenced snapshot')
        node = f['nodes'][q.targetQubitIds[0]] if count else None

        if plan.intent == 'identity':
            if plan.concept == 'consciousness':
                text = say("I'm a character that helps explain the simulation. I don't feel emotions or choose measurement results.",
                           '저는 시뮬레이션을 설명하는 캐릭터예요. 감정을 느끼거나 측정 결과를 마음대로 고르지는 않아요.')
            else:
                text = say("I'm Quantum, your guide to the qubits you're exploring. These spheres let you see and handle simulated quantum states.",
                           '저는 함께 큐빗을 살펴볼 양자예요. 이 구들은 시뮬레이션의 양자 상태를 보고 만질 수 있도록 만든 표현이에요.')
                action('What can I try?', '무엇을 해 볼까요?', 'What can I try?', '뭘 해 볼 수 있어요?')
        elif plan.intent == 'help':
            text = say('Grab a qubit or select its label, then ask what you notice. You can type, or hold Talk and review the words before pressing Ask.',
                       '큐빗을 잡거나 이름을 선택하고, 눈에 띄는 것을 물어보세요. 글로 질문하거나 말한 내용을 확인한 뒤 질문 버튼을 누르셔도 돼요.')
            if count == 1:
                action('Preview H on a copy', '복사본에 H 해 보기', 'Preview H on a copy', '복사본에 H를 적용하면?')
        elif plan.intent == 'repair':
            text = say('Which part should we check: the controls, my explanation, or what changed on screen?',
                       '조작 방법, 제 설명, 화면에서 달라진 것 중 어느 부분을 함께 확인할까요?')
            if count == 1:
                action('Check this arrow', '지금 화살표 확인', 'Explain this arrow', '지금 화살표를 설명해 주세요')
        elif plan.intent in ('repeat', 'detail'):
            result['status'] = 'history_unavailable'
            text = say("I don't have an earlier explanation for this selection. What would you like to look at?",
                       '이 선택 대상에 대해 앞서 설명한 내용이 없어요. 무엇을 살펴볼까요?')
            if previous and plan.intent == 'detail':
                result['status'] = 'needs_clarification'
                text = say("Which part would you like to explore: the arrow, measurement chances, or a gate?",
                           '화살표, 측정 확률, 게이트 중 무엇을 더 살펴볼까요?')
        elif plan.intent == 'stop':
            result.update(status='quiet', text='', detailText='', factIds=[], sources=[])
            return result, plan
        elif plan.intent == 'acknowledge':
            text = say("You're welcome.", '언제든 물어보세요.') if any(w in q.text.lower() for w in ('thank', '고마')) else ''
            if not text:
                result['status'] = 'quiet'
        elif plan.intent == 'probability':
            text = say(f"For {node['label']}, a Z measurement would give 0 with {node['p0']:.1%} probability and 1 with {node['p1']:.1%}. Asking leaves the live state unchanged.",
                       f"{node['label']}을 Z 측정하면 0은 {node['p0']:.1%}, 1은 {node['p1']:.1%} 확률이에요. 물어보는 것만으로 실제 상태가 바뀌지는 않아요.")
            action('See possible measurement results', '가능한 측정 결과 보기', 'Preview a Z measurement', 'Z 측정 가상 실험을 보여 주세요')
        elif plan.intent == 'qubit':
            if node['entangledWithRest'] is True:
                text = say(f"{node['label']}'s arrow is shortened. In this pure joint state, that means this qubit is entangled with the rest.",
                           f"{node['label']}의 화살표가 짧아져 있어요. 지금처럼 공동 상태가 순수할 때는 나머지와 얽혀 있다는 뜻이에요.")
            elif node['entangledWithRest'] is False:
                text = say(f"{node['label']}'s arrow has full length. Its direction helps show how this qubit will respond to gates and measurement.",
                           f"{node['label']}의 화살표는 온전한 길이예요. 방향을 통해 게이트나 측정에 어떻게 반응할지 살펴볼 수 있어요.")
            else:
                text = say(f"{node['label']}'s arrow length is {node['blochRadius']:.2f}. For this mixed joint state, that length alone cannot establish entanglement.",
                           f"{node['label']}의 화살표 길이는 {node['blochRadius']:.2f}예요. 공동 상태가 혼합되어 있어 이 길이만으로 얽힘을 판단할 수는 없어요.")
            action('What would measurement show?', '측정하면 어떻게 될까요?', 'What are the Z probabilities?', 'Z 측정 확률은?')
        elif plan.intent == 'pair':
            pair = next(p for p in f['pairs'] if {p['first'], p['second']} == set(q.targetQubitIds))
            text = say('These two share a quantum relationship here. We call it entanglement.',
                       '질문하신 순간에는 이 둘 사이에 양자적인 관계가 있어요. 이런 관계를 얽힘이라고 해요.') if pair['entanglementStatus'] == 'detected' else say(
                       "I don't detect entanglement for this pair at our numerical precision. That doesn't settle how all three qubits relate.",
                       '현재 수치 정밀도에서는 이 둘의 얽힘이 검출되지 않아요. 이것만으로 셋 전체의 관계를 판단할 수는 없어요.')
            if pair['entanglementStatus'] == 'detected':
                action('What if I move them apart?', '멀리 옮기면 어떻게 될까요?', 'What if I move them apart?', '멀리 옮기면 어떻게 돼요?')
        elif plan.intent == 'event':
            result['referenceMode'] = 'captured'
            result['referenceCue']['label'] = say('Recorded operation', '기록된 조작')
            if event is None or count and event.target not in q.targetQubitIds:
                result.update(status='history_unavailable', detailText='', factIds=[], sources=[])
                result['referenceCue']['showState'] = False
                text = say("I don't have a matching recorded operation for this question. The current picture alone can't tell me what happened earlier.",
                           '이 질문에 맞는 조작 기록이 없어요. 지금 모습만으로 과거에 일어난 일을 알아낼 수는 없어요.')
            elif event.operation == 'measureZ':
                text = say(f"The recorded Z measurement on Q{event.target+1} gave {event.outcome}. Before measurement, that result had probability {event.probability:.1%}.",
                           f"기록된 Q{event.target+1}의 Z 측정 결과는 {event.outcome}이에요. 측정 전 그 결과의 확률은 {event.probability:.1%}였어요.")
            else:
                text = say(f"The recorded operation applied {event.operation} to Q{event.target+1}. The reference shows the state just after it.",
                           f"기록된 조작은 Q{event.target+1}에 {event.operation}를 적용한 것이에요. 옆의 참조는 그 직후의 상태예요.")
                if event_before and event_after:
                    bf, af = facts(event_before)['nodes'][event.target], facts(event_after)['nodes'][event.target]
                    text = say(f"The recorded {event.operation} changed Q{event.target+1}'s chance of 0 from {bf['p0']:.1%} to {af['p0']:.1%}.",
                               f"기록된 {event.operation} 조작에서 Q{event.target+1}의 0 확률이 {bf['p0']:.1%}에서 {af['p0']:.1%}로 바뀌었어요.") if abs(bf['p0']-af['p0']) > .001 else say(
                               f"The recorded {event.operation} left Q{event.target+1}'s Z probabilities unchanged. Gates can still change information that these probabilities don't show.",
                               f"기록된 {event.operation} 조작에서 Q{event.target+1}의 Z 확률은 그대로였어요. 게이트는 이 확률에 드러나지 않는 정보도 바꿀 수 있어요.")
        elif plan.intent == 'preview':
            if plan.gate == 'none':
                result.update(status='needs_clarification', detailText='')
                text = say('Which operation would you like to try on a copy: H, X, Z, S, or a Z measurement?',
                           '복사본에 H, X, Z, S, Z 측정 중 어떤 조작을 해 볼까요?')
            elif plan.gate == 'measureZ':
                branches = result['preview']['branches']
                probabilities = ', '.join(f"{b['outcome']}: {b['probability']:.1%}" for b in branches)
                text = say(f"On a copy of {node['label']}, the possible Z results are {probabilities}. I haven't measured the live qubit.",
                           f"{node['label']}의 복사본에서 가능한 Z 결과는 {probabilities}예요. 실제 큐빗을 측정하지는 않았어요.")
            else:
                after = result['preview']['facts']['nodes'][q.targetQubitIds[0]]
                text = say(f"With {plan.gate} on a copy of {node['label']}, the chance of 0 becomes {after['p0']:.1%} and 1 becomes {after['p1']:.1%}. The live qubit stays as it was.",
                           f"{node['label']}의 복사본에 {plan.gate}를 적용하면 0은 {after['p0']:.1%}, 1은 {after['p1']:.1%} 확률이 돼요. 실제 큐빗은 그대로예요.")
        elif plan.intent == 'compare':
            prior = next((h['result'] for h in reversed(history) if h['intent'] in STATE_INTENTS
                          and h['result'].get('facts') and h['result']['referenceSnapshotId'] != snapshot.snapshotId), None)
            if not prior:
                result['status'] = 'history_unavailable'
                text = say("I don't have an earlier state for this selection to compare. Ask about its arrow now, and we can compare after you change it.",
                           '이 선택 대상의 앞선 상태가 없어 비교할 수는 없어요. 지금 화살표를 물어보시면 조작 후에 비교할 수 있어요.')
                action('Keep this arrow for comparison', '지금 화살표 확인', 'Explain this arrow', '지금 화살표를 설명해 주세요')
            else:
                before = prior['facts']['nodes'][q.targetQubitIds[0]]
                same = abs(before['p0']-node['p0']) < .001
                shorter = node['blochRadius'] < before['blochRadius']-.001
                if same and shorter and node['entangledWithRest'] is True:
                    text = say(f"{node['label']} has the same Z probabilities as before, but a shorter arrow. In this pure joint state, the qubit is now entangled with the rest.",
                               f"{node['label']}의 Z 확률은 아까와 같지만 화살표는 더 짧아요. 지금처럼 공동 상태가 순수할 때는 나머지와 얽혀 있다는 뜻이에요.")
                else:
                    text = say(f"{node['label']}'s chance of 0 went from {before['p0']:.1%} to {node['p0']:.1%}. Its arrow length went from {before['blochRadius']:.2f} to {node['blochRadius']:.2f}.",
                               f"{node['label']}의 0 확률은 {before['p0']:.1%}에서 {node['p0']:.1%}로, 화살표 길이는 {before['blochRadius']:.2f}에서 {node['blochRadius']:.2f}로 바뀌었어요.")
                result.update(comparison=dict(snapshotId=prior['referenceSnapshotId'], nodes=[before]),
                              factIds=[prior['referenceSnapshotId']+':comparison', snapshot.snapshotId+':comparison'])
                result['detailText'] = text + say(' These are two captured states; they do not identify which unrecorded action caused the change.',
                                                  ' 두 순간의 비교이며, 기록되지 않은 변화의 원인까지 알 수 있는 것은 아니에요.')
        elif plan.intent == 'presentation':
            text = say("You're hearing a musical mapping of the simulated state. It isn't a recording of a physical quantum object's sound.",
                       '상태의 수치를 음악으로 표현한 소리예요. 실제 양자가 내는 소리를 녹음한 것은 아니에요.') if plan.concept == 'sonification' else say(
                       'The ribbon is a display of a pair relationship. Select both qubits if you want me to check their entanglement.',
                       '리본은 두 큐빗 사이의 관계를 보여 주는 표시예요. 둘을 함께 선택하시면 얽힘을 확인해 드릴게요.')
            result.update(factIds=[snapshot.snapshotId+':presentation'], sources=['qixr:QuantumArpeggioComposer', 'qixr:Entanglement'])
        elif plan.intent == 'concept':
            concepts = {
                'distance': ("Moving qubits apart stops their interaction when they're beyond coupling range. Distance alone doesn't erase entanglement they already share.",
                             '결합 거리 밖으로 옮기면 추가 상호작용이 멈춰요. 멀어졌다는 이유만으로 이미 생긴 얽힘이 사라지지는 않아요.'),
                'signaling': ("You can't choose a random measurement result to send a message. Entanglement alone doesn't let these qubits communicate faster than light.",
                              '무작위 측정 결과를 마음대로 골라 메시지를 보낼 수는 없어요. 얽힘만으로 빛보다 빠르게 통신할 수는 없어요.'),
                'measurement': ("Looking at me or asking a question doesn't perform the app's measurement. The Z measurement control samples a result and updates the joint state.",
                                '바라보거나 질문하는 것은 이 앱의 측정을 실행하지 않아요. Z 측정 조작을 할 때 결과를 뽑고 공동 상태가 바뀌어요.'),
                'superposition': ('A superposition combines possibilities with amplitudes that can interfere. A Z measurement still gives one result, 0 or 1.',
                                  '중첩은 간섭할 수 있는 진폭으로 가능성을 함께 표현해요. Z 측정 한 번의 결과는 여전히 0 또는 1이에요.'),
                'entanglement': ('Some joint quantum states cannot be reproduced by mixing independent states of each qubit. We call that entanglement.',
                                 '각 큐빗의 독립적인 상태들을 섞는 것으로 설명할 수 없는 공동 상태가 있어요. 그런 관계를 얽힘이라고 해요.'),
                'gates': ('H, X, Z and S change the state in different ways. Select a qubit and we can preview one on a copy before you try it.',
                          'H, X, Z, S는 서로 다른 방식으로 상태를 바꿔요. 큐빗 하나를 선택하면 실제 조작 전에 복사본으로 해 볼 수 있어요.'),
                'sonification': ('The music represents properties of the simulated state. Silence alone does not mean every kind of entanglement is absent.',
                                 '음악은 시뮬레이션 상태의 특성을 표현해요. 조용하다는 것만으로 모든 얽힘이 없다고 할 수는 없어요.'),
            }
            if plan.concept in concepts:
                text = say(*concepts[plan.concept])
                if plan.concept in ('distance', 'signaling'):
                    result.update(factIds=['concept:'+plan.concept+':v1'], sources=['https://arxiv.org/html/2504.08984v1#S3.SS5'])
                    result['detailText'] = text
            else:
                result['status'] = 'unsupported'
                text = say("I can help explore this simulation, but I don't have a grounded answer to that question. What are you noticing here?",
                           '이 시뮬레이션의 탐색은 도와드릴 수 있지만 그 질문에 근거 있는 답은 없어요. 지금 무엇이 눈에 띄세요?')
        else:
            result['status'] = 'unsupported'
            text = say("I can help with what you see and do in QIXR. Would you like to check a qubit, its measurement chances, or a recent operation?",
                       'QIXR에서 보고 조작하는 내용을 도와드릴 수 있어요. 큐빗의 모습, 측정 확률, 방금 조작 중 무엇을 살펴볼까요?')
        result['text'] = text
        return result, plan

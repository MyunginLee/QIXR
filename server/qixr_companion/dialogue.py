"""The model selects an intent; reviewed renderers own scientific statements."""
import json
import re
from pathlib import Path

import httpx

from .contracts import Plan
from .physics import facts, preview

CONCEPTS = {
    "superposition": (
        "중첩은 정해진 기저 상태들의 복소 진폭으로 표현돼. 측정 확률만 같다고 같은 양자 상태인 것은 아니야. 상대 위상도 이후 간섭에 영향을 줘.",
        "Superposition combines basis states with complex amplitudes. Equal measurement probabilities do not necessarily mean equal quantum states: relative phase can affect later interference.",
        "https://arxiv.org/html/2504.08984v1#S3.SS1"),
    "entanglement": (
        "얽힘은 공동 상태를 각 부분의 독립적인 상태의 곱이나 그런 곱들의 확률적 혼합으로 설명할 수 없는 관계야. 우리 셋의 얽힘과 특정 두 큐빗의 얽힘은 구분해야 해.",
        "An entangled joint state cannot be written as a product of local states or a probabilistic mixture of such products. Entanglement of all three qubits and entanglement of a particular pair are different questions.",
        "https://arxiv.org/abs/quant-ph/9605038"),
    "measurement": (
        "여기서 실제 측정은 Z 기저에서 0 또는 1을 확률적으로 뽑고 공동 상태를 갱신해. 나에게 질문하거나 화면을 보는 동작 자체는 그 측정을 실행하지 않아. 얽힘을 이용해 빛보다 빠르게 메시지를 보낼 수는 없어.",
        "A live measurement here samples 0 or 1 in the Z basis and updates the joint state. Asking me or viewing the visualization does not execute that operation. Entanglement cannot send a message faster than light.",
        "https://arxiv.org/html/2504.08984v1#S3.SS5"),
    "gates": (
        "현재 장면에는 H, X, Z, S 게이트가 있어. H는 중첩과 간섭을 탐색하고, X는 계산 기저의 0과 1을 바꾸며, Z와 S는 상대 위상을 바꿔. 효과는 게이트를 적용하기 전 상태에 따라 달라져.",
        "This scene supports H, X, Z and S gates. H explores superposition and interference, X swaps the computational basis states, and Z and S change relative phase. Their effects depend on the initial state.",
        "https://arxiv.org/html/2504.08984v1#S3"),
    "sonification": (
        "내 음악은 상태의 수치를 소리로 옮긴 표현이야. 양자가 실제로 내는 소리는 아니야. 현재 매핑은 특정 쌍의 얽힘과 상호정보량 등을 사용하므로, 조용하다고 모든 얽힘이 없다는 뜻은 아니야.",
        "My music maps numerical state properties to sound; it is not the literal sound of a quantum object. This mapping uses selected pair metrics, so silence does not imply absence of every form of entanglement.",
        "qixr:QuantumArpeggioComposer:qixr-2026-09"),
}


def quick_plan(text, target_count=None):
    """High-confidence speech acts, with conservative handling of vague operations."""
    t = text.lower().replace("’", "'").strip().rstrip(".!?")
    has = lambda *words: any(w in t for w in words)
    if has("stop talking", "be quiet", "let me try", "i'll try", "i will try", "그만", "조용히", "직접 해볼", "직접 해 볼") or t == "stop":
        return Plan(intent="stop")
    if has("say that again", "repeat that", "repeat your", "다시 말", "다시 설명") or t == "again":
        return Plan(intent="repeat")
    if has("who are you", "sphere real", "ball real", "누구", "진짜 양자") or re.search(r'\bare you real\b', t) or t in ("what are you", "hello", "hi", "hey", "안녕"):
        return Plan(intent="identity")
    if has("do you feel", "can you feel", "are you conscious", "are you alive", "의식", "감정", "생각하고"):
        return Plan(intent="identity", concept="consciousness")
    if has("didn't understand", "don't understand", "feel lost", "feel confused", "not what i meant", "didn't say", "didn't work", "nothing happened", "not working", "did i do", "is this right", "changed while", "잘한", "안 바뀌", "안 돼", "모르겠", "이해가 안", "그 뜻", "인식"):
        return Plan(intent="repair")
    if t in ("thanks", "thank you", "okay", "ok", "wow", "i see", "got it", "고마워", "알겠어"):
        return Plan(intent="acknowledge")
    if has("tell me more", "more detail", "explain that", "numbers", "순도", "purity", "negativity", "자세히", "숫자로"):
        return Plan(intent="detail")
    if has("what can i", "what should i", "what do i do", "how do i", "how to use", "controls", "뭘 물어", "뭘 해", "어떻게 해"):
        return Plan(intent="help")
    if has("earlier", "than before", "used to", "shorter", "still fifty", "아까", "이번엔 왜", "짧아"):
        return Plan(intent="compare")
    if has("apart", "far away", "separate", "distance", "떨어뜨", "멀어", "멀리"):
        return Plan(intent="concept", concept="distance")
    if has("faster than light", "send a message", "always zero", "always 0", "choose the outcome", "메시지", "빛보다", "항상 0"):
        return Plan(intent="concept", concept="signaling")
    if has("looking", "look at you", "watching", "observing", "보기만", "보고 있", "바라보"):
        return Plan(intent="concept", concept="measurement")
    if has("방금", "직전", "last event", "just happen", "what happened", "what changed", "after measuring", "after i measured", "after measurement"):
        return Plan(intent="event")
    if has("arrow turn", "arrow move", "why is it sideways", "방향이 왜", "옆으로"):
        return Plan(intent="event")
    gate = next((g for g in ("H", "X", "Z", "S") if re.search(r"\b"+g+r"\b", text, re.I)), "none")
    if has("what if", "preview", "sandbox", "하면", "한번 더", "한 번 더", "미리") or (gate != "none" and has("again", "apply", "use")):
        if has("measur", "측정"):
            gate = "measureZ"
        return Plan(intent="preview", gate=gate)
    if target_count == 1 and has("are you entangled", "are we entangled"):
        return Plan(intent="qubit")
    if has("friends", "친해", "너희", "둘", "pair", "between", "you two", "these two", "those two", "are you entangled", "are we entangled"):
        return Plan(intent="pair")
    if has("소리", "음악", "ribbon", "줄", "리본", "sound", "music", "silent"):
        return Plan(intent="presentation", concept="sonification" if has("소리", "음악", "sound", "music", "silent") else "none")
    if has("확률", "뭐가 나", "probabil", "outcome", "chance", "if i measure", "if we measure"):
        return Plan(intent="probability")
    if has("화살표", "반지름", "arrow", "radius", "내 상태", "your state"):
        return Plan(intent="qubit")
    for concept, words in {"superposition": ("superposition", "중첩"), "entanglement": ("entanglement", "얽힘"),
                           "measurement": ("measurement", "측정"), "gates": ("gate", "게이트")}.items():
        if has(*words):
            return Plan(intent="concept", concept=concept)
    return None


class Planner:
    def __init__(self, url, model, timeout=25):
        self.url, self.model, self.timeout = url.rstrip('/'), model, timeout

    async def ready(self):
        try:
            async with httpx.AsyncClient(timeout=2) as client:
                r = await client.get(self.url+'/api/tags')
                r.raise_for_status()
                return any(m['name'] == self.model or m['name'] == self.model+':latest' for m in r.json()['models'])
        except (httpx.HTTPError, KeyError, ValueError):
            return False

    async def plan(self, question, history):
        plan = quick_plan(question.text, len(question.targetQubitIds))
        if plan: return plan, "rules", {}
        prompt = (Path(__file__).parent/'prompts/quantum_agent.txt').read_text()
        try:
            async with httpx.AsyncClient(timeout=self.timeout) as client:
                r = await client.post(self.url+'/api/chat', json={"model": self.model, "stream": False, "think": False,
                    "format": Plan.model_json_schema(), "messages": [{"role": "system", "content": prompt},
                    {"role": "user", "content": json.dumps({"history": history[-4:], "question": question.text,
                        "selectedQubitIds": question.targetQubitIds, "language": question.language}, ensure_ascii=False)}],
                    "options": {"temperature": 0, "num_predict": 100, "num_ctx": 4096}})
                r.raise_for_status()
                body = r.json()
                plan = Plan.model_validate_json(body['message']['content'])
                if plan.intent == "concept" and plan.concept == "none": plan = Plan(intent="unsupported")
                return plan, "ollama:"+self.model, {k: body.get(k) for k in ['eval_count', 'eval_duration', 'prompt_eval_count', 'prompt_eval_duration']}
        except (httpx.HTTPError, ValueError, KeyError):
            # Explicitly report the fallback, never pass arbitrary model text to TTS.
            for key, words in {"superposition": ["중첩", "superposition"], "entanglement": ["얽힘", "entanglement"],
                               "measurement": ["측정", "measure", "빛", "light", "의식", "conscious"],
                               "gates": ["게이트", "gate"]}.items():
                if any(w in question.text.lower() for w in words): return Plan(intent="concept", concept=key), "fallback", {}
            return Plan(intent="unsupported"), "fallback", {}


def answer(question, snapshot, plan, event=None):
    f = facts(snapshot)
    ko = question.language == 'ko'
    targets = question.targetQubitIds
    result = dict(status="answered", referenceSnapshotId=snapshot.snapshotId, authorityEpoch=snapshot.authorityEpoch,
                  stateVersion=snapshot.stateVersion, selectionId=question.selectionId, targetQubitIds=targets,
                  referenceMode=question.referenceMode, factIds=[], sources=[], facts=f, preview=None)
    def say(k, e): return k if ko else e
    if any(q >= snapshot.qubitCount for q in targets):
        raise ValueError("Selected qubit is absent from the referenced snapshot")
    if plan.intent in ['qubit', 'probability', 'preview'] and len(targets) != 1 or plan.intent == 'pair' and len(targets) != 2:
        result.update(status="needs_selection", text=say("질문할 큐빗 하나를, 두 큐빗의 관계라면 그 둘을 선택해 줘.", "Select one qubit, or select both qubits for a pair question."))
        return result
    if plan.intent in ['qubit', 'probability']:
        node = f['nodes'][targets[0]]
        result['factIds'] = [f"{snapshot.snapshotId}:q{targets[0]}:Z", f"{snapshot.snapshotId}:q{targets[0]}:mixedness"]
        label = node['label']
        if plan.intent == 'probability':
            text = say(f"나는 {label}야. 질문 당시 Z 기저로 측정하면 0은 {node['p0']:.1%}, 1은 {node['p1']:.1%}야. 확률을 계산했을 뿐 실제 측정을 하지는 않았어.",
                       f"I am {label}. For the captured state, a Z measurement gives 0 with {node['p0']:.1%} probability and 1 with {node['p1']:.1%}. I calculated probabilities without measuring the live state.")
        else:
            text = say(f"나는 {label}야. 질문 당시 내 블로흐 화살표 길이는 {node['blochRadius']:.3f}, 국소 순도는 {node['purity']:.3f}야. ",
                       f"I am {label}. My captured Bloch radius is {node['blochRadius']:.3f} and local purity is {node['purity']:.3f}. ")
            if node['entangledWithRest'] is True:
                text += say("공동 상태가 수치적으로 순수하고 내 국소 상태는 혼합되어 있어서, 나는 나머지와 얽혀 있어. 특정 이웃과의 관계는 쌍별로 확인해야 해.", "The joint state is numerically pure and my local state is mixed, so I am entangled with the rest. A particular pair must be checked separately.")
            elif node['entangledWithRest'] is False:
                text += say("공동 상태와 내 국소 상태가 수치적으로 순수해. 설정한 허용오차에서 나머지와의 얽힘은 검출되지 않아.", "The joint and local states are numerically pure. Entanglement with the rest is not detected at the configured tolerance.")
            else:
                text += say("공동 상태가 혼합상태라서 이 길이만으로 얽힘을 판단할 수는 없어.", "The joint state is mixed, so this radius alone cannot establish entanglement.")
    elif plan.intent == 'pair':
        pair = next(p for p in f['pairs'] if {p['first'], p['second']} == set(targets))
        text = say(f"Q{targets[0]+1}와 Q{targets[1]+1}의 질문 당시 로그 음성도는 {pair['logarithmicNegativityBits']:.4f} 비트야. ",
                   f"For the captured pair Q{targets[0]+1} and Q{targets[1]+1}, logarithmic negativity is {pair['logarithmicNegativityBits']:.4f} bits. ")
        text += say("두 큐빗의 얽힘이 수치적으로 검출돼.", "Pair entanglement is numerically detected.") if pair['entanglementStatus'] == 'detected' else say("설정한 수치 허용오차에서 두 큐빗의 얽힘은 검출되지 않았어. 이것만으로 우리 셋의 얽힘까지 부정할 수는 없어.", "Pair entanglement was not detected at the numerical tolerance. This alone does not exclude three-qubit entanglement.")
        text += say(f" 상호정보량 {pair['mutualInformationBits']:.4f} 비트는 전체 상관을 나타내며 얽힘만의 척도는 아니야.", f" Mutual information of {pair['mutualInformationBits']:.4f} bits measures total correlation, not only entanglement.")
        result['factIds'] = [f"{snapshot.snapshotId}:pair:{min(targets)}{max(targets)}:PPT"]
        result['sources'] = ['https://arxiv.org/abs/quant-ph/9605038']
    elif plan.intent == 'event':
        if event is None:
            result['status'] = 'history_unavailable'
            text = say("이 질문에 연결된 조작 기록이 없어. 지금 모습만 보고 과거에 무슨 일이 있었는지 만들어 말하지는 않을게.", "No recorded event is bound to this question. The current appearance cannot reconstruct what happened earlier.")
        else:
            text = say(f"기록된 {event.sequence}번째 조작은 Q{event.target+1}의 {event.operation}야.", f"Recorded event {event.sequence} applied {event.operation} to Q{event.target+1}.")
            if event.operation == 'measureZ':
                text += say(f" 실제 결과는 {event.outcome}, 그 결과의 측정 전 확률은 {event.probability:.1%}였어.", f" The observed outcome was {event.outcome}, with pre-measurement probability {event.probability:.1%}.")
            text += say(" 이것은 기록된 조작 당시의 설명이야. 이후 상호작용으로 상태는 다시 달라질 수 있어.", "This describes the recorded event; later interactions may change the state again.")
            result['factIds'] = [event.eventId]
    elif plan.intent == 'preview':
        result['preview'] = preview(snapshot, plan.gate, targets[0])
        text = say(f"Q{targets[0]+1}에 {plan.gate}를 적용하는 가상 실험이야. 질문 당시 상태의 복사본만 계산했어. 실제 큐빗은 바꾸지 않았어.",
                   f"This is a sandbox preview of {plan.gate} on Q{targets[0]+1}, calculated on a copy of the captured state. The live qubits were not changed.")
        result['factIds'] = [snapshot.snapshotId+':sandbox:'+plan.gate]
    elif plan.intent == 'presentation':
        p = snapshot.presentation
        text = say(f"이 장면의 리본 활성 기준은 쌍별 로그 음성도 {p.ribbonOn:g}, 해제 기준은 {p.ribbonOff:g}야. 음악 진입 기준은 {p.arpeggioOn:g}야. 표현이 없다는 사실만으로 모든 얽힘이 없다고 판단하면 안 돼. 음악은 우리가 정한 상태 표현이야.",
                   f"This scene's pair-ribbon activation threshold is {p.ribbonOn:g}, release threshold {p.ribbonOff:g}, and music entry threshold {p.arpeggioOn:g}. Absence of a visual or sound is not evidence against every form of entanglement. Music is a designed state representation.")
        result['sources'] = ['qixr:Entanglement', 'qixr:QuantumArpeggioComposer']
        result['factIds'] = [snapshot.snapshotId+':presentation']
    elif plan.intent == 'concept' and plan.concept in CONCEPTS:
        block = CONCEPTS[plan.concept]
        text = block[0 if ko else 1]
        result['sources'] = [block[2]]
        result['factIds'] = ['concept:'+plan.concept+':v1']
    else:
        result['status'] = 'unsupported'
        text = say("현재 확인할 수 있는 것은 큐빗 상태, 측정 확률, 두 큐빗의 관계, 기록된 조작, H·X·Z·S 가상 실험이야. 이 범위에 맞춰 다시 물어봐 줘.", "I can explain qubit states, Z probabilities, pair relationships, recorded operations and H/X/Z/S sandbox experiments. Please ask within that scope.")
    result['text'] = text
    return result

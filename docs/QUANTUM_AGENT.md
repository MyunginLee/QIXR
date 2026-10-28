# Quantum: an English-first companion for QIXR

Quantum responds to a person exploring a qubit, with the immediate goal of helping them connect a visible change to something they can inspect or try. It does not require a lesson sequence. This document describes implemented behavior, not observed usability or learning outcomes.

## Interaction contract

1. **Start with what the person noticed.** Use the selected object, a verified recorded action, and the state captured for this question. Begin with one observation, usually in one or two sentences. Leave purity, negativity, tolerances, and citations in opt-in detail and evidence fields.
2. **Accept ordinary conversation.** Greetings, playful pair questions, “nothing happened,” “say that again,” and “let me try” have explicit routes. Do not assume that a novice knows H, Z, or the important scientific question to ask.
3. **Invite an action without taking over.** At most one optional next question appears as a silent button. Clicking it asks the agent; it never operates a live gate. Gate and measurement previews operate on a classical copy of the simulator state. A vague “what if I do that?” asks for the operation instead of defaulting to H.
4. **Keep the answer attached to its evidence.** A small read-only Bloch diagram shows the captured state beside the conversation. Comparison answers show an earlier captured arrow too. The live simulation continues. Repetition and expanded detail retain the original reference rather than relabeling old information as current.
5. **Repair before explaining more.** Ask one clarification about controls, explanation, or the screen. Do not diagnose a broken tracker or claim a successful gate without evidence. Speech transcription goes into the editable question field and requires Ask before becoming an agent turn.
6. **Leave room for exploration.** “Let me try” produces no speech, suggestions, or audio segments. There are no idle timers, automatic tutorials, quiz scores, or unrequested commentary on every state change.
7. **Give the character a bounded identity.** Quantum can speak from the selected object, but does not claim feelings, consciousness, control over random outcomes, or physical messages between entangled objects.

English is the default for API questions, ASR, Unity configuration, UI labels, built-in questions, and newly provisioned pairing files. The EN / 한 control explicitly switches to Korean. A user-provided pairing file with `language: "ko"` remains an explicit locale choice. Existing private host/client files on this machine were changed to English without starting a service; external headset copies need to be replaced or switched by their operator.

## Runtime architecture

`QuantumAgent` wraps the existing local planner and scientific renderers. The English system policy is packaged at `server/qixr_companion/prompts/quantum_agent.txt`.

| Component | Responsibility |
| --- | --- |
| `dialogue.quick_plan` | Recognize high-confidence speech acts and common state questions without a model round trip. |
| `dialogue.Planner` | Route remaining natural-language questions into a strict `Plan` schema using the configured Ollama model. It cannot supply arbitrary speech, numerical values, targets, or executable actions. |
| `QuantumAgent` | Resolve repeat/detail/comparison requests, produce brief answers and optional next questions, and attach reference metadata. |
| `physics` and reviewed renderers | Validate density matrices, calculate state and pair facts, verify events, calculate copy-only previews, and supply scientific explanations. |
| `Service` | Schedule turns, commit answer history at caption delivery, interrupt obsolete turns, and deliver captions/speech. |
| Unity presenter | Show brief captions, a scrollable More response, one optional next question, the read-only reference, editable speech transcripts, and private/shared controls. |

This is a constrained conversational agent, not unrestricted generated scientific dialogue. The model chooses intent; reviewed code owns spoken scientific claims. Model unavailability uses conservative fallback routing. Unknown questions remain explicitly unsupported.

The conversation retains at most eight caption-committed turns in memory. Follow-ups are scoped by conversation, target IDs, authority epoch, language, and visibility. A room-visible question cannot use private dialogue. A shared listener cannot click More on another participant's response and accidentally ask against their own unrelated history. Responses do not automatically disclose past private questions.

Comparison uses actual earlier facts from this scope. It distinguishes equal Z probabilities with different arrow lengths. A shortened local arrow supports an entanglement statement only under the pure-joint-state check; a mixed joint state gets no such inference. Missing history produces a clarification. Comparisons do not claim an unrecorded cause.

## Example interaction

These lines are renderer examples for specified simulated fixtures, not recordings or model/device test results.

**User:** Who are you?

**Quantum:** I'm Quantum, your guide to the qubits you're exploring. These spheres let you see and handle simulated quantum states.

After a verified H on Q1 initially in 0:

**User:** What just happened?

**Quantum:** The recorded H changed Q1's chance of 0 from 100.0% to 50.0%.

**User:** What if I use H again?

**Quantum:** With H on a copy of Q1, the chance of 0 becomes 100.0% and 1 becomes 0.0%. The live qubit stays as it was.

The preview appears separately. It does not apply H to the live qubit.

For a captured entangled Q1/Q2 pair:

**User:** Are you two friends now?

**Quantum:** These two share a quantum relationship here. We call it entanglement.

An optional **What if I move them apart?** button appears silently.

**User:** What if I move them apart?

**Quantum:** Moving qubits apart stops their interaction when they're beyond coupling range. Distance alone doesn't erase entanglement they already share.

For a remembered pure superposition and a later pure joint Bell state with the same local Z probabilities:

**User:** Why is it shorter than before?

**Quantum:** Q1 has the same Z probabilities as before, but a shorter arrow. In this pure joint state, the qubit is now entangled with the rest.

The reference panel shows the two captured arrows. Without the earlier capture, Quantum says it lacks the earlier state rather than inventing this comparison.

**User:** I didn't understand.

**Quantum:** Which part should we check: the controls, my explanation, or what changed on screen?

**User:** Let me try.

Quantum stays quiet. The user can keep manipulating the scene.

## Verification and boundaries

The new interaction checks use analytical state fixtures, an in-process ASGI transport, a disabled speech engine, and mocked model HTTP responses. They test intent distinctions, brief/default-English responses, opt-in detail, historical references, comparison, sandbox isolation, missing evidence, private/shared memory isolation, cancellation, and quiet turns. No model workers, gateway listeners, speech engines, or live capacity benchmarks are started by these checks.

On 2026-10-28, the offline Python suite passed **64 tests**. The .NET harness passed the existing numerical checks and PCM16 round trip, compiled the DTOs, and parsed **12 C# source files** without syntax errors.

The headless C# harness checks numerical code, DTO compilation, and companion syntax. It does not compile the entire Unity project. The new UI, microphone correction flow, reference diagram placement, actual model routing quality, English voice intelligibility, and headset interaction require a later explicitly authorized device/runtime pass. Earlier timing measurements in the implementation record predate this agent update.

The service remains stopped and disabled. Installing or editing this agent does not authorize runtime startup.

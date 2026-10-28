# QIXR quantum companion implementation

Implemented on 2026-10-28 in branch `feat/quantum-companion`, extending `test-multiuser` at `a0fba5a`. See [launch and pairing instructions](../server/README.md), [original design](QUANTUM_COMPANION_DESIGN.md), and [recorded measurements](../server/reports/validation-2026-10-28.json).

The HTTPS gateway and local inference/speech pipeline were exercised on this GB10 host, then stopped and automatic startup disabled at the user's request on 2026-10-28. Runtime execution now requires an explicit user request. Unity integration source is written for the functionality below, but lifecycle, role/configuration handling and device interaction still need hardening and validation. **A full Unity Editor compilation, Android IL2CPP build and Quest execution have not been performed on this ARM64 host.** The numerical C# components were compiled/executed in a .NET harness, and all new companion C# syntax was checked.

An English-first conversational agent was subsequently added using offline checks only. It adds brief first answers, optional detail and next questions, scoped conversation memory, captured-state comparison, repeat/repair/quiet handling, transcript review, and a read-only state reference. See [the agent contract](QUANTUM_AGENT.md). The runtime timings below describe the earlier implementation and do not measure this agent update.

For the Unity-capable PC, use [the source import guide](handoff/00_START_HERE.md), [the implementation prompt](handoff/01_UNITY_QUEST_IMPLEMENTATION_PROMPT.md), [the matching server contract](handoff/02_SERVER_CONTRACT.md) and [Quest acceptance criteria](handoff/03_QUEST_ACCEPTANCE.md). These identify concrete unfinished areas rather than treating the source/syntax checks as a finished standalone app.

## Implemented behavior

| Capability | Implementation |
| --- | --- |
| State-grounded conversation | Independent density-matrix validation, local/pair metrics, Z probabilities, reviewed Korean/English explanations and a typed local-model intent planner. Unknown questions return a scope response. |
| Physical distinctions | Global purity is checked before interpreting local mixedness as entanglement. Pair negativity, total correlation and multipartite structure remain distinct. Near-tolerance statements retain numerical scope. |
| Event explanations | Unity captures actual authoritative gate/measurement before/after matrices, versions, targets and results. The server verifies the claimed transition rather than trusting metadata alone. |
| Sandbox | H/X/Z/S and enumerated Z-measurement branches operate on copies; the Unity presentation creates disposable preview objects and never executes a live quantum operation. |
| Local speech | Multilingual faster-whisper small ASR, Korean Mimic3 KSS and English Piper voices through Sherpa ONNX, bounded upload/queues and caption fallback. |
| Unity interaction | Runtime activation from a pairing file; explicit one/two-qubit selection, grab selection, world-space UI, text input, push-to-talk, language switch, stop control, captions, speaking markers and arpeggio ducking. Korean font and its license are included. |
| Temporal binding | Captured snapshot, authority epoch, selection ID, request ID and turn generation accompany answers. New selections/questions/epoch changes interrupt obsolete playback. Explicit live requests additionally compare numerical state facts before continuing speech. |
| Multiple participants | Separate join and publish credentials, participant conversation tokens, private history/messages/audio, optional room-shared delivery and a serialized shared speaking queue. |
| Recovery | Participant reconnect and host epoch re-establishment, immutable publication retries, bounded journals/caches, expiration, queue rejection and explicit unavailable/busy/error responses. |
| Deployment | Dedicated HTTPS user service, certificate fingerprint pinning in Unity, private role-specific configuration files, pinned speech downloads and package lock, readiness/metrics endpoints. |

Model prose is not treated as numerical evidence. The local model chooses an intent/concept plan; scientific values and sentences come from the verified facts and reviewed blocks. This keeps scope narrower than an unrestricted general-purpose tutor while allowing different phrasing of supported questions.

## Executed checks

- **21 Python tests passed:** analytic product/Bell/GHZ/W/classical/mixed cases, invalid states, copied-state previews, event contradictions, authentication roles, participant isolation, request idempotency, cancellation, epochs, shared/private delivery, client/queue limits, payload limits, expired sessions and WebSocket authentication.
- **Existing C# numerical suites passed:** two-qubit baseline, three-qubit engine and phase-2 entanglement metrics, compiled from the actual repository sources. A minimal Unity value-type/logging shim supplies the few non-runtime references; it is not a Unity build.
- **Six C# wire snapshots independently recomputed in Python:** product, Bell with spectator, GHZ, W, dephased classical GHZ mixture and maximally mixed product. Local and pair metrics agreed within 1e-7.
- **PCM16 WAV codec round trip passed.** Roslyn parsed the companion scripts and changed integration files without syntax errors.
- **Real local model/gateway tests completed** for 1, 2, 4 and 8 simultaneous text requests and 1, 2 and 4 requests including speech synthesis. Isolated fixture rooms were used and removed.
- **Synthetic speech loopbacks completed** in Korean and English: locally synthesized question → HTTPS ASR → actual answer route → first generated response WAV retrieved. These are software tests, not recordings from a Quest microphone.

## Observed latency

The following are ranges across a single burst per configuration, including queue waiting. They are **not p95 estimates or maximum-capacity certifications**. The shared Ollama runtime may have competing workloads. The first cold model-routing request took 4.43 seconds; warm results are shown separately.

| Test | Warm observed caption availability | First generated response audio availability |
| --- | --- | --- |
| 2 simultaneous text requests | 0.68–1.01 s | Not requested |
| 4 simultaneous text requests | 0.40–1.67 s | Not requested |
| 8 simultaneous text requests | 0.45–3.42 s | Not requested |
| 1 request with TTS | 0.38 s | 0.69 s |
| 2 simultaneous requests with TTS | 0.67–1.01 s | 1.07–1.29 s |
| 4 simultaneous requests with TTS | 0.40–2.28 s | 0.80–2.52 s |
| Korean synthetic speech loopback | ASR: 1.22 s | 1.35 s after upload began |
| English synthetic speech loopback | ASR: 0.92 s | 1.83 s after upload began |

Text/TTS bursts exclude ASR and headset playback. The synthetic loopback includes ASR and WAV retrieval, but excludes the time spent speaking the question and actual headset audio buffering. The configured first deployment is 8 connected participants with 2 active answer jobs and 8 waiting jobs. Eight-request text completion does not establish eight simultaneous microphone conversations at a 3-second service level.

## Material limitations and device pass

Korean ASR still makes word errors. An initial short synthetic question lost the word “확률” and was routed incorrectly; locale-specific terminology and a wider ASR beam improved intent recovery. In the successful Korean loopback, the transcript still differed from the input. Initial failures are retained in the report rather than counted as successful recognition. Device microphone accuracy and the voice's intelligibility need listening tests; template safety does not establish speech recognition quality.

On a supported Unity development machine, install the host pairing configuration and compile with the repository's pinned Unity/packages. Then run the enabled musical scene and verify: real state and event publication; single/pair selection; first voice turn in both languages; X/A and B/Space controls; music duck/restore and interruption; sandbox isolation; two-client private/shared dialogue; disconnect/restart and authority changes; and headset frame rate. These observations cannot be supplied by the current headless host.

The implementation does not migrate Fusion/Unity hosting to Linux ARM64, add unrestricted scientific discourse, automatically apply proposed gates to the live experiment, train a new language model, or establish improved human learning. Those require separate implementation or evidence beyond this delivered companion path.

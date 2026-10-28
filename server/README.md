# QIXR quantum companion server

The English-first Quantum agent combines a local language model with independently computed quantum facts and reviewed explanations. Korean is an explicit alternate locale. Unity remains the authority for the live experiment. The server reads published states, records verified gate/measurement events, and evaluates sandbox operations without changing Unity's state. See [the agent's interaction contract and English examples](../docs/QUANTUM_AGENT.md).

The implemented user interface supports selecting/grabbing qubits, text questions, push-to-talk with transcript review, brief captions with opt-in More, one optional next question, a captured-state reference diagram, interruptible speech, speaking markers, local music ducking, and sandbox previews. Install the role-specific pairing file to activate it. The configured pipeline uses **Ollama qwen3:8b**, **faster-whisper small** on CPU, and **Sherpa ONNX English/Korean TTS** on CPU. Existing Ollama settings and other projects are not modified.

## Current local deployment

- HTTPS gateway: `https://10.225.12.32:8786` on the lab LAN; the address is specific to the current host/network.
- User service: `qixr-companion.service`, currently **stopped and disabled** at the user's request. Start only on an explicit runtime request; installation does not start or enable it.
- Initial limits: 8 participant sessions per room, 2 concurrent answer jobs, 8 waiting jobs. These are configured limits, not established hardware maxima.
- Private runtime data: `../.runtime/` relative to this directory; credentials and model files are ignored by Git.
- Readiness: `/readyz` reports the actual model and speech-worker availability. `/healthz` reports only gateway health.

Source implementation, actual execution, and remaining device checks are distinguished in [the implementation record](../docs/QUANTUM_COMPANION_IMPLEMENTATION.md).

## 1 Install or reproduce

Run from the repository root with Python 3.12 or later (the current deployment uses 3.12 on ARM64):

```bash
python3 -m venv .venv
.venv/bin/pip install -r server/requirements.lock
.venv/bin/pip install --no-deps -e server
.venv/bin/python server/scripts/setup_models.py
```

The lock file records the packages actually installed on this host. Speech downloads are pinned in `model-manifest.json`; the script verifies the TTS archive hashes and pins the ASR repository revision. Keep third-party model attribution/license materials with the downloads; links are listed below.

Provide an Ollama server with the model shown in `.runtime/service.env`. This machine already had `qwen3:8b`, so no language-model download or global Ollama reconfiguration was needed. To use a separately managed model server, set `QIXR_OLLAMA_URL` and `QIXR_MODEL` in that environment file. Use a local trusted endpoint; the companion does not send prompts to a remote provider by default.

Install the dedicated user service without starting it:

```bash
.venv/bin/python server/scripts/install_service.py
```

Only after an explicit runtime request, start it and create/reuse pairing files:

```bash
systemctl --user start qixr-companion
.venv/bin/python server/scripts/provision.py
systemctl --user status qixr-companion
curl --cacert .runtime/tls/server.crt https://127.0.0.1:8786/readyz
```

The installer generates a private-key/certificate pair with localhost and current host IP SANs. `provision.py --client-url https://YOUR_LAN_IP:8786` overrides the endpoint exported to Unity. Pairing defaults to English; use `--language ko` for an explicit Korean configuration. Regenerate/reissue the certificate deliberately if host names/IPs change or it expires; the Unity certificate fingerprint must then be updated too. The service is attached to the user's systemd manager. Unattended availability after logout/reboot depends on that manager's existing login/linger policy; no system-wide policy is changed.

To run in a terminal instead of systemd, stop this user service first and use:

```bash
QIXR_RUNTIME="$PWD/.runtime" .venv/bin/python -m uvicorn qixr_companion.api:app \
  --host 0.0.0.0 --port 8786 \
  --ssl-certfile .runtime/tls/server.crt --ssl-keyfile .runtime/tls/server.key \
  --ws-max-size 1048576 --limit-concurrency 64
```

Run **one gateway worker**: room snapshots, conversation generations and scheduling are deliberately in one process. SQLite persists room credentials as hashes and authority epochs; it does not persist audio, matrices or conversations. After a restart the Unity authority establishes a new epoch and republishes its state. Room-scoped publishing credentials and joining credentials have distinct permissions. Participant tokens expire after 120 seconds without API/stream activity; the Unity polling loop keeps an active session alive.

## 2 Connect Unity and Quest

Use the existing `test-multiuser` project baseline, Unity **6000.0.25f1**, and its enabled `MusicalRibbonVisualization` scene. No scene asset migration or package upgrade is required.

1. Transfer `.runtime/host-config.json` privately to the machine/build that owns the quantum experiment. Transfer `.runtime/client-config.json` to the other participants. **Only the authority configuration contains the publishing credential.** Do not place it in every participant build.
2. In Unity select **QIXR → Companion → Install pairing configuration** and choose the appropriate JSON. This writes `Assets/StreamingAssets/qixr-companion.json`, which is ignored by Git. Rebuild each role separately or provision the file through your deployment process.
3. Start the scene. The runtime bootstrap adds the companion panel after loading a valid configuration. The authority uploads states at up to 5 Hz and sends before/after snapshots for actual discrete operations. A Fusion participant reads canonical context from this authority through the server.
4. Select Q1/Q2/Q3 buttons or grab a qubit. To ask about a pair, explicitly select both with the buttons. Use the text field or built-in question buttons.
5. Hold **Hold to talk** and release to transcribe, or hold **T** in the Editor. Review/edit the recognized question and press **Ask**. Allow microphone permission on Quest. **Escape** or **Stop** cancels the turn and stops playback locally. X/A measurement and B/Space metric controls are preserved.
6. Switch `EN / 한` for language. Toggle **Private / Shared** to join shared presentation mode; otherwise questions remain private. Shared-room speech is serialized and only room-scoped shared messages/audio are exposed to other participants.
7. **More** expands the previous answer without relabeling its state as current. The optional next-question button sends only a question; it never applies a live operation. “Let me try” produces no spoken response. There is no idle narration.

Questions use an immutable captured state, labeled as question-time context. Continuous evolution continues. Request IDs, target IDs, selection IDs, authority epoch and generation prevent an obsolete response from moving to a new selection. When a connection or epoch changes, the client interrupts and reconnects; ask again after reconnection. Event-journal overflow creates a new epoch and a visible history boundary rather than silently inventing missing events.

Speech is delivered sentence by sentence. A sandbox appears beside the panel and expires after 25 seconds. Previewing a measurement lists branches and probabilities; it does not sample or measure the live experiment. No “execute arbitrary code” tool or automatic live gate execution exists.

## 3 Protocol and client alternatives

The FastAPI schema is available at `/openapi.json` and `/docs`. Request and response DTOs are in `qixr_companion/contracts.py` and `Assets/Scripts/Companion/QuantumCompanionContracts.cs`.

| Endpoint | Credential and purpose |
| --- | --- |
| `POST /v1/rooms`, `DELETE /v1/rooms/{id}` | Local admin bearer token; room provisioning/cleanup. |
| `GET /v1/rooms/{id}`, `POST .../epoch` | Authority bearer token; inspect/reset authority epoch. |
| `POST .../snapshots`, `POST .../events` | Authority token; immutable, numerically checked state/event publication. |
| `POST .../conversations` | Room join token; obtain a private participant conversation token. |
| `GET /v1/conversations/{id}/context` | Conversation token; latest canonical snapshot and recorded event ID. |
| `POST .../questions`, `GET .../requests/{requestId}` | Conversation token; submit and poll a turn. |
| `GET .../requests/{requestId}/audio/{index}` | Conversation token; sentence WAV bytes, never cached. |
| `POST .../transcribe?language=en` | Conversation token; mono PCM16 WAV, 16 kHz, at most 15 seconds. English defaults; `ko` is supported. ASR queues are bounded. |
| `POST .../cancel`, `DELETE .../{conversationId}` | Conversation token; invalidate turn generation or leave. |
| `GET .../messages?after={cursor}` | Conversation token; bounded private/shared event delivery with a monotonic cursor. |
| `GET .../shared/{speaker}/{requestId}/audio/{index}` | Recipient conversation token; same-room shared audio only. |

Unity uses HTTPS polling and WAV retrieval to avoid introducing a new Android WebSocket library. Other clients can use `wss://HOST:8786/v1/conversations/{id}/stream`. The first JSON message must be `{"type":"auth","protocolVersion":1,"token":"CONVERSATION_TOKEN"}`. Then send `{"type":"question","question":QUESTION_DTO}`, `{"type":"cancel"}` or `{"type":"ping"}`. Event types include `accepted`, `answer`, `segment`, `audio_ready`, `done`, `cancelled`, `context_changed` and `authority_reset`. `audio_ready` carries an index for authenticated WAV retrieval. Keepalive pings must occur within the session idle lifetime. Slow consumers can resynchronize through the messages endpoint. Audio upload uses the bounded WAV endpoint in this version.

Snapshots use interleaved complex float64 values in row-major order, q0 as the most significant bit. Display names Q1/Q2/Q3 map to IDs 0/1/2. Renyi-2 entropy uses nats; logarithmic negativity and mutual information use bits. General mixed-state multipartite entanglement is not inferred from the original local-entropy heuristic.

The language model selects a typed conversational intent and concept block under the packaged English agent policy. The agent supports greetings, help, repair, repetition, opt-in detail, and comparisons using bounded history scoped by participant, targets, authority epoch, language, and visibility. Shared answers cannot use private history. Critical scientific sentences and values are rendered by reviewed templates; arbitrary generated prose is not passed to speech as verified physics. Clear questions can bypass inference. Model failure returns an explicitly labeled fallback or unsupported response, and never pauses Unity physics.

## 4 Verification and capacity measurement

Offline development checks (no gateway/model startup):

```bash
.venv/bin/pytest -q server/tests
dotnet run --project server/csharp/HeadlessChecks.csproj -- "$PWD"
.venv/bin/python server/scripts/check_csharp.py
```

Only after an explicit runtime request, live capacity/speech checks:

```bash
.venv/bin/python server/scripts/smoke.py --concurrency 1,2,4,8
.venv/bin/python server/scripts/smoke.py --concurrency 1,2,4 --voice
.venv/bin/python server/scripts/speech_smoke.py
```

`dotnet` needs an ARM64 SDK on this host; the local development SDK is `.tools/dotnet/dotnet`. The C# harness compiles the actual quantum engine, metric code, wire DTOs and WAV codec using a minimal Unity value-type/logging shim, executes the repository's existing numerical checks and emits golden snapshots for independent Python recomputation. It also parses all new companion C# syntax. **It does not compile the whole Unity project or validate XR input/rendering.**

The live smoke scripts create a separate fixture room and remove it afterward. They never publish synthetic data into the configured Unity room. `smoke.py` measures real model routing plus gateway/queue/optional TTS; it excludes microphone capture and ASR. `speech_smoke.py` additionally measures generated speech through the ASR endpoint and answer/TTS retrieval, in both languages. Both are software tests; actual microphone accuracy, target-device frame rate and audible headset timing require the device pass in the implementation record.

## Dependencies and attribution

- [Ollama structured outputs](https://docs.ollama.com/capabilities/structured-outputs): local JSON intent planning.
- [faster-whisper](https://github.com/SYSTRAN/faster-whisper), [small checkpoint](https://huggingface.co/Systran/faster-whisper-small): local ASR; pinned checkpoint revision in the manifest.
- [Sherpa ONNX](https://github.com/k2-fsa/sherpa-onnx), [model releases](https://github.com/k2-fsa/sherpa-onnx/releases/tag/tts-models): local TTS. Korean weights are converted from [Mimic3 KSS](https://github.com/MycroftAI/mimic3-voices/tree/master/voices/ko_KO/kss_low); English weights include their original `MODEL_CARD`. Keep the upstream model/data license terms with any redistribution.
- [Nanum Gothic](https://github.com/google/fonts/tree/main/ofl/nanumgothic): bundled Korean/Latin font, SIL Open Font License included in `Assets/Resources/Quantum/OFL.txt`.
- Scientific concepts and reviewed source links are versioned in `qixr_companion/dialogue.py`; the original QIXR paper and pair-separability reference remain cited in the design/audit.

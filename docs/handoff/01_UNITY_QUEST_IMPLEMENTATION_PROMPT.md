# Implementation task: finish the QIXR companion for standalone Quest

You are working on the Unity-capable PC. Implement and validate the Unity client corresponding to the existing QIXR companion server. Deliver an Android ARM64 standalone Quest app that uses that server on the LAN. Work on the imported source, not an unrelated replacement application.

## 1. Outcome and execution boundary

The final deployment must be:

```text
Unity development PC --build/install--> Quest APK

Quest authority: quantum engine + XR + microphone + playback
    --HTTPS: state/event publication and questions-->
Linux host: companion gateway + local model + ASR + TTS
    --HTTPS: referenced answers and WAV audio-->
Quest

Additional Quests <--existing Fusion state/pose transport--> Quest authority
Each additional Quest <--own authenticated dialogue--> Linux companion
```

The PC is a development tool, not a runtime dependency. Do not meet the goal with Editor Play Mode, Quest Link, Air Link, browser chat, or a remote desktop microphone. Do not move model inference onto the headset. Preserve the existing three-qubit simulation and musical ribbon scene.

**Default execution scope:** implement, open/import Unity, compile, run isolated offline tests, and build the APK. Do not start/enable/restart the Linux gateway, Ollama, ASR or TTS; do not launch live network integration or the Quest app until the user explicitly requests runtime execution. Offline Play Mode tests must use an injected transport and avoid bootstrap network calls. Prepare everything possible before requesting runtime execution. If the user has already explicitly authorized a live run, proceed within that authorization without asking again. Never modify unrelated host services.

If Unity licensing, Android modules, credentials, or a headset are unavailable, complete independent work and report the exact missing dependency. Distinguish source complete, Editor compiled, offline tests passed, APK built, installed, standalone launched, and end-to-end verified.

## 2. Establish the correct source

Follow `00_START_HERE.md`. The imported baseline is commit `a0fba5a12ef37eef8afb081586d2fff175a11443` plus the supplied working-tree overlay. Do not assume GitHub main contains these additions. Preserve unrelated local changes and all Unity GUIDs.

Read repository `AGENTS.md`, then:

- `docs/QUANTUM_AGENT.md`
- `docs/handoff/02_SERVER_CONTRACT.md`
- `docs/handoff/03_QUEST_ACCEPTANCE.md`
- `server/qixr_companion/contracts.py`, `api.py`, `service.py`, `agent.py`
- `Assets/Scripts/Companion/` and `Assets/Editor/QuantumCompanionSetup.cs`
- `Assets/Scripts/Colocation/QuantumSessionState.cs`
- `Assets/Scripts/Qubit/QubitManager.cs`
- `Packages/manifest.json`, `packages-lock.json`, `ProjectSettings/ProjectVersion.txt`
- `Assets/Plugins/Android/AndroidManifest.xml` and the enabled scene/build settings.

Start with **Unity 6000.0.25f1**, XR Interaction Toolkit **3.0.7**, and the package versions actually pinned in this checkout. Meta packages, OpenXR and Fusion are already present. The old `Assets/Samples/.../2.5.4` directory is not evidence that the installed XRI version is 2.5.4. Resolve actual API/compiler errors; do not upgrade Unity, XR, Meta packages, Fusion or URP just to match newer documentation. Record a necessary compatibility change and its evidence if one is unavoidable.

## 3. What is already written, and what is not proven

| Source area | Existing implementation | Remaining obligation |
| --- | --- | --- |
| `QuantumCompanionClient` | HTTPS pairing, join/poll, state/event upload, selection/generation cancellation, text, microphone PCM, ASR, WAV retrieval, private/shared mode | Full Unity compilation, Android execution, lifecycle/retry correctness, response freshness and diagnostics |
| `QuantumContextExporter` / `QuantumEventJournal` | Canonical density matrix export, authoritative before/after event journal | Verify exact event ordering and single publisher through startup, disconnect and authority changes |
| Presenter / talk / preview / reference | Runtime world-space panel, English controls, brief/More/next, transcript editing, markers, ducking, copy preview and captured-state diagrams | Verify controller/poke events, keyboard, layout, audio, materials, cleanup and Quest frame cost |
| Editor setup | Copies a role-specific pairing file into StreamingAssets | Build validation, device configuration workflow, correct authority/participant APK separation |
| Quantum/Fusion hooks | Real operations emit mutations; clients consume shared state | Preserve existing gates, measurement, colocation and music; verify no second simulator/publisher |

Previous evidence is limited to offline Python tests and a minimal .NET math/DTO/syntax harness. There is no full Unity/Quest validation for this addition. Do not convert that evidence into a claim that the APK works.

## 4. Compile the actual Unity project and finish the Android build path

Open the imported project in the pinned Editor with Android Build Support, SDK/NDK/OpenJDK modules. Fix actual runtime and Editor assembly errors, including package namespaces and Fusion weaving. Keep Editor-only code out of player assemblies.

Keep `Assets/Scenes/MusicalRibbonVisualization.unity` as the intended enabled scene. Inspect missing scripts/references, existing XR Origin, input action bindings and EventSystem before adding duplicates. Configure a standalone Android **ARM64 / IL2CPP** build and the existing Quest/OpenXR features. Check the actual merged Android manifest and build output for network and microphone access; Unity may add permissions automatically, so the source manifest's absence of a permission is not sufficient proof of a bug. Handle permission denial and later grant visibly.

Add a repeatable Editor validation/build entry point, for example `QIXR/Companion/Validate Quest Setup` and a callable build method. It should validate the scene, config format, role, HTTPS endpoint, fingerprint, required assets and XR UI input setup, and write a clear build report. Produce a development APK in an ignored build directory. Do not commit app signing secrets or credentials; do not remove an unrelated installed app to resolve a package-ID conflict.

Support an explicitly **unpaired/offline development build** with a visible pairing screen, so missing operator credentials do not block compilation, mock tests or APK production. Do not embed fake credentials as if the app were connected. Mark pairing/live checks pending separately. If a paired build is requested, validate the supplied role configuration rather than accepting placeholders.

Check IL2CPP serialization, `System.Numerics`, cryptography, callbacks, fonts and URP materials in the real build. Add stripping preservation only for concrete required types; do not disable all stripping as a substitute for diagnosis.

## 5. Complete configuration and transport as a device feature

Use `02_SERVER_CONTRACT.md` and the Python code as the authority. Retain HTTPS polling and WAV download unless an actual limitation warrants another compatible transport. Do not introduce an Android WebSocket dependency just to mirror the server's optional WebSocket API.

The current loader reads `Assets/StreamingAssets/qixr-companion.json` with UnityWebRequest. Preserve Android `jar:file://...!/assets/...` handling. Validate null/empty URL, role tokens, room ID, locale, and a well-formed SHA-256 leaf certificate pin before using them. For this self-signed lab endpoint, never accept every certificate or fall back to HTTP.

Finish a deliberate configuration workflow. Prefer an app-private `persistentDataPath` override before the embedded build-time configuration, with validated import/replacement and a visible connection/pairing screen. If this PC cannot provide a practical on-device import yet, deliver a reliable per-role build-time workflow and explicitly identify the limitation. Do not call editable text input a working pairing importer without testing the headset keyboard.

The source defaults to `https://localhost:8786`, which is a placeholder. On Quest, localhost is the headset. The preparation-time LAN address of the Linux host was `https://10.225.12.32:8786`; confirm the current reachable address and matching certificate with the operator before a live run. Do not hardcode a guessed host, fingerprint or room. Use private `.runtime/host-config.json` / `client-config.json` supplied separately by the server operator. Do not ask for or transfer the server admin token, TLS private key, model files, or service environment to Quest.

The Linux service remains stopped until explicitly requested. Do not start a replacement gateway on the Unity PC when the real server is unavailable. Continue Editor tests with isolated fixtures instead.

Expose useful, redacted diagnostics: connection state, HTTP status/error category, role, room, authority epoch, snapshot/version, and pending turn. Never log bearer tokens, full pairing JSON, or microphone payloads. Distinguish unavailable conversation from unavailable quantum simulation. Make a missing/invalid pairing file visible; currently the client can disable itself before creating the presenter, leaving no in-headset explanation.

## 6. Enforce one simulation authority

First make **one Quest with the authority role** work without an Editor host. That Quest evolves the shared density matrix and publishes it to the companion. The Linux gateway reads and checks state; it does not host the Unity/Fusion simulation.

For multiple Quests, retain Fusion's authoritative state/pose path. Only the device that both has the authority credential **and actually owns simulation authority** may publish. Participants use the join-only configuration. Match the companion room to the intended Fusion experiment; do not let unrelated standalone sessions publish into the same companion room.

Inspect `QuantumContextExporter.IsAuthority`: absence of an active `QuantumSessionState` currently falls back to standalone authority. Handle the distinction between intentional standalone mode and a network startup/disconnect transition. Do not accidentally run two independent experiments during reconnect.

The current credentials are fixed-role and the server has no publisher-lease transfer API. If Fusion authority migrates to a join-only participant, that participant cannot become a companion publisher. Implement a documented fixed-authority policy with a visible degraded state/rejoin path, or propose a coordinated, minimal server/client migration change. Never solve this by embedding the authority token in every participant APK or letting every client reset epochs. Do not claim seamless authority migration if only fixed-authority recovery is implemented.

Verify ordered before snapshot → after snapshot → event publication, monotonic event sequences, retries with the same immutable IDs, journal overflow and epoch resets. Keep failed entries until acknowledged. A network float32 state must be labeled/tolerated accordingly if exported as such; do not silently change density order or precision semantics to bypass validation.

## 7. Finish turn ownership, cancellation and lifecycle

Bind each question/response/audio task to conversation ID, request ID, selection ID, generation, authority epoch and its referenced snapshot. The current private polling checks selection; shared delivery has less complete per-answer ownership. Audit and fix races instead of relying on text arriving in order.

Selecting another qubit, starting a new turn, stopping speech, changing language/privacy, leaving the scene, loss of focus, or application pause must stop obsolete local playback and invalidate old downloads/callbacks. Abort/dispose pending requests where appropriate and guard callbacks even when an abort is late. Ensure one joined conversation and one publish/message loop per active client after resume. Expired conversation credentials must trigger a clean rejoin. A gateway restart, an expired snapshot and an authority change are distinct conditions, not reasons to loop indefinitely on every 404.

Use bounded retry/backoff for unavailable networks and busy responses. Preserve physics when conversation is unavailable. Handle canonical-state republishing and a coherent event-history boundary after server restart. Verify no stale journal from an earlier epoch is uploaded after authority recovery.

For shared presentation, track the speaker/request/generation currently on screen and reject obsolete audio. A cancellation from one shared turn must not clear a different active private turn. Leaving Shared stops shared playback. Keep private questions, histories and audio private. The gateway serializes shared answer jobs; that alone does not prove audible playback on different headsets cannot overlap, so test and coordinate local playback boundaries if needed.

## 8. Make the experience usable entirely inside Quest

Use the English-first interaction contract in `docs/QUANTUM_AGENT.md`. Preserve short first answers, More, one silent optional next question, repeat/repair, captured-state comparison and quiet responses. No idle narration, forced quiz, or automatic live gate execution.

Complete all of these using the actual scene's XR input system:

- Select/grab a single qubit and deliberately select a pair without controller bindings also measuring it.
- Hold/release Talk with a Quest controller; stop recording on pointer cancellation, app pause, or tracking loss. T/Escape are Editor conveniences, not the shipping controls.
- Review and edit recognized speech, press Ask, and use the headset's keyboard for new text questions. No hidden reliance on a physical PC keyboard.
- Use More/next/Stop/language/privacy controls and scroll a long detailed answer.
- Keep one correctly configured EventSystem/input module and working tracked-device UI raycasts. If an EventSystem exists already, inspect its module; the current fallback only creates XRUIInputModule when no EventSystem exists.
- Separate the live qubits, captured-state reference, earlier comparison and sandbox visually. The current preview/reference positions occupy similar space; correct overlap and verify labels/readability in-headset. Never animate a scientific Bloch vector as a talking emotion.
- Play the English WAV on Quest speakers/headphones, duck only intended local music, and restore it on normal end, cancel, failure, pause and scene exit. No microphone echo loop or new audio from an obsolete turn. Keep typed captions when ASR/TTS is unavailable.
- Preserve gates, actual Z measurement controls, colocation, musical ribbons and the existing suppression of legacy FM audio.

Profile rather than guess at headset cost. The current presenter performs repeated scene searches and recreates selection halos on a timer; inspect allocation/material cleanup and reduce avoidable per-frame churn where it affects the actual device. Do not claim a measured frame rate from desktop execution.

## 9. Verify in stages and keep the server contract aligned

Implement focused Unity EditMode/PlayMode tests using `fixtures/protocol-v1.json` and an injectable mock transport. Ensure tests cannot bootstrap the real companion or load model/speech workers. Cover wire round trips, source/preview isolation, selection change during an in-flight turn, cancellation, duplicate delivery, stale generations/epochs, quiet answers, ASR correction and retry/rejoin boundaries.

Compile the full project for Android and build IL2CPP. Then follow `03_QUEST_ACCEPTANCE.md`. Device/live stages remain pending until the user requests runtime execution and supplies the needed headset/network/role configuration. An APK build is not evidence that the headset microphone, TLS or UI works.

If a necessary fix changes the wire contract, add matching Python contract/agent/API tests and return a server patch plus its deployment instructions. Do not silently deploy it to the Linux host. Do not replace its independently calculated science with client guesses or arbitrary generated claims.

## 10. Deliver concrete results

Deliver source changes, Unity `.meta` files, tests, the repeatable build/validation entry point, the APK path and checksum when built, and a report at `docs/QUEST_COMPANION_VALIDATION.md` containing:

1. Imported base/overlay identity and final source revision or working-tree manifest.
2. Actual Editor/package/build-tool versions and changed settings.
3. Which stages passed, with redacted logs and fixture/device distinction.
4. Role configuration/import instructions and how to operate the standalone APK.
5. Actual end-to-end request/reference/audio evidence only if executed.
6. Remaining defects or dependencies, including authority migration limits.
7. Any server-side patch required before the next explicitly authorized run.

Do not stop at a plan or a set of mock UI screens. Complete all authorized source/build work, preserve the stopped-server policy, and state the remaining runtime boundary plainly.

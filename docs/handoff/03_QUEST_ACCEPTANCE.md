# Standalone Quest acceptance and evidence

Record each row as PASS / FAIL / NOT RUN, with the actual build/source identity. These are acceptance criteria, not existing test results. Prepare the implementation and offline/build checks first. Install/launch/live integration requires the user's explicit runtime request; a stopped server is not a failed offline development task.

## A. Import, Editor and offline tests

| Check | Required evidence |
| --- | --- |
| Matched source | Manifest/base/patch/overlay identities verified; no dropped companion files or Unity GUIDs |
| Real Unity compilation | Unity 6000.0.25f1 runtime and Editor assembly logs; no new compile/weaver errors |
| Scene and XR bindings | Enabled musical scene; no missing scripts; one working XR UI event path; intentional standalone vs Fusion mode |
| Wire compatibility | Fixture requests round-trip to the Python schema; response parsing retains target/version/reference/generation semantics |
| Cancellation and recovery | Mock tests with late audio, target change, duplicate deliveries, changed epoch, expired session, busy/unavailable response, pause/resume |
| Physics isolation | Asking, More, comparison, reference drawing and sandbox never mutate the live engine |
| Language and quiet turns | English default throughout; explicit Korean switch; empty quiet responses do not reach audio playback |
| Offline independence | Tests use fixture/mock transport and do not start a gateway or contact inference/speech services |

Fixtures in `fixtures/protocol-v1.json` are analytical simulation examples, not live network traces. Timing, tracking, voice quality and usability cannot be inferred from them.

## B. Android artifact

| Check | Required evidence |
| --- | --- |
| Standalone target | Android ARM64/IL2CPP build succeeds; Editor/build-tool versions logged |
| Packaged dependencies | Intended scene, needed shaders/fonts/serialization types included; no runtime reliance on `UnityEditor` |
| Manifest and permissions | Inspect merged manifest for network/microphone; runtime denial path implemented |
| Per-role pairing | Valid endpoint/pin/room/locale, authority vs participant configs; no admin token or server private key in APK |
| Artifact | APK path, SHA-256, application identifier, version and build report |

An APK can pass this stage without having been installed or launched. Report that distinction.

## C. One Quest, after explicit runtime authorization

Use one designated authority Quest. The operator starts the Linux companion deliberately and provides the correct role pairing. Keep the development PC out of the runtime data path.

1. Install the APK, unplug USB/disable PCVR, and launch from Quest's app library. Record the headset model, OS and build identity. Verify the scene runs without an Editor or PC host.
2. With companion unavailable, verify visible connection feedback and working qubit manipulation/measurement/music. Restore connectivity during an authorized test and verify bounded recovery.
3. Use the actual LAN endpoint and certificate pin. Confirm a deliberately incorrect pin is rejected. Distinguish network routing/firewall/isolation from an app bug; do not broaden firewall rules without a demonstrated need.
4. Select Q1 with a controller and ask “Who are you?” and “What are the Z probabilities?” using in-headset controls. Capture one redacted request ID, selected target, epoch, reference snapshot/version and matching answer.
5. Hold/release Talk using the Quest controller. Verify microphone permission grant/denial, editable transcript, headset keyboard, Ask, captions and English voice playback. Confirm no PC microphone is used. Collect actual device audio/ASR evidence only if performed.
6. Move to Q2 while Q1 speech/audio download is pending. Then press Stop or say “Let me try.” Verify no old Q1 audio or captions return. Check music restoration after cancellation.
7. Use H/X/Z/S and actual Z measurement. Verify the authoritative before/after/event sequence reaches the gateway. Ask “What just happened?” and compare the event ID/outcome with the real recorded operation.
8. Ask “What if I use H again?” Verify a labeled copy preview and unchanged live state. Ask “Say that again” and More; they retain their earlier source reference. Ask “What if I do that?” and verify clarification rather than an invented operation.
9. Capture a pure single-qubit superposition, then a suitable entangled joint state through actual scene operations. Ask why the arrow is shorter. Verify the reply uses stored/current captures; if the exact preparation was not achieved, do not claim the ideal fixture happened. Also verify the mixed-global-state guard with a controlled test.
10. Check reference/sandbox/earlier diagrams and the long-answer scroll inside the headset. Verify no overlap, unreadable text, pink/absent materials, unintended physics colliders, or stale focus marker.
11. Remove/wear the headset, suspend/resume and change Wi-Fi during controlled checks. Verify microphone release, local silence, clean rejoin, no duplicate publisher/message loops, and coherent reference history.
12. If the user authorizes a gateway restart, verify republishing and a fresh event-history boundary. Do not restart the gateway automatically merely to complete this row.

Measure timing from an explicitly defined point (such as Ask release or transcript confirmation) to caption and audible response. Do not reuse the earlier synthetic host timing table as headset latency. Record headset frame timing/allocation observations under the actual workload if measured; do not substitute Editor FPS.

## D. Two Quests, after single-headset success and explicit authorization

1. Use one authority build/config and one join-only participant config, with distinct device participant IDs. Match the intended Fusion experiment and companion room.
2. Verify both see the same canonical density/operation and only one publisher. Moving/using a gate from the participant must go through the shared authority, not create an independent state.
3. Ask private questions on each headset. Confirm the other headset receives neither private captions/history nor private audio.
4. Explicitly enable Shared and ask a shared question. Verify its speaker/targets/reference on both devices, correct ordered audio, no duplicate playback, and local follow-up controls that do not use the wrong participant history.
5. While shared speech is active, start a private turn, change selection or leave Shared. Verify unrelated cancellation cannot clear the private turn and obsolete shared audio cannot resume.
6. Disconnect the authority. Verify the documented fixed-authority degraded/rejoin behavior, or test an explicitly implemented migration protocol. A participant lacking the publishing credential must not reset the room or silently become publisher.

## Deliver the validation report

Use `docs/QUEST_COMPANION_VALIDATION.md`. Separate **source**, **Editor**, **APK**, **device**, **live server**, and **multiuser** status. Include only actual redacted evidence. Missing hardware, operator credentials, runtime authorization or installed modules should be named specifically; complete other work before handing back those dependencies.

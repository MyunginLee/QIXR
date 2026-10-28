# Quest companion implementation and validation

Prepared on 2026-10-28 on the Windows Unity development PC. This report distinguishes development/build evidence from device and live-service validation. No gateway, inference worker, speech worker, or installed Quest app was started for this work.

## Source identity

The starting checkout was clean at `642525584d396dc0a678bbed31482a8f2ad92cc9` (`adding quantum companion. TODO: Unity`). The handoff base `a0fba5a12ef37eef8afb081586d2fff175a11443` is an ancestor. The supplied handoff contains 75 manifest entries. Initial companion/source documents matched the overlay after CRLF normalization; the font matched its binary hash. The handoff patch was not applied twice. Unity GUIDs were retained.

Implementation changes are in the current working tree. Record the committed revision alongside any deployed paired build. The build report includes the APK identity and checksum.

## Development and build evidence

| Stage | Evidence and status |
| --- | --- |
| Source import | Existing overlay verified; no replacement project or dependency upgrades. |
| Unity Editor | Full project import/compilation succeeded in Unity 6000.0.25f1. Final build log is the authority for subsequent source revisions. |
| Offline EditMode checks | 605 assertions passed, including password connection profiles, protocol fixtures, density precision, 64-bit versions, stale turn ownership, WAV decoding, mock client transport, bounded retry, missing-job handling, journal ordering/retention/overflow/reset and existing physics checks. Evidence: `Logs/companion-offline-checks.json`. |
| Offline PlayMode checks | Passed with mock transport only: offline panel, late ASR rejection on selection change, editable transcript with no automatic Ask, disable abort/microphone cleanup. Evidence: `Logs/CompanionPlayMode.log` and `Logs/companion-playmode-checks.json`. |
| Android APK | Passed. Final password-UI ARM64/IL2CPP development APK built with zero build errors. Evidence: `Builds/Quest/build-report.json` and `Logs/PasswordQuestFinalBuild.log`. Packaged INTERNET/RECORD_AUDIO permissions, UnityPlayerActivity, ARM64 libil2cpp and absence of embedded pairing were checked separately. |
| Installed / standalone launched | Not performed. |
| Live companion / TLS / ASR / TTS | Not performed; operator configuration and explicit runtime authorization required. |
| Multiple Quests | Not performed. Fixed designated authority only; no credential delegation or seamless migration. |

Pinned dependencies remain XRI 3.0.7, URP 17.0.3, OpenXR 1.13.1 and the existing Meta/Fusion packages. Unity's bundled Android SDK, NDK and OpenJDK are used. The enabled scene is `Assets/Scenes/MusicalRibbonVisualization.unity`. Source/scene validation checks three IDs, one XR Origin, missing scripts, one EventSystem and UI-capable controller interactors. Runtime panels use tracked-device graphic raycasts. Only the required dynamic unlit shaders are preserved for player builds. APK inspection caught a missing INTERNET permission: the enabled OpenXR Meta Quest feature was configured to force-remove it, overriding both PlayerSettings and the source manifest. This removal option is now disabled and checked by the build validator. INTERNET and RECORD_AUDIO are explicit in the source manifest; both were confirmed in the final APK.

## APK identity

- File: `Builds/Quest/qixr-companion-unpaired.apk`
- SHA-256: `dd65af853c832352cc3aac031938e08d16e080d2c9b90dd3b169e6cc2e88467e`
- Application identifier: `com.unity.vrtemplate`; version `0.1`, version code `1`.
- Build type: Android ARM64, IL2CPP, Development, unpaired. No operator configuration was supplied or embedded.
- Final build completed at `2026-10-28T22:40:41Z`; build summary reports zero errors. APK signing is development signing, not a production publishing claim.
- Exact working-tree file hashes are recorded separately in ignored `Builds/Quest/source-manifest.json`.

## What changed

- Missing or invalid pairing now leaves an English, visible offline panel and working local simulation. No placeholder endpoint or credentials are embedded in the unpaired build.
- Removed one orphaned missing-script component from the disabled legacy `Canvas/Test` button after confirming its script GUID has no source in Assets or packages. Scene validation now rejects any remaining missing script and validates controller UI ray support.
- Pairing validates HTTPS LAN addressing, a 64-hex SHA-256 leaf certificate pin, role credentials, room/participant identity, locale and simulation mode. App-private configuration takes priority over StreamingAssets. Android packaged asset URL loading remains supported. There is no HTTP or accept-all-certificate fallback.
- Transport is injectable. Turn ownership includes conversation, request, selection, local/server generation and authority epoch. Late ASR cannot overwrite a newer selection. Transcript recognition is an editable draft; only Ask submits it. Quiet results remain silent. HTTP payload/reference/job failures are distinguished from expired conversations, and temporary errors receive bounded retries.
- Pause, focus loss and disable stop local speech/recording, abort pending transport, stop loops and clear stale event history. Resume starts one new join/publisher/message set. Rejoins establish a fresh authority epoch/history boundary on the designated publisher.
- Participant configuration freezes local fallback simulation and refuses host promotion. Once Fusion has been observed, disconnect does not authorize standalone publication. Role-specific standalone builds disable automatic Fusion startup. A participant must rejoin the designated authority.
- Shared delivery checks speaker/request/generation/epoch and deduplicates audio indices. Unrelated shared cancellation does not clear a private turn. Different shared turns stop the previous local queue before presentation. This has not established audible synchronization between physical devices.
- Pointer cancellation stops Talk capture. The panel retains text questions, More, a silent optional follow-up, Stop, language and privacy controls. Existing EventSystems are configured for XR UI instead of adding duplicates. Selection halos are reused, materials are cleaned up, and scene scans are reduced. Captured/earlier diagrams and sandbox occupy separate positions. Music ducking is applied at the arpeggio gain source so new notes cannot override it.

## Repeatable offline commands

Run from the project root with no other Editor using the same checkout. These commands do not start any companion service.

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.0.25f1/Editor/Unity.exe'
& $unity -batchmode -quit -projectPath . -executeMethod QuantumCompanionOfflineChecks.Run -logFile Logs/CompanionOffline.log
& $unity -batchmode -projectPath . -executeMethod QuantumCompanionPlayModeChecks.Run -logFile Logs/CompanionPlayMode.log
& $unity -batchmode -quit -projectPath . -executeMethod QuantumCompanionQuestBuild.BuildUnpaired -logFile Logs/QuestCompanionBuild.log
```

The PlayMode runner replaces its batch Editor scene with an empty test scene, suppresses bootstrap and XR startup, requires no embedded/device pairing, and injects mock remote I/O. It exits the Editor after its tests. It must not be used as a live integration command.

Editor menus are under **QIXR > Companion**: install pairing, validate setup, run offline checks, build unpaired APK, build paired APK. Validation updates the intended scene's XR UI configuration. An unpaired build refuses to silently include an existing private pairing file.

## Pairing and operation

1. Obtain the operator's role-specific `host-config.json` or `client-config.json` privately. Do not transfer admin tokens, TLS private keys or server environment files. Confirm the actual LAN endpoint and certificate fingerprint before a live test.
2. Use **Install pairing configuration**. Authority configuration contains authority and join tokens. Participant configuration contains only a join token and an empty authority token. Leave `participantId` empty to generate/store a distinct ID per device. English (`en`) is default; Korean (`ko`) is explicit.
3. The client-only `simulationMode` field accepts `standalone` or `fusion`. If omitted, authority configuration defaults to standalone and join-only participant configuration defaults to fusion. For one authority Quest use standalone. For devices sharing one Fusion experiment, explicitly set fusion in both role configurations and use the existing matching Fusion room setup. Only the fixed designated authority can publish to that companion room. Separate experiments require separate companion rooms.
4. Build using **Build paired Quest APK** for each role. Build-time configuration goes to ignored `Assets/StreamingAssets/qixr-companion.json`. The source tree contains no working operator credentials. Per-role build-time pairing is the supported preparation workflow.
5. An optional app-private override is stored at `Application.persistentDataPath/qixr-companion.json`. The panel can validate/import JSON from its input field. Its headset keyboard usability has NOT been tested; this is not claimed as a verified on-device pairing importer. An override wins over an embedded configuration, so remove/replace it deliberately when changing role.
6. With explicit runtime authorization, install and launch the APK from Quest's app library without Link/Air Link or an Editor host. Use the controller UI to select Q1/Q2/Q3 or a pair, hold/release Talk, review/edit transcript, and press Ask. Text input must be checked with the Quest keyboard. Keep normal gates/measurement/music operational when the companion is unavailable.

## Remaining validation and boundaries

Follow `docs/handoff/03_QUEST_ACCEPTANCE.md` in order. Device tests require a Quest, explicit launch authorization, operator pairing and an explicitly started Linux service. Verify microphone denial/later grant, actual TLS pin rejection, reconnect after Wi-Fi changes, old-caption/audio suppression, readable panels/scroll/keyboard, source/sandbox isolation and music restoration. Measure actual device frame cost and caption/audible latency rather than using desktop timings.

The subsequent shared-password update adds optional password-login and admin password-rotation endpoints, while retaining existing token and scientific contracts. See `docs/PASSWORD_ACCESS.md` and the server update archive. No remote server deployment or startup was performed. The server's stopped/disabled state was not changed. `adb devices -l` returned no connected devices on this PC during the earlier build work. Shared audible timing across headsets, physical colocation, Fusion disconnect/rejoin and fixed-authority recovery remain live acceptance items. Participant promotion cannot transfer the publishing credential; the UI reports degraded authority availability and requires rejoin. Do not label this seamless migration.

## Shared password update

The English Quest panel now provides a masked Server Password field and Connect/Disconnect. A public connection profile can be embedded through the dedicated **Build password Quest APK** menu; it rejects embedded join/authority credentials. Wrong passwords are rejected, accepted passwords yield only a conversation token, cancellation discards late login callbacks, and pause/focus loss/session expiry require re-entry. Passwords are not saved to disk or PlayerPrefs, and the input is cleared on submission/interruption.

The server implementation stores a salted PBKDF2 hash, bounds login attempts and KDF concurrency, redacts validation errors and revokes password-created conversations on password rotation. Operator setup uses hidden terminal input on the Linux machine; no real password was selected during development. The existing designated simulation authority is still required for canonical experiment state.

Offline Python tests passed 68 cases with an in-process ASGI client, fake planner and disabled speech. Unity EditMode checks passed 605 assertions. Updated mock PlayMode checks cover failed/successful password login, no role bearer credential on login, masked/cleared UI, password re-entry after pause and late-login cancellation. Live TLS, headset keyboard, microphone and shared-device checks remain pending. The APK built without an operator profile displays the sign-in UI but still requires a real server connection profile; it cannot authenticate against the unchanged Linux service until that source update is applied and deliberately started.

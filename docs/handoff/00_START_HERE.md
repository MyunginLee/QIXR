# QIXR Unity / standalone Quest handoff

Prepared 2026-10-28. The product language is English by default.

## What to give the agent on the Unity PC

Give it `QIXR-Unity-Quest-handoff.zip` and this instruction:

> Read `overlay/docs/handoff/01_UNITY_QUEST_IMPLEMENTATION_PROMPT.md` from the handoff archive. Follow `overlay/docs/handoff/00_START_HERE.md` to import the matching local changes into a clean QIXR checkout first. Complete the Unity and Android implementation, offline tests, and standalone Quest APK build. Preserve the existing Linux companion server contract. Do not start the Linux server, inference or speech workers, or launch a live integration run unless I explicitly request runtime execution. Prepare the live validation steps and report exactly what remains unverified.

The primary task prompt is [01_UNITY_QUEST_IMPLEMENTATION_PROMPT.md](01_UNITY_QUEST_IMPLEMENTATION_PROMPT.md). Supporting files are [the server contract](02_SERVER_CONTRACT.md), [acceptance checks](03_QUEST_ACCEPTANCE.md), and [offline JSON fixtures](fixtures/protocol-v1.json).

## Why the archive is needed

At preparation time the companion additions were **uncommitted local work** on `feat/quantum-companion`. GitHub alone does not contain this working tree. The base is `MyunginLee/QIXR`, commit `a0fba5a12ef37eef8afb081586d2fff175a11443`, originally `test-multiuser`.

The archive contains a binary-capable `tracked-changes.patch`, new files in `overlay/`, and `manifest.json` with the base commit and SHA-256 values. It is an overlay for the repository, not a complete Unity project. Runtime credentials, model downloads, virtual environments, generated Unity folders, and built apps are excluded. Obtain role-specific pairing credentials separately when needed.

## Import safely before opening Unity

1. Extract the archive into a temporary directory **outside** the Unity project. Verify the archive checksum from its `.sha256` companion file if supplied. Read `manifest.json`.
2. Preserve existing work. Prefer a fresh checkout for this handoff. Do not overwrite an unrelated project's files, reset a dirty branch, or run `git clean`.
3. Clone `https://github.com/MyunginLee/QIXR.git` if needed. Use Git with `core.autocrlf=false` for the fresh import so byte hashes can be checked consistently. Check out the manifest's exact base commit, then create a local implementation branch. Do not assume a newer default branch includes the companion.
4. From the project root, run `git apply --check <EXTRACTED_HANDOFF>/tracked-changes.patch`, then `git apply <EXTRACTED_HANDOFF>/tracked-changes.patch`. If the check fails, inspect and reconcile the differences; do not force or discard work.
5. Copy the **contents** of `overlay/` into the project root, retaining relative paths and Unity `.meta` files. If an overlay destination already exists and differs, reconcile it before writing. Do not nest an extra `overlay` directory under `Assets`.
6. Verify SHA-256 for every final project path listed in `manifest.json`. Hashes describe the imported handoff, before Unity or the implementation agent makes subsequent edits. Windows PowerShell can use `Get-FileHash -Algorithm SHA256`; Python's `hashlib.sha256` is another option.
7. Confirm these files exist before opening Unity: `Assets/Scripts/Companion/QuantumCompanionClient.cs`, `Assets/Scripts/Companion/QuantumAnswerReference.cs`, `Assets/Editor/QuantumCompanionSetup.cs`, `server/qixr_companion/agent.py`, and `docs/QUANTUM_AGENT.md`.
8. Read the primary task prompt and proceed. If the corresponding changes are already present in an existing checkout, verify them against the manifest rather than applying the patch twice.

## Intended deployment

The other PC edits and builds the Unity project. The **installed Quest APK** runs the simulation, XR interactions, microphone capture and playback. This Linux machine runs the HTTPS companion gateway, local model, ASR and TTS when explicitly started. An Editor, PCVR session, USB cable, or PC microphone must not be required after installation.

A single Quest first acts as the simulation authority. Additional Quests use the existing Fusion/shared-state path and their own companion conversations. The companion gateway is not the Fusion simulation host. Read the task prompt's single-authority requirements before enabling multiple headsets.

## Current evidence

Unity companion source exists. The previous offline server suite passed 64 tests; the .NET harness compiled numerical code and DTOs and parsed 12 C# files. **The complete Unity project, Android IL2CPP build, microphone/keyboard/controller behavior, and on-device server connection have not been validated for this addition.** Syntax checks are not a successful Quest build.

The Linux QIXR service was left stopped and disabled. Do not interpret this handoff as an instruction to start it.

## Regenerate a matching source handoff on the originating machine

These commands are offline export/verification only. Run them from the project root after source/document changes:

```bash
.venv/bin/python server/scripts/export_unity_fixtures.py
python3 scripts/export_quest_handoff.py
```

The exporter checks fixture source hashes, rejects known private runtime tokens in content, reconstructs the patch and overlay in a temporary directory, checks the resulting file hashes, and validates the ZIP. The ignored `handoff-export/` directory contains the archive and its `.sha256` checksum. No Git push or server startup occurs.

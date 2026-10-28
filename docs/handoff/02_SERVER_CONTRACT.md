# Companion protocol reference for the Unity implementer

Snapshot schema and gateway protocol are version 1. This is a handoff reference derived from the current Python source. If it differs from `server/qixr_companion/contracts.py`, `api.py`, `service.py` or `agent.py`, inspect the difference and align both ends explicitly. Do not silently introduce an incompatible API.

## Roles and topology

| Role | Credential | Responsibility |
| --- | --- | --- |
| Linux operator | Admin token, TLS private key, service configuration | Provision the gateway/rooms; these credentials stay on Linux. |
| Simulation authority Quest | Room authority token and join token | Evolve the actual Unity quantum state, publish snapshots/events, and join a conversation. |
| Participant Quest | Room join token, no authority token | Render the Fusion shared state, request permitted operations through Fusion, and join its own conversation. |
| Conversation | Token returned by join | Questions, canonical context, own requests/audio, and permitted shared messages/audio. |

The current lab API has no dynamic authority-token delegation or publisher lease. One designated publisher must own the matching Fusion experiment. Separate experiments need separate companion rooms. A model answer does not execute a live gate or measurement.

## Configuration

Example shape only; placeholders are not working credentials:

```json
{
  "serverUrl": "https://LINUX_HOST_ON_LAN:8786",
  "certificateSha256": "REPLACE_WITH_64_HEX_LEAF_CERTIFICATE_SHA256",
  "roomId": "ROOM_FROM_OPERATOR",
  "authorityToken": "EMPTY_FOR_PARTICIPANT",
  "joinToken": "ROOM_JOIN_TOKEN_FROM_OPERATOR",
  "participantId": "",
  "language": "en"
}
```

For a participant, `authorityToken` must be the empty string, not the literal placeholder. Leave `participantId` empty to create/store a distinct ID per headset; do not distribute the same hardcoded participant identity. Room join tokens are not conversation tokens. Do not send a join token to a conversation endpoint.

The provisioner on Linux already writes separate private `.runtime/host-config.json` and `.runtime/client-config.json`. Transfer the appropriate file privately when the operator authorizes pairing. These files are excluded from the source handoff. Fingerprints are calculated over the DER-encoded leaf certificate using SHA-256. Replacing a certificate requires deliberate pin replacement, not a permissive validator.

## HTTP sequence and authentication

Use `Authorization: Bearer <ROLE_TOKEN>` and JSON content type where applicable. Use the conversation's returned token for its endpoints.

| Operation | Endpoint | Role / response |
| --- | --- | --- |
| Join | `POST /v1/rooms/{roomId}/conversations` with `{"participantId":"device-id"}` | Join token → `conversationId`, `conversationToken`, `authorityEpoch` |
| Inspect room | `GET /v1/rooms/{roomId}` | Authority token → `authorityEpoch`, `latestSnapshotId`, `eventSequence` |
| Establish a new epoch | `POST /v1/rooms/{roomId}/epoch` with `{"expectedEpoch":N}` | Authority token; compare-and-set; resets room snapshots/events and notifies participants |
| Publish snapshot | `POST /v1/rooms/{roomId}/snapshots` | Authority token; snapshot ID immutable |
| Publish real event | `POST /v1/rooms/{roomId}/events` | Authority token; requires both referenced snapshots first |
| Read canonical context | `GET /v1/conversations/{cid}/context` | Conversation token → `snapshot`, nullable `lastEventId`; 409 if waiting for authority |
| Ask | `POST /v1/conversations/{cid}/questions` | Conversation token → HTTP 202 job envelope |
| Poll | `GET /v1/conversations/{cid}/requests/{requestId}` | Same conversation token → current job envelope |
| Own audio | `GET /v1/conversations/{cid}/requests/{requestId}/audio/{index}` | Same conversation token → WAV bytes |
| ASR | `POST /v1/conversations/{cid}/transcribe?language=en` | Conversation token; raw `audio/wav` body → `text`, `language` |
| Cancel | `POST /v1/conversations/{cid}/cancel` | Conversation token → new `generation` |
| Read messages | `GET /v1/conversations/{cid}/messages?after={cursor}` | Conversation token → `messages`, monotonic `cursor` |
| Shared audio | `GET /v1/conversations/{recipient}/shared/{speaker}/{requestId}/audio/{index}` | Recipient conversation token; same-room, room-visible turn only |
| Leave | `DELETE /v1/conversations/{cid}` | Conversation token |

`/healthz` reports gateway health. `/readyz` checks model availability and reports speech readiness; only use live checks when runtime is authorized. Unity does not need an admin credential or `/v1/rooms` creation capability. The normal Quest client can use polling; WebSocket support is optional.

## Snapshot and event wire invariants

- Q1/Q2/Q3 map to IDs 0/1/2. The standard scene has three qubits; its density matrix is 8×8.
- `basisOrder` is `q0-msb`: Q1 is the most significant bit. Serialize row-major complex entries as alternating real, imaginary numbers. There are **128 numbers** for three qubits. Do not transpose the matrix or reorder qubits.
- `densityEncoding` is `complex-row-major-f64` for the authoritative double-precision export. The API also supports an explicitly labeled `complex-row-major-f32` path with different numerical tolerance. All numbers must be finite.
- Required snapshot identity includes `roomId`, `authorityEpoch` ≥ 1, `snapshotId`, and `stateVersion`. Other fields include `eventSequence`, `simulationTime`, `capturedAtUtc`, `qubitCount`, `couplings`, and `presentation`. Preserve 64-bit versions in C# where supplied.
- Before/after states must reproduce an actual discrete H/X/Z/S/measureZ operation, without unrecorded continuous evolution between them. The API independently checks this. The after version increases, the before event sequence is N−1 and the after event sequence is N.
- Real events include immutable `eventId`, `sequence`, `authorityEpoch`, `actor`, `operation`, `target`, `beforeSnapshotId`, `afterSnapshotId`. For `measureZ`, include the actual outcome and its probability before measurement. Never invent a result or send a preview as a real event.
- Publish the before state, after state, and event in that order. Retry an immutable payload with its original ID. Epoch changes establish a new history boundary; do not mix old pending events into a new epoch.
- Presentation thresholds are measured from active Unity components. Unknown display/audio fields remain unknown: an empty `visiblePairs` list is not a report that all ribbons are off; nullable `musicPlaying` is not necessarily false.

## Question and answer envelopes

Example question for the synthetic fixture only:

```json
{
  "requestId": "fixture-question-1",
  "selectionId": "fixture-selection",
  "targetQubitIds": [0],
  "referenceSnapshotId": "fixture-superposition",
  "referenceEventId": null,
  "referenceMode": "captured",
  "language": "en",
  "text": "What are the Z probabilities?",
  "speak": false,
  "visibility": "private"
}
```

Targets are unique IDs, at most two. Request IDs use letters/numbers/underscore/hyphen. New logical turns need new IDs; retrying a request ID with different content yields 409. `referenceSnapshotId` is required. `referenceEventId` is optional and must refer to a real available event when set. Default `referenceMode` is `captured`; live mode refreshes context after planning and may end with `context_changed`. The API defaults to English and `speak=false`; the Unity interactive client normally explicitly requests speech. `visibility="room"` requires the operator's room to permit shared presentation. Unknown request fields are rejected by Pydantic rather than ignored.

Job envelopes contain `requestId`, `generation`, `state`, nullable `result`, and `audioCount`. Job states are `queued`, `running`, `speaking`, `done`, `cancelled`, `error`. `audioCount` is availability, not proof that playback already finished. Do not replay downloaded indices on every poll.

Successful answer results include `status`, `text`, `referenceSnapshotId`, `authorityEpoch`, `stateVersion`, `selectionId`, `targetQubitIds`, `referenceMode`, `facts`, `factIds`, `sources`, nullable `preview`, `detailText`, nullable `nextAction`, `referenceCue`, `agentPolicy`, `responseLanguage`, `planner`, `intent`, `visibility` and segments/timing fields. Some fields are optional for particular/error results. Add missing DTO members needed for correctness, including top-level version/generation/identity fields; do not assume a parsed default 0 is a valid supplied version.

`referenceCue` contains a human label, snapshot ID, version, event sequence, target IDs and `showState`. A repeat or More reply may intentionally retain an **earlier** reference. Keep that label and diagram; do not relabel it as current. `comparison` can supply an earlier `snapshotId` and `nodes`. Each node's `bloch` vector has three components. `entangledWithRest` is a nullable scientific fact; do not collapse unknown to false if using it in a client UI.

`nextAction` is at most one `{label, question, kind:"ask"}`. Display it silently; a click submits that question. It is not an executable command. `detailText` backs optional More behavior. A `quiet` result has empty text and no speech segments; do not fabricate a spoken acknowledgement. Other statuses include `answered`, `needs_selection`, `needs_clarification`, `history_unavailable`, `unsupported`, `context_changed`, and `error`. Missing fields on errors/quiet replies must not crash the UI.

Preview `kind` is `sandbox`. A gate preview contains source snapshot ID, operation, target, resulting density and facts. A measurement preview contains branches with outcome, probability and density; it **does not sample one outcome**. Preview/reference rendering cannot call live mutation methods.

Shared message envelopes have `type`, `messageSequence`, `speakerConversationId`, and turn/job fields where appropriate. `messageSequence`/`cursor` order delivery; they are not turn generations. Handle `answer`, `audio_ready`, `done`, `cancelled`, `context_changed`, `authority_reset`, and `room_closed` as appropriate. Match shared audio to its speaker and turn, and do not display another person's private turn.

## Audio and recoverable failures

ASR uploads are mono PCM16 little-endian WAV, **16 kHz**, at most **15 seconds**, raw bytes (not multipart or base64 JSON). Unity may capture at a device-specific rate; resample/downmix before upload. After transcription, let the user edit/confirm before sending a question.

Response audio is a mono PCM16 WAV with its own header/sample rate. The TTS rate need not be 16 kHz; decode it instead of reusing the ASR rate. Keep captions and quantum interaction available if voice is missing.

HTTP 401/404 can mean expired/missing conversations, missing jobs or expired references depending on the endpoint. HTTP 409 can mean waiting for authority, changed epoch or an immutable-ID conflict. HTTP 422 means an invalid payload; fix it rather than retrying unchanged forever. HTTP 429 means a bounded capacity limit; back off. ASR unavailability can return 503. Read endpoint-specific error details without exposing credentials. Conversation inactivity expiry is currently 120 seconds; active authenticated polling keeps it alive. Room credentials/epochs persist in SQLite; conversations, snapshots, events and audio do not survive a gateway restart.

The configured limits (8 participant conversations per room, 2 answer jobs in parallel, 8 waiting jobs by default) are policy limits, not measured hardware maxima. Do not promise a maximum headset count from these values.

## Official Unity references consulted

These links support platform-specific implementation checks, not proof of this project's device behavior:

- [Unity 6 StreamingAssets](https://docs.unity.com/en-us/engine/6000.0/manual/building-and-publishing/streaming-assets): Android reads the packaged asset URL with UnityWebRequest; runtime writes belong in a writable path.
- [XRI 3.0 UI setup](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.0/manual/ui-setup.html): tracked-device UI needs the matching canvas/input setup; inspect the installed 3.0.7 package because the online 3.0 page currently describes a later patch.
- [Unity 6 CertificateHandler](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/networking/certificatehandler): Android supports custom certificate handling. Test the actual configured pin on the Quest build.

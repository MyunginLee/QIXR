# Shared password access for Quest

The Quest app can sign in to an existing companion room using one shared password. Password access grants a private conversation token, never a room join token, authority token or admin token. The designated simulation authority still uses its separate private role configuration to publish canonical state. Participant Quests join the matching Fusion experiment. A password alone does not turn a headset into a simulation host.

## Operator setup on the Linux server

This update does not start or enable any service. The following setup command is offline. Apply the server source update to the matching handoff checkout while its gateway is stopped. Existing provisioned rooms and authority credentials are preserved. No additional runtime Python dependency is required.

Back up the server source and `.runtime/rooms.sqlite3` while stopped before applying the update. The password hash extends the stored room JSON; rolling back to an older server version also requires restoring its database backup.

From the Linux project root:

```bash
PYTHONPATH=server .venv/bin/python server/scripts/set_room_password.py \
  --server-url https://YOUR_LAN_HOST:8786
```

The command reads the existing `.runtime/host-config.json` room and `.runtime/tls/server.crt` public certificate. Alternatively supply `--room ROOM_ID`, `--certificate PUBLIC_CERT.pem`, `--runtime PATH`, or `--output PATH`. It asks for the password twice using hidden input. Choose 8-256 characters. Do not place the real password in source code, a command-line argument, an APK or chat.

Only a salted PBKDF2-HMAC-SHA256 hash with 600,000 iterations is stored in the existing SQLite room record. The implementation uses Python's standard library. The work factor and per-password random salt follow the [OWASP password storage guidance](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html). Password login has bounded KDF concurrency and a five-attempts-per-peer, 60-second rate limit. Do not deploy behind an untrusted proxy or use forwarded headers as a bypass.

The command writes `.runtime/quest-password-profile.json`. This is a public connection profile containing the HTTPS address, leaf certificate fingerprint, room ID, English locale and `authenticationMode: "password"`. It contains neither password nor access tokens. Share this profile with the Unity development PC. The authority configuration stays private and separate.

For an already running gateway, an explicitly authorized operator may instead update a password through `PUT /v1/rooms/{roomId}/password`, authenticated with the server admin token. The request is `{"password":"the new password"}`. Never embed that admin credential in Quest. Updating the password revokes password-created conversations; token-paired authority/participant conversations remain separate. The offline CLI is intended for a stopped gateway, whose next deliberate start reads the new hash.

## Unity preparation

1. Use **QIXR > Companion > Install pairing configuration** to select the generated public profile.
2. Use **QIXR > Companion > Build password Quest APK**. This build rejects embedded join or authority tokens. Server addressing and the public certificate pin are built in; users enter only the password.
3. Install and launch only when runtime execution is authorized. In Quest, enter **Server Password**, then press **Connect**. The field is masked and cleared on submission/interruption. The password is not saved to PlayerPrefs, a configuration file or disk.
4. After successful login, select qubits and use Talk/Ask normally. A recognized utterance remains an editable draft until Ask is pressed. **Disconnect**, pause, focus loss and session expiry clear the local conversation; reconnecting requires password re-entry.

An unpaired development APK also contains the password UI, but cannot contact a server until a real public connection profile is installed or embedded. An HTTPS endpoint and valid leaf certificate pin are mandatory; localhost and insecure certificate bypasses are not accepted. Headset keyboard operation and the live TLS/server path still require device validation.

## Protocol additions

- `POST /v1/rooms/{roomId}/password-conversations`: JSON `participantId` and `password`; no Authorization header. Success returns only `conversationId`, `conversationToken` and `authorityEpoch`, identical to the existing conversation join response.
- `PUT /v1/rooms/{roomId}/password`: admin-only password rotation. Password changes invalidate prior password sessions.
- Wrong/disabled/unavailable room credentials return a generic HTTP 401. Invalid password payloads return a redacted HTTP 422; they never echo the submitted password. Rate limits return HTTP 429 with Retry-After.
- Existing token pairing and scientific snapshot/event/question contracts remain compatible. Private conversation isolation is unchanged. Any headset that knows the shared password can sign in; there is no device approval list in this version.

## Validation boundary

Server tests use an isolated temporary database, an in-process ASGI transport, a fake planner and disabled speech. Unity tests inject mock transport. None of these commands start the Linux gateway, inference or speech workers. Test logs and the new APK/build report live under ignored `Logs/` and `Builds/Quest/` directories. The server source update must be applied on the Linux machine before the password endpoint exists there; no remote deployment or live test is performed by this development work.

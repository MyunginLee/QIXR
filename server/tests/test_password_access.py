import pytest

from qixr_companion.passwords import LoginLimiter, hash_password, verify_password
from qixr_companion.store import Store
from test_api import auth, client_at, setup, wait

PASSWORD = 'offline-test-password'


def test_password_hash_is_salted_and_persistent(tmp_path):
    one, two = hash_password(PASSWORD), hash_password(PASSWORD)
    assert one != two and PASSWORD not in one
    assert verify_password(PASSWORD, one)
    assert not verify_password('wrong', one)
    assert not verify_password(PASSWORD, None)
    assert not verify_password(PASSWORD, 'malformed')
    store = Store(tmp_path/'rooms.sqlite3')
    room, _, _ = store.create_room('offline', False)
    room.passwordHash = one
    store.save(room)
    rid = room.id
    store.db.close()
    restored = Store(tmp_path/'rooms.sqlite3')
    assert restored.rooms[rid].passwordHash == one
    restored.db.close()


def test_login_limiter_bounds_attempts_and_storage():
    limiter = LoginLimiter(attempts=2, window=60, max_peers=2)
    assert limiter.allow('a', 0) and limiter.allow('a', 1)
    assert not limiter.allow('a', 2)
    assert limiter.allow('b', 2) and not limiter.allow('c', 3)
    assert limiter.allow('a', 61)
    assert len(limiter.peers) <= 2


@pytest.mark.asyncio
async def test_password_login_dialogue_isolation_and_no_authority(tmp_path):
    async with client_at(tmp_path) as (c, app):
        room, _, snapshot = await setup(c, tmp_path)
        rid = room['roomId']
        admin = auth((tmp_path/'admin.token').read_text())
        assert (await c.put(f'/v1/rooms/{rid}/password', headers=auth(room['joinToken']), json={'password':PASSWORD})).status_code == 401
        assert (await c.put(f'/v1/rooms/{rid}/password', headers=admin, json={'password':PASSWORD})).status_code == 200
        login = f'/v1/rooms/{rid}/password-conversations'
        assert (await c.post(login, json={'participantId':'one', 'password':'wrong'})).status_code == 401
        response = await c.post(login, json={'participantId':'one', 'password':PASSWORD})
        assert response.status_code == 200
        first = response.json()
        assert set(first) == {'conversationId','conversationToken','authorityEpoch'}
        assert (await c.get(f'/v1/conversations/{first["conversationId"]}/context', headers=auth(first['conversationToken']))).status_code == 200
        question = dict(requestId='password-turn', selectionId='selected', referenceSnapshotId=snapshot.snapshotId,
                        targetQubitIds=[0], text='probability', language='en')
        assert (await c.post(f'/v1/conversations/{first["conversationId"]}/questions', headers=auth(first['conversationToken']), json=question)).status_code == 202
        answer = await wait(c, f'/v1/conversations/{first["conversationId"]}/requests/password-turn', auth(first['conversationToken']))
        assert answer['state'] == 'done' and '50.0%' in answer['result']['text']
        assert (await c.post(f'/v1/rooms/{rid}/snapshots', headers=auth(first['conversationToken']), json=snapshot.model_dump())).status_code == 401
        second = (await c.post(login, json={'participantId':'two', 'password':PASSWORD})).json()
        assert first['conversationId'] != second['conversationId']
        assert (await c.get(f'/v1/conversations/{first["conversationId"]}/context', headers=auth(second['conversationToken']))).status_code == 401
        assert (await c.put(f'/v1/rooms/{rid}/password', headers=admin, json={'password':'replacement-password'})).status_code == 200
        assert (await c.get(f'/v1/conversations/{first["conversationId"]}/context', headers=auth(first['conversationToken']))).status_code in (401,404)
        assert (await c.post(login, json={'participantId':'one', 'password':PASSWORD})).status_code == 401
        assert (await c.post(login, json={'participantId':'one', 'password':'replacement-password'})).status_code == 200


@pytest.mark.asyncio
async def test_password_disabled_throttled_and_validation_redacted(tmp_path):
    async with client_at(tmp_path) as (c, app):
        room, _, _ = await setup(c, tmp_path)
        rid = room['roomId']
        path = f'/v1/rooms/{rid}/password-conversations'
        assert (await c.post(path, json={'participantId':'one','password':PASSWORD})).status_code == 401
        secret = 'do-not-echo-' * 30
        invalid = await c.post(path, json={'participantId':'one','password':secret})
        assert invalid.status_code == 422 and secret not in invalid.text
        app.state.store.rooms[rid].passwordHash = hash_password(PASSWORD)
        for _ in range(4):
            assert (await c.post(path, json={'participantId':'one','password':'wrong'})).status_code == 401
        limited = await c.post(path, json={'participantId':'one','password':PASSWORD})
        assert limited.status_code == 429 and limited.headers['retry-after'] == '60'

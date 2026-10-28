import hashlib
import hmac
import json
import secrets
import sqlite3
import time
from collections import OrderedDict
from dataclasses import dataclass, field

from .physics import check_event, validate


def digest(token): return hashlib.sha256(token.encode()).hexdigest()


def authorized(token, hashed): return hmac.compare_digest(digest(token), hashed)


@dataclass
class Room:
    id: str
    name: str
    authorityHash: str
    joinHash: str
    epoch: int = 0
    allowShared: bool = False
    passwordHash: str | None = None
    snapshots: OrderedDict = field(default_factory=OrderedDict)
    events: OrderedDict = field(default_factory=OrderedDict)
    latestId: str | None = None
    eventSequence: int = 0

    def put_snapshot(self, s):
        if s.roomId != self.id or s.authorityEpoch != self.epoch:
            raise ValueError('Room or authority epoch mismatch')
        validate(s)
        if s.snapshotId in self.snapshots:
            if self.snapshots[s.snapshotId] != s: raise ValueError('Snapshot IDs are immutable')
            return
        self.snapshots[s.snapshotId] = s
        if self.latestId is None or (s.stateVersion, s.eventSequence) >= (self.snapshots[self.latestId].stateVersion, self.snapshots[self.latestId].eventSequence):
            self.latestId = s.snapshotId
        while len(self.snapshots) > 1024:
            self.snapshots.popitem(last=False)

    def put_event(self, e):
        if e.eventId in self.events:
            if self.events[e.eventId] != e: raise ValueError('Event IDs are immutable')
            return
        if e.authorityEpoch != self.epoch or e.sequence != self.eventSequence+1:
            raise ValueError('Event sequence or epoch mismatch')
        before, after = self.snapshots.get(e.beforeSnapshotId), self.snapshots.get(e.afterSnapshotId)
        if before is None or after is None: raise ValueError('Publish event snapshots first')
        if after.stateVersion <= before.stateVersion or after.eventSequence != e.sequence or before.eventSequence != e.sequence-1:
            raise ValueError('Event snapshot ordering mismatch')
        check_event(e, before, after)
        self.events[e.eventId] = e
        self.eventSequence = e.sequence
        while len(self.events) > 256: self.events.popitem(last=False)


@dataclass
class Conversation:
    id: str
    roomId: str
    participantId: str
    tokenHash: str
    passwordHashAtJoin: str | None = None
    generation: int = 0
    selectionId: str = ''
    lastActive: float = field(default_factory=time.monotonic)
    history: list = field(default_factory=list)
    messages: list = field(default_factory=list)
    messageSequence: int = 0


class Store:
    def __init__(self, path, max_rooms=8, max_clients=8):
        self.db = sqlite3.connect(str(path), check_same_thread=False)
        self.db.execute('CREATE TABLE IF NOT EXISTS rooms (id TEXT PRIMARY KEY, payload TEXT NOT NULL)')
        self.rooms, self.conversations = {}, {}
        self.max_rooms, self.max_clients = max_rooms, max_clients
        for room_id, payload in self.db.execute('SELECT id,payload FROM rooms'):
            self.rooms[room_id] = Room(**json.loads(payload))

    def save(self, room):
        payload = {k: getattr(room, k) for k in ['id','name','authorityHash','joinHash','epoch','allowShared','passwordHash']}
        self.db.execute('INSERT OR REPLACE INTO rooms VALUES (?,?)', (room.id, json.dumps(payload)))
        self.db.commit()

    def create_room(self, name, shared):
        if len(self.rooms) >= self.max_rooms: raise OverflowError('Room limit reached')
        authority, join = secrets.token_urlsafe(32), secrets.token_urlsafe(32)
        room = Room(secrets.token_hex(12), name, digest(authority), digest(join), allowShared=shared)
        self.rooms[room.id] = room
        self.save(room)
        return room, authority, join

    def join(self, room, participant):
        now = time.monotonic()
        self.conversations = {k:v for k,v in self.conversations.items() if now-v.lastActive < 120}
        if sum(c.roomId == room.id for c in self.conversations.values()) >= self.max_clients:
            raise OverflowError('Room client limit reached')
        token = secrets.token_urlsafe(32)
        c = Conversation(secrets.token_hex(12), room.id, participant, digest(token))
        self.conversations[c.id] = c
        return c, token

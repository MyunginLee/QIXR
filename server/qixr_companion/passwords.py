"""Shared room passwords. No plaintext password is persisted or logged."""
import hashlib
import hmac
import secrets
import time
from collections import OrderedDict

ITERATIONS = 600_000


def hash_password(password: str) -> str:
    if not 8 <= len(password) <= 256:
        raise ValueError('Use a server password of 8 to 256 characters')
    salt = secrets.token_bytes(16)
    key = hashlib.pbkdf2_hmac('sha256', password.encode('utf-8'), salt, ITERATIONS)
    return f'pbkdf2-sha256${ITERATIONS}${salt.hex()}${key.hex()}'


def verify_password(password: str, encoded: str | None) -> bool:
    # Unknown/disabled rooms still pay the same KDF cost, without revealing a room's existence.
    salt, expected, valid = bytes(16), bytes(32), False
    try:
        algorithm, rounds, salt_hex, key_hex = (encoded or '').split('$')
        candidate_salt, candidate_key = bytes.fromhex(salt_hex), bytes.fromhex(key_hex)
        if algorithm == 'pbkdf2-sha256' and int(rounds) == ITERATIONS and len(candidate_salt) == 16 and len(candidate_key) == 32:
            salt, expected, valid = candidate_salt, candidate_key, True
    except (ValueError, TypeError):
        pass
    actual = hashlib.pbkdf2_hmac('sha256', password.encode('utf-8'), salt, ITERATIONS)
    return hmac.compare_digest(actual, expected) and valid


class LoginLimiter:
    """Bounded per-peer attempts; untrusted forwarded headers are never used."""
    def __init__(self, attempts=5, window=60.0, max_peers=1024):
        self.attempts, self.window, self.max_peers = attempts, window, max_peers
        self.peers = OrderedDict()

    def allow(self, peer, now=None):
        now = time.monotonic() if now is None else now
        for key, (start, _) in list(self.peers.items()):
            if now - start >= self.window:
                del self.peers[key]
        if peer not in self.peers and len(self.peers) >= self.max_peers:
            return False
        start, count = self.peers.get(peer, (now, 0))
        if count >= self.attempts:
            return False
        self.peers[peer] = (start, count + 1)
        return True

"""Offline operator setup: set a room password without starting any service."""
import argparse
import getpass
import hashlib
import json
from pathlib import Path
import ssl

from qixr_companion.passwords import hash_password
from qixr_companion.store import Store


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', type=Path, default=Path(__file__).resolve().parents[2]/'.runtime')
    parser.add_argument('--room', help='Existing room ID; defaults to private host-config.json room')
    parser.add_argument('--server-url', required=True, help='HTTPS LAN endpoint for the headsets')
    parser.add_argument('--certificate', type=Path, help='Public server PEM certificate (never a private key)')
    parser.add_argument('--output', type=Path, help='Public Quest connection profile output path')
    args = parser.parse_args()
    from urllib.parse import urlsplit
    url = urlsplit(args.server_url)
    if url.scheme != 'https' or not url.hostname or url.hostname in ('localhost','127.0.0.1','::1') or url.username or url.password or url.path not in ('','/') or url.query or url.fragment:
        parser.error('Use the HTTPS LAN endpoint, without credentials or a path')
    database = args.runtime/'rooms.sqlite3'
    if not database.exists(): parser.error('No room database. Provision a room on the operator server first.')
    rid = args.room
    if not rid:
        host_path = args.runtime/'host-config.json'
        if not host_path.exists(): parser.error('Supply --room or an existing host-config.json')
        rid = json.loads(host_path.read_text())['roomId']
    cert = args.certificate or args.runtime/'tls/server.crt'
    fingerprint = hashlib.sha256(ssl.PEM_cert_to_DER_cert(cert.read_text())).hexdigest()
    store = Store(database)
    try:
        room = store.rooms.get(rid)
        if room is None: parser.error('Room does not exist')
        password = getpass.getpass('New server password (8-256 characters): ')
        if password != getpass.getpass('Confirm server password: '): parser.error('Passwords do not match')
        room.passwordHash = hash_password(password)
        store.save(room)
        profile = dict(serverUrl=args.server_url.rstrip('/'), certificateSha256=fingerprint, roomId=rid,
                       authenticationMode='password', simulationMode='fusion', participantId='', language='en')
        output = args.output or args.runtime/'quest-password-profile.json'
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(profile, indent=2)+'\n', encoding='utf-8')
        print('Password hash saved. No plaintext password was written.')
        print('Public Quest connection profile:', output)
        print('No service was started. When changing an existing installation, run this while its gateway is stopped.')
    finally:
        store.db.close()


if __name__ == '__main__':
    main()

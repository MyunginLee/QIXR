"""Create/reuse a room and save role-separated Unity pairing files privately."""
import argparse
import hashlib
import json
from pathlib import Path
import socket
import ssl

import httpx

parser=argparse.ArgumentParser()
parser.add_argument('--server-url',default='https://127.0.0.1:8786')
parser.add_argument('--client-url')
parser.add_argument('--language', choices=['en','ko'], default='en')
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]; runtime=root/'.runtime'
cert=runtime/'tls/server.crt'
fingerprint=hashlib.sha256(ssl.PEM_cert_to_DER_cert(cert.read_text())).hexdigest()
client_url=args.client_url
if not client_url:
    with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as sock:
        sock.connect(('192.0.2.1',9)); ip=sock.getsockname()[0]
    client_url='https://'+ip+':8786'
host_path=runtime/'host-config.json'
with httpx.Client(base_url=args.server_url,verify=ssl.create_default_context(cafile=str(cert)),timeout=30) as c:
    if host_path.exists():
        host=json.loads(host_path.read_text())
        response=c.get('/v1/rooms/'+host['roomId'],headers={'Authorization':'Bearer '+host['authorityToken']})
        response.raise_for_status()
    else:
        response=c.post('/v1/rooms',headers={'Authorization':'Bearer '+(runtime/'admin.token').read_text().strip()},json={'name':'QIXR','allowShared':True})
        response.raise_for_status(); room=response.json()
        host={'roomId':room['roomId'],'authorityToken':room['authorityToken'],'joinToken':room['joinToken'],'participantId':''}
    host.update(serverUrl=client_url,certificateSha256=fingerprint,language=args.language)
    client=dict(host,authorityToken='')
    for path,data in [(host_path,host),(runtime/'client-config.json',client)]:
        path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n'); path.chmod(0o600)
print('Private host configuration:',host_path)
print('Private participant configuration:',runtime/'client-config.json')
print('LAN endpoint:',client_url)
print('Certificate fingerprint:',fingerprint)

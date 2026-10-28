"""Install this checkout's user service and a LAN certificate. No global service edits."""
import argparse
import ipaddress
import os
from pathlib import Path
import socket
import subprocess

parser=argparse.ArgumentParser()
parser.add_argument('--port',type=int,default=8786)
start_options=parser.add_mutually_exclusive_group()
start_options.add_argument('--start',action='store_true',help='Explicitly start the service after installation; never enable login startup')
start_options.add_argument('--no-start',action='store_true',help=argparse.SUPPRESS)
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]
runtime=root/'.runtime'; runtime.mkdir(mode=0o700,exist_ok=True)
tls=runtime/'tls'; tls.mkdir(mode=0o700,exist_ok=True)
cert=tls/'server.crt'; key=tls/'server.key'
names=['DNS:localhost','DNS:'+socket.gethostname(),'IP:127.0.0.1']
for value in subprocess.check_output(['hostname','-I'],text=True).split():
    try: names.append('IP:'+str(ipaddress.ip_address(value)))
    except ValueError: pass
if not cert.exists():
    subprocess.run(['openssl','req','-x509','-newkey','rsa:3072','-sha256','-nodes','-days','365',
        '-keyout',str(key),'-out',str(cert),'-subj','/CN=QIXR Quantum Companion',
        '-addext','subjectAltName='+','.join(names)],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    os.chmod(key,0o600)
env=runtime/'service.env'
if not env.exists():
    env.write_text(f'QIXR_RUNTIME={runtime}\nQIXR_MODEL=qwen3:8b\nQIXR_OLLAMA_URL=http://127.0.0.1:11434\nQIXR_VOICE=1\nQIXR_PARALLEL=2\nQIXR_QUEUE=8\nQIXR_MAX_CLIENTS=8\n')
    env.chmod(0o600)
unit=f'''[Unit]
Description=QIXR quantum companion
After=network.target

[Service]
Type=simple
WorkingDirectory={root}/server
EnvironmentFile={env}
ExecStart={root}/.venv/bin/python -m uvicorn qixr_companion.api:app --host 0.0.0.0 --port {args.port} --ssl-certfile {cert} --ssl-keyfile {key} --ws-max-size 1048576 --limit-concurrency 64
Restart=no
UMask=0077
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=default.target
'''
destination=Path.home()/'.config/systemd/user/qixr-companion.service'
destination.parent.mkdir(parents=True,exist_ok=True)
if destination.exists() and 'Description=QIXR quantum companion' not in destination.read_text():
    raise RuntimeError('An unrelated service already uses the qixr-companion name')
destination.write_text(unit)
subprocess.run(['systemctl','--user','daemon-reload'],check=True)
if args.start: subprocess.run(['systemctl','--user','start','qixr-companion'],check=True)
print('Service configuration:',destination)
print('Certificate:',cert)

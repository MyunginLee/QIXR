"""Download pinned local speech models. Model files remain outside Git."""
import hashlib
import json
from pathlib import Path
import tarfile
import urllib.request

from huggingface_hub import snapshot_download

root=Path(__file__).resolve().parents[2]
destination=root/'.runtime/models'; destination.mkdir(parents=True,exist_ok=True)
manifest=json.loads((root/'server/model-manifest.json').read_text())
for asset in manifest['tts']:
    archive=destination/asset['file']
    if not archive.exists(): urllib.request.urlretrieve(asset['url'],archive)
    if hashlib.sha256(archive.read_bytes()).hexdigest()!=asset['sha256']: raise ValueError('Model checksum mismatch: '+asset['file'])
    folder=destination/asset['directory']
    if not folder.exists():
        with tarfile.open(archive) as tar: tar.extractall(destination,filter='data')
snapshot_download(manifest['asr']['repository'],revision=manifest['asr']['revision'],local_dir=destination/'whisper-small')
print('Speech models ready:',destination)

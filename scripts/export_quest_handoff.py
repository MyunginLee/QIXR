"""Export this working-tree handoff and verify patch/overlay replay, entirely offline."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)


def paths(*args):
    return [p.decode('utf-8') for p in git(*args).split(b'\0') if p]


def permitted(name):
    p = Path(name)
    if p.is_absolute() or '..' in p.parts:
        return False
    allowed = name in {'.gitignore', 'AGENTS.md', 'README.md'} or name.startswith((
        'Assets/Scripts/', 'Assets/Editor/', 'Assets/Resources/Quantum', 'server/', 'docs/', 'scripts/'))
    return allowed and p.suffix.lower() not in {'.token', '.env', '.key', '.pem', '.pfx', '.p12', '.apk', '.zip'}


def export(destination):
    base = git('rev-parse', 'HEAD').decode().strip()
    if paths('diff', '--name-only', '--diff-filter=DRT', '-z', 'HEAD'):
        raise RuntimeError('Review deletions/renames/type changes before exporting this handoff.')
    changed = paths('diff', '--name-only', '-z', 'HEAD')
    if any(not permitted(p) for p in changed):
        raise RuntimeError('A tracked change is outside the reviewed handoff source paths.')
    new = [p for p in paths('ls-files', '--others', '--exclude-standard', '-z') if permitted(p)]
    names = sorted(set(changed + new))
    content = {}
    for name in names:
        path = ROOT/name
        if path.is_symlink() or not path.resolve().is_relative_to(ROOT) or not path.is_file():
            raise RuntimeError('Refusing a symlink, missing file or path outside the project: '+name)
        content[name] = path.read_bytes()
    if 'docs/handoff/fixtures/protocol-v1.json' not in names:
        raise RuntimeError('Generate the offline Unity fixtures before exporting.')
    fixture = json.loads(content['docs/handoff/fixtures/protocol-v1.json'])
    if not fixture.get('fixtureOnly'):
        raise RuntimeError('Only synthetic fixtures belong in this source handoff.')
    for name, digest in fixture['sourceSha256'].items():
        if hashlib.sha256((ROOT/name).read_bytes()).hexdigest() != digest:
            raise RuntimeError('Regenerate Unity fixtures after changing '+name)
    patch = git('diff', '--binary', '--full-index', 'HEAD')
    # Refuse accidental disclosure in a source file/report, not only by pathname.
    private_values = []
    admin = ROOT/'.runtime/admin.token'
    if admin.exists():
        private_values.append(admin.read_text().strip().encode())
    for config_name in ('host-config.json', 'client-config.json'):
        config_path = ROOT/'.runtime'/config_name
        if config_path.exists():
            config = json.loads(config_path.read_text())
            private_values.extend(str(config[k]).encode() for k in ('authorityToken', 'joinToken', 'conversationToken') if config.get(k))
    for name, data in [('tracked-changes.patch', patch), *content.items()]:
        if any(value in data for value in private_values if len(value) >= 16):
            raise RuntimeError('A private runtime credential appears in handoff content: '+name)
    files = [dict(path=p, kind='tracked' if p in changed else 'overlay',
                  sha256=hashlib.sha256(content[p]).hexdigest()) for p in names]
    manifest = dict(formatVersion=1, repository='https://github.com/MyunginLee/QIXR', baseCommit=base,
                    sourceBranch=git('branch', '--show-current').decode().strip(),
                    importOrder=['tracked-changes.patch', 'overlay/'],
                    patchSha256=hashlib.sha256(patch).hexdigest(), files=files,
                    excluded='Runtime credentials, TLS keys, models, virtual environments, generated Unity data, and app builds.')

    # Prove the package can recreate these exact source bytes without touching this checkout.
    with tempfile.TemporaryDirectory(prefix='qixr-handoff-check-') as temp:
        scratch = Path(temp)
        subprocess.run(['git', 'init', '-q', str(scratch)], check=True)
        for name in changed:
            original = subprocess.run(['git', 'show', base+':'+name], cwd=ROOT, capture_output=True)
            if original.returncode == 0:
                target = scratch/name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(original.stdout)
        subprocess.run(['git', 'apply', '--check', '-'], input=patch, cwd=scratch, check=True)
        subprocess.run(['git', 'apply', '-'], input=patch, cwd=scratch, check=True)
        for name in new:
            target = scratch/name
            if target.exists() and target.read_bytes() != content[name]:
                raise RuntimeError('Overlay conflicts with the patch: '+name)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(content[name])
        for file in files:
            if hashlib.sha256((scratch/file['path']).read_bytes()).hexdigest() != file['sha256']:
                raise RuntimeError('Replayed source differs: '+file['path'])

    destination.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(destination, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr('README.md', '# QIXR Unity / Quest handoff\n\nRead `overlay/docs/handoff/00_START_HERE.md` first. Import into the matching base checkout before opening Unity. No runtime startup is authorized by this archive.\n')
        archive.writestr('manifest.json', json.dumps(manifest, indent=2)+'\n')
        archive.writestr('tracked-changes.patch', patch)
        for name in new:
            archive.writestr('overlay/'+name, content[name])
    with zipfile.ZipFile(destination) as archive:
        if archive.testzip() is not None:
            raise RuntimeError('Archive CRC validation failed')
        if hashlib.sha256(archive.read('tracked-changes.patch')).hexdigest() != manifest['patchSha256']:
            raise RuntimeError('Archived patch hash differs')
        for name in new:
            if archive.read('overlay/'+name) != content[name]:
                raise RuntimeError('Archived overlay bytes differ: '+name)
    digest = hashlib.sha256(destination.read_bytes()).hexdigest()
    destination.with_suffix(destination.suffix+'.sha256').write_text(digest+'  '+destination.name+'\n')
    print(f'Exported {len(files)} files; scratch patch/overlay replay and all hashes verified.')
    print(f'Archive: {destination} ({destination.stat().st_size} bytes)')
    print(f'SHA-256: {digest}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT/'handoff-export/QIXR-Unity-Quest-handoff.zip')
    export(parser.parse_args().output.resolve())

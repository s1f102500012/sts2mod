#!/usr/bin/env python3
"""Build, validate and optionally deploy the two real game-version variants."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MOD_ID = 'BetterCharacterRelics'
TARGETS = ('0.107.1', '0.111.0')
VARIANTS = 'better-character-relics-variants.manifest'
GAME_APP = Path(os.environ.get('STS2_GAME_APP', '/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app'))
GAME_BIN = GAME_APP / 'Contents/MacOS/Slay the Spire 2'


def run(*command, cwd=ROOT):
    subprocess.run([str(part) for part in command], cwd=cwd, check=True)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def file_map(directory):
    return {str(path.relative_to(directory)): digest(path) for path in directory.rglob('*') if path.is_file()}


def validate(dist):
    manifest = json.loads((dist / f'{MOD_ID}.json').read_text())
    assert manifest['id'] == MOD_ID and manifest['min_game_version'] == TARGETS[0]
    assert manifest['has_dll'] and manifest['has_pck'] and manifest['affects_gameplay']
    entries = json.loads((dist / VARIANTS).read_text())
    assert entries['schema'] == 1
    assert tuple(item['compatTarget'] for item in entries['variants']) == TARGETS
    expected = {f'{MOD_ID}.dll', f'{MOD_ID}.pck', f'{MOD_ID}.json', VARIANTS}
    for entry in entries['variants']:
        target = entry['compatTarget']
        assert entry['directory'] == f'lib/{target}' and entry['assembly'] == f'{MOD_ID}.dll'
        relative = f'lib/{target}/{MOD_ID}.dll'
        assert digest(dist / relative) == entry['sha256']
        assert (dist / f'lib/{target}/compat-target.txt').read_text().strip() == target
        expected.update((relative, f'lib/{target}/compat-target.txt'))
    assert set(file_map(dist)) == expected, 'Unexpected bundle file set'
    assert (dist / f'{MOD_ID}.pck').stat().st_size > 0
    print(f'Validated {MOD_ID} {manifest["version"]}: loader + {", ".join(TARGETS)}', flush=True)


def build(reuse_build=False):
    staging = Path(tempfile.mkdtemp(prefix='bcr-bundle-'))
    try:
        entries = []
        for target in TARGETS:
            # One project at a time: target-specific bin/obj prevent stale-reference reuse.
            if not reuse_build:
                run('dotnet', 'run', '--project', ROOT / 'tests/BetterCharacterRelics.Tests.csproj', '-c', 'Release', f'-p:GameVersion={target}', '--', ROOT, '--runtime-patches')
            output = staging / 'lib' / target
            output.mkdir(parents=True)
            shutil.copy2(ROOT / f'src/bin/Release/{target}/net9.0/{MOD_ID}.dll', output / f'{MOD_ID}.dll')
            (output / 'compat-target.txt').write_text(target + '\n')
            entries.append({'compatTarget': target, 'directory': f'lib/{target}', 'assembly': f'{MOD_ID}.dll', 'sha256': digest(output / f'{MOD_ID}.dll')})
        if not reuse_build:
            run('dotnet', 'build', ROOT / f'loader/{MOD_ID}.Loader.csproj', '-c', 'Release', '-p:GameVersion=0.107.1', '--nologo')
        shutil.copy2(ROOT / f'loader/bin/Release/0.107.1/net9.0/{MOD_ID}.Loader.dll', staging / f'{MOD_ID}.dll')
        shutil.copy2(ROOT / f'assets/{MOD_ID}.json', staging / f'{MOD_ID}.json')
        (staging / VARIANTS).write_text(json.dumps({'schema': 1, 'variants': entries}, indent=2) + '\n')
        pack_log = Path(tempfile.gettempdir()) / (staging.name + '-pack.log')
        run(GAME_BIN, '--headless', '--log-file', pack_log, '--path', ROOT / 'tools', '-s', 'res://pack_mod.gd', '--', ROOT / f'assets/{MOD_ID}.json', staging / f'{MOD_ID}.pck')
        validate(staging)
        dist = ROOT / 'dist'
        if dist.exists():
            shutil.rmtree(dist)
        shutil.copytree(staging, dist)
        version = json.loads((dist / f'{MOD_ID}.json').read_text())['version']
        releases = ROOT / 'releases'
        releases.mkdir(exist_ok=True)
        archive = releases / f'{MOD_ID}-v{version}-0.107.1-0.111.0.zip'
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as bundle:
            for path in sorted(dist.rglob('*')):
                if path.is_file():
                    bundle.write(path, Path(MOD_ID) / path.relative_to(dist))
        with zipfile.ZipFile(archive) as bundle:
            assert bundle.testzip() is None
            assert {name.removeprefix(MOD_ID + '/'): hashlib.sha256(bundle.read(name)).hexdigest() for name in bundle.namelist()} == file_map(dist)
        print(f'Release: {archive}\nSHA256: {digest(archive)}', flush=True)
    finally:
        shutil.rmtree(staging)


def deploy():
    dist = ROOT / 'dist'
    validate(dist)
    destination = GAME_APP / 'Contents/MacOS/mods' / MOD_ID
    destination.parent.mkdir(parents=True, exist_ok=True)
    backup_root = Path(tempfile.mkdtemp(prefix='bcr-deploy-backup-'))
    if destination.exists():
        shutil.copytree(destination, backup_root / MOD_ID)
    stage = Path(tempfile.mkdtemp(prefix='.bcr-stage-', dir=destination.parent))
    try:
        shutil.copytree(dist, stage, dirs_exist_ok=True)
        assert file_map(stage) == file_map(dist)
        if destination.exists():
            shutil.rmtree(destination)
        os.replace(stage, destination)
        assert file_map(destination) == file_map(dist)
    except Exception:
        if destination.exists():
            shutil.rmtree(destination)
        if (backup_root / MOD_ID).exists():
            shutil.copytree(backup_root / MOD_ID, destination)
        raise
    finally:
        if stage.exists():
            shutil.rmtree(stage)
    print(f'Deployed and byte-verified: {destination}\nPrevious package backup: {backup_root}', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--deploy', action='store_true')
    parser.add_argument('--deploy-only', action='store_true')
    parser.add_argument('--validate-only', action='store_true')
    parser.add_argument('--package-only', action='store_true', help='Reuse already built and verified DLLs; only regenerate the package')
    args = parser.parse_args()
    if args.validate_only:
        validate(ROOT / 'dist')
    else:
        if not args.deploy_only:
            build(reuse_build=args.package_only)
        if args.deploy or args.deploy_only:
            deploy()

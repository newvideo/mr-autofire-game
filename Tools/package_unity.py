#!/usr/bin/env python3
"""Create a deterministic Unity import package without requiring Unity Editor."""
from pathlib import Path
import gzip
import io
import tarfile

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'Releases/Polyus-Demo-0.2.unitypackage'


def collect():
    selected = {ROOT / 'Assets/Polyus.Game.asmdef'}
    for relative in ('Assets/Polyus', 'Assets/Scripts/Monetization', 'Assets/Tests'):
        base = ROOT / relative
        selected.add(base)
        selected.update(p for p in base.rglob('*') if p.suffix != '.meta')
    for p in list(selected):
        parent = p.parent
        while parent != ROOT / 'Assets':
            selected.add(parent)
            parent = parent.parent
    return sorted(selected)


def guid(path):
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        raise ValueError(f'Missing metadata: {path}')
    return next(line.split(': ', 1)[1] for line in meta.read_text().splitlines() if line.startswith('guid: '))


def main():
    assets = collect()
    OUTPUT.parent.mkdir(exist_ok=True)
    seen = set()
    with OUTPUT.open('wb') as raw:
        with gzip.GzipFile(fileobj=raw, mode='wb', filename='', mtime=0) as compressed:
            with tarfile.open(fileobj=compressed, mode='w') as archive:
                for asset in assets:
                    identity = guid(asset)
                    if identity in seen:
                        raise ValueError('Duplicate GUID ' + identity)
                    seen.add(identity)
                    entries = {'pathname': asset.relative_to(ROOT).as_posix().encode(),
                               'asset.meta': Path(str(asset) + '.meta').read_bytes()}
                    if asset.is_file():
                        entries['asset'] = asset.read_bytes()
                    for name, data in entries.items():
                        member = tarfile.TarInfo(identity + '/' + name)
                        member.size = len(data)
                        member.mode = 0o644
                        member.mtime = 0
                        archive.addfile(member, io.BytesIO(data))
    # Read back every payload and compare it to the current checkout.
    with tarfile.open(OUTPUT, 'r:gz') as archive:
        for asset in assets:
            identity = guid(asset)
            assert archive.extractfile(identity + '/pathname').read().decode() == asset.relative_to(ROOT).as_posix()
            assert archive.extractfile(identity + '/asset.meta').read() == Path(str(asset) + '.meta').read_bytes()
            if asset.is_file():
                assert archive.extractfile(identity + '/asset').read() == asset.read_bytes()
    print(f'Package verified: {OUTPUT.name}, {len(assets)} assets, {OUTPUT.stat().st_size:,} bytes')


if __name__ == '__main__':
    main()

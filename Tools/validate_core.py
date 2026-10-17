#!/usr/bin/env python3
"""Compile and execute the actual dependency-free gameplay/economy rules."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('--dotnet', default=shutil.which('dotnet'))
args = parser.parse_args()
if not args.dotnet:
    raise SystemExit('A .NET 8 SDK is required for this optional cloud validation.')
root = Path(__file__).resolve().parents[1]
sdk_root = Path(args.dotnet).resolve().parent
compiler = next(iter(sorted((sdk_root / 'sdk').glob('8.*/Roslyn/bincore/csc.dll'), reverse=True)), None)
references = next(iter(sorted((sdk_root / 'packs/Microsoft.NETCore.App.Ref').glob('8.*/ref/net8.0'), reverse=True)), None)
if not compiler or not references:
    raise SystemExit('The .NET 8 SDK and reference pack are required.')
sources = ['Tools/Validation/Program.cs', 'Assets/Polyus/Runtime/PolyusRunRules.cs',
           'Assets/Scripts/Monetization/EconomyState.cs', 'Assets/Scripts/Monetization/MonetizationRules.cs']
with tempfile.TemporaryDirectory(prefix='polyus-check-') as temporary:
    work = Path(temporary)
    output = work / 'Polyus.Validation.dll'
    response = work / 'compile.rsp'
    response.write_text('\n'.join(['-nologo', '-target:exe', f'-out:"{output}"']
        + [f'-r:"{p}"' for p in references.glob('*.dll')]
        + ['"' + str(root / p) + '"' for p in sources]))
    subprocess.run([args.dotnet, str(compiler), '@' + str(response)], check=True)
    (work / 'Polyus.Validation.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': '8.0.0'}}}))
    subprocess.run([args.dotnet, str(output)], check=True)

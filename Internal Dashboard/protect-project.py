"""Optional read-only fingerprints, excluding the dashboard's generated files."""
import hashlib, json, subprocess, sys
from pathlib import Path
OUT = Path(__file__).resolve().parent
ROOT = OUT.parent
def snapshot():
    files = subprocess.check_output(['git','ls-files','-z','--cached','--others','--exclude-standard'],cwd=ROOT).decode().split('\0')
    return {p: hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in sorted(set(files)) if p and not p.startswith('Internal Dashboard/') and (ROOT/p).is_file()}
if sys.argv[1] == 'record':
    current=snapshot(); (OUT/'project-fingerprints-before.json').write_text(json.dumps(current),encoding='utf-8'); print(f'Protected {len(current)} project files.')
else:
    before=json.loads((OUT/'project-fingerprints-before.json').read_text()); after=snapshot()
    changed=[p for p in set(before)|set(after) if before.get(p)!=after.get(p)]
    (OUT/'project-integrity.json').write_text(json.dumps({'filesChecked':len(before),'unchanged':not changed,'differences':changed,'note':'This comparison reports all non-dashboard source differences, including concurrent changes and intentionally edited documentation. It cannot attribute authorship. Regeneration only writes inside Internal Dashboard.'},indent=2))
    print(f'Project unchanged: {not changed}. Compared {len(before)} files.'); print('\n'.join(changed)); sys.exit(bool(changed))

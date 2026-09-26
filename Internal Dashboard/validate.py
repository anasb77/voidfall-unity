"""Validate coverage and byte-for-byte provenance against the source catalogs."""
import json, hashlib, re, os
from pathlib import Path
BASE=Path(__file__).resolve().parent
ROOT=Path(os.environ.get('VOIDFALL_UNITY_ROOT',BASE.parent))
d=json.loads((BASE/'dist/content.json').read_text(encoding='utf-8'))
raw=json.loads((BASE/'raw-content.json').read_text(encoding='utf-8')); defs=raw['Definitions']
checks=[]
def verify(name,ok):
    checks.append({'check':name,'passed':bool(ok)})
    if not ok: raise AssertionError(name)
allvariants=[v for f in d['cards']+d['enemies']+d['weapons'] for v in f['variants']]+[f['evolution'] for f in d['weapons'] if f.get('evolution')]
for title,expected,group in [('weapons',[w['Id'] for w in raw['Weapons']],'Weapons'),('supports',[s['Id'] for s in raw['Supports']],'Supports'),('late upgrades',[s['Id'] for s in raw['LateUpgrades']],'Late upgrades')]:
    fs=[f for f in d['cards'] if f['category']==group];verify('Complete '+title+' families',set(expected)=={f['id'] for f in fs})
    source=raw['Weapons'] if group=='Weapons' else raw['Supports'] if group=='Supports' else raw['LateUpgrades']
    verify('Every '+title+' rank',all(len(f['variants'])==next(len(s['Ranks']) if 'Ranks' in s else s['MaxRank'] for s in source if s['Id']==f['id']) for f in fs))
verify('Every weapon evolution',len([f for f in d['cards'] if f['category']=='Evolutions'])==len(raw['Evolutions']))
verify('All five implemented Wild Cards',len([f for f in d['cards'] if f['category']=='Wild cards'])==len(raw['WildCards']))
enemyids={v['id'].split(':')[0] for f in d['enemies'] for v in f['variants']}
for key in ['ContentCatalog.Enemies','ApprovedMapContent.Enemies','NullCityContent.Enemies','DestroyerContent.Enemies','ContentCatalog.Bosses']:
    verify('Every definition in '+key,all(x['Id'] in enemyids for x in defs[key]))
verify('Hydra original ten populations',all(x['Id'] in enemyids for x in raw['HydraPopulations']))
verify('Shared fourteen families have four tiers',all(len(next(f for f in d['enemies'] if f['id']==x)['variants'])==4 for x in raw['SharedTierFamilies']))
verify('Three specialist elite families have four tiers',all(len(next(f for f in d['enemies'] if f['id']=='elite-'+x['Definition']['BaseId'])['variants'])==4 for x in raw['EliteVariants']))
verify('Every prepared map',set(x['Id'] for x in raw['PreparedArenas'])=={x['id'] for x in d['maps']})
verify('No missing entity art',not d['meta']['missingArt'])
verify('Computed card total',d['meta']['counts']['cards']==sum(len(f['variants']) for f in d['cards']))
verify('Unique card identifiers',len({v['id'] for f in d['cards'] for v in f['variants']})==sum(len(f['variants']) for f in d['cards']))
verify('Three player forms',len(d['forms'])==3)
for v in allvariants:
    for path in [v.get('image'),v.get('whiteImage')]:
        if path: verify('Local artwork exists: '+v['id'],(BASE/'dist'/path).is_file())
    src=v.get('source'); verify('Valid source location: '+v['id'],bool(src) and (ROOT/src['path']).is_file() and 1<=src['line']<=len((ROOT/src['path']).read_text(encoding='utf-8-sig').splitlines()))
sourceAssets={p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets/VoidFall').rglob('*') if p.suffix.lower() in {'.png','.svg','.jpg','.jpeg','.ttf'}}
verify('Every first-party image and font included',sourceAssets=={a['id'] for a in d['assets']})
verify('Every copied asset identical to game source',all(hashlib.sha256((BASE/'dist'/a['image']).read_bytes()).hexdigest()==a['sha256']==hashlib.sha256((ROOT/a['id']).read_bytes()).hexdigest() for a in d['assets']))
verify('No external image or script requests',not any('https://' in (BASE/'dist'/f).read_text(encoding='utf-8') for f in ['index.html','app.js','style.css']))
report={'passed':True,'checks':len(checks),'counts':d['meta']['counts'],'coverageChecks':checks[:20],'assetProvenance':True,'note':'Entity stats are authored/tier baselines, not a simulation of a particular run.'}
(BASE/'validation-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))

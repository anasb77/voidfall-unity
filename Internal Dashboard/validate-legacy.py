"""Coverage, source identity, rank completeness and current-match checks."""
import json,hashlib,re,os
from pathlib import Path
BASE=Path(__file__).resolve().parent
ROOT=Path(os.environ.get('VOIDFALL_UNITY_ROOT',BASE.parent))
LEGACY=Path(os.environ.get('VOIDFALL_LEGACY_ROOT',Path.home()/'Desktop/legacy voidfall'))
read=lambda p:json.loads(p.read_text(encoding='utf-8'))
raw=read(BASE/'legacy-raw.json');d=read(BASE/'dist/legacy-content.json');live=read(BASE/'dist/content.json');checks=[]
def verify(name,ok):
 checks.append(name)
 if not ok:raise AssertionError(name)
verify('All five source versions',set(v['version'] for v in raw['versions'])=={'v1','v2','v3','v4','v5'})
verify('Unique normalized identities',len(d['records'])==len({r['id'] for r in d['records']}))
rows={(r['category'],r['key']):r for r in d['records']}
mapping={'enemies':'Enemies','weapons':'Weapons','cards':'Cards','upgrades':'Upgrades','forms':'Forms','characters':'Characters','bosses':'Bosses','arenas':'Maps','sfx':'Audio','challenges':'Challenges','eliteMods':'Elite modifiers','events':'Events'}
for v in raw['versions']:
 for group,category in mapping.items():
  expected={e['id'] for e in v.get(group,[])}
  actual={r['key'] for r in d['records'] if r['category']==category and any(x['version']==v['version'] for x in r['versions'])}
  verify(v['version']+' complete '+category,expected==actual)
  for entry in v.get(group,[]):
   rendered=next(x for x in rows[(category,entry['id'])]['versions'] if x['version']==v['version'])
   if 'ranks' in entry:verify('Every original rank: '+v['version']+'/'+category+'/'+entry['id'],entry['ranks']==rendered['ranks'])
   if group=='enemies':verify('Enemy original native render: '+v['version']+'/'+entry['id'],rendered['image']==v['images']['enemy.'+entry['id']])
 expected={e['evolution']['id'] for e in v['weapons'] if e.get('evolution')}
 actual={r['key'] for r in d['records'] if r['category']=='Evolutions' and any(x['version']==v['version'] for x in r['versions'])}
 verify(v['version']+' complete evolution paths',expected==actual)
 expected={f['id']+':magic' for f in v['forms'] if f.get('magic')}
 actual={r['key'] for r in d['records'] if r['category']=='Abilities' and any(x['version']==v['version'] for x in r['versions'])}
 verify(v['version']+' complete magic definitions',expected==actual)
 verify(v['version']+' every original sprite key',all(any(u['version']==v['version'] and u['key']==key for a in raw['assets'] for u in a['uses']) for key in v['images']))
for r in d['records']:
 verify('Evidence for '+r['id'],r['status'] in ['Present','Adapted','Missing','Review'] and bool(r['reason']))
 verify('Missing has no asserted counterpart: '+r['id'],r['status']!='Missing' or r['current'] is None)
 for v in r['versions']:
  s=v['source'];p=BASE/'dist'/s['url'];verify('Source available: '+r['id']+'/'+v['version'],p.is_file() and 1<=s['line']<=len(p.read_text(encoding='utf-8-sig').splitlines()))
  if v.get('image'):verify('Local preview: '+r['id']+'/'+v['version'],(BASE/'dist'/v['image']).is_file())
 c=r.get('current')
 if c:
  verify('Live counterpart evidence: '+r['id'],bool(c.get('source')) and bool(c.get('sourceExcerpt')))
  s=c['source'];verify('Exact current source copy: '+r['id'],(BASE/'dist'/s['url']).read_bytes()==(ROOT/s['path']).read_bytes())
  if c.get('image'):verify('Live counterpart art available: '+r['id'],(BASE/'dist'/c['image']).is_file())
for a in raw['assets']:
 p=BASE/'dist'/a['image'];verify('Native render hash: '+a['id'],hashlib.sha256(p.read_bytes()).hexdigest()==a['id'])
 verify('Every rendered asset in gallery: '+a['id'],('Assets',a['id']) in rows)
 verify('Every asset version preserved: '+a['id'],{u['version'] for u in a['uses']}=={v['version'] for v in rows[('Assets',a['id'])]['versions']})
for f in d['files']:
 copy=BASE/'dist'/f['url'];verify('Portable exact source file: '+f['version']+'/'+f['path'],copy.is_file() and hashlib.sha256(copy.read_bytes()).hexdigest()==f['sha256'])
 if LEGACY.exists():verify('Legacy original unchanged: '+f['version']+'/'+f['path'],hashlib.sha256((LEGACY/('Voidfall '+f['version'])/f['path']).read_bytes()).hexdigest()==f['sha256'])
for name in ['index.html','legacy-view.js','app.js','style.css','design-refinement.css']:
 verify('No remote UI dependency: '+name,'https://' not in (BASE/'dist'/name).read_text(encoding='utf-8'))
report={'passed':True,'checks':len(checks),'counts':d['counts'],'versions':[v['id'] for v in d['versions']],'originalFilesVerified':LEGACY.exists(),'note':'Catalog absence is scoped evidence, not a full proof of runtime feature absence. Art renders run original code in an isolated Canvas implementation.'}
(BASE/'legacy-validation-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))

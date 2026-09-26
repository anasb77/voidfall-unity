"""Normalize the executed five-version legacy union, pair evidence with live entries.
Missing means absent as a named catalog entry; it does not prove every mechanic is absent.
Similar roles are explicitly review candidates, never proof of migration or approval.
"""
import json,re,hashlib,os
from pathlib import Path
from collections import Counter
BASE=Path(__file__).resolve().parent
ROOT=Path(os.environ.get('VOIDFALL_UNITY_ROOT',BASE.parent))
read=lambda p:json.loads(p.read_text(encoding='utf-8'))
raw=read(BASE/'legacy-raw.json');live=read(BASE/'dist/content.json')
records={}
enemy_alias={'tank':'brute','shooter':'gunner','charger':'dasher','shielded':'guard','healer':'technician','summoner':'carrier'}
weapon_alias={'shotgun':'scattergun','rocket':'seeker'}
support_alias={'damage':'calibration','critChance':'optics','fireRate':'cycling','projSpeed':'projectileSpeed','areaSize':'amplifier','pierce':'phaseRounds','bossDmg':'giantSlayer','maxHp':'plating','regen':'regenerator','dodge':'dodge','emergHeal':'secondWind','moveSpeed':'mobility','magnet':'collector','xpGain':'scholar','eliteDmg':'giantSlayer','multishot':'split-pistol','secondWind':'secondWind','overcharge':'overload'}
upgrade_alias={'split':'split-pistol','rapid':'cycling','heavy':'calibration','pierce':'phaseRounds','crit':'optics','blades':'blades','swift':'mobility','magnet':'collector','vitality':'plating','patch':'repair'}
weapon_art={'blades':'blade','ice':'iceBolt','crossbow':'bolt','plasma':'plasma','nova':'nova','tendrils':'tendril','mines':'mine','voidorb':'plasma','drone':'dot.cyan'}
current={
 'Enemies':{f['id']:f for f in live['enemies'] if f['category']!='Bosses'},
 'Bosses':{f['id']:f for f in live['enemies'] if f['category']=='Bosses'},
 'Weapons':{f['id']:f for f in live['weapons']},
 'Cards':{f['id']:f for f in live['cards'] if f['category']=='Supports'},
 'Upgrades':{f['id']:f for f in live['cards']},
 'Maps':{f['id']:f for f in live['maps']},
 'Forms':{f['Id']:{'id':f['Id'],'name':f['Name'],'description':f['Blurb'],'stats':f,'source':{'path':'Assets/VoidFall/Content/PlayerForms.cs','line':1}} for f in live['forms']},
}
audioPath='Assets/VoidFall/Audio/ProceduralAudio.cs'
audio=(ROOT/audioPath).read_text(encoding='utf-8-sig')
audio_enum=re.search(r'public enum Cue\s*\{(.*?)\}',audio,re.S).group(1)
audio_cues=set(re.findall(r'\b(\w+)\s*,',audio_enum))
audio_alias={'shoot':'Fire','die':'Die','eliteDie':'EliteDeath','gem':'Gem','levelup':'LevelUp','pick':'Pickup','ui':'Ui','gameover':'GameOver','warn':'Warning','bossAtk':'BossCharge','bossDie':'BossDeath','coin':'Currency','shotgun':'Scattergun','rocket':'Seeker','mine':'MineDrop'}
def normalized_current(f):
 if not f:return None
 v=(f.get('variants') or [f])[0]
 return {'id':f.get('id'),'name':f['name'],'image':v.get('image'),'description':v.get('description',f.get('description')),'stats':v.get('stats'),'source':v.get('source',f.get('source')),'ranks':f.get('variants',[]),'evolution':f.get('evolution')}
def pair(category,entry):
 ident=entry['id'];target=None;status='Missing';note='No named '+category.lower()+' entry with this ID or the documented role mappings exists in the current Unity inventory. Similar mechanics elsewhere are not ruled out.'
 aliases={'Enemies':enemy_alias,'Weapons':weapon_alias,'Cards':support_alias,'Upgrades':upgrade_alias}
 if category in current:
  target=current[category].get(aliases.get(category,{}).get(ident,ident))
  if target:
   status='Present' if target['id']==ident and category in ['Enemies','Bosses'] else 'Adapted'
   note=('The current catalog contains '+target['name']+' ('+target['id']+'). '+('The stable ID survives. ' if target['id']==ident else 'Paired by the authored role, with a different ID. ')+'Compare both source definitions and artwork; this is not a guarantee of visual or behavior parity.')
  # Candidates are shown, but never silently treated as migrated.
  candidate={'Bosses':{'hive':'matriarch'},'Forms':{'sentinel':'default','phantom':'dasher','bastion':'brute'},'Characters':{'sentinel':'default','phantom':'dasher','bastion':'brute'},'Enemies':{'imp':'runner','broodmother':'null-broodmother','crawler':'null-crawler','lancer':'null-prism-lancer'}}.get(category,{}).get(ident)
  if candidate and not target:
   target=current.get('Forms' if category=='Characters' else category,{}).get(candidate)
   if target:status='Review';note='Possible related role: '+target['name']+'. Similarity is not evidence that this legacy identity, art, magic or behavior was migrated. Review the two sources.'
 elif category=='Characters':
  candidate={'sentinel':'default','phantom':'dasher','bastion':'brute'}.get(ident)
  target=current['Forms'].get(candidate)
  if target:status='Review';note='Possible starting-loadout role in the current '+target['name']+' form. The old character identity and stat block are not present as such.'
 elif category=='Audio':
  cue=audio_alias.get(ident,ident[0].upper()+ident[1:])
  if cue in audio_cues:
   status='Adapted';note='Unity has a '+cue+' cue. Both are procedural synthesis; cue-name correspondence does not verify the same sound.'
   target={'id':cue,'name':cue,'description':'Current procedural audio cue','source':{'path':audioPath,'line':next(i+1 for i,l in enumerate(audio.splitlines()) if l.strip()==cue+',')}}
  else:status='Review';note='No direct current cue-name match. The old synthesis code is preserved; a related sound may exist under another cue.'
 elif category in ['Abilities','Elite modifiers','Challenges']:
  status='Missing';note='This authored '+category.lower()+' entry is not in the current content catalogs. This is a content-inventory finding, not proof that every related effect is absent from the runtime.'
 elif category=='Events':
  status='Review';note='Original browser micro-event. Unity has different incident/director systems; no migration equivalence is established by the catalogs. Inspect the exact legacy source before deciding.'
 return status,note,normalized_current(target)
def add(category,entry,version,image=None,art_note=None,availability=None):
 key=category+':'+entry['id'];status,note,target=pair(category,entry)
 row=records.setdefault(key,{'id':'legacy:'+key,'key':entry['id'],'category':category,'name':entry.get('name',entry.get('nm',entry['id'].replace('_',' ').title())),'status':status,'reason':note,'current':target,'versions':[]})
 data={**entry,'version':version,'image':image or entry.get('image'),'artNote':art_note or entry.get('artNote'),'availability':availability,'description':entry.get('description',entry.get('desc',entry.get('behavior','')))}
 row['versions'].append(data);row['name']=data.get('name',row['name']);row['image']=data.get('image') or row.get('image');row['description']=data.get('description') or row.get('description','')
for bundle in raw['versions']:
 version=bundle['version'];images=bundle['images']
 for e in bundle['enemies']:add('Enemies',e,version,images.get('enemy.'+e['id']), 'Original normal-state sprite from this version’s makeSprites(). Hit-state artwork is in Assets. A definition is not proof it is spawned in every mode.')
 for w in bundle['weapons']:
  key=weapon_art.get(w['id'],'bullet');image=images.get(key)
  note='Shared original '+key+' render used by this weapon. This is a projectile/effect, not a weapon model.'
  if w['id'] in ['arc','flame','chain']:image=images.get('engine-projectile.'+w['id']);note='Original engine Canvas draw loop exported directly. This small attack is a line/particle, not a fabricated weapon model.'
  if w['id']=='thunder':note='The original render loop uses the shared bullet sprite for the falling Thunderstrike projectile.'
  add('Weapons',w,version,image,note)
  if w.get('evolution'):
   e={**w['evolution'],'weaponId':w['id'],'source':w['source']};add('Evolutions',e,version,image,'Legacy weapon/effect art; no separate evolution sprite is authored.')
   r=records['Evolutions:'+e['id']];parent=current['Weapons'].get(weapon_alias.get(w['id'],w['id']))
   if parent:
    evolved=parent.get('evolution');r['current']=normalized_current({'id':evolved['id'],'name':evolved['name'],'variants':[evolved]})
    r['status']='Adapted';r['reason']='The corresponding current weapon evolves into '+evolved['name']+'. The legacy name, conditions and effect differ; inspect both descriptions.'
 for p in bundle['cards']:add('Cards',p,version,None,'Original text/icon card. No separate raster card artwork was authored.')
 for u in bundle['upgrades']:add('Upgrades',u,version,None,'Original Lucide icon name: '+str(u.get('icon'))+'. Text is evaluated from the original description function.', 'Active v1 pool' if version=='v1' else 'Stored old pool; this version’s engine imports PASSIVES/WEAPONS instead of UPGRADES.')
 for f in bundle['forms']:
  add('Forms',f,version,images.get('players.'+f['id']) or images.get('player'),'Original form sprite. Browser exploration does not imply owner approval to restore it.')
  if f.get('magic'):add('Abilities',{'id':f['id']+':magic',**f['magic'],'formId':f['id'],'source':f['source']},version,images.get('players.'+f['id']),'The owning form’s original sprite; the ability itself is drawn in the engine.')
 for c in bundle['characters']:add('Characters',c,version,images.get('player'),'Original common player eye; these stored character definitions do not each have a unique sprite in this module.','Imported by v2 engine' if version=='v2' else 'Stored older character catalog; forms.ts owns the later engine selection.')
 for b in bundle['bosses']:add('Bosses',b,version)
 for a in bundle['arenas']:add('Maps',a,version)
 for s in bundle['sfx']:add('Audio',s,version,None,'Original procedural WebAudio definition. No audio file was authored; this inventory preserves the source, not an invented recording.')
 for c in bundle['challenges']:add('Challenges',c,version)
 for e in bundle['eliteMods']:add('Elite modifiers',e,version,availability=e.get('availability'))
 for e in bundle.get('events',[]):add('Events',e,version)
 # Capture stored binary assets too, should a later source export contain any.
for a in raw['assets']:
 uses=a['uses'];row={'id':'legacy:asset:'+a['id'],'key':a['id'],'name':uses[0]['key'],'category':'Assets','status':'Review','reason':'Original legacy render. Compare with the current artwork before deciding to restore it; lack of a byte-identical export is not proof the design is absent.','image':a['image'],'current':None,'versions':[]}
 for version in sorted({u['version'] for u in uses}):
  vs=[u for u in uses if u['version']==version]
  row['versions'].append({'version':version,'image':a['image'],'width':a['width'],'height':a['height'],'source':vs[0]['source'],'aliases':[u['key'] for u in vs],'description':', '.join(u['key'] for u in vs),'artNote':raw['renderer']})
 # Link normal enemy/form artwork to an independently verified catalog match.
 linked=next((records.get('Enemies:'+u['key'][6:]) for u in uses if u['key'].startswith('enemy.') and records.get('Enemies:'+u['key'][6:])),None)
 if linked and linked['current']:row['current']=linked['current'];row['reason']='Used by '+linked['name']+'. '+linked['reason'];row['status']='Review'
 records['Assets:'+a['id']]=row
for file in raw['files']:
 if file['path'].lower().endswith(('.png','.jpg','.jpeg','.svg','.webp','.wav','.mp3','.ogg','.glb','.gltf','.obj')):
  row={'id':'legacy:fileasset:'+file['sha256'],'key':file['path'],'name':Path(file['path']).name,'category':'Assets','status':'Review','reason':'Stored source asset; usage and Unity counterpart need review.','image':file['url'] if file['path'].lower().endswith(('.png','.jpg','.jpeg','.svg','.webp')) else None,'current':None,'versions':[{'version':file['version'],'source':{'version':file['version'],'path':file['path'],'line':1,'url':file['url']},'description':file['path']}]};records.setdefault('Fileasset:'+file['sha256'],row)
# Evidence is portable: include the exact inspected current definitions with each pair.
for row in records.values():
 for item in row['versions']:
  src=item['source'];p=BASE/'dist'/src['url'];lines=p.read_text(encoding='utf-8-sig').splitlines();item['sourceExcerpt']='\n'.join(lines[max(0,src['line']-2):src['line']+15])
 if row['current'] and row['current'].get('source'):
  s=row['current']['source'];p=ROOT/s['path'];lines=p.read_text(encoding='utf-8-sig').splitlines();row['current']['sourceExcerpt']='\n'.join(lines[max(0,s['line']-2):s['line']+18])
  dest=BASE/'dist/legacy/unity-evidence'/s['path'];dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(p.read_bytes());s['url']='legacy/unity-evidence/'+s['path']
def tally(rows):return dict(Counter(r['status'] for r in rows))
rows=list(records.values());content=[r for r in rows if r['category']!='Assets']
categories=list(dict.fromkeys(r['category'] for r in rows))
summary={c:{'total':sum(r['category']==c for r in rows),**tally([r for r in rows if r['category']==c])} for c in categories}
versions=[]
for v in raw['versions']:
 previous=next((p for p in raw['versions'] if p['number']==v['number']-1),None)
 previousFiles={f['path']:f['sha256'] for f in raw['files'] if previous and f['version']==previous['version']}
 changed=[f['path'] for f in raw['files'] if f['version']==v['version'] and previousFiles.get(f['path'])!=f['sha256']]
 versions.append({'id':v['version'],'name':'Voidfall '+v['version'],'files':v['files'],'enemies':len(v['enemies']),'weapons':len(v['weapons']),'cards':len(v['cards']),'forms':len(v['forms']),'changes':changed,'counts':dict(Counter(r['category'] for r in rows if any(x['version']==v['version'] for x in r['versions'])))})
result={'generatedAt':raw['generatedAt'],'liveGeneratedAt':live['meta']['generatedAt'],'versions':versions,'records':rows,'files':raw['files'],'categories':categories,'summary':summary,'counts':{'contentFamilies':len(content),'uniqueRenders':len(raw['assets']),'sourceFiles':len(raw['files']),**tally(content)},'note':'Missing = absent as a named catalog entry after stable-ID and reviewed role mappings. Adapted = current counterpart with changed definition. Present = current stable ID exists; parity is not implied. Review = possible relation or art/audio needing manual comparison. None of these labels approves migration.','scope':'Five complete source versions. Families are a union across versions, not just v5. Original Canvas normal/hit/projectile/form/dot renders and fixed arena/boss stills are included. Cards carry every authored rank. Stored, superseded catalogs are labeled. Sound is source only; no invented recordings.','renderer':raw['renderer']}
(BASE/'dist/legacy-content.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
(BASE/'dist/legacy-data.js').write_text('window.VOIDFALL_LEGACY = '+json.dumps(result,ensure_ascii=False)+';\n',encoding='utf-8')
(BASE/'legacy-inventory-report.json').write_text(json.dumps({'counts':result['counts'],'categories':summary,'versions':versions,'limitations':[result['note'],result['scope']]},indent=2),encoding='utf-8')
print(json.dumps(result['counts']));print(json.dumps(summary,indent=2))

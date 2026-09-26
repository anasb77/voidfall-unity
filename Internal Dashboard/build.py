"""Build a portable inventory using executed C# catalogs and actual Unity GUIDs.
Never writes to the source project. No inferred/prototype game content.
"""
import argparse, hashlib, json, re, shutil, struct, subprocess, os
from pathlib import Path
from datetime import datetime, timezone

BASE=Path(__file__).resolve().parent
ROOT=Path(os.environ.get('VOIDFALL_UNITY_ROOT',BASE.parent))
OUT=BASE/'dist'
CONTENT='Assets/VoidFall/Content/'
RUNTIME='Assets/VoidFall/Runtime/Gameplay/'
RES='Assets/VoidFall/Resources/VoidFall/'
APPROVED='Assets/VoidFall/Generated/ApprovedMaps/'
def text(path): return (ROOT/path).read_text(encoding='utf-8-sig')
def source(path,needle=None):
    lines=text(path).splitlines(); line=next((i+1 for i,s in enumerate(lines) if needle and needle in s),1)
    return {'path':path,'line':line}
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()

parser=argparse.ArgumentParser(); parser.add_argument('--refresh',action='store_true'); args=parser.parse_args()
if args.refresh:
    for folder in ['Core','Content']:
        dst=BASE/'exporter/snapshot'/folder; dst.mkdir(parents=True,exist_ok=True)
        # Replace only snapshots in this dashboard's own exporter folder.
        for p in dst.glob('*.cs'): p.unlink()
        for p in (ROOT/'Assets/VoidFall'/folder).glob('*.cs'): shutil.copy2(p,dst/p.name)
    sdk=Path(os.environ.get('VOIDFALL_DOTNET',shutil.which('dotnet') or r'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data/DotNetSdk/dotnet.exe'))
    subprocess.run([str(sdk),'run','--project',str(BASE/'exporter/Exporter.csproj'),'--',str(BASE/'raw-content.json')],cwd=BASE,check=True)
raw=json.loads((BASE/'raw-content.json').read_text(encoding='utf-8')); defs=raw['Definitions']
OUT.mkdir(exist_ok=True)
assets=[]; bypath={}; byguid={}
for p in sorted((ROOT/'Assets/VoidFall').rglob('*')):
    if p.suffix.lower() not in {'.png','.jpg','.jpeg','.svg','.ttf'}: continue
    rel=p.relative_to(ROOT).as_posix(); url='assets/'+p.relative_to(ROOT/'Assets/VoidFall').as_posix()
    dest=OUT/url; dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists() or digest(dest)!=digest(p): shutil.copy2(p,dest)
    meta=Path(str(p)+'.meta'); guid=re.search(r'^guid: (\w+)',meta.read_text(),re.M).group(1) if meta.exists() else None
    size=None
    if p.suffix=='.png':
        with p.open('rb') as f:
            h=f.read(24)
            if h[:8]==b'\x89PNG\r\n\x1a\n': size=list(struct.unpack('>II',h[16:24]))
    a={'id':rel,'name':p.name,'image':url,'source':{'path':rel,'line':1},'guid':guid,'bytes':p.stat().st_size,'size':size,'area':p.relative_to(ROOT/'Assets/VoidFall').parts[0],'type':p.suffix[1:].upper(),'sha256':digest(p)}
    assets.append(a); bypath[rel]=a
    if guid: byguid[guid]=a
def img(path): return bypath.get(path,{}).get('image')
def guidimg(guid): return byguid.get(guid,{}).get('image')
catalog=text('Assets/VoidFall/Generated/Resources/VoidFall/Generated/ProceduralSpriteCatalog.asset')
sprites={k:guidimg(g) for k,g in re.findall(r'- _key: (.+)\s+_sprite: \{fileID: \d+, guid: (\w+)',catalog)}
def sprite(prefix): return next((v for k,v in sprites.items() if k.startswith(prefix) and not k.endswith('|1')),None) or next((v for k,v in sprites.items() if k.startswith(prefix)),None)
roster={}
for ident,tier,elite,guid in re.findall(r'- Id: (\w+)\s+Tier: (\d+)\s+Elite: (\d+)\s+Sprite: \{fileID: \d+, guid: (\w+)',text(RES+'RosterProgressionVisuals.asset')):
    roster[(ident,int(tier),int(elite))]=guidimg(guid)
city={}
citytext=text('Assets/VoidFall/Generated/ArenaPackages/NullCity/NullCityVisuals.asset')
for ident,body in re.findall(r'- _id: ([^\n]+)\n(.*?)(?=  - _id:|\Z)',citytext,re.S):
    match=re.search(r'_frames:\s+- \{fileID: \d+, guid: (\w+)',body)
    if match: city[ident]=guidimg(match.group(1))
icons={'lifeSteal':'heart','scavenger':'scrap','scattergun':'scatter','railgun':'rail','phaseRounds':'phase','giantSlayer':'giant','secondWind':'wind','collector':'magnet','calibration':'power','output':'power','regenerator':'regen','plating':'armor','frame':'armor','mobility':'speed','adrenal':'speed','projectileSpeed':'speed','cycling':'cool','cooling':'cool','amplifier':'radius','optics':'crit','pistol':'pistol','clock':'clock','blades':'blades'}
def icon(ident):
    key='split' if ident.startswith('split-') else icons.get(ident)
    return img(RES+'ApprovedHud/Icon-'+key+'.png') if key else None
def weaponart(ident,rank,evolved=False):
    if ident in ['mines','summons','clock','boomerang']:
        return sprites.get('arsenal|'+str(['mines','summons','clock','boomerang'].index(ident)*12+(rank-1)*2+int(evolved)))
    if ident in ['pistol','railgun']:
        return sprites.get('arsenal|'+str(49+(7 if ident=='railgun' else 0)+(6 if evolved else rank-1)))
    if ident=='blades': return sprites.get('fixed|hollow-blade' if evolved else 'fixed|blade')
    key={'scattergun':'scattergun','arc':'arc','seeker':'seeker'}[ident]
    # These weapons reuse live projectile/attack artwork across ranks.
    return sprite('projectile-frame|'+key+'|') or icon(ident)

families=[]; weapons=[]
for w in raw['WeaponCards']:
    origin=CONTENT+('ArsenalContent.cs' if w['Id'] in ['mines','summons','clock','boomerang'] else 'ContentCatalog.Generated.cs')
    variants=[{'id':'weapon:'+w['Id']+':'+str(r['Rank']),'name':w['Name'],'rank':r['Rank'],'description':r['Description'],'stats':r['Stats'],'image':weaponart(w['Id'],r['Rank']),'artNote':'Arc Lash is drawn with runtime lines; it has no standalone attack sprite.' if w['Id']=='arc' else 'Live attack artwork; some ranks share the same sprite.','source':source(origin,'Id = "'+w['Id']+'"')} for r in w['Ranks']]
    evo=next(e for e in raw['Evolutions'] if e['WeaponId']==w['Id'])
    support=next(s for s in raw['Supports'] if s['Id']==evo['SupportId'])
    e={'id':'evolution:'+w['Id'],'name':evo['Name'],'rank':'EV','description':evo['Description'],'image':weaponart(w['Id'],6,True),'artNote':'Arc Network is drawn with runtime lines; it has no standalone attack sprite.' if w['Id']=='arc' else 'Live evolved attack artwork.','requirement':w['Name']+' VI + '+support['Name']+' '+str(support['MaxRank']),'accent':evo['Accent'],'source':source(origin,evo['Name'])}
    f={'id':w['Id'],'name':w['Name'],'category':'Weapons','accent':w['Accent'],'description':w['Summary'],'icon':icon(w['Id']),'variants':variants,'evolution':e,'source':source(origin),'kind':next(x['Kind'] for x in raw['Weapons'] if x['Id']==w['Id'])}
    weapons.append(f); families.append(f)
    families.append({'id':'evolution-'+w['Id'],'name':evo['Name'],'category':'Evolutions','accent':evo['Accent'],'variants':[e],'source':e['source'],'description':e['requirement']})
for category,key in [('Supports','SupportCards'),('Late upgrades','LateCards')]:
    for s in raw[key]:
        origin=CONTENT+('UpgradeRules.cs' if category=='Late upgrades' else 'SurvivalSupportCatalog.cs' if s['Id'] in ['lifeSteal','scavenger'] else 'LegacyRestorationRules.cs' if s['Id'] in ['phaseRounds','giantSlayer','secondWind'] or s['Id'].startswith('split-') else 'ExtendedCatalog.cs' if s['Id'] in ['dodge','scholar','projectileSpeed'] else 'ContentCatalog.Generated.cs')
        condition='Applies only to '+s['Name'].split('�')[-1].strip() if s['Id'].startswith('split-') else 'Requires a piercing-compatible weapon: Pulse Pistol, Scattergun or Railgun.' if s['Id']=='phaseRounds' else None
        variants=[{'id':s['Id']+':'+str(r['Rank']),'name':s['Name'],'rank':r['Rank'],'description':r['Description'],'image':icon(s['Id']),'artNote':'Bundled HUD icon.' if icon(s['Id']) else 'The live level-up card uses a circle glyph; no unique icon is assigned.','requirement':condition,'source':source(origin,s['Id'])} for r in s['Ranks']]
        families.append({'id':s['Id'],'name':s['Name'],'category':category,'accent':s['Accent'],'icon':icon(s['Id']),'variants':variants,'source':source(origin,s['Id']),'description':condition or ('Permanent run stat upgrades' if category=='Late upgrades' else 'Run support upgrade')})
for w in raw['WildCards']:
    families.append({'id':'wild-'+w['Id'],'name':w['Name'],'category':'Wild cards','accent':'#c8a3ff','variants':[{'id':'wild-'+w['Id'],'name':w['Name'],'rank':1,'description':w['Description'],'source':source(CONTENT+'WildCardRules.cs',w['Id'])}],'source':source(CONTENT+'WildCardRules.cs',w['Id']),'description':'Unique per run · Roulette'})
repairtext=text(RUNTIME+'VoidFallGameRuntime.cs'); repair=re.search(r'Id = "repair".*?Name = "([^"]+)".*?Description = "([^"]+)"',repairtext,re.S)
families.append({'id':'repair','name':repair[1],'category':'Repair','accent':'#fb7185','variants':[{'id':'repair','name':repair[1],'rank':1,'description':repair[2],'image':img(RES+'ApprovedHud/Icon-heart.png'),'source':source(RUNTIME+'VoidFallGameRuntime.cs','Id = "repair"')}],'source':source(RUNTIME+'VoidFallGameRuntime.cs','Id = "repair"'),'description':'Conditional level-up repair offer'})
claims=text(RUNTIME+'VoidFallGameRuntime.RouletteClaims.cs')
fallbacks=[int(x) for x in re.findall(r'AddPartsClaim\((\d+)',claims)]
for amount in sorted(set(raw['RouletteScraps']+fallbacks)):
    families.append({'id':'reward-scraps-'+str(amount),'name':str(amount)+' Scraps','category':'Rewards','accent':'#e9c786','variants':[{'id':'reward-scraps-'+str(amount),'name':str(amount)+' Scraps','rank':1,'description':'Add '+str(amount)+' Scraps to your run earnings for the Workshop.'+(' Also grants 750 score when all Wild Cards are already owned.' if amount==80 else ''),'image':img(RES+'ApprovedHud/Icon-scrap.png'),'stats':{'Scraps':amount},'source':source(CONTENT+'RouletteRules.cs','PartsReward') if amount in raw['RouletteScraps'] else source(RUNTIME+'VoidFallGameRuntime.RouletteClaims.cs','AddPartsClaim('+str(amount))}],'source':source(RUNTIME+'VoidFallGameRuntime.RouletteClaims.cs'),'description':'Roulette payout' if amount in raw['RouletteScraps'] else 'Conditional reward fallback'})
powerup=re.search(r'Title = "(Random Power-Up Drop)", Detail = "([^"]+)"',claims)
families.append({'id':'random_power_up','name':powerup[1],'category':'Rewards','accent':'#71d7f7','variants':[{'id':'random_power_up','name':powerup[1],'rank':1,'description':powerup[2],'source':source(RUNTIME+'VoidFallGameRuntime.RouletteClaims.cs',powerup[1])}],'source':source(RUNTIME+'VoidFallGameRuntime.RouletteClaims.cs'),'description':'Roulette · physical pickup reward'})
# Permanent Workshop entries are displayed separately from run offer pools.
workshopPath='Assets/VoidFall/UI/Views/WorkshopController.cs'; workshop=text(workshopPath)
names=dict(re.findall(r'case "([^"]+)": return "([^"]+)"',workshop.split('public static string NameFor')[1].split('public static string DescriptionFor')[0]))
descriptions=dict(re.findall(r'case "([^"]+)": return "([^"]+)"',workshop.split('public static string DescriptionFor')[1].split('public static int CostFor')[0]))
costSection=workshop.split('public static int CostFor')[1].split('public static int MaxRankFor')[0]
for ident,name in names.items():
    expr=re.search(r'case "'+ident+r'": return ([^;]+);',costSection)[1]; costs=[int(v) for v in re.findall(r'\? (\d+)',expr)]
    variants=[{'id':'workshop-'+ident+':'+str(i+1),'name':name,'rank':i+1,'description':descriptions[ident],'stats':{'CostInScraps':cost,'PermanentRank':i+1},'image':sprites.get('workshop-layer|'+ident+'/'+str(i+1)),'artNote':'Live cosmetic layer; the final character combines multiple layers.','source':source(workshopPath,'case "'+ident+'"')} for i,cost in enumerate(costs)]
    families.append({'id':'workshop-'+ident,'name':name,'category':'Workshop','accent':'#91bdf2','variants':variants,'source':source(workshopPath,'case "'+ident+'"'),'description':'Permanent progression · not a run offer'})
# Dealer cards are extracted from authored offer construction; fragments through C#.
dealertext=text(CONTENT+'DealerRules.cs')
for kind,title,desc,art in re.findall(r'Kind = DealerOfferKind\.(\w+), Title = "([^"]+)", Description = "([^"]+)"(?:[^\n]*?)Art = "([^"]+)"',dealertext):
    if kind in ['EquipLegendary','UpgradeLegendary']: continue
    if kind=='ExtraProjectile': continue
    families.append({'id':'dealer-'+kind,'name':title,'category':'Dealer','accent':'#e8be75','variants':[{'id':'dealer-'+kind,'name':title,'rank':1,'description':desc,'image':img(RES+'Dealer/'+art+'.png'),'stats':{'Price':raw['Constants']['DealerRules.Price']},'source':source(CONTENT+'DealerRules.cs',title)}],'source':source(CONTENT+'DealerRules.cs',title),'description':'100 run Scraps · one purchase per crossing'})
# Dynamic weapon-target text is kept as authored, with its actual eligible target list.
families.append({'id':'dealer-ExtraProjectile','name':'Extra projectile','category':'Dealer','accent':'#e8be75','variants':[{'id':'dealer-ExtraProjectile','name':'Extra projectile','rank':1,'description':'The selected weapon fires one extra projectile per attack for this run.','image':img(RES+'Dealer/projectile.png'),'stats':{'Price':100},'source':source(CONTENT+'DealerRules.cs','Title = "Extra projectile"')}],'source':source(CONTENT+'DealerRules.cs','Title = "Extra projectile"'),'description':'Weapon-targeted dealer offer'})
for l in raw['Legendaries']:
    variants=[{'id':l['Id']+':'+str(r['Rank']),'name':l['Name'],'rank':r['Rank'],'stats':r['Stats'],'description':'Manual legendary · assembled from three permanent fragments.','image':img(RES+'Dealer/'+l['Art']+'-complete.png'),'source':source(CONTENT+'LegendaryRules.cs',l['Name'])} for r in l['Ranks']]
    weapons.append({'id':l['Id'],'name':l['Name'],'category':'Legendary','accent':'#e8be75','variants':variants,'source':source(CONTENT+'LegendaryRules.cs'),'description':'Separate manual slot · three ranks','icon':img(RES+'Dealer/'+l['Art']+'-complete.png')})
    for frag in l['Fragments']:
        families.append({'id':l['Id']+'-fragment-'+str(frag['Piece']),'name':frag['Title'],'category':'Fragments','accent':'#e8be75','variants':[{'id':l['Id']+'-fragment-'+str(frag['Piece']),'name':frag['Title'],'rank':frag['Piece']+1,'description':frag['Description'],'image':img(RES+'Dealer/'+frag['Art']+'.png'),'stats':{'Price':100},'source':source(CONTENT+'DealerRules.cs','private static readonly string[]')}],'source':source(CONTENT+'DealerRules.cs'),'description':l['Name']})
    for kind,title,desc in [('equip','Equip '+l['Name'],'Equip this legendary for this run. Replaces your current manual weapon.'),('upgrade','Upgrade '+l['Name'],'Upgrade your equipped legendary by one tier for this run.')]:
        families.append({'id':l['Id']+'-'+kind,'name':title,'category':'Dealer','accent':'#e8be75','variants':[{'id':l['Id']+'-'+kind,'name':l['Name'],'rank':1,'description':desc,'image':img(RES+'Dealer/'+l['Art']+'-complete.png'),'stats':{'Price':100},'source':source(CONTENT+'DealerRules.cs','private static DealerOffer Equipment')}],'source':source(CONTENT+'DealerRules.cs'),'description':'Assembled legendary required'})

enemies=[]
shared=defs['ContentCatalog.Enemies']; sharedbyid={e['Id']:e for e in shared}; tiers=raw['TierMultipliers']
def variant(e,ident,rank,image,stats=None,description=None,origin=None,white=None):
    return {'id':ident,'name':e['Name'],'rank':rank,'image':image,'whiteImage':white,'stats':stats or {k:v for k,v in e.items() if k not in ['Id','Name','Behavior','Color'] and v is not None},'description':description or e.get('Behavior',''),'source':source(origin or CONTENT+'ContentCatalog.Generated.cs',e['Id'])}
for e in shared:
    progressive=e['Id'] in raw['SharedTierFamilies']; vs=[]
    origin=CONTENT+('LegacyRestorationRules.cs' if e['Id'] in ['swarmer','shuriken','spiky'] else 'ContentCatalog.Generated.cs')
    for tier in range(1,5) if progressive else [1]:
        mult=tiers[tier-1]; stats={k:v for k,v in e.items() if k not in ['Id','Name','Color','Behavior'] and v is not None}
        for field,factor in [('Health','Health'),('Speed','Speed'),('ContactDamage','Damage'),('Radius','Radius'),('AttackCooldown','Cooldown'),('ProjectileSpeed','Projectile')]:
            if field in stats: stats[field]*=mult[factor]
        im=roster.get((e['Id'],tier,0)) or sprite('enemy|'+e['Id']+'|')
        vs.append(variant(e,e['Id']+':'+str(tier),tier,im,stats,origin=origin))
    enemies.append({'id':e['Id'],'name':e['Name'],'category':'Shared roster','group':'Shared arenas','accent':e['Color'],'variants':vs,'source':source(origin,e['Id']),'description':e['Behavior'],'maps':['abyss','red-nebula','white-sakura','eon-sea','crascendo']+(['monochrome-court'] if e['Id']=='chaser' else [])})
elite=defs['ContentCatalog.Elite']; elite['Behavior']='Standard charging elite'
enemies.append({'id':elite['Id'],'name':elite['Name'],'category':'Elites','group':'Shared arenas','accent':elite['Color'],'variants':[variant(elite,'elite',1,sprite('enemy|elite|'))],'source':source(CONTENT+'ContentCatalog.Generated.cs','EliteDefinition Elite'),'maps':['abyss','red-nebula','white-sakura','eon-sea','crascendo']})
for ev in raw['EliteVariants']:
    e=ev['Definition']; eid=e['BaseId']; vs=[]
    for tier in range(1,5):
        stats=dict(ev['Stats']); stats['Health']*=[1,1.45,2.9,4.35][tier-1];stats['ContactDamage']*=[1,1.12,1.42,1.65][tier-1];stats['Radius']*=1+(tier-1)*.05
        level=tier-1
        stats['Speed']*=((.92+.04*level)/.92 if eid=='mortar' else (1.05+.05*level)/1.05 if eid=='gunner' else [1.2,1.22,1.25,1.28][level]/1.2)
        vs.append({'id':'elite-'+eid+':'+str(tier),'name':e['Name'],'rank':tier,'image':roster.get((eid,tier,1)),'stats':stats,'description':'Specialist elite · tier stats before run scaling. Attack cadence also depends on the runtime controller.','source':source(CONTENT+'EliteRules.cs',e['Name']),'additionalSources':[source(RUNTIME+'VoidFallGameRuntime.RosterProgression.cs','EliteTierHealth')]})
    enemies.append({'id':'elite-'+eid,'name':e['Name'],'category':'Elites','group':'Shared arenas','accent':e['Accent'],'variants':vs,'source':source(CONTENT+'EliteRules.cs',e['Name']),'maps':['abyss','red-nebula','white-sakura','eon-sea','crascendo']})
court={}
for e in defs['MonochromeContent.Enemies']:
    original=e['Id']=='court-knight-original'; family=e['Id'].replace('court-','').replace('-elite','').replace('-iii','').replace('-ii','')
    rank=1 if original else 4 if e['Id'].endswith('-elite') else 3 if e['Id'].endswith('-iii') else 2 if e['Id'].endswith('-ii') else 1
    black=APPROVED+('court-knight-original-black.png' if original else f'court-{family}-{rank}-black.png'); white=black.replace('-black','-white')
    vs=variant(e,e['Id'],rank,img(black),origin=CONTENT+'ApprovedMapContent.cs',white=img(white))
    if family not in court: court[family]={'id':'court-'+family,'name':family.replace('-',' ').title(),'category':'Court','group':'Monochrome Court','accent':'#deded0','variants':[],'source':source(CONTENT+'ApprovedMapContent.cs'),'maps':['monochrome-court']}
    court[family]['variants'].append(vs)
enemies.extend(court.values())
healthrange=re.search(r'RookHealth\(.*?=>\s*([^;]+)',text('Assets/VoidFall/Core/MonochromeEncounterRules.cs'),re.S)
enemies.append({'id':'court-sentinel','name':'Sentinel','presentation':'variants','category':'Court','group':'Monochrome Court','accent':'#deded0','variants':[{'id':'sentinel-'+str(i),'name':'Sentinel','rank':i+1,'image':img(APPROVED+f'sentinel-{i}-black.png'),'whiteImage':img(APPROVED+f'sentinel-{i}-white.png'),'stats':{'Health':'100,000–150,000','Speed':0,'Radius':115},'description':'Three authored forms · 4 × 4 territory · alternating black/white cell strikes · 24-point allied shield.','source':source(RUNTIME+'VoidFallGameRuntime.CourtField.cs','SpawnCourtSentinel')} for i in range(3)],'source':source(RUNTIME+'VoidFallGameRuntime.CourtField.cs'),'maps':['monochrome-court']})
for e in defs['NullCityContent.Enemies']:
    index=defs['NullCityContent.Enemies'].index(e)-12
    im=img(APPROVED+f'city-{index}.png') if index>=0 else city.get(e['Id'])
    enemies.append({'id':e['Id'],'name':e['Name'],'category':'Null City','group':'Null City','accent':e['Color'],'variants':[variant(e,e['Id'],1,im,origin=CONTENT+'NullCityContent.cs')],'source':source(CONTENT+'NullCityContent.cs',e['Id']),'maps':['null-city']})
for e in defs['ApprovedMapContent.Enemies']:
    if not e['Id'].startswith('hydra-'): continue
    index=next((i for i,id in enumerate(['hydra-needlewasp','hydra-hookmantis','hydra-blisterbeetle','hydra-sawroach','hydra-mourningmoth']) if id==e['Id']),None)
    art=f'insect-{index}' if index is not None else 'hydra-hive' if e['Id']=='hydra-hive' else 'guardian-0' if e['Id']=='hydra-mantis-matriarch' else 'guardian-1'
    enemies.append({'id':e['Id'],'name':e['Name'],'category':'Hydra','group':'Hydra I','accent':e['Color'],'variants':[variant(e,e['Id'],1,img(APPROVED+art+'.png'),origin=CONTENT+'ApprovedMapContent.cs')],'source':source(CONTENT+'ApprovedMapContent.cs',e['Id']),'maps':['hydra']})
for h in raw['HydraPopulations']:
    e=sharedbyid[h['BaseId']]; desc=('Virus' if h['Virus'] else 'Hybrid')+' · '+(h['Parents'] or h['BaseId'])
    v=variant(e,h['Id'],1,img(APPROVED+f"hydra-legacy-{h['Kind']}.png"),description=desc,origin='Assets/VoidFall/Core/HydraPopulationRules.cs');v['name']=h['Name'];v['artNote']='Stats inherited from '+e['Name']+' before run scaling.'
    enemies.append({'id':h['Id'],'name':h['Name'],'category':'Hydra','group':'Hydra I','accent':'#b7db8e','variants':[v],'source':source('Assets/VoidFall/Core/HydraPopulationRules.cs',h['Name']),'maps':['hydra']})
for e in defs['DestroyerContent.Enemies']:
    im=img(RES+'Destroyers/'+e['Id'][10:]+'/idle0.png')
    enemies.append({'id':e['Id'],'name':e['Name'],'category':'Destroyers','group':'Major incident','accent':e['Color'],'variants':[variant(e,e['Id'],1,im,origin=CONTENT+'DestroyerContent.cs')],'source':source(CONTENT+'DestroyerContent.cs',e['Name']),'maps':['abyss','red-nebula','white-sakura','eon-sea','crascendo']})
bosses=[]
for key in ['ContentCatalog.Bosses','HydraContent.Boss','MonochromeContent.BlackBoss','MonochromeContent.WhiteBoss','NullCityContent.Motherload']:
    entries=defs[key] if isinstance(defs[key],list) else [defs[key]]
    for e in entries:
        group='Hydra II' if key.startswith('Hydra') else 'Monochrome Court' if key.startswith('Monochrome') else 'Null City' if key.startswith('NullCity') else 'Shared pool'
        maps=['hydra'] if group=='Hydra II' else ['monochrome-court'] if group=='Monochrome Court' else ['null-city'] if group=='Null City' else ['abyss','red-nebula','white-sakura','eon-sea','crascendo']
        im=img(RES+'Hydra/HydraPrime.png') if e['Id']=='hydra-prime' else city.get(e['Id']) if e['Id']=='null-motherload' else sprite('boss|'+e['Id']+'|')
        if e['Id'].startswith('court-grandmaster'): im=img(APPROVED+('court-grandmaster-black.png' if e['Id'].endswith('black') else 'court-grandmaster-white.png')) or im
        origin=CONTENT+key.split('.')[0]+'.cs' if key.split('.')[0]!='ContentCatalog' else CONTENT+'ContentCatalog.Generated.cs'
        f={'id':e['Id'],'name':e['Name'],'category':'Bosses','group':group,'accent':e['Color'],'variants':[variant(e,e['Id'],1,im,description='Arena-exclusive' if group!='Shared pool' else 'Shared boss pool',origin=origin)],'source':source(origin,e['Id']),'maps':maps,'attacks':e['Attacks']}
        bosses.append(f);enemies.append(f)
arenas=[]
for key in ['ContentCatalog.Arenas','HydraContent.Arena','MonochromeContent.Arena','NullCityContent.Arena','EonSeaContent.Arena','CrascendoContent.Arena']:
    for a in defs[key] if isinstance(defs[key],list) else [defs[key]]:
        aid={'void':'abyss','redNebula':'red-nebula','whiteSakura':'white-sakura'}.get(a['Id'],a['Id'])
        # Match prepared stable identities by the actual enum for generated camel-case IDs.
        if aid not in [x['Id'] for x in raw['PreparedArenas']]:
            aid=next((x['Id'] for x in raw['PreparedArenas'] if x['Enum'].lower()==a['Id'].lower()),aid)
        origin=CONTENT+key.split('.')[0]+'.cs' if key.split('.')[0]!='ContentCatalog' else CONTENT+'ContentCatalog.Generated.cs'
        arenas.append({**a,'id':aid,'name':a['Name'],'image':img(RES+'RouteThumbnails/'+aid+'.png'),'source':source(origin,a['Id']),'enemies':[e['id'] for e in enemies if aid in e['maps']]})

missing=[]
for f in weapons+enemies:
    for v in f['variants']:
        if not v.get('image') and f['id']!='arc': missing.append({'kind':f['category'],'id':v['id'],'name':f['name']})
counts={'cardFamilies':len(families),'cards':sum(len(f['variants']) for f in families),'weapons':len(weapons),'automaticWeapons':len(raw['Weapons']),'supports':len(raw['Supports']),'evolutions':len(raw['Evolutions']),'enemyFamilies':len(enemies)-len(bosses),'enemyForms':sum(len(e['variants']) for e in enemies if e['category']!='Bosses'),'bosses':len(bosses),'maps':len(arenas),'images':sum(a['type']!='TTF' for a in assets)}
data={'meta':{'generatedAt':datetime.now(timezone.utc).isoformat(),'projectRoot':'..','revision':subprocess.check_output(['git','rev-parse','--short','HEAD'],cwd=ROOT).decode().strip(),'readOnly':True,'counts':counts,'missingArt':missing,'statNote':'Authored base stats and listed tier multipliers. Actual spawns also apply elapsed-time, director, arena and child modifiers.','cardArtNote':'Card layout is a dashboard presentation. Artwork comes from the bundled HUD or live attack sprites; the level-up view itself uses a circle/diamond glyph.','deckNote':'No named deck catalog exists in the live code. Decks below are the implemented offer pools and catalog groups.'},'cards':families,'weapons':weapons,'enemies':enemies,'maps':arenas,'assets':assets,'forms':raw['Forms'],'spriteKeys':sprites}
(OUT/'content.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'content-data.js').write_text('window.VOIDFALL_CONTENT = '+json.dumps(data,ensure_ascii=False)+';\n',encoding='utf-8')
(BASE/'inventory-report.json').write_text(json.dumps({'counts':counts,'missingArt':missing,'assetHashes':{a['id']:a['sha256'] for a in assets}},indent=2),encoding='utf-8')
print(json.dumps(counts));print('Missing entity artwork:',json.dumps(missing))

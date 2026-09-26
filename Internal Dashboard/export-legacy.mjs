/** Execute the five original browser catalogs and Canvas renderers in isolation.
 * Source trees are read-only. Only this tool's dist/legacy files are written.
 * Node 24 strips TypeScript; @napi-rs/canvas executes the authored Canvas paths.
 */
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import {createRequire,stripTypeScriptTypes} from 'node:module';
import {fileURLToPath} from 'node:url';
import os from 'node:os';
import crypto from 'node:crypto';
const base=path.dirname(fileURLToPath(import.meta.url));
const legacyRoot=path.resolve(process.argv[2]||process.env.VOIDFALL_LEGACY_ROOT||path.join(os.homedir(),'Desktop','legacy voidfall'));
const require=createRequire(process.env.VOIDFALL_CANVAS_MODULE_ROOT||import.meta.url);
let canvasModule;
try{canvasModule=require('@napi-rs/canvas');}catch(error){
 const bundled=path.join(os.homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas');
 if(fs.existsSync(bundled))canvasModule=require(bundled);else throw new Error('Legacy refresh needs @napi-rs/canvas. Run npm install inside Internal Dashboard, or set VOIDFALL_CANVAS_MODULE_ROOT to an installed package root.');
}
const {createCanvas,GlobalFonts}=canvasModule;
const legacyFont=path.join(base,'dist/assets/Resources/VoidFall/ApprovedHud/ChakraPetch-Bold.ttf');
if(fs.existsSync(legacyFont))GlobalFonts.registerFromPath(legacyFont,'Chakra Petch');
const out=path.join(base,'dist','legacy');fs.mkdirSync(out,{recursive:true});
const hash=x=>crypto.createHash('sha256').update(x).digest('hex');
const bundles=[];const files=[];const assetIndex=new Map();
function writeAsset(canvas,version,key,source){
 const bytes=canvas.toBuffer('image/png'),sha=hash(bytes),url='legacy/art/'+sha+'.png';
 if(!assetIndex.has(sha)){fs.mkdirSync(path.join(out,'art'),{recursive:true});fs.writeFileSync(path.join(base,'dist',url),bytes);assetIndex.set(sha,{id:sha,image:url,width:canvas.width,height:canvas.height,uses:[]});}
 assetIndex.get(sha).uses.push({version,key,source});return url;
}
function walk(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>['node_modules','.git','dist','.vite'].includes(e.name)?[]:e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);}
for(let number=1;number<=5;number++){
 const version='v'+number,root=path.join(legacyRoot,'Voidfall '+version),game=path.join(root,'src','game'),cache=new Map();
 if(!fs.existsSync(root))throw new Error('Missing legacy version: '+root);
 const source=(file,needle='')=>{const code=fs.readFileSync(path.join(game,file),'utf8');return {version,path:'src/game/'+file,line:Math.max(1,code.slice(0,Math.max(0,code.indexOf(needle))).split('\n').length),url:'legacy/source/'+version+'/src/game/'+file};};
 for(const file of walk(root)){
  const rel=path.relative(root,file).split(path.sep).join('/'),bytes=fs.readFileSync(file);
  const url='legacy/source/'+version+'/'+rel;
  files.push({version,path:rel,bytes:bytes.length,sha256:hash(bytes),url});
  // Preserve the original evidence with the portable dashboard, not the runtime dependencies.
  {const dest=path.join(base,'dist',url);fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest,bytes);}
 }
 let rng=431+number;const math=Object.create(Math);math.random=()=>((rng=(Math.imul(rng,1664525)+1013904223)>>>0)/4294967296);
 function canvas(){const c=createCanvas(1,1);c.addEventListener=()=>{};c.removeEventListener=()=>{};c.style={};return c;}
 const context={console,Math:math,performance:{now:()=>0},document:{createElement:canvas,addEventListener(){},removeEventListener(){},hidden:false},window:{innerWidth:640,innerHeight:360,devicePixelRatio:1,addEventListener(){},removeEventListener(){}},localStorage:{getItem:()=>null,setItem(){}},requestAnimationFrame:()=>0,cancelAnimationFrame(){},setTimeout:()=>0,clearTimeout(){},btoa:s=>Buffer.from(s).toString('base64'),atob:s=>Buffer.from(s,'base64').toString(),Date};
 function load(name){
  if(cache.has(name))return cache.get(name);
  const filename=path.join(game,name+'.ts');if(!fs.existsSync(filename))return {};
  let code=fs.readFileSync(filename,'utf8');const dependencies={};
  code=code.replace(/import\s+([\s\S]*?)\s+from\s+["']([^"']+)["'];?/g,(all,binding,from)=>{
   if(binding.trim().startsWith('type '))return '';
   const names=binding.replace(/[{}]/g,'').split(',').map(s=>s.trim()).filter(s=>s&&!s.startsWith('type '));
   const dep=from.startsWith('./')?load(from.slice(2)):{};
   for(const n of names){const [original,alias]=n.split(/\s+as\s+/);dependencies[alias||original]=dep[original]||original;}
   return '';
  });
  const exports=[...code.matchAll(/export\s+(?:async\s+)?(?:const|function|class)\s+(\w+)/g)].map(m=>m[1]);
  if(name==='engine')exports.push(number===1?'ENEMY_DEFS':'ARENA');
  code=stripTypeScriptTypes(code,{mode:'strip',sourceUrl:filename}).replace(/\bexport\s+(?=(?:const|function|class))/g,'');
  const result=vm.runInNewContext('(function(){'+code+'\nreturn {'+exports.join(',')+'};})()',Object.assign({},context,dependencies),{filename,timeout:10000});cache.set(name,result);return result;
 }
 const spriteModule=load('sprites'),sprites=spriteModule.makeSprites();const images={};
 function flatten(value,prefix=''){
  for(const [key,v] of Object.entries(value)){const id=prefix?prefix+'.'+key:key;if(v&&typeof v.toBuffer==='function')images[id]=writeAsset(v,version,id,source('sprites.ts',key));else if(v&&typeof v==='object')flatten(v,id);}
 }
 flatten(sprites);
 const weaponModule=load('weapons'),weapons=(weaponModule.WEAPONS||[]).map(w=>({...w,source:source('weapons.ts','id: "'+w.id+'"'),ranks:Array.from({length:8},(_,i)=>({rank:i+1,stats:weaponModule.weaponStats(w.id,i+1,false)}))}));
 const cards=(load('passives').PASSIVES||[]).map(p=>({...p,description:undefined,source:source('passives.ts','id: "'+p.id+'"'),ranks:Array.from({length:p.max},(_,i)=>({rank:i+1,description:p.description(i+1)}))}));
 const upgrades=(load('upgrades').UPGRADES||[]).map(u=>({...u,desc:undefined,source:source('upgrades.ts','id: "'+u.id+'"'),ranks:Array.from({length:u.max},(_,i)=>({rank:i+1,description:u.desc(i+1)}))}));
 const forms=(load('forms').FORMS||[]).map(f=>({...f,source:source('forms.ts','id: "'+f.id+'"')}));
 const characters=(load('characters').CHARACTERS||[]).map(c=>({...c,source:source('characters.ts','id: "'+c.id+'"')}));
 const engineModule=load('engine');
 const enemies=Object.entries(number===1?engineModule.ENEMY_DEFS:load('enemies').ENEMIES).map(([id,e])=>({id,...e,source:source(number===1?'engine.ts':'enemies.ts',id+':')}));
 const engineCode=fs.readFileSync(path.join(game,'engine.ts'),'utf8');
 const bosses=[...engineCode.matchAll(/\{id:"(\w+)",nm:"([^"]+)",c:"([^"]+)",hp:(\d+),spd:(\d+),dm:(\d+)\}/g)].map(m=>({id:m[1],name:m[2],color:m[3],hp:+m[4],speed:+m[5],dmg:+m[6],source:source('engine.ts',m[0])}));
 const audioCode=fs.readFileSync(path.join(game,'audio.ts'),'utf8');
 const sfx=[...audioCode.matchAll(/^  (\w+)\([^\n]*?\)\s*\{/gm)].filter(m=>!['constructor','init','setMuted','dispose'].includes(m[1])).map(m=>({id:m[1],source:source('audio.ts',m[0]),body:audioCode.slice(m.index,m.index+1500).split(/^  \w+\(/m)[0]}));
 const saveCode=fs.existsSync(path.join(game,'save.ts'))?fs.readFileSync(path.join(game,'save.ts'),'utf8'):'';
 const challenges=[...saveCode.matchAll(/\{ name: "([^"]+)", desc: "([^"]+)", modifiers: \[([^\]]*)\], rewardMult: ([\d.]+) \}/g)].map(m=>({id:m[1],name:m[1],description:m[2],modifiers:[...m[3].matchAll(/"([^"]+)"/g)].map(x=>x[1]),rewardMult:+m[4],source:source('save.ts',m[0])}));
 const typeCode=fs.readFileSync(path.join(game,'types.ts'),'utf8');const eliteMatch=typeCode.match(/export type EliteMod\s*=([^;]+);/);
 const eliteMods=eliteMatch?[...eliteMatch[1].matchAll(/"([^"]+)"/g)].map(m=>({id:m[1],source:source('types.ts','export type EliteMod')})):[];
 const elitePool=engineCode.match(/(?:private )?rollEM\(\)\{const all=\[([^\]]*)\]/);
 const rolledMods=elitePool?[...elitePool[1].matchAll(/"([^"]+)"/g)].map(m=>m[1]):[];
 for(const e of eliteMods)e.availability=rolledMods.includes(e.id)?'Included in this version’s rollEM pool.':'Declared in the type; not in this version’s rollEM pool.';
 const eventArray=engineCode.match(/const events=\[([^\]]*)\]/);
 const eventsList=eventArray?[...eventArray[1].matchAll(/"([^"]+)"/g)].map(m=>({id:m[1],name:m[1].replace(/([a-z])([A-Z])/g,'$1 $2'),source:source('engine.ts','if(this.eventKind==="'+m[1]+'")'),description:'Authored rotating micro-event. The source excerpt shows its exact spawning, reward and pressure rules.'})):[];
 const arenas=Object.entries(engineModule.ARENA||{}).map(([id,a])=>({id,...a,source:source('engine.ts',id+':')}));
 // Exact renderer, paused at a fixed time with no update loop, profile I/O or UI.
 const events=new Proxy({}, {get:()=>()=>{}});const gameCanvas=canvas();const instance=new engineModule.Game(gameCanvas,events);
 instance.aT=0;instance.time=0;instance.trauma=0;
 if(number===1){instance.player.alive=false;instance.render();const origin=source('engine.ts','private buildGradients');arenas.push({id:'v1-unnamed',name:'v1 unnamed arena',source:origin,image:writeAsset(gameCanvas,version,'arena.unnamed',origin),artNote:'The original v1 backdrop has no named arena catalog. Original engine still with player hidden.'});}
 for(const a of arenas){
  if(number===1)continue;
  instance.arenaId=a.id;instance.arenaM=engineModule.ARENA[a.id];instance.buildGrads();instance.phase='menu';instance.palive=false;instance.render();
  a.image=writeAsset(gameCanvas,version,'arena.'+a.id,a.source);a.artNote='Original engine render at time 0, with player hidden; seeded cosmetic dust. No gameplay simulation.';
 }
 for(const b of bosses){
  // Intercept draw calls into the world layer so the authored boss path stays untouched.
  instance.arenaId='neon';instance.arenaM=engineModule.ARENA.neon;instance.phase='menu';instance.palive=false;instance.bgC=null;instance.nebs=[];instance.dust=[];instance.vig=null;instance.rVig=null;
  instance.cam={x:0,y:0};instance.px=0;instance.py=0;instance.ens=[];instance.boss={...b,x:0,y:0,r:65,maxHp:b.hp,dmg:b.dmg,color:b.color,phase:0,age:0,hit:0,introT:0,dead:false,deathT:0,atkT:0,moveT:0,pattern:'chase',patternT:0,enrage:0,projs:[]};instance.render();
  b.image=writeAsset(gameCanvas,version,'boss.'+b.id,b.source);b.artNote='Original engine boss render at phase 0, including the authored boss HUD. Fixed still, not a full animation.';instance.boss=null;
 }
 // Export line/particle attacks through their original engine draw loop too.
 if(number>1){
  const start=engineCode.indexOf('for(let i=0;i<this.buls.length;i++)',engineCode.indexOf('private render()'));
  const end=engineCode.indexOf('ctx.globalCompositeOperation="lighter"',start);
  if(start<0||end<0)throw new Error('Original projectile draw loop missing in '+version);
  const draw=vm.runInNewContext('(function(ctx){'+engineCode.slice(start,end)+'})',{...context,TAU:Math.PI*2});
  for(const id of ['arc','flame','chain'].filter(id=>weapons.some(w=>w.id===id))){
   const c=createCanvas(72,48),ctx=c.getContext('2d');ctx.translate(36,24);
   instance.buls=[{x:0,y:0,vx:1,vy:0,weapon:id}];draw.call(instance,ctx);instance.buls=[];
   images['engine-projectile.'+id]=writeAsset(c,version,'engine-projectile.'+id,source('engine.ts','if(b.weapon==="arc")'));
  }
 }
 bundles.push({version,number,files:files.filter(f=>f.version===version).length,weapons,cards,upgrades,forms,characters,enemies,bosses,arenas,challenges,eliteMods,events:eventsList,sfx,images});
 console.log(version+': '+enemies.length+' enemies, '+weapons.length+' weapons, '+Object.keys(images).length+' sprite renders');
}
const result={generatedAt:new Date().toISOString(),versions:bundles,files,assets:[...assetIndex.values()],legacyRootLabel:'Desktop/legacy voidfall',renderer:'Original TypeScript Canvas code executed with Node 24 and @napi-rs/canvas. No invented art.'};
// Only remove obsolete, reproducible hash-named previews in this tool's own art folder.
for(const file of fs.readdirSync(path.join(out,'art'))){
 if(/^[a-f0-9]{64}\.png$/.test(file)&&!assetIndex.has(file.slice(0,-4)))fs.unlinkSync(path.join(out,'art',file));
}
fs.writeFileSync(path.join(base,'legacy-raw.json'),JSON.stringify(result,null,2));
console.log('Saved '+result.assets.length+' unique native legacy renders and '+files.length+' file records.');

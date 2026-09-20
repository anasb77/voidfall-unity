import {CourtSimulation} from './court-sim.js';
import {drawCourt} from './court-art.js';
import {BOARD_HALF} from './court-rules.js';
import {registerCityRoster,chooseCityAmbient,updateCityAddition,drawCityAddition} from './city-roster.js';
import {buildCity,drawCity,cityWorld,CITY_SCALE} from './city.js';
import {drawHydraFloor,drawLegacy} from './hydra-art.js';
import {drawInsect,drawHive,INSECTS} from './insects.js';
import {broodMember,armExploder,stepExploder,EXPLOSION_RADIUS,INSECT_SCALE} from './insect-rules.js';
import {createHives,stepBrood,damageHive} from './brood.js';
const TAU=Math.PI*2,clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
export function createStudy(canvas,onStats){
 const g=canvas.getContext('2d',{alpha:false}),art=window.NullCityArt,images={};
 for(const [key,src] of Object.entries({zack:'zack.png',hydra:'hydra-base.png',transit:'transit.png'})){images[key]=new Image();images[key].src='/assets/'+src}
 let floorStyle='subtle',cityBg=buildCity();const reducedMotion=matchMedia('(prefers-reduced-motion: reduce)');
 registerCityRoster(window.NullCitySim);
 const city=new window.NullCitySim.Simulation(),keys=new Set(),taps=new Set();
 city.roster=window.NullCitySim.ROSTER;city.chooseAmbient=chooseCityAmbient;city.updateAddition=(m,dt)=>updateCityAddition(city,m,dt);
 const originalSpawn=city.spawn.bind(city);city.spawn=(type,x,y,options={})=>{if(options.fromHangar){x=800+(x-800)/CITY_SCALE;y=800+(y-800)/CITY_SCALE}const m=originalSpawn(type,x,y,options);m.r/=CITY_SCALE;return m};
 const originalHazard=city.hazard.bind(city);city.hazard=()=>{const h=originalHazard();if(!h)return null;if(h.lane>=2){h.x+=h.w*(1-1/CITY_SCALE)/2;h.w/=CITY_SCALE}else{h.y+=h.h*(1-1/CITY_SCALE)/2;h.h/=CITY_SCALE}return h};
 const court=new CourtSimulation();let gallery=false;
 let mode='court',overview=false,width=1,height=1,dpr=1,viewHeight=800,targetHeight=800,last=performance.now(),raf=0,dead=false,time=0,demo=false,crowd=true,statClock=0,shotClock=0,ambientClock=0;
 let player,camera,enemies=[],shots=[],sparks=[],hives=[],rings=[],manual=false,pointer={x:0,y:0},focusHive=false,spawned=0,kills=0,guardianCount=0,notice='',noticeTime=0,travel=0;
 const legacyNames=['Cleft','Hook','Rachis','Bloat','Graft','Bastion','Aegis','Riftkin','Reclaimer','Broodsmith'];
 function resize(){width=innerWidth;height=innerHeight;dpr=Math.max(devicePixelRatio||1,Math.min(3840/width,2160/height));canvas.width=Math.round(width*dpr);canvas.height=Math.round(height*dpr)}resize();window.addEventListener('resize',resize);
 function reset(){
  court.reset();
  player={x:0,y:0,vx:1,vy:0,hp:100,invuln:2,dash:0,cooldown:0};camera={x:0,y:0};enemies=[];shots=[];sparks=[];rings=[];hives=createHives();time=0;shotClock=0;ambientClock=0;spawned=0;kills=0;guardianCount=0;focusHive=false;demo=false;manual=false;travel=0;keys.clear();taps.clear();notice='';noticeTime=0;
  city.start();city.player.r=10;city.spawn(5,1100,310);city.spawn(8,500,340);
  // Replace five starter Crawlers with the new pursuers, keeping the opening population stable.
  let replaced=0;city.mobs=city.mobs.filter(m=>!(m.type===3&&replaced++<5));
  for(let i=0;i<5;i++){const a=i*Math.PI*2/5-.6;city.spawn(18+i,760+Math.cos(a)*225,472+Math.sin(a)*185);city.spawnAmbient(13+i)}
  if(mode==='city'){Object.assign(player,cityWorld(city.player.x,city.player.y));camera={x:player.x,y:player.y}}
  if(mode==='court')player=court.player;
  for(let i=0;i<10;i++){const a=i*TAU/10;enemies.push(makeEnemy(Math.cos(a)*560,Math.sin(a)*560,{type:i,legacy:true}))}
  syncStats();
 }
 function makeEnemy(x,y,options={}){const kind=options.kind||0,boss=!!options.guardian,type=options.type||0;return {x,y,kind,type,legacy:!!options.legacy,guardian:boss,name:boss?['MANTIS MATRIARCH','IRON CARAPACE'][options.guardianKind||0]:options.legacy?legacyNames[type]:INSECTS[kind],r:boss?62:options.legacy?24:24*INSECT_SCALE,hp:boss?480:options.legacy?38:24,maxHp:boss?480:options.legacy?38:24,speed:boss?61:options.legacy?68:85+(kind%3)*17,angle:0,phase:Math.random()*TAU,hit:0,attack:2.5+Math.random(),wind:0,charge:0,aim:0,guardianKind:options.guardianKind||0}}
 function key(code,down){if(overview||gallery)return;if(down){keys.add(code);taps.add(code);demo=false}else keys.delete(code)}
 const codes=['KeyW','KeyA','KeyS','KeyD','ArrowUp','ArrowDown','ArrowLeft','ArrowRight','Space','KeyE'];
 const eventCode=e=>e.code||({w:'KeyW',a:'KeyA',s:'KeyS',d:'KeyD',e:'KeyE',' ':'Space'}[e.key?.toLowerCase()]||e.key);
 function keyDown(e){const code=eventCode(e);if(!codes.includes(code)||e.target instanceof HTMLInputElement||e.target instanceof HTMLSelectElement||e.target instanceof HTMLButtonElement)return;e.preventDefault();if(code==='KeyE'){if(!e.repeat&&mode==='hydra')focusHive=!focusHive;if(!e.repeat&&mode==='court')court.focusQueen=!court.focusQueen;court.focusSentinel=false;return}key(code,true)}
 function keyUp(e){keys.delete(eventCode(e))}function blur(){keys.clear();taps.clear();manual=false}
 function pointerMove(e){const rect=canvas.getBoundingClientRect();pointer={x:e.clientX-rect.left,y:e.clientY-rect.top}}
 function pointerDown(e){if(overview||gallery||e.pointerType==='touch')return;pointerMove(e);manual=true;canvas.setPointerCapture(e.pointerId);canvas.focus();demo=false}function pointerUp(){manual=false}
 window.addEventListener('keydown',keyDown);window.addEventListener('keyup',keyUp);window.addEventListener('blur',blur);canvas.addEventListener('pointermove',pointerMove);canvas.addEventListener('pointerdown',pointerDown);canvas.addEventListener('pointerup',pointerUp);canvas.addEventListener('pointercancel',pointerUp);
 function burst(x,y,color,n=10){for(let i=0;i<n;i++){const a=Math.random()*TAU,v=35+Math.random()*150;sparks.push({x,y,vx:Math.cos(a)*v,vy:Math.sin(a)*v,life:.35+Math.random()*.4,color})}}
 function hitPlayer(damage){if(player.invuln>0||player.dash>0)return;player.hp-=damage;player.invuln=.65;burst(player.x,player.y,'#80d8ff',8);if(player.hp<=0){player.hp=100;player.invuln=3;player.x=0;player.y=0;notice='ZACK RECOVERED · THE HIVES REMAIN';noticeTime=3}}
 function releaseGuardian(h){const kinds=[1,2];enemies.push(makeEnemy(h.x,h.y,{kind:kinds[h.id%2],guardian:true,guardianKind:h.id%2}));guardianCount++;burst(h.x,h.y,'#c7ce96',40);rings.push({x:h.x,y:h.y,r:110,life:1,max:1,color:'#bec68c'});notice=['MANTIS MATRIARCH AWAKENS','IRON CARAPACE AWAKENS'][h.id%2];noticeTime=4;focusHive=false}
 function damageEnemy(e,amount){
  if(e.hp<=0||e.exploded)return;e.hp-=amount;e.hit=.1;
  if(e.hp>0)return;
  if(armExploder(e,.45)){e.hp=1;return}
  kills++;burst(e.x,e.y,'#b9c586',e.guardian?40:10);if(e.guardian){notice=e.name+' DEFEATED';noticeTime=3}
 }
 function explodeInsect(e){
  kills++;burst(e.x,e.y,'#e3ba78',30);rings.push({x:e.x,y:e.y,r:EXPLOSION_RADIUS,life:.6,max:.6,color:'#e3ba78'});
  if(Math.hypot(player.x-e.x,player.y-e.y)<EXPLOSION_RADIUS)hitPlayer(18);
  for(const other of enemies)if(other!==e&&Math.hypot(other.x-e.x,other.y-e.y)<EXPLOSION_RADIUS)damageEnemy(other,24);
 }
 function nearestHive(){let found=null,d=Infinity;for(const h of hives)if(!h.dead){const dist=Math.hypot(h.x-player.x,h.y-player.y);if(dist<d){found=h;d=dist}}return {h:found,d}}
 function updateCity(dt,mx,my,dash){
  if(dash)city.dash();
  if(crowd)city.step(dt,{x:mx,y:my});
  else{city.player.x=clamp(city.player.x+mx*250/CITY_SCALE*dt,180,1420);city.player.y=clamp(city.player.y+my*250/CITY_SCALE*dt,220,746)}
  Object.assign(player,cityWorld(city.player.x,city.player.y));player.hp=city.player.hp;player.dash=city.player.dash;player.cooldown=city.player.dashCD;
  for(const event of city.events){if(['death','explosion','stomp','hit','hatch','dash','repair','bomb'].includes(event.kind)){const p=cityWorld(event.x,event.y);burst(p.x,p.y,event.color,event.kind==='death'?15:6);if(event.r)rings.push({...p,r:event.r,life:.55,max:.55,color:event.color})}}city.events=[];
 }
 function updateHydra(dt,mx,my,dash){
  player.invuln=Math.max(0,player.invuln-dt);player.cooldown=Math.max(0,player.cooldown-dt);player.dash=Math.max(0,player.dash-dt);if(mx||my){player.vx=mx;player.vy=my}if(dash&&player.cooldown<=0){player.dash=.16;player.cooldown=1.1}
  if(player.dash>0){mx=player.vx;my=player.vy}const speed=player.dash>0?920:250;player.x+=mx*speed*dt;player.y+=my*speed*dt;
  if(!crowd)return;
  stepBrood(hives,dt,(h,i,wave)=>{const a=h.angle+i*TAU/5;enemies.push(makeEnemy(h.x+Math.cos(a)*99,h.y+Math.sin(a)*99,broodMember(h.id,wave,i)));spawned++;if(i===0)burst(h.x,h.y,'#93ab71',10)});
  ambientClock-=dt;if(ambientClock<=0){ambientClock=4;const a=Math.random()*TAU;enemies.push(makeEnemy(player.x+Math.cos(a)*650,player.y+Math.sin(a)*650,{type:Math.floor(Math.random()*10),legacy:true}))}
  // Spatial buckets keep separation local as the hive population grows.
  const cells=new Map();for(const e of enemies){const k=`${Math.floor(e.x/100)},${Math.floor(e.y/100)}`;if(!cells.has(k))cells.set(k,[]);cells.get(k).push(e)}
  for(const e of enemies){if(e.hp<=0)continue;e.hit=Math.max(0,e.hit-dt);const dx=player.x-e.x,dy=player.y-e.y,d=Math.hypot(dx,dy)||1;e.angle=Math.atan2(dy,dx);e.attack-=dt;
   if(!e.legacy&&!e.guardian&&e.kind===2){if(d<105)armExploder(e);if(stepExploder(e,dt,explodeInsect))continue}
   if(e.guardian){if(e.wind>0){e.wind-=dt;e.angle=e.aim;if(e.wind<=0){if(e.guardianKind===0)e.charge=.52;else if(e.guardianKind===1){rings.push({x:e.x,y:e.y,r:180,life:.7,max:.7,color:'#d4be83'});if(d<180)hitPlayer(30)}else for(let i=-2;i<=2;i++){const a=e.aim+i*.18;shots.push({x:e.x,y:e.y,vx:Math.cos(a)*235,vy:Math.sin(a)*235,life:3,enemy:true})}}}else if(e.attack<=0&&d<500){e.attack=4.3;e.wind=1;e.aim=e.angle}}
   if(!e.legacy&&!e.guardian&&e.kind===0&&e.attack<=0&&d<430){e.attack=3.8;shots.push({x:e.x,y:e.y,vx:dx/d*185,vy:dy/d*185,life:2.8,enemy:true})}
   let vx=dx/d*e.speed,vy=dy/d*e.speed;if(e.wind>0||e.fuse!=null){vx=vy=0}if(e.charge>0){e.charge-=dt;vx=Math.cos(e.aim)*590;vy=Math.sin(e.aim)*590;e.angle=e.aim}
   const cx=Math.floor(e.x/100),cy=Math.floor(e.y/100);for(let ix=cx-1;ix<=cx+1;ix++)for(let iy=cy-1;iy<=cy+1;iy++)for(const o of cells.get(`${ix},${iy}`)||[]){if(o===e)continue;const ox=e.x-o.x,oy=e.y-o.y,od=Math.hypot(ox,oy)||1,rr=e.r+o.r;if(od<rr){vx+=ox/od*(rr-od)*2;vy+=oy/od*(rr-od)*2}}
   if(d>e.r+16||e.charge>0){e.x+=vx*dt;e.y+=vy*dt}if(d<=e.r+20&&e.fuse==null)hitPlayer(e.guardian?24:8);
  }
  shotClock-=dt;if(shotClock<=0){let target=null;const nh=nearestHive();if(manual){const s=height/viewHeight;target={x:camera.x+(pointer.x-width/2)/s,y:camera.y+(pointer.y-height/2)/s}}else if(focusHive&&nh.h&&nh.d<700)target=nh.h;else{let range=520;for(const e of enemies){const d=Math.hypot(e.x-player.x,e.y-player.y);if(e.hp>0&&d<range){range=d;target=e}}if(!target&&nh.h&&nh.d<520)target=nh.h}if(target){const a=Math.atan2(target.y-player.y,target.x-player.x);shots.push({x:player.x,y:player.y,vx:Math.cos(a)*850,vy:Math.sin(a)*850,life:1,enemy:false});shotClock=.15}}
  for(const b of shots){b.x+=b.vx*dt;b.y+=b.vy*dt;b.life-=dt;if(b.enemy){if(Math.hypot(b.x-player.x,b.y-player.y)<20){hitPlayer(9);b.life=0}continue}
   for(const h of hives)if(!h.dead&&Math.hypot(b.x-h.x,b.y-h.y)<64){damageHive(h,12,releaseGuardian);b.life=0;burst(b.x,b.y,'#a9b984',3);break}
   if(b.life<=0)continue;for(const e of enemies)if(e.hp>0&&Math.hypot(b.x-e.x,b.y-e.y)<e.r){damageEnemy(e,12);b.life=0;burst(b.x,b.y,'#b9c586',3);break}
  }
  enemies=enemies.filter(e=>e.hp>0);shots=shots.filter(b=>b.life>0);
 }
 function syncStats(){const nh=nearestHive();canvas.dataset.courtPhase=mode==='court'?court.phase.stage||'rest':'none';canvas.dataset.knightState=mode==='court'?(court.enemies.find(e=>e.type===3)?.state||'none'):'none';onStats({mode,floorStyle,courtTier:court.tierMode,courtWave:court.wave,courtRank:court.tierMode==='auto'?(court.time<35?'I':court.time<80?'I / II':'I / II / III'):['I','II','III','III + ELITE'][court.tierMode],courtPhase:court.phase.stage,courtFocus:court.focusQueen,courtSentinelFocus:court.focusSentinel,courtTime:Math.floor(court.time),courtNotice:court.noticeTime>0?court.notice:'',district:mode==='city'?(city.phase?'LOCKDOWN / POLICE DEPLOYED':'SURVEILLANCE / ORIGINAL CITY'):'INSECT TERRITORY',phase:city.phase,purge:city.hazard()?.fire?1:city.hazard()?4:0,resolution:`${canvas.width} × ${canvas.height}`,demo,crowd,focusHive,health:Math.ceil(player.hp),hives:hives.filter(h=>!h.dead).length,brood:spawned,guardians:enemies.filter(e=>e.guardian).length,kills,notice:noticeTime>0?notice:'',nearestHive:nh.h?Math.round(nh.d):null});canvas.dataset.world=JSON.stringify({mode,player:{x:player.x,y:player.y,hp:player.hp},camera:{...camera},viewHeight,enemies:mode==='city'?city.mobs.length:mode==='court'?court.enemies.length:enemies.length,travel,court:mode==='court'?{time:court.time,tierMode:court.tierMode,forms:[...new Set(court.enemies.map(e=>`${e.type}:${e.tier}`))],knights:court.enemies.filter(e=>e.type===3).map(e=>({x:e.x,y:e.y,state:e.state,path:e.path,leg:e.leg})),promotions:court.promotions,interruptions:court.interruptions,knightDashes:court.knightDashes,shotsBlocked:court.shotsBlocked,floorKills:court.floorKills,armorBlocks:court.armorBlocks,shieldAbsorbed:court.shieldAbsorbed,shielded:court.enemies.filter(e=>e.sentinelShield>0).length,territoryCells:court.cells.length,covers:court.covers.map(c=>({x:c.x,y:c.y,hp:c.hp,dead:c.dead,mouth:c.mouth,phase:c.phase})),phase:court.phase}:null,cityRoster:[...new Set(city.mobs.filter(m=>!m.dead).map(m=>m.type))],hives:hives.map(h=>({id:h.id,hp:h.hp,clock:h.clock,waves:h.waves,dead:h.dead})),spawned,guardianCount,roster:{original:enemies.filter(e=>e.legacy).length,insects:enemies.filter(e=>!e.legacy&&!e.guardian).length,forms:[...new Set(enemies.filter(e=>!e.legacy&&!e.guardian).map(e=>e.kind))],armed:enemies.filter(e=>e.fuse!=null).length},floorStyle})}
 function update(dt){time+=dt;noticeTime=Math.max(0,noticeTime-dt);viewHeight+=(targetHeight-viewHeight)*(1-Math.exp(-dt*7));const frameKeys=new Set([...keys,...taps]);taps.clear();let mx=(frameKeys.has('KeyD')||frameKeys.has('ArrowRight')?1:0)-(frameKeys.has('KeyA')||frameKeys.has('ArrowLeft')?1:0),my=(frameKeys.has('KeyS')||frameKeys.has('ArrowDown')?1:0)-(frameKeys.has('KeyW')||frameKeys.has('ArrowUp')?1:0);
  if(demo){if(mode==='city'){const tx=Math.cos(time*.17)*740,ty=Math.sin(time*.23)*270,dx=tx-player.x,dy=ty-player.y;mx=dx;my=dy}else{mx=Math.cos(time*.23);my=Math.sin(time*.31)*.7}}const len=Math.hypot(mx,my);if(len){mx/=len;my/=len}const px=player.x,py=player.y;
  if(mode==='city')updateCity(dt,mx,my,frameKeys.has('Space'));else if(mode==='court'){const scale=height/viewHeight;court.step(dt,{mx,my,dash:frameKeys.has('Space'),crowd,aim:manual?{x:camera.x+(pointer.x-width/2)/scale,y:camera.y+(pointer.y-height/2)/scale}:null})}else updateHydra(dt,mx,my,frameKeys.has('Space'));travel+=Math.hypot(player.x-px,player.y-py);
  let tx=player.x,ty=player.y;if(mode==='city'){const vw=viewHeight*width/height;tx=vw>=2560?0:clamp(tx,-1280+vw/2,1280-vw/2);ty=clamp(ty,-720+viewHeight/2,720-viewHeight/2)}if(mode==='court'){const halfW=viewHeight*width/height/2;tx=halfW>=BOARD_HALF?0:clamp(tx,-BOARD_HALF+halfW,BOARD_HALF-halfW);ty=clamp(ty,-BOARD_HALF+viewHeight/2,BOARD_HALF-viewHeight/2)}const f=1-Math.exp(-dt*12);camera.x+=(tx-camera.x)*f;camera.y+=(ty-camera.y)*f;
  for(const p of sparks){p.life-=dt;p.x+=p.vx*dt;p.y+=p.vy*dt}sparks=sparks.filter(p=>p.life>0);for(const r of rings)r.life-=dt;rings=rings.filter(r=>r.life>0);statClock-=dt;if(statClock<=0){statClock=.15;syncStats()}
 }
 function line(x,y,xx,yy,color,w=1){g.strokeStyle=color;g.lineWidth=w;g.beginPath();g.moveTo(x,y);g.lineTo(xx,yy);g.stroke()}
 function drawCityWarning(m,p){
  if(m.attack<=0)return;const color=city.roster[m.type].color;
  g.save();g.strokeStyle=color+'aa';g.fillStyle=color+'12';g.lineWidth=1.2;
  if(m.type===16){const q=cityWorld(m.targetX,m.targetY);g.beginPath();g.arc(q.x,q.y,80,0,TAU);g.fill();g.stroke();line(q.x-12,q.y,q.x+12,q.y,color);line(q.x,q.y-12,q.x,q.y+12,color)}
  else if(m.type===17){const target=m.repairTarget;if(target&&!target.dead){const q=cityWorld(target.x,target.y);g.setLineDash([4,7]);line(p.x,p.y,q.x,q.y,color+'aa',1.5);g.beginPath();g.arc(q.x,q.y,25,0,TAU);g.stroke()}}
  else {g.translate(p.x,p.y);g.rotate(m.aim);if(m.type===15){g.fillRect(0,-19,242,38);g.strokeRect(0,-19,242,38)}else for(const a of (m.type===13?[-.16,0,.16]:[-.13,.13]))line(0,0,Math.cos(a)*400,Math.sin(a)*400,color+'77',1)}
  g.restore();
 }
 function drawCityActors(){for(const m of city.mobs){if(m.dead)continue;const p=cityWorld(m.x,m.y);if(m.type>=13){drawCityAddition(g,m,p.x,p.y,time);drawCityWarning(m,p);continue}art.robot(g,m.type,p.x,p.y,m.angle,1.1,time,m.hit>0,m.type===10&&m.guard?-2:0);if(m.attack>0){line(p.x,p.y,p.x+Math.cos(m.aim)*180,p.y+Math.sin(m.aim)*180,'#f1ac8455',1);if(m.type===6||m.type===4){g.beginPath();g.arc(p.x,p.y,m.type===6?128:124,0,TAU);g.strokeStyle='#e8ae7266';g.stroke()}}}for(const b of city.shots){const p=cityWorld(b.x,b.y);line(p.x,p.y,p.x-b.vx*.015,p.y-b.vy*.015,b.enemy?b.color:'#9defff',b.enemy?3:2)}for(const z of city.zones){const p=cityWorld(z.x,z.y);g.beginPath();g.arc(p.x,p.y,z.r*(z.reinforcement?CITY_SCALE:1),0,TAU);g.strokeStyle='#f0b170';g.stroke()}}
 function draw(){
  g.setTransform(dpr,0,0,dpr,0,0);g.fillStyle=mode==='city'?'#050a14':'#060c0c';g.fillRect(0,0,width,height);
  const scale=overview?Math.min((width-70)/2560,(height-230)/1440):height/viewHeight,cx=overview?0:camera.x,cy=overview?0:camera.y;
  g.setTransform(dpr*scale,0,0,dpr*scale,canvas.width/2-cx*dpr*scale,canvas.height/2-cy*dpr*scale+(overview?20*dpr:0));
  const bounds={left:cx-width/scale/2,right:cx+width/scale/2,top:cy-height/scale/2,bottom:cy+height/scale/2};
  if(mode==='city'){drawCity(g,cityBg,city,time,images.transit,reducedMotion.matches);drawCityActors()}else if(mode==='court'){drawCourt(g,court,time,bounds,reducedMotion.matches)}else{drawHydraFloor(g,art,images,time,bounds);for(const h of hives)drawHive(g,h,time);for(const e of enemies){if(e.x<bounds.left-170||e.x>bounds.right+170||e.y<bounds.top-170||e.y>bounds.bottom+170)continue;if(e.guardian&&e.wind>0){g.strokeStyle='#d7c288';g.fillStyle='#baa97812';g.lineWidth=2;if(e.guardianKind===1){g.beginPath();g.arc(e.x,e.y,180,0,TAU);g.fill();g.stroke()}else{g.save();g.translate(e.x,e.y);g.rotate(e.aim);g.strokeRect(0,-40,360,80);g.restore()}}if(e.fuse!=null){g.beginPath();g.arc(e.x,e.y,EXPLOSION_RADIUS,0,TAU);g.fillStyle='#a1742118';g.fill();g.strokeStyle='#e5b875';g.lineWidth=1.5;g.stroke();g.beginPath();g.arc(e.x,e.y,EXPLOSION_RADIUS*(1-Math.max(0,e.fuse)/.9),0,TAU);g.stroke()}if(e.legacy)drawLegacy(g,e);else drawInsect(g,e,time);if(e.guardian){g.fillStyle='#121a12';g.fillRect(e.x-75,e.y-135,150,5);g.fillStyle='#cfc389';g.fillRect(e.x-75,e.y-135,150*e.hp/e.maxHp,5);g.font='11px Chakra';g.textAlign='center';g.fillText(e.name,e.x,e.y-147)}}
   for(const b of shots)line(b.x,b.y,b.x-b.vx*.014,b.y-b.vy*.014,b.enemy?'#c3bb7a':'#a3f3ff',b.enemy?3:2.5);
   if(focusHive){const {h}=nearestHive();if(h){g.save();g.setLineDash([5,9]);line(player.x,player.y,h.x,h.y,'#b3cb7866');g.restore();g.beginPath();g.arc(h.x,h.y,94,0,TAU);g.strokeStyle='#d0dd96';g.lineWidth=1.5;g.stroke()}}
  }
  if(images.zack.complete&&images.zack.naturalWidth){g.globalAlpha=player.invuln>0&&Math.sin(time*25)>0?.65:1;g.drawImage(images.zack,player.x-37,player.y-37,74,74);g.globalAlpha=1}
  for(const p of sparks){g.globalAlpha=Math.min(1,p.life*2);g.fillStyle=p.color;g.fillRect(p.x,p.y,2.5,2.5)}g.globalAlpha=1;for(const r of rings){g.beginPath();g.arc(r.x,r.y,r.r*(1-r.life/r.max*.6),0,TAU);g.globalAlpha=r.life/r.max;g.strokeStyle=r.color;g.lineWidth=2;g.stroke()}g.globalAlpha=1;
  if(overview){const vw=viewHeight*width/height;g.fillStyle='#70e7d618';g.fillRect(camera.x-vw/2,camera.y-viewHeight/2,vw,viewHeight);g.strokeStyle='#81f3dc';g.lineWidth=2/scale;g.strokeRect(camera.x-vw/2,camera.y-viewHeight/2,vw,viewHeight);g.beginPath();g.arc(player.x,player.y,4/scale,0,TAU);g.fillStyle='#e3ffff';g.fill()}
  canvas.dataset.overview=String(overview);
 }
 reset();function frame(now){if(dead)return;const dt=Math.min((now-last)/1000,.035);last=now;if(!overview&&!gallery)update(dt);draw();raf=requestAnimationFrame(frame)}raf=requestAnimationFrame(frame);
 return {setGallery(v){gallery=v;keys.clear();taps.clear();manual=false},setCourtTier(v){if(!['auto','0','1','2','3'].includes(v))return;court.setTier(v);player=court.player;camera={x:0,y:0};syncStats()},focusSentinel(){court.focusSentinel=!court.focusSentinel;court.focusQueen=false;syncStats()},focusQueen(){court.focusQueen=!court.focusQueen;court.focusSentinel=false;syncStats()},toggleFloor(){floorStyle=floorStyle==='subtle'?'original':'subtle';cityBg=buildCity(floorStyle);syncStats()},setOverview(v){overview=mode==='city'&&v;keys.clear();taps.clear();syncStats()},setMode(m){overview=false;mode=m;reset()},setDistance(d){targetHeight=d},setDemo(d){demo=d;keys.clear()},setCrowd(c){crowd=c;syncStats()},focusHive(){focusHive=!focusHive;syncStats()},reset,key,purge(){if(mode==='city'){city.setPhase(1);city.phaseTime=0}},destroy(){dead=true;cancelAnimationFrame(raf);window.removeEventListener('resize',resize);window.removeEventListener('keydown',keyDown);window.removeEventListener('keyup',keyUp);window.removeEventListener('blur',blur);canvas.removeEventListener('pointermove',pointerMove);canvas.removeEventListener('pointerdown',pointerDown);canvas.removeEventListener('pointerup',pointerUp);canvas.removeEventListener('pointercancel',pointerUp)}}
}

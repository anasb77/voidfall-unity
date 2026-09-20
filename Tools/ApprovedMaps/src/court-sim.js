import {sentinelCells,insideTerritory,sentinelPhase,SENTINEL_HP,SENTINEL_SHIELD} from './court-sentinel-rules.js';
import {CELL,BOARD_HALF,pieceStats,directorTier,planKnightPath,inWarnedCell,promotePawn,pointSegmentDistance} from './court-rules.js';
const TAU=Math.PI*2,clamp=(v,a,b)=>Math.max(a,Math.min(b,v)),distance=(a,b)=>Math.hypot(a.x-b.x,a.y-b.y);
export class CourtSimulation{
 constructor(random=Math.random){this.random=random;this.tierMode='auto';this.reset()}
 reset(){
  this.time=0;this.wave=1;this.sequence=0;this.kills=0;this.promotions=0;this.interruptions=0;this.knightDashes=0;this.shotsBlocked=0;this.floorKills=0;this.armorBlocks=0;this.shieldAbsorbed=0;
  this.player={x:0,y:0,hp:100,r:17,invuln:2,dash:0,cooldown:0,vx:1,vy:0};this.enemies=[];this.groups=[];this.shots=[];this.effects=[];this.pickups=[];this.focusQueen=false;this.focusSentinel=false;this.notice='';this.noticeTime=0;this.shotClock=0;this.spawnClock=8;this.specialClock=13;
  this.covers=[{x:420,y:65},{x:-430,y:280},{x:670,y:-600},{x:-710,y:-570},{x:920,y:720},{x:-920,y:940}].map((p,i)=>({...p,id:i+1,r:65,hp:SENTINEL_HP,maxHp:SENTINEL_HP,white:i%2===0,dead:false,mouth:i%3,delay:1+i*1.3,phase:{stage:"rest",age:100},lastStrike:-1}));
  this.phase={cycle:-1,stage:"rest",age:100};this.cells=[];this.addFormation(-390,-100,0);this.addFormation(360,250,1);
  for(const [type,x,y] of [[5,200,-140],[5,-190,200],[3,-330,-250],[2,380,-250],[1,-480,30],[4,320,20]]){const e=this.spawn(type,x,y,1);e.cool=type===3?1.4:type===4?4:2+type}
  this.updateFloor(0);this.updateShields(0);
 }
 tier(){return this.tierMode==='auto'?directorTier(this.time,this.random()):Number(this.tierMode)}
 spawn(type,x,y,white,tier=this.tier()){
  tier=Math.min(type===1?3:2,tier);if(type===1&&this.tierMode==='auto'&&this.time>=120&&this.random()<.22)tier=3;
  const stats=pieceStats(type,tier),e={id:++this.sequence,type,tier,x,y,white:!!white,...stats,maxHp:stats.hp,angle:0,hit:0,cool:2+this.random()*3,state:'move',timer:0,dead:false,group:null,path:null,leg:1,targets:[]};this.enemies.push(e);return e;
 }
 addFormation(x,y,white){
  const angle=Math.atan2(this.player.y-y,this.player.x-x),group={x,y,angle,speed:pieceStats(0,this.tier()).speed*.8};this.groups.push(group);
  for(const offset of [-2,-1,1,2]){const e=this.spawn(0,x-Math.sin(angle)*offset*55,y+Math.cos(angle)*offset*55,white);e.group=group;e.offset=offset*55}
 }
 setTier(value){this.tierMode=value;this.reset()}
 say(message){this.notice=message;this.noticeTime=3}
 hitPlayer(n){const p=this.player;if(p.invuln>0||p.dash>0)return;p.hp-=n;p.invuln=.8;this.effects.push({x:p.x,y:p.y,r:38,color:'#a2eaff',life:.3,max:.3});if(p.hp<=0){p.hp=100;p.invuln=3;p.x=0;p.y=0;this.say('ZACK RECOVERED · THE MATCH CONTINUES')}}
 hurt(e,n,source='weapon'){
  if(e.dead)return;
  if(e.type===5&&source==='weapon'&&this.random()<.2){e.blockFlash=.3;this.armorBlocks++;this.effects.push({x:e.x,y:e.y,r:e.r+14,color:'#eee3ba',life:.25,max:.25});return}
  const sheltered=this.covers.some(c=>insideTerritory(e,c));if(!sheltered)e.sentinelShield=0;
  if(e.sentinelShield>0){const absorbed=Math.min(e.sentinelShield,n);e.sentinelShield-=absorbed;n-=absorbed;this.shieldAbsorbed+=absorbed;e.shieldFlash=.2;if(e.sentinelShield===0)e.shieldRecharge=4}
  if(e.ward>0){const absorbed=Math.min(e.ward,n);e.ward-=absorbed;n-=absorbed}e.hp-=n;e.hit=.1;if(e.hp>0)return;e.dead=true;this.kills++;if(source==='floor')this.floorKills++;
  this.effects.push({x:e.x,y:e.y,r:e.r*2,color:e.white?'#f0ede1':'#abb3bd',life:.45,max:.45});
  if(e.type===4&&e.state==='promote'){this.interruptions++;this.say('QUEEN FALLEN · PROMOTION INTERRUPTED');e.targets=[]}
 }
 damageCover(c,n){if(c.dead)return;c.hp-=n;if(c.hp<=0){c.dead=true;c.phase={stage:'rest',age:100,cycle:-1};this.updateShields(0);this.pickups.push({x:c.x,y:c.y,heal:22});this.effects.push({x:c.x,y:c.y,r:110,color:'#d5ceb9',life:.8,max:.8});this.say('SENTINEL FALLEN · TERRITORY DISARMED')}}
 collideCover(o){for(const c of this.covers){if(c.dead)continue;const d=distance(o,c),min=c.r+o.r;if(d<min){const a=d<.01?0:Math.atan2(o.y-c.y,o.x-c.x);o.x=c.x+Math.cos(a)*min;o.y=c.y+Math.sin(a)*min}}o.x=clamp(o.x,-BOARD_HALF+o.r,BOARD_HALF-o.r);o.y=clamp(o.y,-BOARD_HALF+o.r,BOARD_HALF-o.r)}
 updateShields(dt){
  for(const e of this.enemies){
   e.shieldRecharge=Math.max(0,(e.shieldRecharge||0)-dt);e.shieldFlash=Math.max(0,(e.shieldFlash||0)-dt);
   const source=this.covers.find(c=>insideTerritory(e,c));e.shieldSource=source?.id??null;
   if(!source){e.sentinelShield=0;continue}
   if(!(e.sentinelShield>0)&&!e.shieldRecharge)e.sentinelShield=SENTINEL_SHIELD;
  }
 }
 updateFloor(dt){
  this.cells=[];const active=[];
  for(const c of this.covers){
   if(c.dead)continue;c.phase=sentinelPhase(this.time,c);const cells=sentinelCells(c);
   this.cells.push(...cells.map(cell=>({...cell,phase:c.phase,source:c.id})));
   if(c.phase.stage==='burst'&&c.lastStrike!==c.phase.cycle){c.lastStrike=c.phase.cycle;if(inWarnedCell(this.player,cells))this.hitPlayer(20)}
   // The sentinel protects its army; its territory attack targets Zack only.
   if(distance(c,this.player)<800)active.push(c.phase);
  }
  this.phase=active.find(p=>p.stage==='burst')||active.find(p=>p.stage==='warning')||{stage:'rest',age:100,cycle:-1};
 }
 step(dt,{mx=0,my=0,dash=false,crowd=true,aim=null}={}){
  const p=this.player;p.invuln=Math.max(0,p.invuln-dt);p.dash=Math.max(0,p.dash-dt);p.cooldown=Math.max(0,p.cooldown-dt);
  if(mx||my){p.vx=mx;p.vy=my}if(dash&&p.cooldown<=0){p.dash=.16;p.cooldown=1.1}
  const speed=p.dash>0?920:250;p.x+=(p.dash>0?p.vx:mx)*speed*dt;p.y+=(p.dash>0?p.vy:my)*speed*dt;this.collideCover(p);
  for(const orb of this.pickups)if(distance(orb,p)<42){p.hp=Math.min(100,p.hp+orb.heal);orb.dead=true}this.pickups=this.pickups.filter(o=>!o.dead);
  if(!crowd)return;
  this.time+=dt;this.noticeTime=Math.max(0,this.noticeTime-dt);this.updateFloor(dt);
  this.spawnClock-=dt;this.specialClock-=dt;
  if(this.spawnClock<=0){this.spawnClock=8;if(this.enemies.filter(e=>!e.dead).length<44){this.wave++;const a=this.random()*TAU;this.addFormation(clamp(p.x+Math.cos(a)*620,-1500,1500),clamp(p.y+Math.sin(a)*620,-1500,1500),this.wave%2);if(this.enemies.length<48){const b=a+.4;this.spawn(5,clamp(p.x+Math.cos(b)*620,-1500,1500),clamp(p.y+Math.sin(b)*620,-1500,1500),this.wave%2)}}}
  if(this.specialClock<=0){this.specialClock=12;const specials=this.enemies.filter(e=>!e.dead&&e.type!==0&&e.type!==5);if(specials.length<7){const a=this.random()*TAU,type=1+Math.floor(this.random()*4);if(type!==4||specials.filter(e=>e.type===4).length<2)this.spawn(type,clamp(p.x+Math.cos(a)*540,-1500,1500),clamp(p.y+Math.sin(a)*540,-1500,1500),this.wave%2)}}
  for(const group of this.groups){const a=Math.atan2(p.y-group.y,p.x-group.x);group.x+=Math.cos(a)*group.speed*dt;group.y+=Math.sin(a)*group.speed*dt}
  for(const e of this.enemies){
   if(e.dead)continue;e.blockFlash=Math.max(0,(e.blockFlash||0)-dt);if(e.wardTime>0){e.wardTime-=dt;if(e.wardTime<=0)e.ward=0}e.hit=Math.max(0,e.hit-dt);e.cool-=dt;const d=distance(e,p)||1,a=Math.atan2(p.y-e.y,p.x-e.x);e.angle=a;
   if(e.state==='recover'){e.timer-=dt;if(e.timer<=0)e.state='move'}
   else if(e.state==='promote'){
    e.timer-=dt;e.targets=e.targets.filter(t=>!t.dead&&(t.tier<2||(e.tier===2&&!t.ward)));
    if(e.timer<=0){let count=0,wards=0;for(const pawn of e.targets){if(promotePawn(pawn)){count++;this.promotions++;pawn.group=null}else if(e.tier===2&&!pawn.dead&&pawn.tier===2){pawn.ward=24;pawn.wardTime=6;wards++}}e.targets=[];e.state='recover';e.timer=.9;e.cool=9;if(count)this.say('PROMOTION · '+count+' PAWN'+(count===1?'':'S')+' ASCENDED');else if(wards)this.say('ROYAL WARD · PAWNS SHIELDED FOR 6 SECONDS')}
   }else if(e.state==='warn'){
    e.timer-=dt;if(e.timer<=0){if(e.type===3||e.type===1){e.state='dash';e.leg=1;if(e.type===3)this.knightDashes++}else{const count=e.tier===2?2:1;for(let i=0;i<count;i++)this.shots.push({x:e.x,y:e.y,vx:Math.cos(e.aim+(i?Math.PI/2:0))*260,vy:Math.sin(e.aim+(i?Math.PI/2:0))*260,life:2.8,enemy:true,damage:9+e.tier*2});e.state='recover';e.timer=.8;e.cool=e.cooldown}}
   }else if(e.state==='corner'){e.timer-=dt;if(e.timer<=0)e.state='dash'}
   else if(e.state==='dash'){
    const dest=e.path[e.leg],length=distance(e,dest),travel=Math.min(length,[460,540,620,590][e.tier]*dt),before={x:e.x,y:e.y};
    if(length>.001){e.x+=(dest.x-e.x)/length*travel;e.y+=(dest.y-e.y)/length*travel;e.angle=Math.atan2(dest.y-before.y,dest.x-before.x)}
    if(pointSegmentDistance(p,before,e)<p.r+e.r)this.hitPlayer(e.damage+5);
    if(length<=travel+.001){if(e.leg<e.path.length-1){e.leg++;e.state='corner';e.timer=.14}else{e.state='recover';e.timer=.75;e.cool=e.cooldown}}
   }else{
    if(e.type===0||e.type===5){const target=e.group?{x:e.group.x-Math.sin(e.group.angle)*e.offset,y:e.group.y+Math.cos(e.group.angle)*e.offset}:p;const gap=distance(e,target);if(gap>1){e.x+=(target.x-e.x)/gap*Math.min(gap,e.speed*dt);e.y+=(target.y-e.y)/gap*Math.min(gap,e.speed*dt)}}
    else{
     const range=e.type===2?310:e.type===4?280:90,direction=d>range?1:d<range-70&&e.type!==1&&e.type!==3?-.5:0;e.x+=Math.cos(a)*direction*e.speed*dt;e.y+=Math.sin(a)*direction*e.speed*dt;
     const busy=this.enemies.filter(o=>!o.dead&&['warn','dash','promote'].includes(o.state)).length;
     if(e.cool<=0&&d<620&&busy<2){
      if(e.type===4){e.targets=this.enemies.filter(o=>!o.dead&&o.type===0&&(o.tier<2||(e.tier===2&&!o.ward))&&o.white===e.white&&distance(o,e)<430).slice(0,e.tier===0?1:2);if(e.targets.length){e.state='promote';e.timer=2.7;this.say(e.targets.every(t=>t.tier===2)?'QUEEN IS WEAVING A WARD · E TO TARGET':'QUEEN IS CROWNING PAWNS · E TO TARGET')}else e.cool=2}
      else if(e.type===3){e.path=planKnightPath(e,p,this.covers);if(e.path){e.state='warn';e.timer=e.warning}else e.cool=1}
      else if(e.type===1){const horizontal=Math.abs(p.x-e.x)>Math.abs(p.y-e.y);e.path=[{x:e.x,y:e.y},{x:clamp(e.x+(horizontal?Math.sign(p.x-e.x)*290:0),-1700,1700),y:clamp(e.y+(horizontal?0:Math.sign(p.y-e.y)*290),-1700,1700)}];e.state='warn';e.timer=e.warning}
      else {e.aim=Math.round((a-Math.PI/4)/(Math.PI/2))*Math.PI/2+Math.PI/4;e.state='warn';e.timer=e.warning}
     }
    }
   }
   const beforeCover={x:e.x,y:e.y};this.collideCover(e);if(e.state==='dash'&&distance(e,beforeCover)>1){e.state='recover';e.timer=.8;e.cool=e.cooldown}
   if(distance(e,p)<e.r+p.r)this.hitPlayer(e.damage);
  }
  // Local separation leaves gaps in pawn ranks and avoids unreadable stacks.
  const alive=this.enemies.filter(e=>!e.dead);for(let i=0;i<alive.length;i++)for(let j=i+1;j<alive.length;j++){const a=alive[i],b=alive[j],d=distance(a,b),min=(a.r+b.r)*.8;if(d>.01&&d<min){const push=(min-d)*.4,dx=(a.x-b.x)/d*push,dy=(a.y-b.y)/d*push;if(a.state==='move'){a.x+=dx;a.y+=dy}if(b.state==='move'){b.x-=dx;b.y-=dy}}}
  this.updateShields(dt);
  this.shotClock-=dt;if(this.shotClock<=0){let target=aim;
   if(!target){const pool=(this.focusSentinel?this.covers.filter(c=>!c.dead):alive.filter(e=>!this.focusQueen||e.type===4)).sort((a,b)=>distance(a,p)-distance(b,p));if(pool[0]&&distance(pool[0],p)<(this.focusQueen||this.focusSentinel?800:560))target=pool[0]}
   if(target){const a=Math.atan2(target.y-p.y,target.x-p.x);this.shots.push({x:p.x,y:p.y,vx:Math.cos(a)*850,vy:Math.sin(a)*850,life:1,enemy:false});this.shotClock=.15}
  }
  for(const b of this.shots){const before={x:b.x,y:b.y};b.x+=b.vx*dt;b.y+=b.vy*dt;b.life-=dt;
   const obstacles=this.covers.filter(c=>!c.dead&&pointSegmentDistance(c,before,b)<c.r).sort((a,c)=>distance(a,before)-distance(c,before));
   if(obstacles.length){b.life=0;this.shotsBlocked++;this.damageCover(obstacles[0],b.enemy?0:12);continue}
   if(b.enemy){if(pointSegmentDistance(p,before,b)<p.r){this.hitPlayer(b.damage);b.life=0}}
   else for(const e of alive)if(!e.dead&&pointSegmentDistance(e,before,b)<e.r){this.hurt(e,12);b.life=0;break}
  }
  this.enemies=this.enemies.filter(e=>!e.dead);this.groups=this.groups.filter(group=>this.enemies.some(e=>e.group===group));this.shots=this.shots.filter(b=>b.life>0);
  for(const effect of this.effects)effect.life-=dt;this.effects=this.effects.filter(e=>e.life>0);
 }
}

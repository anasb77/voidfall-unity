(function(root){
  'use strict';
  const TYPES={PATROL:0,ENFORCER:1,SENTINEL:2,CRAWLER:3,EXPLODER:4,GUNSHIP:5,MECH:6,BROODMOTHER:7,LIGHT_GUNSHIP:8,INTERCEPTOR:9,MARSHAL:10,SUPPRESSOR:11,MOTHERLOAD:12};
  const isHeavy=t=>[5,6,7,12].includes(t),isCop=t=>t>=9&&t<=11,isShip=t=>t===5||t===8;
  const ARENA=Object.freeze({left:180,right:1420,top:220,bottom:746});
  const ROSTER=[
    {name:'Patrol',code:'P / 01',role:'HARASSER',color:'#64efe5',r:13,hp:44,speed:70,description:'A split-wing sensor drone. Circles at a distance, fires a short burst, then slips sideways.',connection:'Its lateral movement keeps you moving between purge lanes. Blackout interrupts its weapons.'},
    {name:'Enforcer',code:'E / 09',role:'CHARGER',color:'#e989de',r:22,hp:175,speed:32,description:'Broad ceramic shoulders protect a dark mechanical core. Locks a direction before a committed charge.',connection:'Lure its charge into a marked conduit and dash through the opening.'},
    {name:'Rail Sentinel',code:'S / 04',role:'PRECISION',color:'#ffd19b',r:17,hp:85,speed:20,description:'A long rail barrel unfolds between three stabilizers. Its sightline freezes before the shot.',connection:'Each shot has a visible warning. Blackout shuts down its weapon, letting you close in.'},
    {name:'Crawler',code:'M / 02',role:'RUSHER',color:'#b6efb4',r:15,hp:36,speed:85,description:'The former Mender chassis, now a dedicated rusher. Six articulated legs carry a compact armored body.',connection:'Simple, persistent crowd pressure. Purge lanes can clear whole packs.'},
    {name:'Volatile Crawler',code:'V / 08',role:'EXPLOSIVE',color:'#ff985f',r:25,hp:90,speed:58,description:'A swollen reactor hangs inside a wide insect frame. Stops, unfolds and warns before detonating.',connection:'Its blast damages nearby machines too. Destroy it beside a pack to trigger a chain reaction.'},
    {name:'Heavy Gunship',code:'G / 12',role:'FOUR CANNONS',color:'#87b7ff',r:43,hp:630,speed:25,description:'An armored hovering hull carries four visible cannon mounts. Fires one after another, leaving moving gaps.',connection:'A rare heavy unit. Its cannon sequence pauses during blackout; its hull remains vulnerable to purge.'},
    {name:'Siege Mech',code:'K / 06',role:'TERRITORIAL',color:'#e5a4f6',r:39,hp:760,speed:24,description:'Heavy articulated feet, oversized shoulders and a recessed reactor. Advances, braces, then delivers a shockwave stomp.',connection:'The stomp marks its radius before impact. Its slow advance contests the ground between active lanes.'},
    {name:'Broodmother',code:'B / 00',role:'BROOD CARRIER',color:'#d4f790',r:57,hp:840,speed:18,description:'A massive mechanical insect with snapping, toothed mandibles, armored egg chambers and eight jointed legs. Releases two Crawlers every eight seconds.',connection:'Destroying it releases exactly FOUR Crawlers. The shell ruptures, then its surviving brood rushes outward.'},
    {name:'Light Gunship',code:'G / 02',role:'TWIN CANNONS',color:'#b4b0ff',r:27,hp:240,speed:48,description:'A compact escort hull with two forward cannons. Repositions faster than the heavy gunship and fires a quick paired volley.',connection:'Part of the regular pool. It can escort a heavy unit, but its weapons still shut down during blackout.'},
    {name:'Interceptor',code:'NCP / 01',role:'PURSUIT COP',color:'#4b9dff',r:17,hp:95,speed:90,description:'A blue pursuit frame with split siren pods and an armored spearhead. Rushes out of the hangar and commits to short interception dashes.',connection:'Blackout response squad. Backup power keeps it active while ordinary city weapons are offline.'},
    {name:'Marshal',code:'NCP / 02',role:'RIOT SHIELD',color:'#72b6ff',r:28,hp:310,speed:31,description:'A broad police chassis carrying a curved riot shield. Periodically braces, greatly reducing shots that hit its shield from the front.',connection:'Move around its flank or use city damage. The shield does not protect against purge lanes or explosions.'},
    {name:'Suppressor',code:'NCP / 03',role:'SUPPRESSION COP',color:'#4088ff',r:21,hp:170,speed:35,description:'A floating police weapons platform with blue identification lights. Fires one deliberate shot, alternating its right and left guns.',connection:'Deploys during lockdown. Each single shot has a visible aiming warning; the next shot comes from the opposite barrel.'},
    {name:'Motherload',code:'NULL / SOVEREIGN',role:'CITY SECRET WEAPON',color:'#ffd58d',r:114,hp:12000,speed:22,description:'The city’s concealed war carrier: eight deck cannons, drone foundries, four plasma engines and the Event Horizon capture array.',connection:'Permanent lockdown. Sidestep or dash out of the warned tractor cone while it fires, then punish its overheating reactor. A depleted shield ends the attempt; the next attempt starts fresh.'}
  ];
  const PHASES=[
    {name:'Surveillance',title:'THE CITY IS WATCHING',copy:'Alien apertures open. Searchlights sweep the conduits.',color:'#63eade',duration:22},
    {name:'Lockdown',title:'LAW ENFORCEMENT DEPLOYED',copy:'Power drops. Purge lanes ignite. The hangar deploys its response squad.',color:'#859cff',duration:24}
  ];
  const clamp=(n,a,b)=>Math.max(a,Math.min(b,n)),distance=(a,b)=>Math.hypot(a.x-b.x,a.y-b.y),TAU=Math.PI*2;
  class Simulation{
    constructor(seed=9823){
      this.seed=seed;this.t=0;this.phase=0;this.phaseTime=0;this.auto=true;this.paused=false;this.playing=false;this.ambient=true;this.kills=0;this.heavyIndex=0;this.spawnClock=0;this.heavyClock=0;this.birthCount=0;
      this.mobs=[];this.shots=[];this.events=[];this.zones=[];this.purgePass=-1;this.hangarWave=0;this.bossFight=false;this.bossDefeated=false;this.exitOpen=false;this.cleared=false;this.failed=false;
      this.player={x:800,y:470,r:11,hp:100,angle:-1.57,invuln:0,dash:0,dashCD:0,shot:0,target:null};
      this.observe();
    }
    random(){this.seed=(Math.imul(this.seed,1664525)+1013904223)>>>0;return this.seed/4294967296;}
    range(a,b){return a+this.random()*(b-a);}
    emit(kind,x,y,color,extra={}){this.events.push({kind,x,y,color,...extra});}
    spawn(type,x,y,options={}){
      const def=ROSTER[type];
      const m={type,x,y,r:def.r,hp:def.hp,angle:this.range(0,TAU),age:this.range(0,8),seed:this.range(0,TAU),stun:0,invuln:0,hit:0,cool:this.range(2,5),attack:0,aim:0,charge:0,volley:0,volleyTime:0,brood:6,dead:false,emerging:0,fromHangar:false,guard:false,exposed:0,bossClock:1.6,bossStep:0,bossAction:'wake',barrel:0,tractor:0,tractorShot:0,...options};
      this.mobs.push(m);return m;
    }
    spawnAmbient(type){
      if(isHeavy(type)&&this.mobs.some(m=>!m.dead&&isHeavy(m.type)))return null;
      if(this.mobs.filter(m=>!m.dead).length>=44)return null;
      const edge=Math.floor(this.random()*4);
      return this.spawn(type,edge===0?ARENA.left+18:edge===1?ARENA.right-18:this.range(250,1350),edge===2?228:edge===3?736:this.range(262,716));
    }
    observe(){
      this.playing=false;this.ambient=true;this.paused=false;this.mobs=[];this.shots=[];this.events=[];this.zones=[];this.bossFight=false;this.bossDefeated=false;this.exitOpen=false;this.cleared=false;this.hangarWave=0;this.failed=false;
      for(let i=0;i<11;i++)this.spawn(i<6?3:i<9?0:2,this.range(380,1200),this.range(290,678));
      this.spawn(1,594,390);this.spawn(4,1120,663);this.spawn(5,1095,289);this.spawn(6,487,604);this.spawn(7,1160,481);this.spawn(8,733,291);
    }
    start(testType=null){
      this.playing=true;this.ambient=testType===null;this.mobs=[];this.shots=[];this.events=[];this.zones=[];this.kills=0;this.birthCount=0;this.spawnClock=0;this.heavyClock=0;this.auto=testType!==12;this.paused=false;this.bossFight=testType===12;this.bossDefeated=false;this.exitOpen=false;this.cleared=false;this.failed=false;
      Object.assign(this.player,{x:760,y:472,hp:100,dash:0,dashCD:0,invuln:3,shot:0,target:null});this.setPhase(this.bossFight?1:0);
      if(testType!==null){this.spawn(testType,testType===TYPES.MECH?900:testType===12?1040:1080,472,{cool:1,brood:3});if(isCop(testType)){this.setPhase(1);this.auto=false;}if(testType===12){this.player.x=650;this.emit('boss-arrival',1040,472,ROSTER[12].color);}}
      else{for(let i=0;i<14;i++)this.spawnAmbient(i<8?3:i<11?0:i<13?2:1);this.spawnAmbient(7);}
    }
    setPhase(p){
      if(!Number.isInteger(p)||p<0||p>=PHASES.length)return;
      if(this.bossFight&&!this.bossDefeated&&p!==1)return;
      this.phase=p;this.phaseTime=0;this.demoHack=false;
      if(p===1)this.purgePass++;
      if(p===1){this.hangarWave=0;for(const m of this.mobs){if([0,1,2,5,6,8].includes(m.type)){m.attack=0;m.volley=0;m.charge=0;}}}
    }
    hatch(m,count,reason){
      for(let i=0;i<count;i++){
        const a=m.angle+i/count*TAU;
        const radius=m.type===12?150:64;
        this.spawn(3,clamp(m.x+Math.cos(a)*radius,ARENA.left+5,ARENA.right-5),clamp(m.y+Math.sin(a)*radius,224,745),{stun:.55,invuln:.75,cool:1,angle:a});
      }
      this.birthCount+=count;this.emit('hatch',m.x,m.y,ROSTER[7].color,{count,reason});
    }
    hurt(m,n,source='weapon',hitAngle){
      if(m.dead||m.invuln>0)return;
      if(m.type===12)n*=m.exposed>0?2.1:.7;
      if(m.type===10&&m.guard&&source==='weapon'&&Number.isFinite(hitAngle)&&Math.cos(hitAngle-m.angle)>.25)n*=.3;
      m.hp-=n;m.hit=.10;if(m.hp<=0)this.kill(m,source);
    }
    kill(m,source='weapon'){
      if(m.dead)return;m.dead=true;this.kills++;this.emit('death',m.x,m.y,ROSTER[m.type].color,{r:m.r,type:m.type});
      if(m.type===7)this.hatch(m,4,'death');
      if(m.type===12){m.tractor=0;this.bossDefeated=true;this.exitOpen=true;this.ambient=false;this.shots=[];this.zones=[];for(const other of this.mobs)other.dead=true;this.emit('boss-defeated',m.x,m.y,ROSTER[12].color);}
      if(m.type===4){
        this.emit('explosion',m.x,m.y,ROSTER[4].color,{r:124});
        if(distance(m,this.player)<124)this.hitPlayer(27);
        for(const other of this.mobs.slice())if(!other.dead&&other!==m&&distance(m,other)<124)this.hurt(other,150,'blast');
      }
    }
    hitPlayer(n){
      const p=this.player;if(!this.playing||p.invuln>0)return;p.hp=Math.max(0,p.hp-n*(this.bossFight?.9:.45));p.invuln=.85;this.emit('hit',p.x,p.y,'#e8fff3');
      if(p.hp<=0){
        if(this.bossFight){this.failed=true;this.playing=false;this.shots=[];this.zones=[];this.emit('boss-failure',p.x,p.y,'#e3a48e');return;}
        Object.assign(p,{hp:100,x:760,y:472,target:null,invuln:4});for(const m of this.mobs)if(distance(m,p)<220)m.stun=3;this.emit('reset',p.x,p.y,'#b6efb4');
      }
    }
    dash(){const p=this.player;if(!this.playing||p.dashCD>0||this.paused)return;p.dash=.15;p.dashCD=1.7;p.invuln=.25;this.emit('dash',p.x,p.y,'#ceffee');}
    hazard(){
      if(this.phase!==1||this.bossDefeated)return null;
      const beat=Math.floor(this.phaseTime/6),lane=beat%4,progress=this.phaseTime%6;
      if(progress>4.3)return null;
      const sub=(Math.floor(beat/4)+Math.max(0,this.purgePass))%2,vertical=lane>=2;
      return {lane,progress,fire:progress>=2.4,x:lane===3?488:lane===2?(sub?1030:956):ARENA.left,y:vertical?218:lane===0?311:558,w:vertical?54:ARENA.right-ARENA.left,h:vertical?527:68};
    }
    motherloadMuzzle(m,mount){
      const lx=m.r*(-.75+Math.floor(mount/2)*.45)+33,ly=(mount%2?1:-1)*m.r*.64;
      return {x:m.x+Math.cos(m.angle)*lx-Math.sin(m.angle)*ly,y:m.y+Math.sin(m.angle)*lx+Math.cos(m.angle)*ly};
    }
    fire(m,angle,speed=330,offset=0,mount=-1){
      let x=m.x+Math.cos(angle)*m.r-Math.sin(angle)*offset,y=m.y+Math.sin(angle)*m.r+Math.cos(angle)*offset;
      if(m.type===12&&mount>=0)({x,y}=this.motherloadMuzzle(m,mount));
      if(m.type===11&&mount>=0){const ly=mount===0?11:-11;x=m.x+Math.cos(m.angle)*30-Math.sin(m.angle)*ly;y=m.y+Math.sin(m.angle)*30+Math.cos(m.angle)*ly;}
      this.shots.push({x,y,vx:Math.cos(angle)*speed,vy:Math.sin(angle)*speed,life:2.3,enemy:true,color:ROSTER[m.type].color});
      this.emit(mount>=0?'cannon':'shot',x,y,ROSTER[m.type].color,{mount,type:m.type});
    }
    deployPolice(){
      for(let i=0;i<3;i++){if(this.mobs.filter(m=>!m.dead&&isCop(m.type)).length>=9)break;this.spawn(9+i,680+i*110,777,{angle:-Math.PI/2,fromHangar:true,emerging:1.3,invuln:.9,cool:2});}
      this.emit('hangar',800,790,'#589dff',{wave:this.hangarWave});
    }
    updateMotherload(m,dt){
      const p=this.player,a=Math.atan2(p.y-m.y,p.x-m.x);
      if(distance(m,p)<m.r+p.r)this.hitPlayer(24);
      if(this.failed)return;
      if(m.tractor>0||(m.bossAction==='tractor'&&m.attack>0))m.angle=m.aim;
      else m.angle+=Math.atan2(Math.sin(a-m.angle),Math.cos(a-m.angle))*Math.min(1,dt*.8);
      if(m.exposed>0){m.exposed=Math.max(0,m.exposed-dt);return;}
      if(m.tractor>0){
        m.tractor=Math.max(0,m.tractor-dt);
        const dx=p.x-m.x,dy=p.y-m.y,forward=dx*Math.cos(m.aim)+dy*Math.sin(m.aim),side=Math.abs(-dx*Math.sin(m.aim)+dy*Math.cos(m.aim));
        if(this.playing&&p.dash<=0&&forward>145&&forward<640&&side<forward*Math.tan(.38)){
          const d=Math.hypot(dx,dy)||1,pull=125*dt;p.x=clamp(p.x-dx/d*pull,ARENA.left,ARENA.right);p.y=clamp(p.y-dy/d*pull,ARENA.top,ARENA.bottom);
        }
        m.tractorShot-=dt;
        if(m.tractorShot<=0&&m.tractor>0){const mount=m.barrel%2?7:6,muzzle=this.motherloadMuzzle(m,mount);this.fire(m,Math.atan2(p.y-muzzle.y,p.x-muzzle.x),295,0,mount);m.barrel++;m.tractorShot=.65;}
        if(m.tractor===0){m.exposed=4;m.bossAction='vent';this.emit('boss-action',m.x,m.y,ROSTER[12].color,{action:'vent'});}
        return;
      }
      if(m.volley>0){m.volleyTime-=dt;if(m.volleyTime<=0){const mount=8-m.volley;this.fire(m,m.aim+(mount-3.5)*.15,320,[-75,75,-55,55,-34,34,-13,13][mount],mount);m.volley--;m.volleyTime=.18;}return;}
      if(m.attack>0){m.attack-=dt;if(m.attack<=0){
        if(m.bossAction==='cannons'){m.volley=8;m.volleyTime=0;}
        if(m.bossAction==='tractor'){m.tractor=4;m.tractorShot=.6;this.emit('tractor-start',m.x,m.y,'#a4eaff');}
        if(m.bossAction==='brood'){if(this.mobs.filter(n=>!n.dead).length<24){this.hatch(m,4,'boss');this.spawn(4,clamp(m.x-140,285,1310),clamp(m.y+70,240,720),{stun:1,invuln:1});}}
        if(m.bossAction==='bombardment'){for(const [dx,dy] of [[0,0],[-125,65],[125,-65]])this.zones.push({x:clamp(p.x+dx,300,1280),y:clamp(p.y+dy,250,710),r:70,timer:1.6});}
      }return;}
      m.bossClock-=dt;
      const target={x:960+Math.sin(this.t*.13)*120,y:465+Math.cos(this.t*.16)*70},d=distance(m,target)||1;
      m.x+=(target.x-m.x)/d*ROSTER[12].speed*dt;m.y+=(target.y-m.y)/d*ROSTER[12].speed*dt;
      if(m.bossClock<=0){m.bossAction=['cannons','tractor','brood','bombardment','vent'][m.bossStep++%5];this.emit('boss-action',m.x,m.y,ROSTER[12].color,{action:m.bossAction});m.bossClock=1.6;m.aim=a;
        if(m.bossAction==='vent')m.exposed=5;else m.attack=m.bossAction==='tractor'?1.8:m.bossAction==='bombardment'?1.1:1.4;
      }
    }
    step(dt,input={}){
      if(this.paused||this.failed||!Number.isFinite(dt)||dt<=0)return;
      this.t+=dt;this.phaseTime+=dt;
      if(this.bossFight&&!this.bossDefeated){this.auto=false;if(this.phase!==1)this.setPhase(1);}
      else if(this.auto&&this.phaseTime>=PHASES[this.phase].duration)this.setPhase((this.phase+1)%PHASES.length);
      const p=this.player;p.invuln=Math.max(0,p.invuln-dt);p.dashCD=Math.max(0,p.dashCD-dt);p.dash=Math.max(0,p.dash-dt);
      if(this.playing){
        let dx=input.x||0,dy=input.y||0;if(dx||dy)p.target=null;
        if(p.target){const d=distance(p,p.target);if(d>5){dx=(p.target.x-p.x)/d;dy=(p.target.y-p.y)/d;}else p.target=null;}
        const l=Math.hypot(dx,dy);if(l){dx/=l;dy/=l;p.angle=Math.atan2(dy,dx);}
        if(p.dash>0&&!l){dx=Math.cos(p.angle);dy=Math.sin(p.angle);}
        const speed=p.dash>0?660:187;p.x=clamp(p.x+dx*speed*dt,ARENA.left,ARENA.right);p.y=clamp(p.y+dy*speed*dt,ARENA.top,ARENA.bottom);
        p.shot-=dt;if(p.shot<=0&&!input.noFire){let target=null,best=470;for(const m of this.mobs){const d=distance(m,p);if(!m.dead&&d<best){target=m;best=d;}}
          if(target){const a=Math.atan2(target.y-p.y,target.x-p.x);this.shots.push({x:p.x,y:p.y,vx:Math.cos(a)*680,vy:Math.sin(a)*680,life:1,enemy:false});p.shot=.17;}}
      }else{p.x=780+Math.sin(this.t*.16)*115;p.y=478+Math.cos(this.t*.21)*90;p.angle=this.t*.16+Math.PI/2;}
      if(this.phase===1&&!this.bossDefeated&&(this.ambient||this.bossFight)&&(this.bossFight||this.hangarWave<3)&&this.phaseTime>1.5+this.hangarWave*(this.bossFight?14:5)){this.deployPolice();this.hangarWave++;}
      if(this.exitOpen&&this.playing&&distance(p,{x:800,y:256})<39){this.cleared=true;this.playing=false;this.emit('escaped',800,256,'#b5ffe5');}
      for(const m of this.mobs.slice()){
        if(m.dead)continue;const def=ROSTER[m.type];m.age+=dt;m.hit=Math.max(0,m.hit-dt);m.stun=Math.max(0,m.stun-dt);m.invuln=Math.max(0,m.invuln-dt);
        if(m.emerging>0){m.emerging-=dt;m.y-=53*dt;continue;}
        if(m.stun>0)continue;
        if(m.type===12){this.updateMotherload(m,dt);continue;}
        m.cool-=dt;m.brood-=dt;
        if(m.type===7&&m.brood<=0){m.brood=8;if(this.mobs.filter(n=>!n.dead).length<42)this.hatch(m,2,'spawn');}
        let tx=p.x,ty=p.y;
        if(!this.playing){tx=800+Math.cos(m.seed+this.t*.06)*350;ty=472+Math.sin(m.seed+this.t*.09)*190;}
        const dx=tx-m.x,dy=ty-m.y,len=Math.hypot(dx,dy)||1,a=Math.atan2(dy,dx);
        let speed=def.speed*(this.phase===1?.7:1),mx=dx/len,my=dy/len;
        if(isCop(m.type))speed=def.speed;
        if(m.type===10){m.guard=m.age%6<3;if(m.guard&&len<180)speed*=.2;}
        if(m.attack<=0&&m.charge<=0&&m.volley<=0)m.angle=a;
        if(m.type===0){
          const approach=len>290?1:len<205?-.85:.05;
          mx=dx/len*approach-dy/len*.8*Math.sin(m.seed);my=dy/len*approach+dx/len*.8*Math.sin(m.seed);
          if(m.cool<=0&&this.phase===0){m.volley=3;m.volleyTime=0;m.cool=4.4;m.aim=a;}
        }
        if((m.type===2&&len<320)||(isShip(m.type)&&len<(m.type===5?385:300))||(m.type===7&&len<220)||(m.type===11&&len<260))speed=0;
        if(m.volley>0&&(this.phase===0||isCop(m.type))){m.volleyTime-=dt;speed*=.35;if(m.volleyTime<=0){if(isShip(m.type)){const count=m.type===5?4:2,mount=count-m.volley;this.fire(m,m.aim,m.type===5?295:340,(m.type===5?[-32,32,-14,14]:[-16,16])[mount],mount);}else this.fire(m,a+(m.volley-2)*.05,250);m.volley--;m.volleyTime=isShip(m.type)?.27:.14;}}
        if(m.charge>0){m.charge-=dt;m.x+=Math.cos(m.aim)*410*dt;m.y+=Math.sin(m.aim)*410*dt;speed=0;}
        if(m.attack>0){
          m.attack-=dt;speed=0;
          if(m.attack<=0){
            if(m.type===1||m.type===9)m.charge=m.type===9?.3:.5;
            if(m.type===2)this.fire(m,m.aim,465);
            if(m.type===4){this.kill(m,'detonation');continue;}
            if(isShip(m.type)){m.volley=m.type===5?4:2;m.volleyTime=0;}
            if(m.type===11){this.fire(m,m.aim,250,m.barrel===0?11:-11,m.barrel);m.barrel=1-m.barrel;}
            if(m.type===6){this.emit('stomp',m.x,m.y,def.color,{r:128});if(distance(m,p)<128)this.hitPlayer(29);}
          }
        }else if(m.cool<=0&&m.volley<=0&&m.charge<=0){
          const canAttack=(m.type===4&&len<100)||(m.type===9&&len<340)||m.type===11||this.phase===0&&([1,2,5,8].includes(m.type)||(m.type===6&&len<153));
          if(canAttack){m.attack=m.type===4?1.5:m.type===6?1.55:1.35;m.aim=a;m.cool=m.type===5?7:m.type===1?6:5;}
        }
        m.x=clamp(m.x+mx*speed*dt,ARENA.left+m.r*.3,ARENA.right-m.r*.3);m.y=clamp(m.y+my*speed*dt,ARENA.top+m.r*.3,ARENA.bottom-m.r*.3);
        if(distance(m,p)<m.r+p.r)this.hitPlayer(isHeavy(m.type)?20:8);
      }
      for(let i=0;i<this.mobs.length;i++)for(let j=i+1;j<this.mobs.length;j++){
        const a=this.mobs[i],b=this.mobs[j];if(a.dead||b.dead)continue;const d=distance(a,b),min=a.r+b.r+5;
        if(d>.01&&d<min){const k=(min-d)*dt*.9,dx=(a.x-b.x)/d*k,dy=(a.y-b.y)/d*k;a.x+=dx;a.y+=dy;b.x-=dx;b.y-=dy;}
      }
      const hz=this.hazard();if(hz&&hz.fire){const inside=o=>o.x>hz.x-o.r*.3&&o.x<hz.x+hz.w+o.r*.3&&o.y>hz.y-o.r*.3&&o.y<hz.y+hz.h+o.r*.3;
        for(const m of this.mobs.slice())if(!m.dead&&inside(m))this.hurt(m,dt*125,'city');if(inside(p))this.hitPlayer(18);}
      for(const z of this.zones){z.timer-=dt;if(z.timer<=0){this.emit('bomb',z.x,z.y,'#ffc080',{r:z.r});if(distance(z,p)<z.r)this.hitPlayer(28);}}
      this.zones=this.zones.filter(z=>z.timer>0);
      for(const s of this.shots){
        if(this.bossDefeated||this.failed)break;
        s.x+=s.vx*dt;s.y+=s.vy*dt;s.life-=dt;
        if(s.enemy){if(distance(s,p)<15){this.hitPlayer(12);s.life=0;}}
        else for(const m of this.mobs.slice())if(!m.dead&&distance(s,m)<m.r+4){this.hurt(m,24,'weapon',Math.atan2(s.y-m.y,s.x-m.x));s.life=0;break;}
      }
      this.shots=this.shots.filter(s=>s.life>0);this.mobs=this.mobs.filter(m=>!m.dead);
      this.spawnClock+=dt;this.heavyClock+=dt;
      if(this.ambient&&this.playing&&this.spawnClock>1.3&&this.mobs.length<26){this.spawnClock=0;const n=this.random();this.spawnAmbient(n<.45?3:n<.62?0:n<.74?2:n<.84?1:n<.92?4:8);}
      if(this.ambient&&this.playing&&this.heavyClock>19&&!this.mobs.some(m=>isHeavy(m.type))){this.heavyClock=0;this.spawnAmbient([5,6,7][this.heavyIndex++%3]);}
      if(this.ambient&&!this.playing&&this.mobs.length<15&&this.spawnClock>2){this.spawnClock=0;this.spawnAmbient(this.random()<.65?3:0);}
    }
  }
  const api={Simulation,TYPES,ROSTER,PHASES,ARENA};if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.NullCitySim=api;
})(typeof globalThis!=='undefined'?globalThis:this);

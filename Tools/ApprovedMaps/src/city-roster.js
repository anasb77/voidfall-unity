export const CITY_ADDITIONS=[
 {name:'Prism Lancer',role:'SPLIT VOLLEY',color:'#90ede7',r:23,hp:120,speed:39},
 {name:'Orbit Warden',role:'ORBITAL CROSSFIRE',color:'#b8b5ff',r:27,hp:150,speed:48},
 {name:'Phase Skimmer',role:'COMMITTED DASH',color:'#f19bdf',r:24,hp:110,speed:58},
 {name:'Grav Loom',role:'DELAYED BOMBARDMENT',color:'#ffd19b',r:29,hp:190,speed:29},
 {name:'Relay Tender',role:'REPAIR SUPPORT',color:'#95d6ff',r:25,hp:130,speed:43},
 {name:'Grid Walker',role:'FOOT SOLDIER',color:'#88ded3',r:20,hp:48,speed:64,pursuer:true},
 {name:'Needle Runner',role:'LIGHT PURSUER',color:'#bacfff',r:17,hp:36,speed:88,pursuer:true},
 {name:'Arc Hound',role:'ROBOTIC HOUND',color:'#e8a4d6',r:21,hp:58,speed:74,pursuer:true},
 {name:'Ram Crawler',role:'HEAVY PURSUER',color:'#dfc697',r:27,hp:90,speed:46,pursuer:true},
 {name:'Latch Drone',role:'HOVER PURSUER',color:'#aeaef1',r:20,hp:44,speed:70,pursuer:true},
];
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v)),dist=(a,b)=>Math.hypot(a.x-b.x,a.y-b.y);
export function registerCityRoster(api){CITY_ADDITIONS.forEach((def,i)=>{api.ROSTER[13+i]={...def,code:`NULL / ${13+i}`}})}
export function chooseCityAmbient(n){
 if(n<.4)return 18+Math.min(4,Math.floor(n/.08));
 if(n<.6)return 3;
 if(n<.75)return 13+Math.min(4,Math.floor((n-.6)/.03));
 const p=(n-.75)/.25;return p<.45?3:p<.62?0:p<.74?2:p<.84?1:p<.92?4:8;
}
export function updateCityAddition(sim,m,dt){
 const def=CITY_ADDITIONS[m.type-13],p=sim.player,dx=p.x-m.x,dy=p.y-m.y,d=Math.hypot(dx,dy)||1,a=Math.atan2(dy,dx);
 const move=(angle,speed)=>{m.x=clamp(m.x+Math.cos(angle)*speed*dt,190,1410);m.y=clamp(m.y+Math.sin(angle)*speed*dt,230,736)};
 // Ordinary contact enemies: no windup, charge, projectiles, zones or death tricks.
 if(def.pursuer){
  m.angle=a;m.attack=0;m.charge=0;
  const gap=Math.max(0,d-(m.r+p.r)*.9),speed=def.speed*(sim.phase===1?.7:1);
  move(a,Math.min(speed,gap/dt));
  if(dist(m,p)<m.r+p.r)sim.hitPlayer(8);
  return;
 }
 if(sim.phase===1){m.attack=0;m.charge=0;m.repairTarget=null;m.cool=Math.max(1,m.cool);m.angle=a;move(a,def.speed*.3);return}
 m.cool-=dt;
 if(m.charge>0){m.charge=Math.max(0,m.charge-dt);m.angle=m.aim;move(m.aim,360);if(dist(m,p)<m.r+p.r+4)sim.hitPlayer(17);return}
 if(m.attack>0){m.attack-=dt;if(m.attack>0)return;
  if(m.type===13)for(const off of [-.16,0,.16])sim.fire(m,m.aim+off,230);
  if(m.type===14)for(const side of [-1,1])sim.fire(m,m.aim+side*.13,200,side*13);
  if(m.type===15)m.charge=.42;
  if(m.type===16)sim.zones.push({x:m.targetX,y:m.targetY,r:50,timer:1.25,reinforcement:true,color:def.color});
  if(m.type===17){const target=m.repairTarget;if(target&&!target.dead&&dist(m,target)<170){target.hp=Math.min(sim.roster[target.type].hp,target.hp+28);sim.emit('repair',target.x,target.y,def.color,{r:30})}m.repairTarget=null}
  return;
 }
 m.angle=a;
 if(m.type===14){move(a+Math.PI/2*(Math.sin(m.seed)>=0?1:-1),def.speed*.7);if(d>205)move(a,def.speed*.55);if(d<140)move(a,-def.speed*.6)}
 else if(m.type===15)move(a,def.speed);
 else{const range=m.type===16?260:210;if(d>range)move(a,def.speed);if(d<range-60)move(a,-def.speed*.55)}
 if(m.cool<=0&&d<460){
  if(m.type===17){m.repairTarget=sim.mobs.find(o=>o!==m&&!o.dead&&o.hp<sim.roster[o.type].hp&&dist(m,o)<150);if(!m.repairTarget){m.cool=.7;return}}
  m.aim=a;m.attack=m.type===15?.9:1.1;m.cool=[4.6,4,5.8,6.5,5][m.type-13];
  m.targetX=clamp(p.x,215,1385);m.targetY=clamp(p.y,255,711);
 }
 if(d<m.r+p.r)sim.hitPlayer(8);
}

// Same dark ceramic hulls, luminous apertures and thin colored edges as the source city.
export function drawCityAddition(g,m,x,y,t,scale=1.1){
 const def=CITY_ADDITIONS[m.type-13],c=m.hit>0?'#f5fff9':def.color,ink='#080c18',inner='#142132';
 const poly=(p,fill=ink,edge=c,w=1.8)=>{g.beginPath();p.forEach(([xx,yy],i)=>i?g.lineTo(xx,yy):g.moveTo(xx,yy));g.closePath();g.fillStyle=fill;g.fill();if(edge){g.strokeStyle=edge;g.lineWidth=w;g.stroke()}};
 const line=(x,y,xx,yy,color=c,w=1.2)=>{g.beginPath();g.moveTo(x,y);g.lineTo(xx,yy);g.strokeStyle=color;g.lineWidth=w;g.stroke()};
 const dot=(x,y,r,color='#d9faff')=>{g.beginPath();g.arc(x,y,r,0,Math.PI*2);g.fillStyle=color;g.fill()};
 const arc=(r,a,b,color=c,w=1.5)=>{g.beginPath();g.arc(0,0,r,a,b);g.strokeStyle=color;g.lineWidth=w;g.stroke()};
 g.save();g.translate(x,y);g.rotate(m.angle);g.scale(scale,scale);
 const halo=g.createRadialGradient(0,0,1,0,0,def.r*2);halo.addColorStop(0,c+'22');halo.addColorStop(1,c+'00');g.fillStyle=halo;g.fillRect(-def.r*2,-def.r*2,def.r*4,def.r*4);
 if(m.type===13){
  for(const side of [-1,1]){poly([[-23,side*9],[-8,side*25],[17,side*20],[7,side*10]],inner);poly([[3,side*13],[30,side*9],[34,side*12],[11,side*18]]);line(-23,side*12,-30-Math.sin(t*12)*2,side*12,c,2)}
  poly([[30,0],[-8,-10],[-22,0],[-8,10]]);poly([[11,0],[-5,-5],[-12,0],[-5,5]],c,null);line(14,0,36,0,'#d9ffff',1.5);
 }else if(m.type===14){
  for(let i=0;i<3;i++){const a=t*.5+i*Math.PI*2/3;g.save();g.rotate(a);arc(29,.15,1.8,c,3);poly([[26,-3],[34,-3],[36,3],[27,7]],inner);dot(30,1,1.5);g.restore()}
  poly([[15,0],[5,-12],[-12,-8],[-18,0],[-12,8],[5,12]]);arc(8,0,Math.PI*2,'#d9e0ff');dot(0,0,4,'#c8c3ff');
  for(const side of [-1,1]){line(10,side*8,22,side*14);dot(22,side*14,2)}
 }else if(m.type===15){
  poly([[18,-29],[5,-12],[0,0],[5,12],[18,29],[-8,24],[-22,10],[-25,0],[-22,-10],[-8,-24]]);
  for(const side of [-1,1]){poly([[-15,side*10],[-6,side*19],[4,side*21],[-2,side*12]],inner);line(-14,side*13,-25-(m.charge>0?26:Math.sin(t*18)*3),side*16,c+'99',2)}
  poly([[16,0],[-1,-7],[-12,0],[-1,7]],inner);line(0,-4,5,0,'#ffe6fa',2);line(5,0,0,4,'#ffe6fa',2);
 }else if(m.type===16){
  poly([[35,0],[0,-33],[-32,0],[0,33]],ink,c,2);poly([[24,0],[0,-21],[-20,0],[0,21]],inner,c,1);
  for(const side of [-1,1]){poly([[-10,side*29],[4,side*38],[14,side*29],[1,side*23]]);dot(2,side*30,2,'#ffe1b3')}
  g.save();g.rotate(-t);arc(12,0,Math.PI*1.5,c,2);g.restore();dot(0,0,m.attack>0?6:4,'#ffe6b8');line(-25,-5,-25,5);line(28,-4,28,4);
 }else if(m.type===17){
  poly([[27,0],[10,-11],[-21,-8],[-27,0],[-21,8],[10,11]],inner);poly([[15,0],[0,-5],[-12,0],[0,5]],ink);dot(1,0,3);
  for(const side of [-1,1]){const yy=side*(24+Math.sin(t*2)*2);line(-10,side*6,-6,yy,c+'77');poly([[-18,yy-6],[4,yy-8],[11,yy],[4,yy+8],[-18,yy+6]],ink);line(-11,yy,3,yy,'#d4f0ff',2);line(-4,yy-4,-4,yy+4,'#d4f0ff',2);dot(-19,yy,1.5)}
  line(-24,-3,-33,-3,c,1.5);line(-24,3,-33,3,c,1.5);
 }else if(m.type===18){
  // Biped: squared shoulder plates, swinging arms and two armored boots.
  const stride=Math.sin(t*8+m.seed)*3;
  for(const s of [-1,1]){
   poly([[-11,s*5],[-23+stride*s,s*7],[-26+stride*s,s*14],[-15,s*14],[-6,s*9]],inner);
   poly([[8,s*12],[5,s*23],[-8-stride*s,s*24],[-10-stride*s,s*18],[-1,s*14]]);
   poly([[11,s*8],[14,s*16],[3,s*18],[-1,s*10]],inner);line(-21+stride*s,s*8,-20+stride*s,s*12,c,1);
  }
  poly([[12,-8],[13,8],[-4,12],[-15,6],[-15,-6],[-4,-12]],inner);
  poly([[8,-5],[8,5],[-4,7],[-7,0],[-4,-7]],ink);line(3,-4,3,4,c,2);
  poly([[21,-7],[24,-4],[24,4],[21,7],[13,6],[12,-6]]);line(20,-4,20,4,'#ddfff8',2);
 }else if(m.type===19){
  // Narrow legged runner; long split shins rather than a gun-shaped nose.
  const stride=Math.sin(t*13+m.seed)*5;
  for(const s of [-1,1]){
   line(-7,s*5,-17+stride*s,s*14,c,2);poly([[-17+stride*s,s*12],[-28+stride*s,s*17],[-26+stride*s,s*22],[-12+stride*s,s*16]],inner);
   poly([[7,s*7],[-1,s*15],[-8,s*13],[2,s*5]]);line(-4,s*13,-9-stride*s,s*17,c);
  }
  poly([[17,0],[4,-8],[-11,-5],[-16,0],[-11,5],[4,8]],inner);poly([[8,0],[-1,-4],[-7,0],[-1,4]],ink);
  line(11,-3,13,0,'#ebf0ff',2);line(13,0,11,3,'#ebf0ff',2);line(-11,0,-19,0,c);
 }else if(m.type===20){
  // Four piston legs, a long back and a blunt sensor muzzle.
  for(const s of [-1,1])for(const fore of [-1,1]){const step=Math.sin(t*10+m.seed+(s*fore>0?0:Math.PI))*3,x=fore*10;
   line(x,s*7,x-6+step,s*18,c,2);poly([[x-6+step,s*16],[x+4+step,s*22],[x+9+step,s*20],[x,s*13]],inner);dot(x,s*8,2,c);
  }
  poly([[14,-8],[15,8],[-15,10],[-22,4],[-22,-4],[-15,-10]],inner);line(-13,0,7,0,c,1.5);
  for(let i=0;i<3;i++)line(-10+i*6,-6,-10+i*6,6,c+'77');
  poly([[12,-8],[23,-6],[28,0],[23,6],[12,8],[16,0]]);line(23,-3,23,3,'#ffe1f5',2);
  poly([[14,-7],[9,-14],[20,-10]],ink);poly([[14,7],[9,14],[20,10]],ink);line(-20,0,-29,-5,c,2);
 }else if(m.type===21){
  // Broad tracked ram: short armored bumper, no cannon or charging behavior.
  for(const s of [-1,1]){
   poly([[-23,s*12],[-27,s*18],[-19,s*25],[15,s*25],[21,s*18],[15,s*12]],inner);
   for(let j=0;j<5;j++){const x=-18+j*7;line(x,s*16,x+2,s*22,c+'aa')}
  }
  poly([[19,-11],[24,0],[19,11],[-15,14],[-23,7],[-23,-7],[-15,-14]],inner);
  poly([[12,-8],[16,0],[12,8],[-8,8],[-14,0],[-8,-8]],ink);dot(1,0,4,'#f7e3bc');
  poly([[20,-15],[27,-11],[29,0],[27,11],[20,15],[22,0]],ink);line(25,-7,26,7,c,2);
 }else if(m.type===22){
  // Compact hovering puck with five passive grabbing vanes.
  g.save();g.rotate(t*.45+m.seed);for(let i=0;i<5;i++){g.rotate(Math.PI*2/5);poly([[9,-5],[21,-8],[28,-2],[24,7],[20,2],[12,5]],inner);line(19,-4,23,-1,c,1)}g.restore();
  arc(12,0,Math.PI*2,c,2);poly([[8,0],[3,-7],[-6,-5],[-9,0],[-6,5],[3,7]],ink);dot(2,0,3,'#e7e5ff');
 }
 g.restore();
}

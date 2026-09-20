// Additional native line-art inhabitants; same palette and dark interiors as Hydra.
import {INSECT_SCALE} from './insect-rules.js';
export const INSECTS=['Needlewasp','Hookmantis','Blisterbeetle','Sawroach','Mourningmoth'];
const TAU=Math.PI*2;
function poly(g,p,fill='#101a12',edge='#99a66d',lw=1.8){g.beginPath();for(let i=0;i<p.length;i+=2)i?g.lineTo(p[i],p[i+1]):g.moveTo(p[i],p[i+1]);g.closePath();if(fill){g.fillStyle=fill;g.fill()}if(edge){g.strokeStyle=edge;g.lineWidth=lw;g.stroke()}}
function stroke(g,p,color='#879967',w=1.6){g.beginPath();for(let i=0;i<p.length;i+=2)i?g.lineTo(p[i],p[i+1]):g.moveTo(p[i],p[i+1]);g.strokeStyle=color;g.lineWidth=w;g.stroke()}
function eye(g,x,y,r=2,c='#e0d396'){g.beginPath();g.arc(x,y,r,0,TAU);g.fillStyle=c;g.fill()}
export function drawInsect(g,e,t){
 const kind=e.kind||0,boss=e.guardian,size=boss?2.5:INSECT_SCALE,edge=boss?'#c1bb80':['#a0ac73','#91a887','#b0ad79','#9da183','#a3ada0'][kind];
 g.save();g.translate(e.x,e.y);g.rotate(e.angle+Math.PI/2);g.scale(size,size);
 const f=e.hit>0?'#7a8860':'#101911',stride=e.wind>0?0:Math.sin(t*(boss?8:15)+e.phase);
 // Six articulated legs; mantis forelegs become catching scythes.
 for(const side of [-1,1]){g.save();g.scale(side,1);for(let j=kind===1?1:0;j<3;j++){
  const y=-12+j*15,sway=Math.sin(t*14+e.phase+j*2)*4,reach=(kind===3?34:kind===4?21:27)+(j===1?7:0);
  poly(g,[8,y,reach*.68,y-8+sway,reach,y-20+sway,reach+5,y-18+sway,reach*.75,y+2,13,y+5],f,edge,1.4);
  stroke(g,[reach,y-20+sway,reach+3,y-25+sway,reach-2,y-22+sway],edge,1.2);
 }g.restore()}
 if(kind===0){
  // Narrow waist, transparent blade wings, long sting.
  for(const side of [-1,1]){g.save();g.scale(side,1);const flap=Math.sin(t*24+e.phase)*3;
   poly(g,[6,-15,25,-31,45,-19+flap,37,0,10,15,5,2],'#1b2e252c',edge+'aa',1.1);
   stroke(g,[8,-10,37,-19,21,0,8,10],edge+'77',.9);g.restore()}
  poly(g,[0,4,-9,15,-7,29,0,48,7,29,9,15],f,edge);
  for(let j=0;j<3;j++)stroke(g,[-7+j,17+j*6,0,20+j*6,7-j,17+j*6],edge,1);
  poly(g,[-2,37,0,59,3,37],f,edge,1);
 }else if(kind===1){
  // Lean abdomen and oversized serrated catching arms.
  poly(g,[0,-12,-7,6,-7,28,0,45,7,28,7,6],f,edge);
  for(let j=0;j<4;j++)stroke(g,[-6,7+j*7,0,10+j*7,6,7+j*7],edge,1);
  for(const side of [-1,1]){g.save();g.scale(side,1);poly(g,[8,-11,28,-29,38,-57,32,-69,24,-40,15,-31,22,-50,16,-44,12,-23],f,edge,1.8);
   for(let j=0;j<3;j++)poly(g,[24-j*3,-41+j*6,17-j*2,-43+j*6,22-j*3,-35+j*6],f,edge,1);g.restore()}
 }else if(kind===2){
  // Distended explosive sacs; the guardian has sealed armored plates.
  poly(g,[0,-18,-21,-7,-26,14,-19,34,0,43,19,34,26,14,21,-7],f,edge,2);
  for(const side of [-1,1]){g.save();g.scale(side,1);for(let j=0;j<3;j++){
   poly(g,[3,-2+j*11,16,-9+j*11,22,1+j*11,17,12+j*11,3,9+j*11],boss?'#1a2015':e.fuse!=null?'#5c3821':'#302b1a',edge,1);
   if(!boss)eye(g,12,3+j*11,2,e.fuse!=null?'#ffda8c':'#b99b61');
   else poly(g,[21,j*10,32,5+j*10,22,10+j*10],f,edge,1);
  }g.restore()}stroke(g,[0,-12,0,39],edge,1.5);
 }else if(kind===3){
  // Broad, low shield with saw teeth and oversized mandibles.
  poly(g,[-13,-25,-25,-12,-30,5,-26,24,-17,34,0,37,17,34,26,24,30,5,25,-12,13,-25],f,edge,2);
  for(const side of [-1,1]){g.save();g.scale(side,1);for(let j=0;j<4;j++)poly(g,[23,-10+j*10,35,-7+j*10,27,-2+j*10],f,edge,1);
   stroke(g,[4,-18,18,-9,23,9,17,26,3,33],edge,1);g.restore()}stroke(g,[0,-19,0,32],edge,1.2);
 }else{
  // Ragged wing lobes and feathered feelers.
  for(const side of [-1,1]){g.save();g.scale(side,1);const flap=Math.sin(t*12+e.phase)*2;
   poly(g,[5,-17,29,-36,49,-25+flap,43,-15,50,-5,39,1,44,10,30,16,40,27,28,30,26,43,15,35,7,21],'#1b252180',edge,1.3);
   stroke(g,[7,-11,34,-23,21,5,40,8,17,17,25,34,7,13],edge+'88',.9);
   poly(g,[22,-13,28,-17,34,-12,27,-6],'#070d09',edge,1);g.restore()}
  poly(g,[0,-14,-7,0,-6,20,0,35,6,20,7,0],f,edge,1.6);
  for(let j=0;j<3;j++)stroke(g,[-5,5+j*7,5,5+j*7],edge,1);
 }
 if(kind!==3)poly(g,[-7,-24,-11,-12,-6,0,0,5,6,0,11,-12,7,-24],f,edge,1.8);
 const head=kind===1?17:kind===3?13:10;
 poly(g,[-head,-30,-7,-39,7,-39,head,-30,5,-20,-5,-20],f,edge,1.8);
 for(const side of [-1,1]){g.save();g.scale(side,1);eye(g,kind===1?11:6,-30,boss?3:2.4);
  stroke(g,[7,-36,17,-48+stride,14,-58+stride,21,-64+stride],edge,1.2);
  if(kind===4)for(let j=0;j<4;j++)stroke(g,[15,-45-j*4,22,-44-j*4,16,-48-j*4],edge,1);
  if(kind===3)poly(g,[8,-34,22,-46,19,-58,10,-46,13,-49,10,-37],f,edge,1.8);
  else poly(g,[4,-38,7,-49,14,-51,9,-43,7,-34],f,edge,1.2);
  g.restore()}
 if(boss){stroke(g,[-6,-18,0,-11,6,-18,0,-4,-6,-18],'#d7ce9b',1.5);for(let j=0;j<3;j++)eye(g,0,8+j*8,2.3,'#d8c891')}
 g.restore();
}

export function drawHive(g,h,t){
 g.save();g.translate(h.x,h.y);const pulse=1+Math.sin(t*2+h.phase)*.018;g.scale(pulse,pulse);g.rotate(h.angle);
 const edge=h.hit>0?'#eee5ba':'#697651',body=h.dead?'#070b09':'#0b130e';
 // Jagged anchored limbs frame a cluster of chitin chambers, not a beehive icon.
 for(let i=0;i<7;i++){g.save();g.rotate(i*TAU/7+.1);poly(g,[-10,-28,-24,-66,-48,-91,-37,-93,-12,-78,7,-42],body,edge,1.5);stroke(g,[-19,-60,-15,-81,-25,-100],edge+'99',1);g.restore()}
 const sacs=[[-27,-30,25,37],[23,-31,29,34],[-38,12,24,35],[32,16,29,42],[-8,35,31,39],[0,-4,34,43]];
 for(let i=0;i<sacs.length;i++){const [x,y,rx,ry]=sacs[i];g.save();g.translate(x,y);g.rotate((i-2)*.19);poly(g,[0,-ry,-rx*.7,-ry*.7,-rx,-ry*.1,-rx*.8,ry*.6,-rx*.25,ry,rx*.4,ry*.9,rx*.9,ry*.4,rx,-ry*.4,rx*.6,-ry*.9],body,edge,1.6);for(let j=0;j<4;j++)stroke(g,[-rx*.7,-ry*.5+j*ry*.35,0,-ry*.32+j*ry*.35,rx*.7,-ry*.5+j*ry*.35],edge+'70',1);g.restore()}
 poly(g,[-13,-30,-23,-11,-19,13,-7,29,12,24,22,7,17,-18,4,-31],'#030906',edge,2);
 for(const side of [-1,1])for(let j=0;j<4;j++)poly(g,[side*18,-16+j*10,side*9,-12+j*10,side*18,-7+j*10],'#111b10',edge,.8);
 if(!h.dead){for(let i=0;i<5;i++){const x=Math.sin(i*4.1)*12,y=-15+i*7;eye(g,x,y,2.3,'#a9b479');stroke(g,[x-3,y+2,x-7,y+7,x-5,y+12],'#627a4d',.7)}
  const q=1-h.clock/3;g.strokeStyle='#d0d9a166';g.lineWidth=2;g.beginPath();g.arc(0,0,81,-Math.PI/2,-Math.PI/2+TAU*q);g.stroke()
 }else{stroke(g,[-24,-36,-3,-16,-15,7,13,29,2,62],'#060906',8)}g.restore();
 if(!h.dead&&!h.suppressUI){g.fillStyle='#080e0b';g.fillRect(h.x-45,h.y-115,90,4);g.fillStyle='#9aaa73';g.fillRect(h.x-45,h.y-115,90*Math.max(0,h.hp/h.maxHp),4);g.font='6px Chakra';g.textAlign='center';g.fillStyle='#a5b98d';g.fillText(`HYDRA HIVE ${String(h.id+1).padStart(2,'0')} · ${Math.max(0,h.clock).toFixed(1)}s`,h.x,h.y-125)}
}

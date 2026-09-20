// Cosmetic only: the simulation owns the snapshotted cells and damage instant.
const TAU=Math.PI*2,clamp=v=>Math.max(0,Math.min(1,v));
export function drawCourtCellStrike(g,x,y,size,phase,white,reducedMotion){
 const warning=phase.stage==='warning',burst=phase.stage==='burst',elapsed=phase.age-3;
 if(!warning&&!burst&&(elapsed<0||elapsed>1.45))return;
 const seed=Math.abs(Math.round(x/size)*37+Math.round(y/size)*61);
 g.save();g.beginPath();g.rect(x+2,y+2,size-4,size-4);g.clip();g.translate(x+size/2,y+size/2);
 const edge=size/2-6;
 if(warning){
  const charge=clamp(phase.age/3),pulse=reducedMotion?1:.5+.5*Math.cos(phase.age*TAU*2);
  // Dark rust on white stone; brighter embers on black. A filled warning remains
  // readable throughout the pulse, so safe cells never resemble armed cells.
  g.fillStyle=`rgba(160,44,22,${.12+charge*.2+pulse*.06})`;g.fillRect(-edge,-edge,edge*2,edge*2);
  g.strokeStyle=white?'#943b22':'#ffad69';g.lineWidth=2+charge*2+pulse;g.strokeRect(-edge,-edge,edge*2,edge*2);
  const tremor=reducedMotion?0:Math.sin(phase.age*43+seed)*charge*charge*1.6;
  g.translate(tremor,-tremor*.6);
  for(let i=0;i<4;i++){
   g.save();g.rotate(i*TAU/4);g.beginPath();g.moveTo(-edge+5,-edge+22);g.lineTo(-edge+5,-edge+5);g.lineTo(-edge+22,-edge+5);g.stroke();
   // The fractures fill inward as the square charges.
   g.beginPath();g.moveTo(-edge+7,0);g.lineTo(-edge+18,5);g.lineTo(-edge+25,-4);g.lineTo(-edge+27+charge*20,1);g.lineWidth=1.5+charge;g.stroke();g.restore();
  }
  g.strokeStyle=white?'#57291d':'#ffcd8d';g.lineWidth=2;
  const radius=31-charge*12;
  for(let i=0;i<4;i++){const a=i*TAU/4+.2;g.beginPath();g.arc(0,0,radius,a,a+1.08);g.stroke()}
  g.beginPath();g.moveTo(-7,0);g.lineTo(7,0);g.moveTo(0,-7);g.lineTo(0,7);g.stroke();
  g.restore();return;
 }
 const progress=clamp(elapsed/.45),tail=clamp(1-(elapsed-.45));
 if(burst){
  const impact=reducedMotion?.36:Math.exp(-elapsed*14);
  g.fillStyle='#301717';g.fillRect(-edge,-edge,edge*2,edge*2);
  const glow=g.createRadialGradient(0,0,1,0,0,size*.73);
  glow.addColorStop(0,`rgba(255,245,201,${.7+impact*.3})`);glow.addColorStop(.35,`rgba(255,156,64,${.68+impact*.3})`);glow.addColorStop(1,'#b4311740');
  g.fillStyle=glow;g.fillRect(-edge,-edge,edge*2,edge*2);
  g.strokeStyle='#ffda94';g.lineWidth=5*(1-progress)+2;g.strokeRect(-edge,-edge,edge*2,edge*2);
  if(!reducedMotion){g.fillStyle=`rgba(255,248,216,${impact*.85})`;g.fillRect(-edge,-edge,edge*2,edge*2)}
  // Square pressure front and jagged fissures sell an impact through the slab.
  const front=8+(edge-8)*Math.sqrt(progress);g.strokeStyle=`rgba(255,244,192,${1-progress*.7})`;g.lineWidth=4;g.strokeRect(-front,-front,front*2,front*2);
 }else{
  g.fillStyle=`rgba(30,17,15,${tail*.42})`;g.fillRect(-edge+1,-edge+1,edge*2-2,edge*2-2);
 }
 const shake=reducedMotion?0:Math.sin(elapsed*82+seed)*Math.exp(-elapsed*7)*4;
 g.translate(shake,-shake*.65);
 for(let i=0;i<8;i++){
  const a=i*TAU/8+(seed%11)*.025,cs=Math.cos(a),sn=Math.sin(a),length=24+((seed+i*17)%29);
  g.beginPath();g.moveTo(cs*7,sn*7);g.lineTo(cs*length*.55-sn*6,sn*length*.55+cs*6);g.lineTo(cs*length,sn*length);
  g.strokeStyle=burst?'#ffd49a':`rgba(121,71,43,${tail*.65})`;g.lineWidth=burst?3:1.6;g.stroke();
 }
 if(!reducedMotion){
  for(let i=0;i<14;i++){
   const a=i*2.399+seed,velocity=35+(seed+i*13)%48;
   const distance=8+elapsed*velocity,px=Math.cos(a)*distance,py=Math.sin(a)*distance-elapsed*36+elapsed*elapsed*38;
   const alpha=clamp(1-elapsed/(.55+(i%4)*.2));if(alpha<=0)continue;
   g.save();g.translate(px,py);g.rotate(a+elapsed*(i%2?3:-3));g.globalAlpha=alpha;
   g.fillStyle=i%3===0?(white?'#d6d1c1':'#242c33'):'#ffd38a';g.strokeStyle='#211a15';g.lineWidth=.7;
   g.beginPath();g.moveTo(-3-i%3,-2);g.lineTo(3,-4-i%2);g.lineTo(5,2);g.lineTo(-2,4);g.closePath();g.fill();if(i%3===0)g.stroke();g.restore();
  }
 }
 g.restore();
}

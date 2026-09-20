import {sentinelCells} from './court-sentinel-rules.js';
import {drawCourtCellStrike} from './court-cell-fx.js';
import {drawOriginalFamily,drawOriginalSentinel,drawArmoredKnight} from './court-original-art.js';
import {CELL,BOARD_HALF,PIECES,TIER_NAMES,pieceStats} from './court-rules.js';
const TAU=Math.PI*2;
function line(g,x,y,xx,yy,c,w=1){g.beginPath();g.moveTo(x,y);g.lineTo(xx,yy);g.strokeStyle=c;g.lineWidth=w;g.stroke()}
function poly(g,points,fill,edge,width=1.5){g.beginPath();points.forEach(([x,y],i)=>i?g.lineTo(x,y):g.moveTo(x,y));g.closePath();if(fill){g.fillStyle=fill;g.fill()}if(edge){g.strokeStyle=edge;g.lineWidth=width;g.stroke()}}
function ring(g,x,y,r,c,w=1){g.beginPath();g.arc(x,y,r,0,TAU);g.strokeStyle=c;g.lineWidth=w;g.stroke()}
function disc(g,x,y,r,c){g.beginPath();g.arc(x,y,r,0,TAU);g.fillStyle=c;g.fill()}
export function drawCourtPiece(g,e,time=0,scale=1){
 const tier=e.type===1?Math.max(0,(e.tier||0)-1):(e.tier||0),white=e.white,edge=white?'#f0eee4':'#aab2be',body=white?'#d4d4c9':'#0b0e13',detail=white?'#242933':'#cad0d8',secondary=white?'#969b9b':'#475364',r=e.r||pieceStats(e.type,tier).r;
 g.save();g.translate(e.x,e.y);if(e.type===3||e.type===2)g.rotate(e.angle||0);g.scale(r*scale,r*scale);
 const shape=(p,fill=body,stroke=edge,w=.055)=>poly(g,p,fill,stroke,w);
 // Restore source-native families; Rooks II/III retain the preceding castle designs.
 if(e.type===5){g.save();if(Math.cos(e.angle||0)<0)g.scale(-1,1);drawArmoredKnight(g,e.tier||0,white,e.blockFlash||0,time);g.restore()}else if(e.type===0||e.type===2||e.type===4||(e.type===1&&!e.tier)){
  drawOriginalFamily(g,e.type,e.tier||0,white,time);
 }else if(e.type===1){
  if(tier>0)for(const s of [-1,1])shape([[s*.6,-.65],[s*1.12,-.82],[s*1.22,.67],[s*.74,.9],[s*.77,-.12]],body,edge);
  shape([[-.87,-.8],[-.43,-.8],[-.43,-.5],[-.18,-.5],[-.18,-.98],[.18,-.98],[.18,-.5],[.43,-.5],[.43,-.8],[.87,-.8],[.8,.43],[1,.75],[.58,.94],[-.58,.94],[-1,.75],[-.8,.43]]);
  shape([[-.53,-.2],[.53,-.2],[.48,.49],[0,.7],[-.48,.49]],body,detail,.045);line(g,-.3,.13,.3,.13,detail,.12);
  if(tier>0){line(g,-.67,.69,.67,.69,detail,.04);for(const s of [-1,1])line(g,s*.94,-.4,s*.98,.34,secondary,.04)}
  if(tier===2){shape([[-.32,-.95],[-.29,-1.28],[.29,-1.28],[.32,-.95]],body,edge);disc(g,0,-1.08,.08,detail);shape([[-.24,.48],[0,.28],[.24,.48],[0,.66]],detail,null)}
  if(e.tier===3){for(const side of [-1,1]){line(g,side*1.36,-.64,side*1.42,.38,'#bba779',.075);shape([[side*1.3,.5],[side*1.52,.57],[side*1.23,.93]],body,'#bba779',.05)}}
 }else if(e.type===3){
  if(tier>0){shape([[-.65,-.62],[-1.04,-.91],[-1.13,-.28],[-.98,.3],[-.61,.68],[-.47,.27]],body,edge);shape([[-.35,.22],[.48,.24],[.88,.86],[-.65,.98],[-.87,.61]],body,edge)}
  shape([[-.65,.81],[-.44,.13],[-.61,-.45],[-.18,-1.08],[.1,-.83],[.5,-.74],[.92,-.27],[.62,-.04],[.13,-.24],[.34,.19],[.67,.7],[.29,.94],[-.47,.97]]);
  shape([[-.16,-.66],[.26,-.59],[.54,-.29],[.04,-.36]],body,detail,.04);disc(g,.17,-.52,.08,detail);line(g,-.29,.2,.13,.55,detail,.065);
  if(tier>0)for(let i=0;i<3;i++)line(g,-.72+i*.04,-.45+i*.23,-.92+i*.1,-.36+i*.23,detail,.04);
  if(tier===2){shape([[-.14,-.99],[-.36,-1.44],[.1,-1.16],[.26,-.79]],body,edge);shape([[.25,.41],[.69,.38],[1.04,.75],[.56,.79]],body,edge);line(g,-.47,.76,.39,.75,detail,.04)}
 }

 if(e.hit>0){g.globalAlpha=.3;disc(g,0,0,1.1,'#fff');g.globalAlpha=1}
 g.restore();
}
export function drawCourt(g,sim,time,bounds,reducedMotion=false){
 g.fillStyle='#080b11';g.fillRect(bounds.left,bounds.top,bounds.right-bounds.left,bounds.bottom-bounds.top);
 const cells=new Map();
 for(const c of sim.covers.filter(c=>!c.dead))for(const cell of sentinelCells(c)){
  const key=`${cell.x},${cell.y}`,old=cells.get(key),rank=p=>p.stage==='burst'?3:p.stage==='warning'?2:p.age<4.45?1:0;
  const phase=c.phase||{stage:'rest',age:100};if(!old||rank(phase)>rank(old))cells.set(key,phase);
 }
 for(let ix=Math.max(-14,Math.floor(bounds.left/CELL));ix<=Math.min(13,Math.floor(bounds.right/CELL));ix++)for(let iy=Math.max(-14,Math.floor(bounds.top/CELL));iy<=Math.min(13,Math.floor(bounds.bottom/CELL));iy++){
  const x=ix*CELL,y=iy*CELL,white=((ix+iy)%2+2)%2===0,active=cells.has(`${ix},${iy}`),phase=cells.get(`${ix},${iy}`)||{stage:'rest',age:100},warning=active&&phase.stage==='warning',burst=active&&phase.stage==='burst';
  const charge=warning?Math.min(1,phase.age/3):0,inset=burst?5:warning?2+charge*2:2;
  if(warning||burst){g.shadowColor=burst?'#e26755':'#bd6e58';g.shadowBlur=burst?15:charge*8;g.fillStyle=burst?'#bc6551':`rgba(156,90,72,${charge*.65})`;g.fillRect(x,y,CELL,CELL);g.shadowBlur=0}
  const floor=g.createLinearGradient(x,y,x+CELL,y+CELL);floor.addColorStop(0,white?'#d1d1c9':'#20262c');floor.addColorStop(.52,white?'#c3c5bf':'#161d24');floor.addColorStop(1,white?'#b3b8b4':'#0f171e');g.fillStyle=floor;g.fillRect(x+inset,y+inset,CELL-inset*2,CELL-inset*2);
  line(g,x+inset,y+inset,x+CELL-inset,y+inset,white?'#f2f0dc80':'#8b989d45');line(g,x+CELL-inset,y+inset,x+CELL-inset,y+CELL-inset,'#0006',2);
  // Quiet stone grain, wear and inset seams give the fixed board material depth.
  g.save();g.beginPath();g.rect(x+inset+1,y+inset+1,CELL-inset*2-2,CELL-inset*2-2);g.clip();
  const seed=Math.abs(ix*31+iy*17),k=seed%7;
  for(let j=0;j<6;j++){const yy=y+14+((seed*19+j*23)%104);line(g,x+8,yy,x+CELL-8,yy+Math.sin(seed+j)*5,white?'#56646a08':'#cdd5ce06',.6)}
  if(k<3){const xx=x+22+(seed%51);line(g,xx,y+17,xx+18,y+42,white?'#68757438':'#afb3a82b',.8);line(g,xx+18,y+42,xx+12,y+56,white?'#68757428':'#afb3a822',.7)}
  g.strokeStyle=white?'#737e791c':'#91a3a21b';g.lineWidth=.7;g.strokeRect(x+9,y+9,CELL-18,CELL-18);
  // Slow, low-opacity light inside the joints only; never pan or rotate the ground.
  const breathe=reducedMotion?.035:.035+.025*Math.sin(time*.65+ix*.32+iy*.23);
  line(g,x+12,y+CELL-1,x+CELL-12,y+CELL-1,`rgba(180,206,200,${breathe})`,1.3);g.restore();
  if(active)drawCourtCellStrike(g,x,y,CELL,phase,white,reducedMotion);
 }
 g.strokeStyle='#858981';g.lineWidth=8;g.strokeRect(-BOARD_HALF,-BOARD_HALF,BOARD_HALF*2,BOARD_HALF*2);g.strokeStyle='#333e48';g.lineWidth=2;g.strokeRect(-BOARD_HALF-18,-BOARD_HALF-18,BOARD_HALF*2+36,BOARD_HALF*2+36);
 // A quiet square perimeter makes the same attack/shield territory readable at rest.
 for(const c of sim.covers.filter(c=>!c.dead)){
  const area=sentinelCells(c),left=area[0].x*CELL,top=area[0].y*CELL;
  g.save();g.strokeStyle='#9ecfc08c';g.lineWidth=1.5;g.setLineDash([9,9]);g.strokeRect(left+2,top+2,CELL*4-4,CELL*4-4);g.setLineDash([]);
  g.fillStyle='#9ecfc009';g.fillRect(left+3,top+3,CELL*4-6,CELL*4-6);g.restore();
 }
 for(const c of sim.covers){
  g.save();g.translate(c.x,c.y);g.scale(1,.4);disc(g,0,110,86,'#0005');g.restore();
  drawOriginalSentinel(g,c,sim.player,time,reducedMotion);if(c.dead)continue;
  g.font='8px Chakra';g.textAlign='center';g.fillStyle='#a9afac';g.fillText('SENTINEL',c.x,c.y+92);
  if(!c.dead){g.fillStyle='#0b1015';g.fillRect(c.x-38,c.y+103,76,3);g.fillStyle='#bbb7a5';g.fillRect(c.x-38,c.y+103,76*c.hp/c.maxHp,3)}
 }
 for(const e of sim.enemies){
  if(e.x<bounds.left-200||e.x>bounds.right+200||e.y<bounds.top-200||e.y>bounds.bottom+200)continue;
  if(['warn','dash','corner'].includes(e.state)&&e.path){g.save();g.strokeStyle=e.state==='warn'?'#d8b99999':'#ebdbba66';g.lineWidth=e.r*1.6;g.lineJoin='miter';g.beginPath();e.path.forEach((p,i)=>i?g.lineTo(p.x,p.y):g.moveTo(p.x,p.y));g.globalAlpha=.13;g.stroke();g.globalAlpha=1;g.lineWidth=1.5;g.setLineDash([7,6]);g.stroke();g.setLineDash([]);for(let i=1;i<e.path.length;i++){ring(g,e.path[i].x,e.path[i].y,7,'#d7c5a4',1.2)}g.restore()}
  if(e.state==='warn'&&e.type===2){const angles=e.tier===2?[e.aim,e.aim+Math.PI/2]:[e.aim];for(const a of angles)line(g,e.x,e.y,e.x+Math.cos(a)*400,e.y+Math.sin(a)*400,'#c5bda45a',1.3)}
  if(e.state==='promote')for(const p of e.targets){if(p.dead)continue;g.save();g.setLineDash([5,9]);line(g,e.x,e.y,p.x,p.y,'#dccfaa',1.2);g.restore();const q=1-e.timer/2.7;g.strokeStyle='#f0dfb9';g.lineWidth=2;g.beginPath();g.arc(p.x,p.y,p.r+14,-Math.PI/2,-Math.PI/2+TAU*q);g.stroke();poly(g,[[p.x-9,p.y-p.r-18],[p.x-11,p.y-p.r-29],[p.x-4,p.y-p.r-24],[p.x,p.y-p.r-33],[p.x+4,p.y-p.r-24],[p.x+11,p.y-p.r-29],[p.x+9,p.y-p.r-18]],'#d6c59a55','#d6c59a',1)}
  if(e.sentinelShield>0){g.save();g.translate(e.x,e.y);g.strokeStyle=e.shieldFlash>0?'#e1fff5':'#90cbbb';g.lineWidth=e.shieldFlash>0?3:1.4;g.fillStyle='#9ae8cd0c';const r=e.r+8;g.beginPath();for(let i=0;i<6;i++){const a=i*TAU/6;const x=Math.cos(a)*r,y=Math.sin(a)*r;i?g.lineTo(x,y):g.moveTo(x,y)}g.closePath();g.fill();g.stroke();g.restore()}
  if(e.ward>0)ring(g,e.x,e.y,e.r+9,'#e5d2a1',1.5);disc(g,e.x+2,e.y+5,e.r+3,'#070a10aa');g.save();g.shadowColor='#02060ca0';g.shadowBlur=4;drawCourtPiece(g,e,reducedMotion?0:sim.time);g.restore();
  if(e.tier>0){for(let i=0;i<=e.tier;i++)disc(g,e.x+(i-e.tier/2)*6,e.y+e.r+8,1.5,'#cbc9bb')}
  if(e.type===4||e.hit>0){g.fillStyle='#090e13';g.fillRect(e.x-23,e.y-e.r-17,46,3);g.fillStyle='#cfc7ad';g.fillRect(e.x-23,e.y-e.r-17,46*Math.max(0,e.hp/e.maxHp),3)}
 }
 for(const b of sim.shots)line(g,b.x,b.y,b.x-b.vx*.025,b.y-b.vy*.025,b.enemy?'#e7bda0':'#9ce8fa',b.enemy?3:2);
 for(const effect of sim.effects){g.save();g.globalAlpha=effect.life/effect.max;ring(g,effect.x,effect.y,effect.r*(1-effect.life/effect.max*.5),effect.color,2);g.restore()}
 for(const orb of sim.pickups){ring(g,orb.x,orb.y,17,'#a8e3d8',1.5);line(g,orb.x-7,orb.y,orb.x+7,orb.y,'#baffee',2);line(g,orb.x,orb.y-7,orb.x,orb.y+7,'#baffee',2)}
 if(sim.focusQueen){const q=sim.enemies.filter(e=>e.type===4).sort((a,b)=>Math.hypot(a.x-sim.player.x,a.y-sim.player.y)-Math.hypot(b.x-sim.player.x,b.y-sim.player.y))[0];if(q)ring(g,q.x,q.y,q.r+16,'#e3d4a6',1.5)}
}
export function drawCourtLineup(g,width,height){
 g.fillStyle='#0c1119';g.fillRect(0,0,width,height);const col=width/6,row=(height-360)/3;
 g.font='28px Chakra';g.fillStyle='#e6e4d9';g.textAlign='left';g.fillText('MONOCHROME COURT / ORIGINAL LINEAGE',40,49);g.font='14px Chakra';g.fillStyle='#949fa7';g.fillText('Six families in black and white · Armored Knights pursue and can block incoming hits.',40,78);
 for(let tier=0;tier<3;tier++)for(let type=0;type<6;type++){
  const x=col*(type+.5),y=110+row*tier+row*.39,e={x,y,type,tier,white:true,angle:type===3?-.1:0,...pieceStats(type,tier)};
  drawCourtPiece(g,e,0,1.6);g.textAlign='center';g.fillStyle='#e0ddd0';g.font='18px Chakra';g.fillText(`${PIECES[type]} ${['I','II','III'][tier]}`,x,y+(type===5?138:112));g.fillStyle='#8b9aab';g.font='12px Chakra';g.fillText(TIER_NAMES[tier].toUpperCase(),x,y+(type===5?161:136));
  // Black counterpart at true relative scale, beside each larger inspection rendering.
  drawCourtPiece(g,{...e,x:x+col*.36,y:y+(type===5?69:27),white:false},0,type===5?.4:.65);
 }
 const y=height-112;
 drawCourtPiece(g,{x:160,y:y-15,type:1,tier:3,white:true,...pieceStats(1,3)},0,1.35);
 drawCourtPiece(g,{x:290,y:y+5,type:1,tier:3,white:false,...pieceStats(1,3)},0,.75);
 g.textAlign='left';g.font='19px Chakra';g.fillStyle='#e0ddd0';g.fillText('ROOK ELITE',360,y-35);g.font='14px Chakra';g.fillStyle='#8b9aab';g.fillText('Fourth form · heavier armor · longer warning',360,y-7);
 drawOriginalSentinel(g,{x:1080,y:y-30,r:58,white:true,mouth:0},{x:1100,y:y});
 drawOriginalSentinel(g,{x:1240,y:y-30,r:58,white:false,mouth:1},{x:1100,y:y});
 drawOriginalSentinel(g,{x:1420,y:y-30,r:58,white:true,mouth:2},{x:1100,y:y});
 g.fillStyle='#e0ddd0';g.font='18px Chakra';g.fillText('SENTINEL / THREE GRINS',1610,y-35);g.fillStyle='#8b9aab';g.font='14px Chakra';g.fillText('Shared eye · 4 × 4 attack + shield territory',1610,y-7);

}

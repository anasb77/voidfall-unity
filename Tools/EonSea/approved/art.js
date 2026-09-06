/* Eon Sea II. Everything beneath the snow belongs to world space. */
window.EonArt=(()=>{
 const TAU=Math.PI*2,TILE=1024;let ctx,tiles=[],stamps=new Map();
 const h=Glacier.hash;
 function path(points,fill,stroke,width=1){ctx.beginPath();points.forEach(([x,y],i)=>i?ctx.lineTo(x,y):ctx.moveTo(x,y));if(fill){ctx.closePath();ctx.fillStyle=fill;ctx.fill();}if(stroke){ctx.strokeStyle=stroke;ctx.lineWidth=width;ctx.stroke();}}
 function ellipse(x,y,rx,ry,fill){ctx.beginPath();ctx.ellipse(x,y,Math.max(.1,rx),Math.max(.1,ry),0,0,TAU);ctx.fillStyle=fill;ctx.fill();}
 function glow(x,y,r,color){let g=ctx.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,color);g.addColorStop(1,'transparent');ctx.fillStyle=g;ctx.fillRect(x-r,y-r,r*2,r*2);}
 function noise(x,y,n,period=0){const a=Math.floor(x),b=Math.floor(y),sample=(x,y)=>h(period?(x%period+period)%period:x,period?(y%period+period)%period:y,n);let u=x-a,v=y-b;u=u*u*(3-2*u);v=v*v*(3-2*v);return (sample(a,b)*(1-u)+sample(a+1,b)*u)*(1-v)+(sample(a,b+1)*(1-u)+sample(a+1,b+1)*u)*v;}
 function fissure(x,y,angle,length,seed,width=1,depth=0){let pts=[[x,y]],a=angle;for(let i=0;i<11;i++){a+=(h(i,seed,3)-.5)*.8;x+=Math.cos(a)*length/11;y+=Math.sin(a)*length/11;pts.push([x,y]);if(i===5&&depth<2)fissure(x,y,a+(h(i,seed,4)>.5?1:-1)*.65,length*.4,seed+14,width*.6,depth+1);}
   path(pts,null,'#03132f7a',width*6);path(pts.map(([x,y])=>[x+2,y-1]),null,'#93cad978',width*1.8);path(pts,null,'#102b4d',width);}
 function makeTile(n){const c=document.createElement('canvas');c.width=c.height=TILE;ctx=c.getContext('2d');const tiny=document.createElement('canvas');tiny.width=tiny.height=512;const tc=tiny.getContext('2d'),data=tc.createImageData(512,512);
  for(let y=0;y<512;y++)for(let x=0;x<512;x++){const a=noise(x/128,y/128,1,4),b=noise(x/32,y/32,3,16),f=noise(x/8,y/8,5,64),grain=h(x,y,n)*3,cloud=Math.max(0,a*.75+b*.25-.32),i=(y*512+x)*4;data.data[i]=10+cloud*67+f*4+grain;data.data[i+1]=29+cloud*101+f*7+grain;data.data[i+2]=53+cloud*128+f*10+grain;data.data[i+3]=255;}tc.putImageData(data,0,0);ctx.drawImage(tiny,0,0,TILE,TILE);
  // Ancient shapes are trapped under the surface, never floating above it.
  ctx.save();ctx.translate(250+h(n,0)*400,250+h(n,1)*400);ctx.rotate(h(n,2)*TAU);ctx.globalAlpha=.14;
  for(let i=0;i<6;i++){ctx.strokeStyle='#011628';ctx.lineWidth=9;ctx.beginPath();ctx.ellipse(i*50-120,0,65,120-i*5,.4,-1.4,1.4);ctx.stroke();ctx.strokeStyle='#71afb7';ctx.lineWidth=2;ctx.stroke();}ctx.restore();
  for(let j=0;j<14;j++)fissure(h(j,n,7)*TILE,h(j,n,8)*TILE,h(j,n,9)*TAU,90+h(j,n,10)*310,j+n*70,.35+h(j,n,11)*1.5);
  // Hairline stress, trapped bubbles and brush-like layers of compacted frost.
  for(let i=0;i<350;i++){const x=h(i,n,23)*TILE,y=h(i,n,24)*TILE,r=1+h(i,n,25)*4;ctx.strokeStyle='#a8dce126';ctx.lineWidth=.6;ctx.beginPath();ctx.ellipse(x,y,r,r*.7,0,0,TAU);ctx.stroke();}
  for(let i=0;i<240;i++){const x=h(i,n,43)*TILE,y=h(i,n,44)*TILE,l=10+h(i,n,45)*110;path([[x,y],[x+l*.4,y-2],[x+l,y-7]],null,'#bbdfed0c',1+h(i,n,46)*3);}
  for(let i=0;i<3500;i++){ctx.fillStyle=i%7===0?'#c4e7f72b':'#badde413';ctx.fillRect(h(i,n,71)*TILE,h(i,n,73)*TILE,1+h(i,n,74)*1.2,.8);}
  return c;
 }
 function hull(kind,variant,scale=1){const points=[];for(let i=0;i<20;i++){const a=i/20*TAU,rx=140,ry=kind==='wall'?31:kind==='mass'?65:50;const side=Math.sign(Math.cos(a))*30;let x=Math.cos(a)*(rx-30)+side,y=Math.sin(a)*ry;const n=.86+h(i,variant,51)*.25;y+=Math.sin(a*3+variant)*ry*.16;points.push([x*n*scale,y*n*scale]);}return points;}
 function stamp(kind,v){const key=kind+v;if(stamps.has(key))return stamps.get(key);const c=document.createElement('canvas');c.width=440;c.height=300;const previous=ctx;ctx=c.getContext('2d');ctx.translate(220,167);
  ellipse(9,26,162,kind==='wall'?36:68,'#010b1da6');
  const base=hull(kind,v),height=kind==='mass'?42:kind==='wave'?45:kind==='wall'?17:28;
  for(let i=height;i>=0;i-=2){ctx.save();ctx.translate(0,-height+i);const light=1-i/height;path(base,`rgb(${10+light*30},${35+light*60},${62+light*90})`,i===height?'#84b8d14d':null,.8);ctx.restore();}
  for(let i=0;i<12;i++){const x=-125+i*22;path([[x,20],[x+3,1],[x-4,-height+10]],null,'#abd7e344',.8);}
  ctx.save();ctx.translate(0,-height);let g=ctx.createLinearGradient(-90,-80,60,70);g.addColorStop(0,'#c0dfeb');g.addColorStop(.18,'#7fb0c8');g.addColorStop(.48,'#38708e');g.addColorStop(.7,'#20445f');g.addColorStop(1,'#7baabe');path(base,g,'#b4dcebaa',1.2);
  ctx.beginPath();base.forEach(([x,y],i)=>i?ctx.lineTo(x,y):ctx.moveTo(x,y));ctx.closePath();ctx.clip();
  for(let j=0;j<14;j++){const y=-65+j*10;ctx.beginPath();ctx.moveTo(-160+h(j,v,73)*70,y);ctx.bezierCurveTo(-70,y-20+h(j,v)*50,20,y+30+h(j,v,9)*35,160-h(j,v,74)*110,y-10);ctx.strokeStyle=j%3===0?'#d7eff45b':'#83c0d44d';ctx.lineWidth=j%3===0?1:5+h(j,v,75)*7;ctx.stroke();}
  path([[-120,-20],[-60,-52],[-10,-27],[55,-35],[30,-7],[-25,2]],'#d8eef13c');
  for(let i=0;i<430;i++){ctx.fillStyle=i%3?'#dcf3fa22':'#ecfbfb65';ctx.fillRect(h(i,v,5)*300-150,h(i,v,6)*140-70,.8+h(i,v,7)*2,.7);}
  for(let j=0;j<5;j++)fissure(-120+j*53,-15+h(j,v,93)*40,-1.3+h(j,v,96)*.8,50+j*4,v*6+j,.45);
  if(kind==='brittle'){glow(0,-7,90,'#64dcff64');path([[-100,13],[-68,-9],[-43,1],[-10,-22],[13,-14],[39,-36],[85,-23]],null,'#d3fbff',2);}
  if(kind==='wave'){ctx.beginPath();ctx.moveTo(-120,25);ctx.bezierCurveTo(-70,-45,35,-80,110,-23);ctx.bezierCurveTo(130,-3,97,9,72,-7);ctx.strokeStyle='#b2dfefa8';ctx.lineWidth=15;ctx.stroke();ctx.strokeStyle='#e1f3f59c';ctx.lineWidth=2;ctx.stroke();}
  ctx.restore();ctx=previous;stamps.set(key,c);return c;
 }
 function init(){tiles=[];for(let i=0;i<4;i++)tiles.push(makeTile(i));for(const kind of ['wall','wave','mass','brittle'])for(let v=0;v<8;v++)stamp(kind,v);}
 function screenToWorld(x,y,s){return {x:x-800+s.camera.x,y:y-485+s.camera.y};}
 function ice(o,selected){ctx.save();ctx.translate(o.x,o.y);ctx.rotate(o.angle);const w=o.length+o.r*2,sy=o.r/(o.kind==='wall'?31:o.kind==='mass'?65:50);
  if(o.state==='broken'){for(let i=0;i<14;i++){const a=h(i,o.variant,90)*TAU,r=h(i,o.variant,91)*w*.6;path([[Math.cos(a)*r,Math.sin(a)*r*.45],[Math.cos(a)*r+6,Math.sin(a)*r*.45-4],[Math.cos(a)*r+12,Math.sin(a)*r*.45+3]],'#83b7cb48');}ctx.restore();return;}
  const meltHeight=1-(o.melt||0)*.46;ctx.drawImage(stamp(o.kind,o.variant),-220*w/280,-167*sy*meltHeight,440*w/280,300*sy*meltHeight);
  const damage=Math.max(o.melt||0,(o.cracks||0)*.22);if(damage>.08){for(let i=0;i<Math.ceil(damage*7);i++)path([[-w*.4+i*w*.13,-o.r*.7],[-w*.3+i*w*.09,0],[-w*.38+i*w*.13,o.r*.5]],null,'#bfe8f3b3',.8+damage);}
  if(selected){ctx.setLineDash([4,6]);ctx.strokeStyle='#b7eaff';ctx.lineWidth=1;ctx.beginPath();ctx.ellipse(0,0,w*.6,o.r+16,0,0,TAU);ctx.stroke();ctx.setLineDash([]);}
  if(o.flash>0){ctx.globalAlpha=o.flash*2;path(hull(o.kind,o.variant).map(([x,y])=>[x*w/280,y*sy]),'#d8fbff');}ctx.restore();
  if(selected||o.hp<o.maxHp){ctx.fillStyle='#071a2aaa';ctx.fillRect(o.x-28,o.y+o.r+18,56,3);ctx.fillStyle=o.kind==='brittle'?'#afdfff':'#95b9d0';ctx.fillRect(o.x-28,o.y+o.r+18,56*o.hp/o.maxHp,3);}
 }
 function unit(e,images,s,player=false){const img=images[player?'operative':e.type+'_'+(e.flash>0?1:0)];if(!img)return;ellipse(e.x+4,e.y+7,e.r*1.2,e.r*.7,'#000c2280');ctx.save();ctx.translate(e.x,e.y);ctx.rotate((e.angle||0)+Math.PI/2);if(e.flash>0){ctx.shadowColor='#fff';ctx.shadowBlur=10;}if(player&&e.inv>0)ctx.globalAlpha=.5;if(e.elite&&window.EonEliteRoster)EonEliteRoster.draw(ctx,e.type,e.tier||1,0,0,e.r*3,s.time,images);else if(!player&&!e.boss&&window.EonRoster)EonRoster.draw(ctx,e.type,e.tier||1,0,0,e.r*3,s.time,images,e.variant);else ctx.drawImage(img,-e.r*1.5,-e.r*1.5,e.r*3,e.r*3);ctx.restore();if(player||e.slow>0){ctx.strokeStyle=e.slow>0?'#c9f1ff':'#a4e5ff6b';ctx.lineWidth=e.slow>0?2:1;ctx.setLineDash(e.slow>0?[4,6]:[]);ctx.beginPath();ctx.arc(e.x,e.y,e.r+10,0,TAU);ctx.stroke();ctx.setLineDash([]);}if(e.slow>0){ctx.fillStyle='#cbe8fa';ctx.textAlign='center';ctx.font='9px monospace';ctx.fillText('❄ '+Math.ceil(e.slow)+'s',e.x,e.y-e.r-13);}}
 function render(c,s,images,t,options={}){ctx=c;ctx.save();ctx.translate(800-s.camera.x,485-s.camera.y);
  const left=s.camera.x-850,top=s.camera.y-520,cx=Math.floor(left/TILE),cy=Math.floor(top/TILE);
  for(let y=cy;y<=cy+2;y++)for(let x=cx;x<=cx+2;x++){const tile=tiles[Math.floor(h(x,y,61)*tiles.length)];ctx.drawImage(tile,x*TILE,y*TILE,TILE+1,TILE+1);}
  // Ground-bound deposits and great buried arcs establish a world without an edge.
  for(let y=cy;y<=cy+2;y++)for(let x=cx;x<=cx+2;x++){const px=x*TILE+500,py=y*TILE+480;ctx.save();ctx.translate(px,py);ctx.rotate(h(x,y,22)*TAU);ctx.globalAlpha=.13;ctx.strokeStyle='#b8d4e8';ctx.lineWidth=2;ctx.beginPath();ctx.ellipse(0,0,280,110,0,.2,2.7);ctx.stroke();ctx.globalAlpha=.08;ctx.lineWidth=15;ctx.stroke();ctx.restore();}
  for(const p of s.glacier.patches||[]){ctx.save();ctx.translate(p.x,p.y);ctx.rotate(p.angle);const g=ctx.createRadialGradient(0,0,10,0,0,p.rx);g.addColorStop(0,'#b8eaff21');g.addColorStop(.8,'#669cce20');g.addColorStop(1,'transparent');ellipse(0,0,p.rx,p.ry,g);ctx.strokeStyle='#accfee45';ctx.lineWidth=1;ctx.beginPath();ctx.ellipse(0,0,p.rx,p.ry,0,0,TAU);ctx.stroke();ctx.beginPath();ctx.ellipse(0,0,p.rx,p.ry,0,0,TAU);ctx.clip();for(let i=0;i<14;i++){const y=-p.ry+i*p.ry/7;path([[-p.rx,y],[p.rx*.25,y-8],[p.rx,y-15]],null,'#c2e8ff37',.7);}ctx.restore();}
  for(const o of s.glacier.ice)if(o.state==='warning'){const radius=140+o.r;ctx.save();ctx.strokeStyle='#abdfff7a';ctx.lineWidth=1;ctx.setLineDash([4,9]);ctx.beginPath();ctx.arc(o.x,o.y,radius,0,TAU);ctx.stroke();ctx.restore();glow(o.x,o.y,radius,'#87bcf20a');}
  for(const b of s.blasts){ctx.strokeStyle='#f3bca7';ctx.lineWidth=1.5;ctx.beginPath();ctx.arc(b.x,b.y,b.r,0,TAU);ctx.stroke();ctx.fillStyle='#ef815410';ctx.fill();}
  const objects=[...s.glacier.ice.map(o=>({...o,isIce:true})),...s.enemies,...(s.boss?[s.boss]:[]),{...s.player,isPlayer:true}].sort((a,b)=>a.y-b.y);
  for(const o of objects){if(Math.abs(o.x-s.camera.x)>1100||Math.abs(o.y-s.camera.y)>750)continue;if(o.isIce)ice(o,o.id===options.selected||o.id===s.focus);else{
   if(o.state==='windup'||o.state==='warning'||o.state==='aiming'){ctx.setLineDash([8,8]);path([[o.x,o.y],[o.x+Math.cos(o.aim)*300,o.y+Math.sin(o.aim)*300]],null,'#efa99f99',2);ctx.setLineDash([]);}unit(o,images,s,o.isPlayer);
  }}
  for(const b of s.bullets)path([[b.x-b.vx*.012,b.y-b.vy*.012],[b.x,b.y]],null,'#d3f3ff',2.5);
  for(const b of s.shots){ellipse(b.x,b.y,b.r+3,b.r+3,'#ff97722e');ellipse(b.x,b.y,b.r,b.r,'#ffd2a0');}
  for(const b of s.glacier.bursts){const p=1-b.life/b.max;ctx.globalAlpha=1-p;ctx.strokeStyle='#c4efff';ctx.lineWidth=2;ctx.beginPath();ctx.arc(b.x,b.y,b.radius*Math.min(1,p*2),0,TAU);ctx.stroke();for(let i=0;i<28;i++){const a=h(i,b.variant,3)*TAU,r=p*b.radius*(h(i,b.variant,4)*.6+.4),x=b.x+Math.cos(a)*r,y=b.y+Math.sin(a)*r;path([[x,y],[x-6*Math.cos(a),y-6*Math.sin(a)],[x-3*Math.cos(a)+3,y-3*Math.sin(a)-4]],i%3?'#b1d6ea':'#f0fbff');}ctx.globalAlpha=1;glow(b.x,b.y,b.radius,'#b0dbff1b');}
  for(const e of s.effects){const p=1-e.life/e.max;ctx.globalAlpha=1-p;ctx.strokeStyle=e.color;ctx.lineWidth=1;ctx.beginPath();ctx.arc(e.x,e.y,4+e.r*p,0,TAU);ctx.stroke();ctx.globalAlpha=1;}
  if(s.rift){const {x,y}=s.rift;glow(x,y,130,'#aeecff44');ctx.strokeStyle='#d1f7ff';for(let i=0;i<4;i++){ctx.lineWidth=1.4;ctx.beginPath();ctx.ellipse(x,y,30+i*5,50+i*5,Math.sin(t+i)*.15,0,TAU);ctx.stroke();}ctx.textAlign='center';ctx.fillStyle='#d0effa';ctx.font='12px monospace';ctx.fillText('ESCAPE RIFT',x,y-90);}
  ctx.restore();
  // Weather is only a thin layer over the physical glacier.
  if(s.phase===1){ctx.fillStyle='#8296c312';ctx.fillRect(0,0,1600,900);}if(s.phase===2){ctx.fillStyle='#a4c1d20c';ctx.fillRect(0,0,1600,900);}
  for(let i=0;i<(s.phase===1?140:45);i++){const x=((h(i,1)*1800+t*(s.phase===1?75:15))%1800)-100,y=(h(i,2)*1000+t*(s.phase===1?12:3))%1000-50;ctx.globalAlpha=.06+h(i,3)*.2;path([[x,y],[x+(s.phase===1?17:4),y-1]],null,'#d7edf5',.6+h(i,4));}ctx.globalAlpha=1;
  const vig=ctx.createRadialGradient(800,460,220,800,460,1050);vig.addColorStop(0,'transparent');vig.addColorStop(1,'#010b2199');ctx.fillStyle=vig;ctx.fillRect(0,0,1600,900);
 }
 return {init,render,screenToWorld};
})();

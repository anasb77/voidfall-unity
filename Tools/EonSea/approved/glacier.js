(function(root,factory){const api=factory();if(typeof module!=='undefined')module.exports=api;else root.Glacier=api;})(globalThis,function(){
 const SIZE=1000,clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
 function hash(x,y,n=0){let h=Math.imul(x^n,374761393)^Math.imul(y,668265263);h=Math.imul(h^(h>>>13),1274126177);return ((h^(h>>>16))>>>0)/4294967296;}
 function create(seed){return {seed,time:0,ice:[],patches:[],destroyed:new Set(),saved:new Map(),sector:'',bursts:[],broken:0,frozenCount:0};}
 function stream(w,x,y){const cx=Math.floor(x/SIZE),cy=Math.floor(y/SIZE),key=cx+','+cy;if(key===w.sector)return;w.sector=key;
  for(const o of w.ice)w.saved.set(o.id,{...o});
  const next=[];for(let j=cy-1;j<=cy+1;j++)for(let i=cx-1;i<=cx+1;i++)for(let k=0;k<9;k++){
   const id=i+':'+j+':'+k;if(w.destroyed.has(id))continue;
   const px=i*SIZE+120+(k%3)*290+(hash(i,j,k+12)*100-50),py=j*SIZE+120+Math.floor(k/3)*290+(hash(i,j,k+77)*100-50);
   if(Math.hypot(px-800,py-620)<170)continue;
   const kind=['mass','brittle','wall','wave','brittle','wall'][Math.floor(hash(i,j,k+200)*6)],r=kind==='mass'?48:kind==='wave'?33:kind==='wall'?20:28;
   const hp=kind==='mass'?680:kind==='wave'?420:kind==='wall'?115:155;
   const o=w.saved.get(id)||{id,x:px,y:py,kind,r,length:kind==='brittle'?55:90+hash(i,j,k+90)*100,angle:hash(i,j,k+177)*Math.PI,hp,maxHp:hp,state:'solid',variant:Math.floor(hash(i,j,k+88)*8),flash:0,melt:hash(i,j,k+215)*.3,duration:(kind==='mass'?135:kind==='wave'?115:kind==='wall'?80:95)+hash(i,j,k+213)*30,lastAt:w.time,cracks:0,explosionCracks:0};
   ageIce(w,o,Math.max(0,w.time-o.lastAt));if(o.melt>=1){w.destroyed.add(id);continue;}next.push(o);
  }w.ice=next;w.patches=[];
  for(let j=cy-1;j<=cy+1;j++)for(let i=cx-1;i<=cx+1;i++)for(let k=0;k<3;k++)w.patches.push({id:'slip'+i+':'+j+':'+k,x:i*SIZE+140+hash(i,j,k+500)*720,y:j*SIZE+140+hash(i,j,k+502)*720,rx:90+hash(i,j,k+503)*90,ry:55+hash(i,j,k+504)*45,angle:hash(i,j,k+505)*Math.PI});
 }
 function nearest(o,p){const dx=Math.cos(o.angle)*o.length/2,dy=Math.sin(o.angle)*o.length/2,ax=o.x-dx,ay=o.y-dy,bx=o.x+dx,by=o.y+dy;
  const t=clamp(((p.x-ax)*(bx-ax)+(p.y-ay)*(by-ay))/((bx-ax)**2+(by-ay)**2||1),0,1);return {x:ax+(bx-ax)*t,y:ay+(by-ay)*t};}
 function inside(o,p,r=0){const q=nearest(o,p);return Math.hypot(q.x-p.x,q.y-p.y)<o.r+r;}
 // Direct damage is deliberately ignored. Only explosion proximity adds stress.
 function damage(){return false;}
 function explode(w,point,radius){for(const o of w.ice)if(o.state!=='broken'&&inside(o,point,radius)){o.explosionCracks=Math.min(3,(o.explosionCracks||0)+1);o.cracks=Math.max(o.cracks||0,o.explosionCracks);o.flash=.5;}}
 function meltRate(o){return (1+(o.explosionCracks||0)*1.1)/(o.duration||100);}
 function ageIce(w,o,dt){o.melt=Math.min(1,(o.melt||0)+dt*meltRate(o));o.lastAt=w.time;o.hp=(o.maxHp||100)*(1-o.melt);o.cracks=Math.max(o.explosionCracks||0,Math.min(3,Math.floor(o.melt*4)));o.state=o.melt>=.88?'warning':'solid';}
 function breakIce(w,o,targets){o.state='broken';w.destroyed.add(o.id);w.broken++;const radius=140+o.r;w.bursts.push({x:o.x,y:o.y,radius,life:1.6,max:1.6,kind:'freeze',variant:o.variant||0});
  for(const e of targets)if(e.hp>0&&Math.hypot(e.x-o.x,e.y-o.y)<radius+(e.r||0)){e.slow=20;w.frozenCount++;}
 }
 function move(w,p,dx,dy,heavy=false,dt=0){const steps=Math.max(1,Math.ceil(Math.hypot(dx,dy)/8));let blocked=false;
  for(let j=0;j<steps;j++){p.x+=dx/steps;p.y+=dy/steps;
   for(const o of w.ice){if(o.state==='broken'||Math.abs(p.x-o.x)>o.length+o.r+p.r||Math.abs(p.y-o.y)>o.length+o.r+p.r)continue;
    const q=nearest(o,p),d=Math.hypot(p.x-q.x,p.y-q.y),limit=o.r+p.r;if(d>=limit)continue;blocked=true;
    const nx=d>.0001?(p.x-q.x)/d:-Math.sin(o.angle),ny=d>.0001?(p.y-q.y)/d:Math.cos(o.angle);p.x=q.x+nx*(limit+.01);p.y=q.y+ny*(limit+.01);
   }
  }return blocked;
 }
 function firstHit(w,a,b,r=0,exclude=null){let best=null;const distance=Math.hypot(b.x-a.x,b.y-a.y),steps=Math.max(1,Math.ceil(distance/6));
  for(const o of w.ice){if(o.id===exclude||o.state==='broken')continue;if(Math.min(a.x,b.x)>o.x+o.length+o.r+r||Math.max(a.x,b.x)<o.x-o.length-o.r-r||Math.min(a.y,b.y)>o.y+o.length+o.r+r||Math.max(a.y,b.y)<o.y-o.length-o.r-r)continue;
   for(let j=0;j<=steps;j++){const t=j/steps;if(best&&t>=best.t)break;const p={x:a.x+(b.x-a.x)*t,y:a.y+(b.y-a.y)*t};if(inside(o,p,r)){best={ice:o,t,x:p.x,y:p.y};break;}}
  }return best;
 }
 function speedScale(actor){return actor.slow>0?.5:1;}
 function slipAt(w,p){return (w.patches||[]).find(o=>{const dx=p.x-o.x,dy=p.y-o.y,c=Math.cos(o.angle),s=Math.sin(o.angle);return ((dx*c+dy*s)/o.rx)**2+((-dx*s+dy*c)/o.ry)**2<1;});}
 function movePlayer(w,p,ix,iy,dt){const len=Math.hypot(ix,iy),speed=225*speedScale(p),tx=len?ix/len*speed:0,ty=len?iy/len*speed:0;const slip=slipAt(w,p);p.sliding=!!slip;
  if(slip){const f=1-Math.exp(-dt*(len?1.8:.3));p.vx=(p.vx||0)+(tx-(p.vx||0))*f;p.vy=(p.vy||0)+(ty-(p.vy||0))*f;const magnitude=Math.hypot(p.vx,p.vy);if(magnitude>speed){p.vx*=speed/magnitude;p.vy*=speed/magnitude;}}
  else {p.vx=tx;p.vy=ty;}const oldX=p.x,oldY=p.y;const blocked=move(w,p,p.vx*dt,p.vy*dt,false,dt);if(blocked){p.vx=(p.x-oldX)/dt;p.vy=(p.y-oldY)/dt;}
 }
 function step(w,dt,targets=[]){dt=Math.max(0,dt);w.time+=dt;for(const e of targets)e.slow=Math.max(0,(e.slow||0)-dt);
  for(const o of w.ice){o.flash=Math.max(0,(o.flash||0)-dt);if(o.state==='broken')continue;ageIce(w,o,dt);if(o.melt>=1)breakIce(w,o,targets);}
  for(const b of w.bursts)b.life-=dt;w.bursts=w.bursts.filter(b=>b.life>0);
 }
 return {create,stream,hash,nearest,inside,damage,explode,meltRate,move,movePlayer,speedScale,slipAt,firstHit,step,SIZE};
});

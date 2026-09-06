/* Native canvas art for The Destroyer's Domain. World units are pixels. */
(function (root) {
  'use strict';
  const TAU = Math.PI * 2, DARK = '#080c18';
  const colors = { breaker: '#ffb45d', ram: '#b690ff', executioner: '#78e7ea', devourer: '#c084fc', 'first-destroyer': '#efd192' };
  const palettes=Object.freeze([{"id":"violet","name":"Crying Violet","subtitle":"03 · OVERLOAD","description":"Mysterious, magical and cold. The reference version.","primary":"#9452cf","secondary":"#d19bf4","base":"#100d1b","stone":["#151120","#1b1429","#231932","#281b3b","#100d19","#302044"],"edge":"#74518a35","branch":"#7c36ad70","bed":"#56277d","vein":"#9d4cd2a8","hot":"#bd73edae","bloom":"#ae56f5","tip":"#d8a3ffb0","chip":"#773aa15c","chipHot":"#b66fe4a3","tear":"#c17cf1","tearGlow":"#bb6bff","tearTip":"#ddb0ff","washA":"#281a392b","washB":"#38204536","nameIdea":"Obsidian Hunger"},{"id":"coral","name":"Violet / Coral","subtitle":"02 · STRAIN","description":"Bruised violet stone with hot coral seams. Growth feels unstable and ready to burst.","primary":"#745099","secondary":"#ef8c76","base":"#100d19","stone":["#17111f","#1e152b","#251930","#302039","#15101c","#342334"],"edge":"#9d668b32","branch":"#92506868","bed":"#5b304f","vein":"#9b63bf94","hot":"#ee8c73bc","bloom":"#ec776f","tip":"#ffd5b5b5","chip":"#7b456951","chipHot":"#e58d78a0","tear":"#eb947e","tearGlow":"#ef8a7b","tearTip":"#ffe1c2","washA":"#351a3020","washB":"#4a242a28","nameIdea":"Rupture"},{"id":"gold","name":"Indigo / Amber","subtitle":"01 · AWAKENING","description":"Deep ink-colored stone with amber and pale-gold fractures. Enemies feel monumental.","primary":"#414d79","secondary":"#d2aa61","base":"#0c101a","stone":["#121827","#172032","#20283a","#242d43","#0e1420","#293147"],"edge":"#7788a72c","branch":"#70657c66","bed":"#514d67","vein":"#8279b58f","hot":"#d6ab65b0","bloom":"#dfaf61","tip":"#ffe4aab0","chip":"#57618351","chipHot":"#c7a66f9b","tear":"#deb977","tearGlow":"#d8b268","tearTip":"#fff0c8","washA":"#19243c24","washB":"#38302625","nameIdea":"The Overreach"}].map(p=>Object.freeze(p)));
  const materials=new Map();let initialized=false;
  function random(seed) { return () => { seed |= 0; seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
  function surface(w, h = w) { const c = document.createElement('canvas'); c.width = w; c.height = h; return c; }
  function path(c, p, fill, stroke, width = 2) { c.beginPath(); p.forEach((v, i) => i ? c.lineTo(v[0], v[1]) : c.moveTo(v[0], v[1])); c.closePath(); if (fill) { c.fillStyle = fill; c.fill(); } if (stroke) { c.strokeStyle = stroke; c.lineWidth = width; c.stroke(); } }
  function line(c, p, color, width = 1) { c.beginPath(); p.forEach((v, i) => i ? c.lineTo(v[0], v[1]) : c.moveTo(v[0], v[1])); c.strokeStyle = color; c.lineWidth = width; c.stroke(); }
  function circle(c, x, y, r, fill, stroke, width = 1) { c.beginPath(); c.arc(x, y, Math.max(.01, r), 0, TAU); if (fill) { c.fillStyle = fill; c.fill(); } if (stroke) { c.strokeStyle = stroke; c.lineWidth = width; c.stroke(); } }
  function halo(c, x, y, r, color, alpha = .2) { c.save(); c.globalAlpha *= alpha; const g = c.createRadialGradient(x, y, 0, x, y, r); g.addColorStop(0, color); g.addColorStop(.28, color); g.addColorStop(1, 'transparent'); circle(c, x, y, r, g); c.restore(); }
  function arc(c, x, y, r, a, b, color, width = 2) { c.beginPath(); c.arc(x, y, r, a, b); c.strokeStyle = color; c.lineWidth = width; c.stroke(); }
  function core(c, x, y, r, color, pulse = 1) { halo(c, x, y, r * 5, color, .18 * pulse); circle(c, x, y, r * 1.7, '#101526'); c.shadowColor = color; c.shadowBlur = 18; circle(c, x, y, r, color); c.shadowBlur = 0; circle(c, x - r * .15, y - r * .13, r * .46, '#fff8ef'); }
  function makeMaterial(p) {
    const rng=random(9231),floor=surface(1024),tears=[];const c=floor.getContext('2d');
    c.fillStyle = p.base; c.fillRect(0, 0, 1024, 1024);
    // Original crying-obsidian-inspired material: chipped violet glass, glowing
    // inclusions and luminous tears. No Minecraft textures are used.
    const stone=p.stone;
    for(let i=0;i<2400;i++){
      const x=rng()*1024,y=rng()*1024,w=8+rng()*39,h=5+rng()*23;
      path(c,[[x,y],[x+w*.65,y-3],[x+w,y+h*.3],[x+w*.75,y+h],[x+w*.15,y+h*.8]],stone[Math.floor(rng()*stone.length)],'#04030b55',1);
      if(i%4===0)line(c,[[x+2,y],[x+w*.63,y-2]],p.edge,1);
    }
    for(let i=0;i<115;i++){
      const x=rng()*1024,y=rng()*1024,points=[[x,y]];let px=x,py=y;
      const steps=3+Math.floor(rng()*5),direction=rng()>.5?1:-1;
      for(let j=0;j<steps;j++){px+=direction*(5+rng()*12);py+=4+rng()*15;points.push([px,py]);if(j===1&&i%3===0)line(c,[[px,py],[px-14*direction,py+12],[px-18*direction,py+25]],p.branch,2);}
      line(c,points,'#05020c',9);line(c,points,p.bed,6);line(c,points,i%3?p.vein:p.hot,2.1);
      if(i%4===0){halo(c,px,py,32,p.bloom,.15);line(c,points.slice(-2),p.tip,1);}
      tears.push({x:px,y:py,length:12+rng()*27,phase:rng()});
    }
    for(let i=0;i<450;i++){
      const x=rng()*1024,y=rng()*1024,w=2+rng()*7;
      path(c,[[x,y],[x+w,y+1],[x+w*.7,y+5],[x+1,y+7]],i%3?p.chip:p.chipHot);
    }
    for (let i = 0; i < 17000; i++) { c.fillStyle = rng() > .48 ? '#b49bd10b' : '#0000001a'; c.fillRect(rng() * 1024, rng() * 1024, 1 + rng() * 3, .5 + rng()); }
    const dust = Array.from({length: 100}, () => ({x:rng()*1600,y:rng()*900,r:.3+rng()*1.25,s:rng(),a:.08+rng()*.25}));
    return {floor,tears,dust};
  }
  function init(){if(initialized)return;for(const p of palettes)materials.set(p.id,makeMaterial(p));initialized=true;}
  function drawUnit(c,e,time=0,images={}){
    const r=e.r||20;c.save();c.translate(e.x||0,e.y||0);
    const color=e.elite?'#efce8e':e.boss?'#ed9acc':'#ba83ee';
    if((e.flash||0)>0)halo(c,0,0,r*2,color,.2);
    if(e.boss){const image=images[e.type+'_0'];if(image){c.rotate(e.angle||0);c.drawImage(image,-r*1.5,-r*1.5,r*3,r*3);}}
    else{c.rotate((e.angle||0)+Math.PI/2);(e.elite?EonEliteRoster:EonRoster).draw(c,e.type,e.tier||1,0,0,r*3.2,time,images);}
    c.restore();
    if(e.growthCount>0){c.save();c.textAlign='center';c.font='10px monospace';c.fillStyle=e.growth>=5?'#e8b9ff':'#c19bdd';c.fillText('×'+e.growth.toFixed(1),e.x,e.y+r+30);c.restore();}
  }
  function background(c,s,paletteId,progress) {
    const flow=Number.isFinite(progress)?PaletteFlow.at(progress):{from:paletteId||'violet',to:paletteId||'violet',mix:0};
    const first=palettes.find(p=>p.id===flow.from)||palettes[0],second=palettes.find(p=>p.id===flow.to)||first;
    const palette={...first};for(const key of ['tear','tearGlow','tearTip','washA','washB'])palette[key]=PaletteFlow.blend(first[key],second[key],flow.mix);
    const {floor,tears,dust}=materials.get(first.id);
    const cam=s.camera||s.player||{x:0,y:0}, t=s.time||0, phase=s.phase||0;
    c.fillStyle='#0b0913';c.fillRect(0,0,1600,900);
    const p=c.createPattern(floor,'repeat');c.save();c.translate(800-cam.x,480-cam.y);c.fillStyle=p;c.fillRect(cam.x-800,cam.y-480,1600,900);c.restore();
    if(flow.mix>0){c.save();c.globalAlpha=flow.mix;c.translate(800-cam.x,480-cam.y);c.fillStyle=c.createPattern(materials.get(second.id).floor,'repeat');c.fillRect(cam.x-800,cam.y-480,1600,900);c.restore();}
    c.save();c.translate(800-cam.x,480-cam.y);
    const tileX=Math.floor((cam.x-800)/1024),tileY=Math.floor((cam.y-480)/1024);
    for(let iy=tileY;iy<=tileY+2;iy++)for(let ix=tileX;ix<=tileX+2;ix++)for(const tear of tears){
      const phase=(t*.19+tear.phase)%1;if(phase>.62)continue;
      const x=ix*1024+tear.x,y=iy*1024+tear.y+phase*tear.length;
      if(Math.abs(x-cam.x)>840||Math.abs(y-cam.y)>510)continue;
      const strength=Math.sin(phase/.62*Math.PI);c.globalAlpha=strength*.65;
      line(c,[[x,y-6],[x,y]],palette.tear,1.1);halo(c,x,y,12,palette.tearGlow,.2);circle(c,x,y,1.65,palette.tearTip);
    }c.restore();
    const wash=c.createLinearGradient(0,0,1600,900);wash.addColorStop(0,palette.washA);wash.addColorStop(.48,'#0d0b1000');wash.addColorStop(1,palette.washB);c.fillStyle=wash;c.fillRect(0,0,1600,900);
    const mod=(v,n)=>((v%n)+n)%n;
    for(const d of dust){const x=mod(d.x-cam.x*.25+t*d.s*2,1600),y=mod(d.y-cam.y*.25-t*(2+d.s*3),900);c.globalAlpha=d.a*(.7+.3*Math.sin(t+d.x));circle(c,x,y,d.r,d.s>.8?'#e1b579':'#b7a0c5');}c.globalAlpha=1;
  }
  function attack(c,a,time) {
    c.save(); c.translate(a.x,a.y);c.rotate(a.angle||0);const warning=a.warning!==false, color=warning?'#e6be71':'#ff9bba', r=a.r||90, width=a.width||34,length=a.length||250;
    const progress=Math.max(0,Math.min(1,a.progress==null?.5:a.progress));
    c.lineCap='round';c.lineJoin='round';
    if(warning)c.setLineDash([8,7]);
    c.strokeStyle=color;c.lineWidth=warning?1.5:3;
    if(a.kind==='line'){
      c.fillStyle=warning?'#c4984012':'#ff6d912b';c.fillRect(0,-width/2,length,width);c.strokeRect(0,-width/2,length,width);c.setLineDash([]);
      line(c,[[0,0],[length*progress,0]],warning?'#edc57e88':'#fff1e6',warning?1.2:4);
      for(let x=38;x<length;x+=60)line(c,[[x-6,-5],[x,0],[x-6,5]],color,1.2);
    }else if(a.kind==='sweep'){
      const sweep=a.arc||(warning?3:.56);c.beginPath();c.moveTo(0,0);c.arc(0,0,r,-sweep/2,sweep/2);c.closePath();c.fillStyle=warning?'#d2a44913':'#ff84a82b';c.fill();c.stroke();c.setLineDash([]);
      arc(c,0,0,r*(.4+.6*progress),-sweep/2,sweep/2,warning?'#e1b96c66':'#ffe4e8',warning?1:4);
    }else{
      circle(c,0,0,r,warning?'#d2a44910':'#ff6d882a',color,warning?1.6:3);c.setLineDash([]);arc(c,0,0,r-7,-Math.PI/2,-Math.PI/2+TAU*progress,warning?'#e7bc76aa':'#fff1df',warning?2.5:4);
      line(c,[[-7,0],[7,0]],color,1.5);line(c,[[0,-7],[0,7]],color,1.5);
    }
    if(!warning){c.globalCompositeOperation='lighter';halo(c,a.kind==='line'?length*.5:0,0,a.kind==='line'?width*2:r*1.2,'#ff768d',.1);}
    c.restore();
  }
  function render(c,s,images={},options={}) {
    init();const cam=s.camera||s.player||{x:0,y:0},t=s.time||0;
    c.save();background(c,s,options.palette,options.progress);c.translate(800-cam.x,480-cam.y);
    for(const scar of s.scars||[]){c.save();c.translate(scar.x,scar.y);c.rotate(scar.angle||0);c.globalAlpha=Math.min(1,(scar.life||1)/(scar.max||1))*.75;line(c,[[0,0],[(scar.length||100)*.35,-5],[(scar.length||100)*.62,4],[(scar.length||100),0]],'#05050b',12);line(c,[[0,0],[(scar.length||100)*.35,-5],[(scar.length||100)*.62,4],[(scar.length||100),0]],'#ad734faa',2);c.restore();}
    if(s.rift){const r=s.rift;c.save();c.translate(r.x,r.y);halo(c,0,0,130,'#8ef2d6',.23);c.rotate(t*.3);for(let i=0;i<3;i++){c.save();c.rotate(i*TAU/3);arc(c,0,0,44+i*7,.1,1.7,'#a4ffe5',2);c.restore();}circle(c,0,0,29,'#051f2788','#edfff6',1.5);c.restore();}
    for(const a of s.attacks||[])attack(c,a,t);
    const all=(s.enemies||[]).filter(e=>!e.dead&&e.hp!==0);if(s.boss&&!s.boss.dead&&s.boss.hp!==0&&!all.includes(s.boss))all.push(s.boss);
    for(const e of all){if(Math.abs(e.x-cam.x)>1100||Math.abs(e.y-cam.y)>750)continue;drawUnit(c,e,t,images);if(e.hp<e.maxHp&&!e.boss&&e.type!=='first-destroyer'){const w=e.r*1.1;c.fillStyle='#03050bcc';c.fillRect(e.x-w/2,e.y+e.r+12,w,3);c.fillStyle=e.exposed>0?'#ffe4a4':'#bf8fa1';c.fillRect(e.x-w/2,e.y+e.r+12,w*Math.max(0,e.hp/e.maxHp),3);}}
    const p=s.player;if(p&&s.mode!=='dead'){c.save();c.translate(p.x,p.y);halo(c,0,0,42,'#acffda',.14);c.save();c.scale(1,.55);circle(c,0,7,18,'#03050b77');c.restore();if((p.inv||p.invincible||p.invuln||0)>0)circle(c,0,0,(p.r||14)+12,null,'#d6ffe49a',1.5);c.rotate(p.angle||0);if(images.operative&&images.operative.complete!==false){const size=(p.r||14)*3.4;c.drawImage(images.operative,-size/2,-size/2,size,size);}else{path(c,[[19,0],[-10,-10],[-5,0],[-10,10]],DARK,'#bbffe4',2);core(c,0,0,4,'#beffe8');}c.restore();}
    c.save();c.globalCompositeOperation='lighter';
    for(const q of s.bullets||[]){const a=Math.atan2(q.vy||0,q.vx||1);line(c,[[q.x-Math.cos(a)*12,q.y-Math.sin(a)*12],[q.x,q.y]],'#80ffd9aa',2.7);circle(c,q.x,q.y,q.r||2.3,'#e8fff1');}
    for(const q of s.shots||[]){halo(c,q.x,q.y,(q.r||6)*3.8,'#f76662',.24);circle(c,q.x,q.y,q.r||5.5,'#ff7e63');circle(c,q.x,q.y,(q.r||5.5)*.45,'#ffedba');}
    c.restore();
    for(const e of s.effects||[]){const f=Math.max(0,Math.min(1,e.life/(e.max||1))),r=(e.r||30)*(1.3-f*.45);c.save();c.globalAlpha=f;const col=e.color||'#f6c17f';if(e.kind==='death-pulse'){
      const wave=e.r*(1-f);circle(c,e.x,e.y,wave,null,'#e1eeff',1.5+f*4);circle(c,e.x,e.y,wave*.87,null,'#bf8aff',1.4);halo(c,e.x,e.y,Math.max(30,wave),'#b78cec',f*.13);
      for(let i=0;i<16;i++){const a=i*TAU/16;line(c,[[e.x+Math.cos(a)*wave,e.y+Math.sin(a)*wave],[e.x+Math.cos(a)*(wave+10),e.y+Math.sin(a)*(wave+10)]],'#e4caff',1.2);}
    }else if(e.kind==='clash'){
      const wave=(e.r||270)*(1.15-f*.75);halo(c,e.x,e.y,wave*1.3,'#e7d7ff',.26*f);circle(c,e.x,e.y,wave,null,'#fff1d2',1+f*5);circle(c,e.x,e.y,wave*.88,null,'#c19bff',1+f*2);
      for(let k=0;k<22;k++){const a=k*TAU/22+(e.x%3),d=wave*(.9+(k%3)*.1);line(c,[[e.x+Math.cos(a)*d,e.y+Math.sin(a)*d],[e.x+Math.cos(a)*(d+8+f*28),e.y+Math.sin(a)*(d+8+f*28)]],k%2?'#e0c0ff':'#fff2c5',1.5+f);}
      if(f>.72){c.globalCompositeOperation='lighter';line(c,[[e.x-wave*.8,e.y],[e.x+wave*.8,e.y]],'#fff0d6',f*4);halo(c,e.x,e.y,75,'#fff7e8',(f-.72)*2);}
    }else if(e.kind==='ring'||e.kind==='death'||e.kind==='slam'||e.kind==='absorb'){circle(c,e.x,e.y,r,null,col,Math.max(.7,f*3));halo(c,e.x,e.y,r*1.4,col,.15);}else{halo(c,e.x,e.y,r,col,.24);for(let k=0;k<6;k++){const a=k*TAU/6+(e.x%3),d=r*(1-f*.5);line(c,[[e.x+Math.cos(a)*d*.65,e.y+Math.sin(a)*d*.65],[e.x+Math.cos(a)*d,e.y+Math.sin(a)*d]],col,1.5);}}c.restore();}
    c.restore();c.save();const vig=c.createRadialGradient(800,470,250,800,460,940);vig.addColorStop(0,'#00000000');vig.addColorStop(1,'#0403099c');c.fillStyle=vig;c.fillRect(0,0,1600,900);c.restore();
  }
  function screenToWorld(x,y,s){const cam=s.camera||s.player||{x:0,y:0};return{x:x-800+cam.x,y:y-480+cam.y};}
  root.DomainArt=Object.freeze({init,drawUnit,render,screenToWorld,colors,palettes});
})(typeof window!=='undefined'?window:globalThis);

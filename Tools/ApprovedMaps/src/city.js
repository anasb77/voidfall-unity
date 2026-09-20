// Expanded original Null City. Positions extend by 60%; architecture and actors do not.
import {transitFrame,TRANSIT_LEFT,TRANSIT_RIGHT,TRANSIT_Y,roadShake} from './presentation-rules.js';
export const CITY_SCALE=1.6;
export const cityWorld=(x,y)=>({x:(x-800)*CITY_SCALE,y:(y-450)*CITY_SCALE});
export function buildCity(floorStyle='subtle'){
 const a=window.NullCityArt,S=CITY_SCALE,W=1600*S,H=900*S,b=document.createElement('canvas');a.resetDecor();b.width=5120;b.height=2880;const g=b.getContext('2d');g.scale(2,2);
 const {path,line,rect,ellipse,circle,glow}=a;let seed=31791;const rand=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296};
 const sky=g.createLinearGradient(0,0,W,H);sky.addColorStop(0,'#1c0e2d');sky.addColorStop(.5,'#071224');sky.addColorStop(1,'#122735');g.fillStyle=sky;g.fillRect(0,0,W,H);
 for(const [x,y,r,c] of [[130,220,530,'#912b7e'],[1510,430,520,'#086c83'],[400,890,360,'#403681']])glow(g,x*S,y*S,r,c,.44);
 for(let i=0;i<950;i++){g.globalAlpha=.1+rand()*.4;circle(g,rand()*W,rand()*H,.4+rand(),'#c7e5ee')}g.globalAlpha=1;
 for(let i=0;i<46;i++){g.save();g.globalAlpha=.62;a.cityBlock(g,18+i*59,155+(i%4)*16,40+(i%3)*12,75+(i%5)*19,i);g.restore()}
 for(let i=0;i<27;i++)a.cityBlock(g,18+i*99,178*S+(i%3)*9,52+(i%4)*8,100+(i%5)*15,i+2);
 for(let i=0;i<7;i++){a.cityBlock(g,12,450+i*123,45,82+(i%2)*20,i);a.cityBlock(g,W-10,470+i*123,47,100+(i%2)*15,i+2)}
 for(const y of [130,175]){line(g,0,y*S,W,y*S+13,'#355c78',5);line(g,0,y*S-1,W,y*S+12,'#72bed277',1)}
 const points=arr=>arr.map(([x,y])=>[x*S,y*S]);const shape=points([[140,185],[1457,185],[1482,227],[1482,711],[1428,788],[190,788],[119,724],[119,240]]);
 for(let i=0;i<shape.length;i++){const p=shape[i],q=shape[(i+1)%shape.length];path(g,[p,q,[q[0]+25,q[1]+99],[p[0]-14,p[1]+82+rand()*86]],i%2?'#0b1727':'#142334','#203249')}
 for(let i=0;i<55;i++){const x=140+rand()*(W-250),y=1250+rand()*190,r=8+rand()*31;path(g,[[x,y],[x+r,y-13],[x+r*.6,y+r],[x-r*.4,y+r*.5]],'#182738','#394053')}
 path(g,shape,'#273449','#6b657855',2);
 const inner=points([[160,207],[1445,207],[1460,232],[1460,712],[1420,759],[180,759],[145,718],[145,240]]),floor=g.createLinearGradient(160*S,210*S,1440*S,760*S);floor.addColorStop(0,floorStyle==='original'?'#402c55':'#252437');floor.addColorStop(.45,'#1b3547');floor.addColorStop(1,floorStyle==='original'?'#3c2c55':'#26283b');path(g,inner,floor,'#bf99cb66',1.4);
 g.save();g.beginPath();inner.forEach((p,i)=>i?g.lineTo(...p):g.moveTo(...p));g.closePath();g.clip();
 if(floorStyle==='original')for(let row=0;row<22;row++)for(let col=0;col<38;col++){
  const x=140*S+col*58,y=202*S+row*44,k=Math.floor(rand()*4),tile=g.createLinearGradient(x,y,x+58,y+44);tile.addColorStop(0,['#423858','#324359','#293e52','#4a345b'][k]);tile.addColorStop(.46,['#282a47','#21354a','#1d3448','#2e2b4b'][k]);tile.addColorStop(1,['#393656','#35455c','#314559','#422f54'][k]);rect(g,x+1,y+1,55,41,tile,'#8194b536');line(g,x+2,y+2,x+55,y+2,'#c4bad93b',1);line(g,x+2,y+3,x+2,y+40,'#9295bc35',.8);line(g,x+2,y+42,x+56,y+42,'#030a22aa',1);
  if((row*5+col)%11===0){line(g,x+5,y+4,x+18,y+4,'#8ae5e566',1.2);line(g,x+5,y+4,x+5,y+13,'#c28de44a',1)}
 }
 else for(let row=0;row<11;row++)for(let col=-1;col<17;col++){
  // Broad, matte panels retain the old purple/teal lighting without checkerboard bevels.
  const x=140*S+col*146+(row%2)*73,y=202*S+row*94,k=Math.floor(rand()*4),panel=g.createLinearGradient(x,y,x+140,y+88);
  panel.addColorStop(0,['#252d3c','#24303d','#242d3a','#2a293c'][k]);panel.addColorStop(1,['#202735','#202c38','#1d2b38','#262536'][k]);
  path(g,[[x+2,y+2],[x+134,y+2],[x+144,y+12],[x+144,y+91],[x+2,y+91]],panel,'#70818d16',.8);
  line(g,x+4,y+3,x+131,y+3,'#a1afb316',.7);
  if((col+row*3)%7===0){line(g,x+13,y+12,x+29,y+12,'#669d9e40',1);line(g,x+13,y+12,x+13,y+18,'#8a7eac35',.8)}
  if((col+row)%5===0){line(g,x+114,y+72,x+135,y+72,'#080f1933',1);line(g,x+114,y+76,x+135,y+76,'#080f1933',1)}
 }
 for(const [x,y,c] of [[250,430,'#d92bcd'],[1300,500,'#226cc9'],[755,252,'#753ca2']]){g.save();g.translate(x*S,y*S);g.scale(1,.45);glow(g,0,0,380,c,.17);g.restore()}g.restore();
 for(const y of [345,592]){rect(g,180*S,y*S-38,1240*S,76,'#0b1827','#506479');for(const off of [-35,-29,29,35])line(g,182*S,y*S+off,1418*S,y*S+off,Math.abs(off)===35?'#7899aa55':'#182c42',1.2);for(let x=197*S;x<1405*S;x+=40)path(g,[[x,y*S-7],[x+11,y*S],[x,y*S+7]],null,'#46657d45',1)}
 for(const x of [515,983,1057]){rect(g,x*S-27,219*S,54,526*S,'#0c1b2a','#426178');line(g,x*S-23,221*S,x*S-23,743*S,'#73d8d628');line(g,x*S+23,221*S,x*S+23,743*S,'#73d8d628');for(let y=238*S;y<742*S;y+=39)path(g,[[x*S-6,y],[x*S,y+10],[x*S+6,y]],null,'#506b8148')}
 for(const [x,y] of [[613,265],[1167,449],[443,700],[720,500],[1050,675]]){g.save();g.translate(x*S,y*S);path(g,[[-33,0],[-20,-15],[30,-15],[40,0],[26,14],[-24,14]],'#192c3a','#57747d65');for(let j=0;j<4;j++)line(g,-15+j*8,-4,-10+j*8,4,'#518b9855');g.restore()}
 [[295,201,38,163],[1103,200,41,209],[63,424,51,237],[54,677,54,216],[1533,401,51,207],[1540,683,52,240],[347,892,47,132],[1283,895,54,123]].forEach(([x,y,r,h],i)=>a.tower(g,x*S,y*S,r,h,['#ee70db','#64efe5','#85baff'][i%3],i));
 a.habitat(g,431*S,205*S,63,'#6cddfa');a.forked(g,585*S,205*S,46,'#ffd59d');a.citadel(g,750*S,206*S,75,'#8effdd');a.reactor(g,930*S,207*S,56,'#ea89ef');a.dome(g,1268*S,188*S,72,'#ffc998');
 for(const [x,y,r,c] of [[49,249,70,'#64efe5'],[1538,223,68,'#ee70db'],[84,789,72,'#ee70db'],[1520,795,70,'#64efe5'],[560,845,83,'#85baff'],[1044,853,85,'#ee70db']])a.dome(g,x*S,y*S,r,c);
 g.save();g.translate(800*(S-1),800*(S-1));a.hangar(g);g.restore();
 const sign={x:1172*S,y:104*S};g.save();g.translate(sign.x-1172,sign.y-104);for(const x of [1078,1250])line(g,x,92,x,163,'#77829b',4);path(g,[[1023,88],[1040,72],[1310,72],[1322,86],[1322,136],[1023,136]],'#12162c','#8587aa',1.7);rect(g,1032,80,281,48,'#020919','#e18bd470');g.restore();
 for(const y of [224,239])line(g,281*S,y*S,1319*S,y*S,'#9fa0b744',2);line(g,280*S,231*S,1320*S,231*S,'#37d9ef26',5);for(const x of [279,1320]){ellipse(g,x*S,230*S,14,27,'#101527','#8d94ad',3);ellipse(g,x*S,230*S,10,22,'#030914','#64efe5',1.4)}
 return b;
}

export function drawCity(g,bg,sim,time,transit,reducedMotion=false){
 const S=CITY_SCALE,a=window.NullCityArt;g.drawImage(bg,-1280,-720,2560,1440);
 if(sim.phase===1){g.fillStyle='#02081577';g.fillRect(-1280,-720,2560,1440)}
 // Original architecture registers its light positions during background creation.
 g.save();g.translate(-1280,-720);a.live(g,time,sim.phase,sim.phaseTime,sim.phase===1?.82:0,null,{core:true,transit:false,traffic:false,hangar:false,lcd:false});g.restore();
 g.save();g.translate(-1280+800*(S-1),-720+800*(S-1));a.live(g,time,sim.phase,sim.phaseTime,sim.phase===1?.82:0,null,{core:false,transit:false,traffic:false,hangar:true,lcd:false});g.restore();
 const sign=cityWorld(1172,104);g.fillStyle=sim.phase?'#ffa3b8':'#f3c4ff';g.font='600 17px Chakra';g.textAlign='center';g.fillText(sim.phase?'INTRUDER DETECTED':'WELCOME TO NULL CITY',sign.x,sign.y+2);g.font='8px Chakra';g.fillStyle='#b6a6d9';g.fillText(sim.phase?'NCPD / RESPONSE DEPLOYED':'NULL SECURITY / SURVEILLANCE',sign.x,sign.y+18);
 const train=transitFrame(time);
 if(transit.complete&&transit.naturalWidth&&train.visible){g.save();g.beginPath();g.rect(train.clipLeft,TRANSIT_Y-55,train.clipRight-train.clipLeft,110);g.clip();g.drawImage(transit,train.x-95,TRANSIT_Y-32,190,80);g.restore()}
 // Portal mouths are in front of the vehicle, so its carriages disappear into them.
 for(const x of [TRANSIT_LEFT,TRANSIT_RIGHT]){a.ellipse(g,x,TRANSIT_Y,14,27,'#101527','#8d94ad',3);a.ellipse(g,x,TRANSIT_Y,10,22,'#030914','#64efe5',1.4);a.glow(g,x,TRANSIT_Y,32,'#64efe5',.12)}
 const hz=sim.hazard();if(hz){
  const p=cityWorld(hz.x+hz.w/2,hz.y+hz.h/2),vertical=hz.lane>=2,w=vertical?54:hz.w*S,h=vertical?hz.h*S:68,x=p.x-w/2,y=p.y-h/2;
  if(!hz.fire){g.fillStyle='#df8f5325';g.fillRect(x,y,w,h);g.strokeStyle='#e39c67';g.lineWidth=1.5;g.strokeRect(x,y,w,h)}
  else{
   const shake=roadShake(time,true,reducedMotion),heat=reducedMotion?1:.88+Math.sin(time*23)*.12;
   g.save();g.beginPath();g.rect(x-5,y-5,w+10,h+10);g.clip();g.translate(vertical?shake:0,vertical?0:shake);
   g.drawImage(bg,(x+1280)*2,(y+720)*2,w*2,h*2,x,y,w,h);
   g.fillStyle=`rgba(245,107,54,${heat*.32})`;g.fillRect(x,y,w,h);g.strokeStyle='#ffbe88';g.lineWidth=2;g.strokeRect(x+2,y+2,w-4,h-4);
   const length=vertical?h:w;g.save();g.translate(p.x,p.y);if(vertical)g.rotate(Math.PI/2);
   g.shadowColor='#ff8f54';g.shadowBlur=20;for(const thickness of [24,10,3]){g.strokeStyle=thickness===3?'#fff0c9':thickness===10?'#ffd28fcc':'#ff864055';g.lineWidth=thickness;g.beginPath();g.moveTo(-length/2,0);g.lineTo(length/2,0);g.stroke()}g.shadowBlur=0;
   for(let n=0;n<length;n+=68){const phase=Math.floor(time*(reducedMotion?0:16))+n;const off=Math.sin(phase*1.73)*15;a.path(g,[[n-length/2,-13],[n+13-length/2,off],[n+27-length/2,-off*.5],[n+40-length/2,11]],null,'#ffdda399',1.2)}
   g.restore();g.restore();
   // Energy spill is local to the conduit, never a camera or screen shake.
   g.save();g.globalAlpha=.12;g.shadowColor='#ff9a62';g.shadowBlur=35;g.strokeStyle='#ffad7c';g.lineWidth=7;g.strokeRect(x,y,w,h);g.restore();
  }
 }
}

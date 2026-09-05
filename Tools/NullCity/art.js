/* Canvas-native art: alien architecture, VoidFall silhouettes, no runtime image dependencies. */
(() => {
  'use strict';
  const W=1600,H=900,TAU=Math.PI*2;
  const palette={cyan:'#64efe5',pink:'#ee70db',blue:'#85baff',gold:'#ffc29a'};
  let seed=31791;const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
  const range=(a,b)=>a+random()*(b-a);
  const ports=[],bands=[],holograms=[];
  function path(g,p,fill,stroke,width=1){g.beginPath();p.forEach((v,i)=>i?g.lineTo(...v):g.moveTo(...v));g.closePath();if(fill){g.fillStyle=fill;g.fill();}if(stroke){g.strokeStyle=stroke;g.lineWidth=width;g.stroke();}}
  function line(g,x,y,xx,yy,c,w=1){g.strokeStyle=c;g.lineWidth=w;g.beginPath();g.moveTo(x,y);g.lineTo(xx,yy);g.stroke();}
  function rect(g,x,y,w,h,fill,stroke){if(fill){g.fillStyle=fill;g.fillRect(x,y,w,h);}if(stroke){g.strokeStyle=stroke;g.lineWidth=1;g.strokeRect(x,y,w,h);}}
  function ellipse(g,x,y,rx,ry,fill,stroke,w=1){g.beginPath();g.ellipse(x,y,Math.max(.01,rx),Math.max(.01,ry),0,0,TAU);if(fill){g.fillStyle=fill;g.fill();}if(stroke){g.strokeStyle=stroke;g.lineWidth=w;g.stroke();}}
  function circle(g,x,y,r,fill,stroke,w=1){ellipse(g,x,y,r,r,fill,stroke,w);}
  function glow(g,x,y,r,c,alpha=.2){g.save();g.globalAlpha*=alpha;const z=g.createRadialGradient(x,y,0,x,y,r);z.addColorStop(0,c);z.addColorStop(1,'transparent');g.fillStyle=z;g.fillRect(x-r,y-r,r*2,r*2);g.restore();}
  function text(g,s,x,y,size=10,c='#94a9b8',align='left'){g.fillStyle=c;g.font=`${size}px monospace`;g.textAlign=align;g.fillText(s,x,y);}
  function rounded(g,x,y,w,h,r,fill,stroke,lw=1){g.beginPath();g.roundRect(x,y,w,h,r);if(fill){g.fillStyle=fill;g.fill();}if(stroke){g.strokeStyle=stroke;g.lineWidth=lw;g.stroke();}}
  function glyph(g,x,y,size,index,color){
    g.save();g.translate(x,y);g.scale(size/20,size/20);g.strokeStyle=color;g.lineWidth=1.7;g.lineCap='square';
    const shapes=[[[0,-9],[7,-3],[0,3],[-7,-3],[0,-9],[0,10]],[[8,-9],[-6,-9],[-6,9],[7,9],[7,1],[-1,1]],[[0,-10],[-7,2],[7,2],[0,-10],[0,10]],[[0,-8],[-7,0],[0,8],[7,0],[0,-8]],[[7,-8],[-7,-8],[0,0],[-7,8],[7,8]],[[0,-10],[0,10],[-7,4],[7,-4],[-7,-4]]];
    g.beginPath();shapes[index%shapes.length].forEach((p,i)=>i?g.lineTo(...p):g.moveTo(...p));g.stroke();circle(g,8,11,1.3,color);g.restore();
  }
  function tower(g,x,base,r,h,c,i){
    const top=base-h;
    ellipse(g,x+18,base+19,r*1.2,r*.37,'#01040a99');
    ellipse(g,x,base+6,r*1.14,r*.30,'#101525','#687388',1);
    const body=new Path2D();body.moveTo(x-r,base);body.bezierCurveTo(x-r*.8,base-h*.29,x-r*.42,top+h*.36,x-r*.33,top+20);body.quadraticCurveTo(x-r*.26,top,x,top);body.quadraticCurveTo(x+r*.26,top,x+r*.33,top+20);body.bezierCurveTo(x+r*.38,top+h*.4,x+r*.72,base-h*.3,x+r,base);body.quadraticCurveTo(x,base+r*.35,x-r,base);
    const material=[['#333751','#798696','#b2adba','#818798','#252b43'],['#233d43','#5d999a','#c3d5c4','#73a6a8','#19333f'],['#493845','#a17487','#e2b9b2','#9b7186','#30243d'],['#3b354b','#827693','#c6bfd8','#807294','#24243e']][Math.abs(i)%4];
    const grad=g.createLinearGradient(x-r,0,x+r,0);material.forEach((v,j)=>grad.addColorStop([0,.26,.46,.62,1][j],v));
    g.fillStyle=grad;g.fill(body);g.strokeStyle='#bac0c24d';g.lineWidth=1;g.stroke(body);
    g.save();g.clip(body);
    for(let j=0;j<5;j++){const y=base-h*.14-j*h*.16,rr=r*(.95-j*.11);ellipse(g,x,y,rr,rr*.18,null,'#17213e',2);ellipse(g,x,y-2,rr,rr*.18,null,'#c0c8d932',.7);}
    for(let j=0;j<3;j++){const y=base-h*.67-j*13;circle(g,x+r*.16,y,2.5,'#111c32','#879ca0',.6);}
    const vx=x+r*.24,vy=base-h*.35;
    ellipse(g,vx,vy,r*.34,h*.12,'#071b2a','#64798b',2);
    for(let j=-5;j<=5;j++)line(g,vx-r*.24,vy+j*4,vx+r*.24,vy+j*4,'#1c829166',1);
    line(g,x-r*.19,top+20,x-r*.59,base-20,'#edf0f528',2);g.restore();
    path(g,[[x-3,top+6],[x-1,top-55],[x+2,top-65],[x+4,top+6]],'#627086','#adb6c6',.7);
    circle(g,x+1,top-56,2,c);
    ports.push({x:vx,y:vy,rx:r*.27,ry:h*.095,c,seed:i*.73,search:i%4===0});
    bands.push({x,y:base-h*.13,rx:r*.83,ry:r*.16,c,index:i});
    if(i%2===0)holograms.push({x:x-r*.54,y:base-h*.54,w:r*.46,h:h*.44,c,index:i});
  }
  function dome(g,x,y,r,c){
    ellipse(g,x+13,y+21,r*1.1,r*.4,'#010610aa');
    const grd=g.createLinearGradient(x-r,y-r,x+r,y+30);grd.addColorStop(0,'#a8a2b3');grd.addColorStop(.42,'#667993');grd.addColorStop(.8,'#263650');grd.addColorStop(1,'#111e34');
    ellipse(g,x,y+8,r,r*.42,'#202b3e','#7c86a0',1.5);
    g.beginPath();g.moveTo(x-r,y);g.bezierCurveTo(x-r,y-r*.47,x-r*.45,y-r*.8,x,y-r*.8);g.bezierCurveTo(x+r*.45,y-r*.8,x+r,y-r*.47,x+r,y);g.bezierCurveTo(x+r*.6,y+r*.37,x-r*.6,y+r*.37,x-r,y);g.fillStyle=grd;g.fill();g.strokeStyle='#b6bfd044';g.stroke();
    ellipse(g,x,y,r*.88,r*.24,'#07121f','#697f9a',1.5);
    for(let j=-3;j<=3;j++){const a=j*.32;ellipse(g,x+Math.sin(a)*r*.77,y+Math.cos(a)*r*.09,4,7,'#365c72',c,1);}
    ellipse(g,x,y-r*.46,r*.58,r*.16,'#27344d','#bec1ce',1.3);
    bands.push({x,y:y-r*.04,rx:r*.96,ry:r*.27,c,index:r});
    line(g,x,y-r*.46,x+1,y-r*.97,'#a0b3c4',1.5);circle(g,x+1,y-r*.97,2.4,c);
  }
  function habitat(g,x,y,r,c){
    ellipse(g,x,y+10,r*1.1,18,'#071322','#6b8290');
    for(let j=0;j<3;j++){
      const yy=y-j*35,rr=r-j*8,grad=g.createLinearGradient(x-rr,yy,x+rr,yy);grad.addColorStop(0,'#213c48');grad.addColorStop(.4,'#809fab');grad.addColorStop(1,'#2b435d');
      rounded(g,x-rr,yy-45,rr*2,49,24,grad,'#a3b9c566',1.3);ellipse(g,x,yy-37,rr*.85,12,'#183444','#b0d0d5',1);
      for(let k=-2;k<=2;k++)rounded(g,x+k*19-5,yy-24,10,13,5,'#152536',c,1);
      bands.push({x,y:yy,rx:rr*.9,ry:10,c,index:j+5});
    }
    line(g,x,y-119,x,y-165,'#829fae',2);circle(g,x,y-168,4,c);
  }
  function forked(g,x,y,r,c){
    const grd=g.createLinearGradient(x-r,0,x+r,0);grd.addColorStop(0,'#3a3b38');grd.addColorStop(.4,'#b8b18e');grd.addColorStop(1,'#4e5761');
    path(g,[[x-r,y],[x-r*.62,y-65],[x-r*.48,y-166],[x-r*.18,y-181],[x-r*.11,y-104],[x+r*.13,y-99],[x+r*.2,y-188],[x+r*.5,y-174],[x+r*.66,y-57],[x+r,y]],grd,'#c4c5aa88',1.5);
    ellipse(g,x,y-63,r*.30,26,'#071525',c,2);ports.push({x,y:y-63,rx:r*.25,ry:22,c,seed:2,search:false});
    for(const side of [-1,1]){line(g,x+side*r*.3,y-170,x+side*r*.31,y-213,'#9cacb9',1.5);circle(g,x+side*r*.31,y-216,2.5,c);}
    bands.push({x,y:y-15,rx:r*.82,ry:10,c,index:6});
  }
  function citadel(g,x,y,r,c){
    ellipse(g,x,y+7,r*1.1,19,'#0a1c22','#6e9993');
    const grd=g.createLinearGradient(x-r,y,x+r,y-160);grd.addColorStop(0,'#1c444c');grd.addColorStop(.4,'#588482');grd.addColorStop(.7,'#acbab2');grd.addColorStop(1,'#23484b');
    const p=new Path2D();p.moveTo(x-r,y);p.lineTo(x-r*.75,y-122);p.quadraticCurveTo(x,y-197,x+r*.75,y-122);p.lineTo(x+r,y);p.lineTo(x+r*.43,y);p.lineTo(x+r*.37,y-94);p.quadraticCurveTo(x,y-134,x-r*.37,y-94);p.lineTo(x-r*.43,y);p.closePath();g.fillStyle=grd;g.fill(p);g.strokeStyle='#c0d5c455';g.stroke(p);
    for(let i=0;i<6;i++){const a=Math.PI+i/5*Math.PI,xx=x+Math.cos(a)*r*.65,yy=y-106+Math.sin(a)*r*.45;circle(g,xx,yy,4,'#0b2937',c,1);}
    rounded(g,x-r*.4,y-130,r*.8,18,7,'#102e38',c,1);for(let i=0;i<4;i++)glyph(g,x-22+i*15,y-122,8,i,c);
    bands.push({x,y:y-7,rx:r*.94,ry:12,c,index:7});
  }
  function reactor(g,x,y,r,c){
    path(g,[[x-r,y],[x-r*.8,y-41],[x+r*.8,y-41],[x+r,y]],'#392943','#8e789f',1.4);
    ellipse(g,x,y-61,r*.62,r*.65,'#211a33',c,1.6);
    const z=g.createRadialGradient(x-10,y-77,4,x,y-61,r*.61);z.addColorStop(0,'#fbdcff');z.addColorStop(.24,'#be66b8');z.addColorStop(.65,'#673f7b');z.addColorStop(1,'#18152c');ellipse(g,x,y-61,r*.48,r*.51,z);
    for(const s of [-1,1])path(g,[[x+s*r*.79,y-13],[x+s*r*.83,y-109],[x+s*r*.5,y-151],[x+s*r*.42,y-117],[x+s*r*.56,y-15]],'#6c537b','#ab89ac',1.2);
    ellipse(g,x,y-67,r*.92,12,null,'#bc9fc966',2);bands.push({x,y:y-67,rx:r*.9,ry:12,c,index:8});
  }
  function hangar(g){
    path(g,[[619,780],[644,747],[943,747],[985,780],[984,866],[619,866]],'#172537','#73849b',1.7);
    path(g,[[626,776],[651,739],[931,739],[977,776]],'#3b4b60','#92a0b277',1.5);
    for(let i=0;i<3;i++){
      const x=680+i*110;rounded(g,x-49,777,98,80,8,'#060e1c','#566c87',2);rect(g,x-43,781,86,70,'#080f1b');
      for(let j=0;j<8;j++)line(g,x-40,788+j*8,x+40,788+j*8,'#243c58',2);
      line(g,x-43,858,x+43,858,'#6e8fb4',2);text(g,'0'+(i+1),x,771,10,'#a8bdcc','center');
    }
    for(let i=0;i<4;i++)rect(g,635+i*107,754,25,5,'#6689b5');
    for(const x of [614,991]){rect(g,x-4,785,8,63,'#38475b');circle(g,x,783,5,'#c0d5ed');}
    text(g,'N U L L  /  R E S P O N S E',801,878,10,'#7597b8','center');
  }
  function cityBlock(g,x,y,w,h,i){
    const colors=[['#2d3357','#7486ab','#3e6582'],['#513254','#ae77a3','#874a89'],['#204657','#699eaf','#366477'],['#514553','#a895a8','#72556c']],c=colors[i%4];
    g.save();const grd=g.createLinearGradient(x-w/2,y,x+w/2,y);grd.addColorStop(0,c[0]);grd.addColorStop(.43,c[1]);grd.addColorStop(1,c[0]);
    path(g,[[x-w*.55,y],[x-w*.35,y-h+18],[x-w*.15,y-h],[x+w*.27,y-h+4],[x+w*.55,y]],grd,c[1]+'aa',.8);
    if(i%3===0){ellipse(g,x,y-h+22,w*.36,11,c[0],c[1],1);line(g,x,y-h+9,x+4,y-h-32,c[1],1);}
    for(let row=0;row<Math.floor(h/16)-1;row++)for(let col=0;col<3;col++){
      if((row+col+i)%5===0)continue;
      rounded(g,x-w*.23+col*w*.2,y-14-row*14,Math.max(2,w*.09),4,1,(i+col)%2?'#72bfc5':'#f0a0d6');
    }
    line(g,x-w*.35,y-8,x+w*.38,y-8,i%2?'#f180d6':'#58d1de',2);
    if(i%4===0){rounded(g,x+w*.15,y-h*.62,w*.23,h*.4,3,'#101d31','#bb6bad',1);for(let k=0;k<3;k++)glyph(g,x+w*.27,y-h*.53+k*14,9,i+k,'#e18bd0');}
    g.restore();
  }
  function makeBackground(){
    const bg=document.createElement('canvas');bg.width=W;bg.height=H;const g=bg.getContext('2d');
    const sky=g.createLinearGradient(0,0,W,H);sky.addColorStop(0,'#1c0e2d');sky.addColorStop(.5,'#071224');sky.addColorStop(1,'#122735');g.fillStyle=sky;g.fillRect(0,0,W,H);
    for(const [x,y,r,c] of [[130,220,530,'#912b7e'],[1510,430,520,'#086c83'],[400,890,360,'#403681']])glow(g,x,y,r,c,.44);
    for(let i=0;i<570;i++){const x=range(0,W),y=range(0,H),a=range(.1,.6);g.globalAlpha=a;circle(g,x,y,range(.4,1.2),'#c7e5ee');}g.globalAlpha=1;
    // Lit districts at several depths, with rooflines, windows and elevated links.
    for(let i=0;i<28;i++){const x=18+i*59,y=90+(i%4)*16;g.save();g.globalAlpha=.62;cityBlock(g,x,y,40+(i%3)*12,75+(i%5)*19,i);g.restore();}
    for(let i=0;i<17;i++){const x=18+i*99;cityBlock(g,x,178+(i%3)*9,52+(i%4)*8,100+(i%5)*15,i+2);}
    for(let i=0;i<4;i++){cityBlock(g,12,332+i*123,45,82+(i%2)*20,i);cityBlock(g,1590,360+i*121,47,100+(i%2)*15,i+2);}
    for(const y of [130,175]){line(g,0,y,1600,y+13,'#355c78',5);line(g,0,y-1,1600,y+12,'#72bed277',1);}
    // The foundation is a severed fragment, with sheer fractured undersides.
    const shape=[[140,185],[1457,185],[1482,227],[1482,711],[1428,788],[190,788],[119,724],[119,240]];
    for(let i=0;i<shape.length;i++){const a=shape[i],b=shape[(i+1)%shape.length];path(g,[a,b,[b[0]+25,b[1]+99],[a[0]-14,a[1]+range(82,168)]],i%2?'#0b1727':'#142334','#203249');}
    for(let i=0;i<31;i++){const x=range(90,1510),y=range(775,930),r=range(8,39);path(g,[[x,y],[x+r,y-13],[x+r*.6,y+r],[x-r*.4,y+r*.5]],'#182738','#394053');}
    path(g,shape,'#273449','#6b657855',2);
    const inner=[[160,207],[1445,207],[1460,232],[1460,712],[1420,759],[180,759],[145,718],[145,240]];
    const floor=g.createLinearGradient(160,210,1440,760);floor.addColorStop(0,'#402c55');floor.addColorStop(.45,'#1b3547');floor.addColorStop(1,'#3c2c55');path(g,inner,floor,'#bf99cb66',1.4);
    // Glazed vaporwave stone: bevelled individual tiles, tinted reflections and fine seams.
    g.save();g.beginPath();inner.forEach((p,i)=>i?g.lineTo(...p):g.moveTo(...p));g.closePath();g.clip();
    for(let row=0;row<13;row++)for(let col=0;col<23;col++){
      const x=140+col*58,y=202+row*44,choice=Math.floor(random()*4);
      const tile=g.createLinearGradient(x,y,x+58,y+44);tile.addColorStop(0,['#423858','#324359','#293e52','#4a345b'][choice]);tile.addColorStop(.46,['#282a47','#21354a','#1d3448','#2e2b4b'][choice]);tile.addColorStop(1,['#393656','#35455c','#314559','#422f54'][choice]);
      rect(g,x+1,y+1,55,41,tile,'#8194b536');line(g,x+2,y+2,x+55,y+2,'#c4bad93b',1);line(g,x+2,y+3,x+2,y+40,'#9295bc35',.8);line(g,x+2,y+42,x+56,y+42,'#030a22aa',1);
      if((row*5+col)%11===0){line(g,x+5,y+4,x+18,y+4,'#8ae5e566',1.2);line(g,x+5,y+4,x+5,y+13,'#c28de44a',1);}
      if(random()>.8)line(g,x+7,y+9,x+42,y+32,'#b3c3e413',.8);
    }
    for(let i=0;i<9000;i++){const x=range(140,1460),y=range(200,770);rect(g,x,y,range(.6,1.4),.6,random()>.5?'#d2d2ec0c':'#050b171a');}
    for(const [x,y,c] of [[250,430,'#d92bcd'],[1300,500,'#226cc9'],[755,252,'#753ca2']]){g.save();g.translate(x,y);g.scale(1,.45);glow(g,0,0,300,c,.17);g.restore();}g.restore();
    // Two horizontal conduits and an offset double vertical conduit.
    for(const y of [345,592]){
      rect(g,180,y-38,1240,76,'#0b1827','#506479');
      for(const off of [-35,-29,29,35])line(g,182,y+off,1418,y+off,Math.abs(off)===35?'#7899aa55':'#182c42',1.2);
      for(let x=197;x<1405;x+=40){path(g,[[x,y-7],[x+11,y],[x,y+7]],null,'#46657d45',1);}
    }
    for(const x of [515,983,1057]){
      rect(g,x-27,219,54,526,'#0c1b2a','#426178');
      line(g,x-23,221,x-23,743,'#73d8d628');line(g,x+23,221,x+23,743,'#73d8d628');
      for(let y=238;y<742;y+=39)path(g,[[x-6,y],[x,y+10],[x+6,y]],null,'#506b8148');
    }
    // Alien junction plates sit between the conduits; the combat floor stays open.
    for(const [x,y] of [[613,265],[1167,449],[443,700]]){
      path(g,[[x-33,y],[x-20,y-15],[x+30,y-15],[x+40,y],[x+26,y+14],[x-24,y+14]],'#192c3a','#57747d65');
      for(let j=0;j<4;j++)line(g,x-15+j*8,y-4,x-10+j*8,y+4,'#518b9855');
    }
    // Tall tapered shells + low saucer structures echo the architectural reference.
    [[295,201,38,163],[1103,200,41,209],
     [63,424,51,237],[54,677,54,216],[1533,401,51,207],[1540,683,52,240],
     [347,892,47,132],[1283,895,54,123]].forEach((a,i)=>tower(g,...a,i%3===0?palette.pink:i%3===1?palette.cyan:palette.blue,i));
    habitat(g,431,205,63,'#6cddfa');forked(g,585,205,46,'#ffd59d');citadel(g,750,206,75,'#8effdd');reactor(g,930,207,56,'#ea89ef');dome(g,1268,188,72,'#ffc998');
    dome(g,49,249,70,palette.cyan);dome(g,1538,223,68,palette.pink);
    dome(g,84,789,72,palette.pink);dome(g,1520,795,70,palette.cyan);
    dome(g,560,845,83,palette.blue);dome(g,1044,853,85,palette.pink);
    hangar(g);
    // The upper district's wall-mounted security display.
    for(const x of [1078,1250])line(g,x,92,x,163,'#77829b',4);
    path(g,[[1023,88],[1040,72],[1310,72],[1322,86],[1322,136],[1023,136]],'#12162c','#8587aa',1.7);
    rect(g,1032,80,281,48,'#020919','#e18bd470');
    // Transit track curls through gateways above the play area.
    for(const yy of [224,239])line(g,281,yy,1319,yy,'#9fa0b744',2);
    line(g,280,231,1320,231,'#37d9ef26',5);
    for(const x of [279,1320]){ellipse(g,x,230,14,27,'#101527','#8d94ad',3);ellipse(g,x,230,10,22,'#030914',palette.cyan,1.4);}
    return bg;
  }
  function live(g,t,phase,phaseTime,dark,sim,options){
    const layers=options||{};
    const power=1-dark*.77,charge=phase===1?.55+Math.sin(phaseTime*2.6)*.2:0;
    if(layers.core!==false){
    // Ground reflections are broad and saturated; actor outlines are drawn later.
    for(const [x,y,c] of [[350,248,palette.pink],[1280,288,palette.cyan],[305,683,palette.pink],[1258,717,palette.blue],[777,243,palette.cyan]]){
      g.save();g.translate(x,y);g.scale(1,.38);glow(g,0,0,195,c,.28*power);g.restore();
      g.save();g.globalAlpha=.21*power;for(let k=0;k<25;k++){const yy=y+k*3.2,len=(25-k)*2.4,wave=Math.sin(k*1.7+t*1.3)*6;line(g,x-len+wave,yy,x+len+wave,yy,c,k%3?1:2.4);}g.restore();
    }
    for(const band of bands){
      const c=phase===1?'#ffad8c':band.c;
      g.save();g.globalAlpha=power*(.78+Math.sin(t+band.index)*.14);g.shadowColor=c;g.shadowBlur=18;
      ellipse(g,band.x,band.y,band.rx,band.ry,null,c,3);ellipse(g,band.x,band.y-5,band.rx*.93,band.ry*.9,null,c,1);g.restore();
      glow(g,band.x,band.y,band.rx*2,c,power*.18);
    }
    for(const p of ports){
      const open=phase===0?.75+Math.sin(t*.35+p.seed)*.2:.16;
      const c=phase===1?'#ffb78c':p.c;
      g.save();g.globalAlpha=.2+power*.8;ellipse(g,p.x,p.y,p.rx,p.ry,null,c,1.4);ellipse(g,p.x,p.y,p.rx*open,p.ry*.8,c+'50');glow(g,p.x,p.y,55,c,power*(.3+charge*.2));
      for(let n=-3;n<=3;n++)line(g,p.x-p.rx*.65,p.y+n*p.ry*.2,p.x+p.rx*.65,p.y+n*p.ry*.2,c+'90',.7);
      g.restore();
      if(phase===1){const f=(t*.7+p.seed)%1;line(g,p.x,p.y+f*70,p.x,p.y+f*70+10,'#ffe6c7',2);}
    }
    for(const h of holograms){
      g.save();g.globalAlpha=power*.8+(dark>.7&&Math.sin(t*3+h.index)>.985?.4:0);
      glow(g,h.x,h.y+h.h/2,100,h.c,.25);g.shadowColor=h.c;g.shadowBlur=12;
      for(let i=0;i<3;i++)glyph(g,h.x,h.y+i*23,17,(i+h.index+Math.floor(t*.13))%6,h.c);
      g.shadowBlur=0;line(g,h.x-13,h.y-16,h.x+15,h.y-16,h.c,1);line(g,h.x-13,h.y+63,h.x+15,h.y+63,h.c,1);g.restore();
    }
    // Larger floating glyph installations replace rectangular Earth advertisements.
    for(const [x,y,c,i] of [[226,469,palette.pink,1],[1377,472,palette.cyan,3],[790,178,palette.pink,2]]){
      const yy=y+Math.sin(t*.65+i)*5;g.save();g.globalAlpha=power*.76;
      glow(g,x,yy,123,c,.42);path(g,[[x-35,yy-42],[x+35,yy-42],[x+29,yy+38],[x-29,yy+38]],c+'09',c+'aa',1);
      glyph(g,x,yy,46,i,c);line(g,x-21,yy+29,x+21,yy+29,c,3);
      for(let j=0;j<24;j++)line(g,x-30,yy-40+j*3,x+30,yy-40+j*3,c+'1a',.8);g.restore();
    }
    }
    // Rounded, segmented autonomous transit craft. It crosses only the perimeter.
    if(layers.transit!==false){
    const bus=(t*78)%1280+190;
    for(let n=2;n>=0;n--){const x=bus-n*51;
      g.save();g.translate(x,230);ellipse(g,6,11,29,8,'#02071180');glow(g,0,7,39,palette.cyan,.18);
      rounded(g,-27,-13,53,24,12,'#4d5872','#9fa9be',1.2);rounded(g,-22,-9,43,11,5,'#061d30',palette.cyan,.7);
      for(let k=-15;k<=15;k+=10)rounded(g,k-2,-7,5,7,2,power>.3?'#65eef2':'#324f66');
      line(g,-19,10,18,10,palette.pink,2);ellipse(g,22,0,2,4,'#eefff4');g.restore();
    }
    }
    // Lane signals and small autonomous road traffic. Orange hatching remains danger-only.
    if(layers.traffic!==false){
    const trafficColor=phase===1?'#639fff':'#78d9d2';
    for(const y of [345,592]){
      for(let i=0;i<4;i++){
        const x=phase===1?1400-((t*27+i*297)%1190):198+((t*42+i*295)%1200);
        const yy=y+(i%2?16:-16);g.save();g.globalAlpha=phase===1?.55:.8;
        ellipse(g,x+3,yy+4,9,4,'#030716');rounded(g,x-8,yy-4,16,8,4,'#4a6173',trafficColor,.6);rect(g,x+5,yy-2,2,4,'#e0ffec');glow(g,x,yy,17,trafficColor,.16);line(g,x-15,yy,x-9,yy,trafficColor+'55',1);g.restore();
      }
      for(const x of [515,983,1057]){
        const c=phase===1?'#4e8dff':'#5ec8a7';
        rounded(g,x+33,y-46,17,8,3,'#0b1728','#4a6679');for(let i=0;i<3;i++)circle(g,x+37+i*4.5,y-42,1.5,c);
      }
    }
    for(const x of [515,983,1057])for(let i=0;i<6;i++){
      const y=230+((t*(phase===1?30:48)+i*87)%510);g.save();g.globalAlpha=phase===1?.28:.2;
      path(g,[[x-5,y],[x,y+6],[x+5,y]],null,trafficColor,1.2);g.restore();
    }
    }
    // Blackout shutters retract over the first 1.5 seconds, before police deployment.
    if(layers.hangar!==false){
    const open=phase===1&&!(sim&&sim.bossDefeated)?Math.min(1,phaseTime/1.5):0;
    for(let i=0;i<3;i++){
      const x=680+i*110;
      if(open>0){rect(g,x-43,781,86,70,'#02091a');glow(g,x,811,67,'#2684ff',.35*open);
        for(const s of [-1,1])rect(g,x+(s<0?-43:43*(1-open)),781,43*(1-open),69,'#233753');
        path(g,[[x-40,793],[x+40,793],[x+57,714],[x-57,714]],'#388eff0e');
        for(let k=0;k<3;k++)path(g,[[x-8,762-k*15],[x,756-k*15],[x+8,762-k*15]],null,'#589fff55',1.5);
      }
      const beacon=phase===1?(Math.sin(t*8+i)>.1?'#64a8ff':'#1552b5'):'#465f74';
      line(g,x-43,780,x+43,780,beacon,3);if(open>0)glow(g,x,780,52,beacon,.32);
    }
    if(phase===1)text(g,'NCPD / RESPONSE DEPLOYED',800,765,9,'#83baff','center');
    }
    // Backlit LCD text and a slow scan line stay live through the blackout.
    if(layers.lcd!==false){
    glow(g,1172,104,166,phase===1?'#ff637f':'#d35ce0',.20);g.save();g.beginPath();g.rect(1033,81,278,46);g.clip();
    const scanY=81+(t*16)%46;rect(g,1033,scanY,278,3,'#bcaaff19');
    for(let k=0;k<15;k++)line(g,1034,82+k*3,1310,82+k*3,'#977dc111',.6);
    g.font='600 19px monospace';g.textAlign='center';g.fillStyle=phase===1?'#ff9cbb':'#f3c4ff';g.fillText('INTRUDER DETECTED',1172,106);
    text(g,phase===1?'LAW ENFORCEMENT / DEPLOYED':'NULL SECURITY / TRACKING',1172,122,7,'#b6a6d9','center');g.restore();
    }
  }
  function robot(g,type,x,y,angle=0,scale=1,t=0,hit=false,activity=0){
    const def=NullCitySim.ROSTER[type],r=def.r,c=hit?'#f5fff9':def.color,ink=hit&&type!==12?'#e4eaf0':'#080c18',inner='#111827';
    g.save();g.translate(x,y);g.rotate(angle);g.scale(scale,scale);glow(g,0,0,r*2.4,c,.14);
    if(type===0){
      for(const s of [-1,1]){path(g,[[-13,s*7],[2,s*7],[-1,s*14],[-17,s*12]],ink,c,1.6);line(g,-16,s*10,-21-Math.sin(t*18)*2,s*10,c,1.5);}
      path(g,[[17,0],[4,-9],[-10,-7],[-12,0],[-10,7],[4,9]],ink,c,1.8);circle(g,-1,0,6,inner);circle(g,4,0,3.5,'#d8fffa');
    }else if(type===1){
      for(const s of [-1,1])path(g,[[12,s*12],[18,s*22],[-13,s*23],[-18,s*12]],ink,c,2);
      path(g,[[19,-10],[19,10],[6,18],[-15,14],[-22,3],[-19,-12],[4,-18]],ink,c,2.1);
      circle(g,-2,0,10,inner);rect(g,-5,-6,11,12,'#33223c');line(g,6,-7,6,7,'#ffe6fa',2.2);
    }else if(type===2){
      for(const s of [-1,1]){line(g,-7,s*3,-15,s*15,c,2);line(g,-15,s*15,-5,s*19,c,1.5);}
      path(g,[[-15,-7],[3,-8],[7,-3],[28,-3],[28,3],[7,3],[3,8],[-15,7]],ink,c,1.8);circle(g,-4,0,5,inner);circle(g,-4,0,2.8,'#ffe6c3');line(g,8,0,25,0,'#fff1d9');
    }else if(type===12){
      // Motherload is a capital carrier: swept hull, four engine nacelles, deck guns, launch bays.
      const engine='#7dccff';
      for(const s of [-1,1])for(const yy of [42,77]){
        const y=s*yy,length=27+Math.sin(t*19+yy)*6;
        glow(g,-154,y,57,engine,.34);
        path(g,[[-131,y-6],[-168-length,y],[-131,y+6]],'#7dcaff25');
        line(g,-136,y,-163-length,y,engine,3);line(g,-135,y,-156-length*.5,y,'#e0f4ff',1.3);
      }
      for(const s of [-1,1]){
        path(g,[[-130,s*42],[-101,s*101],[2,s*108],[101,s*70],[75,s*42],[-11,s*44]],'#0b1729',c,2.6);
        path(g,[[-108,s*63],[-86,s*89],[4,s*92],[58,s*69],[10,s*61]],'#233044','#b49a73',1.3);
        line(g,-110,s*58,-34,s*70,'#78aada',1.5);line(g,-95,s*96,-21,s*100,c+'88',1.3);
      }
      path(g,[[164,0],[98,-45],[28,-66],[-93,-60],[-139,-30],[-145,0],[-139,30],[-93,60],[28,66],[98,45]],ink,c,3);
      path(g,[[150,0],[88,-31],[32,-43],[-38,-39],[-68,0],[-38,39],[32,43],[88,31]],'#1e293a','#977e60',1.7);
      // Layered armor plates, service channels, exposed circuit runs and recessed equipment.
      for(const s of [-1,1]){
        for(let j=0;j<5;j++){
          const xx=-86+j*35,yy=s*(37+(j<2?7:0));
          path(g,[[xx-13,yy-s*6],[xx+12,yy-s*8],[xx+16,yy+s*5],[xx-11,yy+s*7]],j%2?'#2a3443':'#202f43','#566075',.7);
          line(g,xx-7,yy,xx+8,yy,'#8c8a7a',.7);circle(g,xx-8,yy-s*3,1,'#c1bca1');
        }
        for(let j=0;j<4;j++){
          const xx=-85+j*51;
          path(g,[[xx,s*52],[xx+10,s*52],[xx+15,s*60],[xx+15,s*67]],null,'#6585aa',1.1);
          circle(g,xx,s*52,1.8,'#95cbea');circle(g,xx+15,s*67,1.3,'#e1be7d');
          for(let k=0;k<4;k++)line(g,xx+k*4,s*85,xx+k*4,s*93,'#6e849b',1);
        }
        for(let j=0;j<11;j++)circle(g,-102+j*20,s*(53-Math.max(0,j-6)*3),.85,'#b7a891');
      }
      path(g,[[137,0],[89,-19],[73,-13],[90,0],[73,13],[89,19]],'#2b3c4e','#d9bb89',1.4);
      line(g,94,0,150,0,'#e9d3b4',1.4);
      // Bridge canopy and the exposed central reactor.
      path(g,[[43,-22],[71,-13],[77,0],[71,13],[43,22],[22,15],[22,-15]],'#11253c','#89b9d8',1.8);
      for(let j=0;j<4;j++)line(g,32+j*8,-13,35+j*8,13,'#87cbe6',2);
      rounded(g,-52,-25,61,50,9,'#0b1424',c,2);
      for(const s of [-1,1]){
        rounded(g,-58,s*18-5,6,10,2,'#304352','#79a6c5',.8);
        line(g,-57,s*11,-57,s*6,'#9ee9ff',1.4);
      }
      const exposed=activity===-1;
      if(exposed){glow(g,-21,0,47,'#fff0c5',.65);ellipse(g,-21,0,17,19,'#ffe6b4','#fff8e5',2);}
      else{path(g,[[-46,-19],[-21,-12],[-21,12],[-46,19]],'#3d3d46',c+'aa',1.3);path(g,[[-21,-12],[3,-19],[3,19],[-21,12]],'#3d3d46',c+'aa',1.3);line(g,-21,-16,-21,16,'#bc955f',2);}
      // Drone bays are mechanical doors, without insect chambers or limbs.
      for(const s of [-1,1]){
        rounded(g,-116,s*27-9,40,18,4,'#040c16','#6f91b3',1.5);
        for(let j=0;j<4;j++)line(g,-110+j*8,s*27-6,-110+j*8,s*27+6,'#4b748e',1);
        text(g,s<0?'01':'02',-95,s*27+3,5,'#b2bac5','center');
      }
      for(let j=0;j<4;j++)for(const s of [-1,1]){
        const xx=-r*.75+j*r*.45,yy=s*r*.64;
        rounded(g,xx-12,yy-11,25,22,7,'#182538','#d8b783',1.8);
        circle(g,xx,yy,6,'#303e52',c,1);rounded(g,xx-3,yy-4,36,8,2,'#0c1827',c,1.4);line(g,xx+18,yy,xx+33,yy,'#f8e4bb',1.7);
        for(let k=0;k<3;k++)line(g,xx+10+k*5,yy-4,xx+10+k*5,yy+4,'#6886a3',.7);
      }
      for(let j=0;j<4;j++){line(g,-111+j*10,-12,-111+j*10,12,'#547794',1.8);}
      g.save();g.translate(-126,0);circle(g,0,0,8,'#243248','#adbac9',1);g.rotate(t*.8);ellipse(g,0,0,7,3,'#547185','#bdd4df',.8);line(g,-9,0,9,0,'#d8e3e8',.8);g.restore();
      // Event Horizon capture aperture is an integrated weapon in the prow.
      const capturing=activity===-3,charging=activity===-4;
      path(g,[[128,-11],[147,-6],[155,0],[147,6],[128,11],[122,0]],'#071a2a',capturing?'#ceefff':'#769cb6',1.2);
      for(let j=0;j<4;j++)line(g,129+j*5,-5,129+j*5,5,capturing?'#e0f9ff':charging?'#89dbff':'#395b76',1.2);
      if(capturing||charging){glow(g,145,0,capturing?62:34,'#a3e6ff',capturing?.6:.3);ellipse(g,149,0,4,11,'#b3eaff');}
    }else if(type===3||type===4||type===7){
      const big=type===7,boom=type===4,bodyLength=big?1.2:1,legs=big?4:3;
      for(let i=0;i<legs;i++)for(const s of [-1,1]){
        const lx=(i-(legs-1)/2)*r*.49,wave=Math.sin(t*(big?3:9)+i*1.9+s)*r*.1;
        const pts=[[lx,s*r*.35],[lx-r*.2,s*r*.86],[lx+r*.16+wave,s*r*(big?1.48:1.2)]];
        line(g,...pts[0],...pts[1],'#080c18',big?10:5);line(g,...pts[1],...pts[2],'#080c18',big?7:4);
        line(g,...pts[0],...pts[1],c,big?2.7:1.7);line(g,...pts[1],...pts[2],c,big?2:1.4);circle(g,...pts[1],big?4:2,'#152131',c,1);
      }
      path(g,[[r*.9,-r*.35],[r*.4,-r*.72],[-r*.64*bodyLength,-r*.65],[-r*bodyLength,-r*.12],[-r*.79*bodyLength,r*.56],[r*.3,r*.72],[r*.92,r*.31]],ink,c,big?2.8:2);
      if(big){
        for(let j=0;j<4;j++){const xx=-r*.65+j*r*.3;path(g,[[xx-r*.13,-r*.55],[xx+r*.12,-r*.59],[xx+r*.19,r*.48],[xx-r*.16,r*.54]],'#18242c',c+'99',1.5);}
        for(const s of [-1,1])for(let j=0;j<3;j++){const xx=-r*.6+j*r*.39;ellipse(g,xx,s*r*.36,r*.12,r*.17,'#283328',c,1.5);circle(g,xx,s*r*.36,r*.047,'#eaffbe');}
        path(g,[[r*.35,-r*.32],[r*.94,-r*.29],[r*1.05,0],[r*.94,r*.29],[r*.35,r*.32]],'#0b101b',c,2);
        ellipse(g,r*1.05,0,r*.33,r*.23,'#010208','#648a42',1.5);
        for(const s of [-1,1]){
          circle(g,r*.79,s*r*.18,3.8,'#f2ffd4');
          g.save();g.translate(r*.86,s*r*.2);g.rotate(s*(.23+Math.sin(t*3.4)*.18));g.scale(1,-s);
          path(g,[[0,0],[r*.45,-r*.18],[r*.75,-r*.05],[r*.44,r*.08],[r*.17,r*.1]],'#152016',c,2);
          for(let j=0;j<3;j++){const xx=r*(.17+j*.15);path(g,[[xx,0],[xx+r*.08,r*.14],[xx+r*.12,-r*.02]],'#e6ffc2','#85a95c',.7);}
          g.restore();
        }
        circle(g,-r*.28,0,r*.22,'#364536',c,1.8);circle(g,-r*.28,0,r*.1,'#e4ffb9');
      }else if(boom){
        const pulse=1+Math.sin(t*(activity?22:4))*.1;circle(g,-r*.1,0,r*.54*pulse,'#522b22',c,1.7);circle(g,-r*.1,0,r*.34,'#f48656');circle(g,-r*.1,0,r*.13,'#fff2bb');
        for(let j=0;j<6;j++){const a=j*TAU/6;line(g,Math.cos(a)*r*.39-r*.1,Math.sin(a)*r*.39,Math.cos(a)*r*.57-r*.1,Math.sin(a)*r*.57,'#fff2b5',1.2);}
      }else{rect(g,-5,-5,9,10,'#2d4134',c);line(g,-4,0,4,0,'#e9ffe0',1.5);line(g,0,-4,0,4,'#e9ffe0',1.5);}
    }else if(type===5||type===8){
      const mini=type===8;if(mini)g.scale(.66,.66);
      // Four separate mounts, side engine nacelles and a broad armored hull.
      for(const s of [-1,1]){
        path(g,[[-37,s*23],[-8,s*43],[24,s*43],[31,s*28],[4,s*18]],ink,c,2);
        line(g,-23,s*33,-42-Math.sin(t*15)*4,s*33,'#83e9ff',3);
      }
      path(g,[[45,0],[22,-22],[-29,-27],[-44,-13],[-44,13],[-29,27],[22,22]],ink,c,2.2);
      for(const off of (mini?[-24,24]:[-32,32,-14,14])){rounded(g,9,off-5,35,10,3,'#0d1522',c,1.5);line(g,21,off,44,off,'#d1e7ff',1.2);circle(g,44,off,2,'#e6f8ff');}
      path(g,[[-19,-12],[7,-10],[21,0],[7,10],[-19,12],[-27,0]],'#182a42','#7196c6',1.5);ellipse(g,-8,0,8,5,'#b5daff');
      for(let j=0;j<3;j++)line(g,-34+j*5,-11,-34+j*5,11,'#3d627e');
    }else if(type===6){
      const step=Math.sin(t*3)*4;
      for(const s of [-1,1]){
        const yy=s*31;path(g,[[-14,yy-s*6],[-37+step*s,yy],[-41+step*s,yy+s*11],[-12+step*s,yy+s*13],[-3,yy+s*2]],'#111a2a',c,2.5);
        circle(g,-8,s*20,7,'#233246',c,1.5);
        path(g,[[13,s*16],[22,s*35],[-7,s*37],[-17,s*16]],ink,c,2.2);
      }
      path(g,[[27,-14],[27,14],[4,24],[-20,18],[-29,0],[-20,-18],[4,-24]],ink,c,2.5);
      path(g,[[9,-11],[16,0],[9,11],[-11,11],[-18,0],[-11,-11]],'#292236',c,1.5);circle(g,0,0,6,'#eec5ff');
      rounded(g,24,-9,15,18,4,'#0a1120',c,1.5);line(g,32,-5,32,5,'#ffeafa',2);
    }else if(type===9){
      path(g,[[23,0],[3,-12],[-16,-10],[-9,0],[-16,10],[3,12]],ink,c,2);
      for(const s of [-1,1]){path(g,[[-6,s*8],[-13,s*17],[-22,s*15],[-17,s*7]],'#10253a',c,1.5);circle(g,-12,s*12,2.5,Math.sin(t*12+s)>0?'#e9f6ff':'#2870ef');}
      line(g,5,-5,11,0,'#d9f3ff',2);line(g,11,0,5,5,'#d9f3ff',2);
    }else if(type===10){
      path(g,[[18,-17],[23,0],[18,17],[-11,23],[-25,11],[-25,-11],[-11,-23]],ink,c,2.2);
      rounded(g,-11,-10,20,20,5,'#122d4b',c,1.5);path(g,[[-5,-5],[4,-5],[4,4],[-1,8],[-5,4]],'#a5d5ff');
      g.beginPath();g.arc(-4,0,35,-.97,.97);g.strokeStyle=activity===-2?'#bbdcff':'#599be0';g.lineWidth=5;g.stroke();
      for(const s of [-1,1])circle(g,-16,s*15,3,Math.sin(t*8+s)>.1?'#eaf7ff':'#357fea');
    }else if(type===11){
      path(g,[[18,-9],[26,0],[18,9],[1,20],[-22,13],[-27,0],[-22,-13],[1,-20]],ink,c,2);
      circle(g,-4,0,10,'#19365b',c,1.4);circle(g,-4,0,4,'#b4e2ff');
      for(const s of [-1,1]){rounded(g,7,s*11-4,23,8,3,'#0a1424',c,1.3);circle(g,-17,s*10,2.5,Math.sin(t*10+s)>.1?'#ebf8ff':'#2b68db');}
    }
    g.restore();
  }
  window.NullCityArt={makeBackground,live,robot,path,line,rect,ellipse,circle,glow,text,glyph,rounded};
})();

// Source-native paths from ProceduralSpriteFactory.EnemyPoints/DrawEnemyDetails
// and VoidFallGameRuntime.CourtField. Keep these separate from the approved Knight.
const TAU=Math.PI*2;
function poly(g,p,fill,edge,w=.14){g.beginPath();p.forEach(([x,y],i)=>i?g.lineTo(x,y):g.moveTo(x,y));g.closePath();if(fill){g.fillStyle=fill;g.fill()}if(edge){g.strokeStyle=edge;g.lineWidth=w;g.stroke()}}
function line(g,x,y,xx,yy,c,w){g.beginPath();g.moveTo(x,y);g.lineTo(xx,yy);g.strokeStyle=c;g.lineWidth=w;g.stroke()}
function disc(g,x,y,r,c){g.beginPath();g.arc(x,y,r,0,TAU);g.fillStyle=c;g.fill()}
function arc(g,r,a,b,c,w=.07){g.beginPath();g.arc(0,0,r,a,b);g.strokeStyle=c;g.lineWidth=w;g.stroke()}
const PAWN=[[0,-1],[.36,-.52],[.92,-.42],[.58,.06],[.78,.7],[.2,.58],[0,1],[-.25,.58],[-.82,.75],[-.62,.05],[-1,-.4],[-.38,-.52]];
const ROOK=[[-.9,-.45],[-.72,-.95],[-.4,-.88],[-.34,-.58],[-.08,-.78],[0,-1],[.28,-.75],[.58,-.88],[.82,-.55],[1,-.1],[.78,.28],[.95,.65],[.38,.78],[0,1],[-.42,.76],[-.92,.7],[-.76,.2],[-1,-.18]];
const BISHOP=[[-1,-.7],[-.28,-1],[.48,-.72],[.7,-.28],[1.55,-.18],[1.55,.2],[.62,.3],[.34,.82],[-.42,1],[-1,.58]];
export function drawOriginalFamily(g,type,tier,white,time=0){
 const body=white?'#f1f0ea':'#080c18',ink=white?'#080c18':'#f1f0ea',inner=white?'#d5d4ce':'#111827';
 const core=(x,y,r)=>{disc(g,x,y,r,ink);disc(g,x+r*.18,y-r*.14,Math.max(.045,r*.34),'#f8fafc')};
 const shape=p=>poly(g,p,body,ink);
 if(type===0){
  // Growth follows the same six-point body: layered shell, then split tips.
  if(tier===2){for(let i=0;i<6;i++){g.save();g.rotate(i*TAU/6);poly(g,[[-.14,-.78],[-.19,-1.12],[0,-1.25],[.19,-1.12],[.14,-.78]],body,ink,.085);g.restore()}}
  shape(PAWN);disc(g,-.08,.04,.52,inner);
  if(tier>0)poly(g,PAWN.map(([x,y])=>[x*.7,y*.7]),null,ink,.055);
  if(tier===2)for(let i=0;i<6;i++){const a=i*TAU/6;line(g,Math.cos(a)*.49,Math.sin(a)*.49,Math.cos(a)*.65,Math.sin(a)*.65,ink,.06)}
  core(0,0,.31);
 }else if(type===1){
  shape(ROOK);disc(g,-.08,.04,.52,inner);
  poly(g,[[-.58,-.42],[-.2,-.58],[.22,-.46],[.58,-.2],[.48,.42],[0,.62],[-.52,.38]],null,ink,.083);
  core(-.05,0,.24);line(g,-.65,.55,-.35,.32,ink,.073);line(g,.32,.48,.62,.62,ink,.073);
 }else if(type===2){
  if(tier>0)for(const s of [-1,1])poly(g,[[-.76,s*.47],[-.96,s*.91],[-.31,s*1.15],[.37,s*.94],[.58,s*.62]],body,ink,.09);
  shape(BISHOP);disc(g,-.08,.04,.52,inner);
  g.fillStyle=ink;g.fillRect(.695,-.11,1.05,.22);core(-.25,0,.23);
  if(tier>0){line(g,.2,-.55,.55,-.43,ink,.055);line(g,.2,.56,.52,.45,ink,.055);poly(g,[[1.05,-.25],[1.65,-.3],[1.81,-.13],[1.81,.13],[1.65,.3],[1.05,.25]],null,ink,.065)}
  if(tier===2){for(const s of [-1,1]){poly(g,[[-.64,s*.79],[-.45,s*1.32],[.03,s*1.24],[.51,s*.79]],body,ink,.08);line(g,-.42,s*1.07,.05,s*.97,ink,.055)}arc(g,.58,Math.PI*.55,Math.PI*1.45,ink,.07)}
 }else if(type===4){
  // Preserve the original circular eye and small, ink-cut crown. No flower lobes.
  if(tier>0)for(let i=0;i<4;i++){const a=i*TAU/4+.18;arc(g,1.15,a,a+1.14,ink,.08)}
  if(tier===2)for(let i=0;i<8;i++){const a=i*TAU/8;g.save();g.rotate(a);poly(g,[[1.18,-.14],[1.37,-.2],[1.46,0],[1.37,.2],[1.18,.14]],body,ink,.055);g.restore()}
  shape(Array.from({length:16},(_,i)=>{const a=i*TAU/16+Math.PI/16-Math.PI/2;return [Math.cos(a),Math.sin(a)]}));disc(g,-.08,.04,.52,inner);
  arc(g,.72,0,TAU,ink,.085);poly(g,[[-.42,-.72],[-.2,-1.08],[0,-.8],[.22,-1.08],[.45,-.7]],ink,null);core(0,0,.28);
  if(tier>0)for(const s of [-1,1]){disc(g,s*.48,.18,.07,ink);line(g,s*.35,.46,s*.48,.34,ink,.05)}
  if(tier===2){arc(g,.88,Math.PI*.1,Math.PI*.9,ink,.04);arc(g,.88,Math.PI*1.1,Math.PI*1.9,ink,.04)}
 }
}
const SENTINEL=[[-110,110],[110,110],[110,82],[76,68],[63,-40],[104,-61],[104,-114],[64,-114],[64,-87],[22,-87],[22,-120],[-22,-120],[-22,-87],[-64,-87],[-64,-114],[-104,-114],[-104,-61],[-63,-40],[-76,68],[-110,82]];
export function drawOriginalSentinel(g,c,player,time=0,reducedMotion=false){
 const fill=c.white?'#f4f4f4':'#080808',ink=c.white?'#080808':'#f8f8f8';g.save();g.translate(c.x,c.y);g.scale(c.r/90,c.r/90);
 if(c.dead){
  g.rotate(.08);g.scale(.82,.82);
  for(const p of [[[-150,-62],[-119,-62],[-119,-42],[-62,-37],[-78,-12],[-57,9],[-77,36],[-119,42],[-119,62],[-150,62]],[[-48,-36],[-63,-10],[-44,10],[-61,36],[83,43],[97,66],[145,66],[145,35],[123,35],[123,12],[153,12],[153,-15],[123,-15],[123,-37],[145,-37],[145,-66],[97,-66],[83,-43]],[[-75,61],[-56,51],[-44,64],[-61,73]]])poly(g,p,fill,ink,3);
  g.save();g.translate(14,0);g.scale(1,.69);disc(g,0,0,29,ink);g.restore();line(g,2,-11,26,11,fill,4);line(g,2,11,26,-11,fill,4);
 }else{
  poly(g,SENTINEL,fill,ink,3);poly(g,[[-44,-7],[-20,-17],[0,-11],[20,-17],[44,-7],[27,18],[0,27],[-27,18]],ink,null);
  for(const [x,y,xx,yy,w] of [[-72,69,72,69,2],[-64,-39,64,-39,2],[-49,-29,-6,-20,4],[49,-29,6,-20,4],[-31,17,-40,37,1.5],[31,17,40,37,1.5]])line(g,x,y,xx,yy,ink,w);
  const a=Math.atan2(player.y-c.y,player.x-c.x),x=Math.cos(a)*12,y=4+Math.sin(a)*5;if(!c.suppressPupil)poly(g,[[x,y-19],[x+5,y],[x,y+20],[x-5,y]],fill,null);
  // Small, full rows of teeth: no empty maw or isolated fangs.
  const variant=c.mouth||0,laugh=c.phase?.stage==='burst'?1.8:0;
  const jaw=reducedMotion?0:laugh*(.6+.4*Math.sin(time*12+(c.id||0)));
  g.save();g.translate(0,45);g.rotate(variant===1?-.12:variant===2?.1:0);
  const w=variant===1?25:23,h=(variant===2?18:14)+jaw;
  const mouth=()=>{g.beginPath();g.moveTo(-w,-5);g.quadraticCurveTo(0,variant===2?2:8,w,-5);g.quadraticCurveTo(w-3,h*.7,0,h);g.quadraticCurveTo(-w+3,h*.7,-w,-5);g.closePath()};
  mouth();g.fillStyle='#f5f1df';g.fill();g.strokeStyle='#090b10';g.lineWidth=2.4;g.stroke();
  g.save();mouth();g.clip();g.strokeStyle='#15171b';g.lineWidth=1.2;
  g.beginPath();g.moveTo(-w,0);g.quadraticCurveTo(0,h*.94,w,0);g.stroke();
  for(let i=-3;i<=3;i++){const xx=i*6;g.beginPath();g.moveTo(xx,-7);g.quadraticCurveTo(xx*.87,h*.4,xx*.7,h+2);g.stroke()}
  g.restore();g.restore();

 }
 g.restore();
}

export function drawArmoredKnight(g,tier,white,block=0,time=0){
 const body=white?'#eeeae0':'#0b1019',ink=white?'#111722':'#e0e3dc',shade=white?'#9aa5a5':'#25323e';
 const shape=(p,fill=body,w=.055)=>poly(g,p,fill,ink,w);
 const stride=Math.sin(time*9)*.13,flow=Math.sin(time*3)*.06;
 // Long, tapered mane and tail ribbons give the armored animal a mythical silhouette.
 for(let i=0;i<3;i++){
  g.save();g.globalAlpha=.75-i*.17;
  shape([[-1.04,-.4+i*.1],[-1.43,-.56+i*.12],[-1.78,-.18+i*.18+flow],[-1.62,.16+i*.2+flow],[-1.92,.52+i*.15],[-1.56,.38+i*.11],[-1.43,.04+i*.08],[-1.14,-.07+i*.08]],shade,.025);
  g.restore();
 }
 // Four articulated legs, heavy plated barrel, tail and an unmistakable long equine head.
 for(const [x,front] of [[-.88,false],[-.59,true],[.48,false],[.78,true]]){
  const step=(front?stride:-stride),offset=front?0:-.09;
  shape([[x-.12,.3],[x+.15,.35],[x+.12+step,.78],[x-.03-step,1.15],[x+.14-step,1.2],[x+.11-step,1.3],[x-.25-step,1.29],[x-.24-step,1.1],[x-.1+step,.75]],front?body:shade,.045);
  if(tier>0)line(g,x-.11+step,.78,x+.09+step,.78,ink,.04);
 }
 shape([[-1.02,-.38],[-1.35,-.44],[-1.52,-.02],[-1.44,.54],[-1.62,.84],[-1.35,.72],[-1.24,.19],[-1.08,-.05]],shade);
 shape([[-1.13,-.48],[-.65,-.7],[.14,-.65],[.7,-.52],[.97,-.13],[.77,.47],[.14,.63],[-.72,.58],[-1.18,.17]]);
 // Barding follows the horse's barrel rather than reading as a humanoid breastplate.
 shape([[-.91,-.44],[-.36,-.55],[.23,-.45],[.31,.43],[-.13,.72],[-.69,.57],[-1.02,.29]],tier?body:shade);
 line(g,-.72,-.37,-.58,.45,ink,.05);line(g,-.23,-.45,-.13,.48,ink,.05);
 shape([[.35,.27],[.3,-.4],[.49,-.99],[.66,-1.38],[.97,-1.54],[1.15,-1.38],[1.42,-1.17],[1.73,-.89],[1.67,-.57],[1.39,-.53],[1.1,-.73],[.98,-.31],[.99,.17],[.71,.43]]);
 shape([[.68,-1.25],[.54,-1.12],[.29,-.68],[.24,-.13],[.4,.05],[.64,-.54],[.84,-.95]],shade);
 // Ears, chamfron, eye slit, muzzle and jaw establish a warhorse at gameplay scale.
 shape([[.79,-1.38],[.72,-1.76],[.93,-1.6],[1.01,-1.37]]);
 shape([[1.02,-1.4],[1.13,-1.7],[1.22,-1.42],[1.25,-1.22]],shade);
 shape([[1.02,-1.3],[1.32,-1.12],[1.63,-.86],[1.52,-.7],[1.2,-.88],[.97,-.95]],body);
 line(g,1.04,-1.09,1.24,-1.02,ink,.075);disc(g,1.56,-.72,.035,ink);line(g,1.43,-.59,1.65,-.61,ink,.035);
 line(g,.78,-.66,.99,-.5,ink,.04);line(g,.72,-.43,.97,-.28,ink,.04);
 if(tier>0){
  shape([[.37,-.17],[.65,-.42],[.95,-.22],[.88,.31],[.59,.49],[.33,.21]],shade);
  shape([[-1.1,-.34],[-.87,-.54],[-.6,-.35],[-.66,.2],[-.93,.35],[-1.13,.1]],shade);
  for(let i=0;i<3;i++)line(g,-.49,-.2+i*.2,.19,-.13+i*.2,ink,.045);
  poly(g,[[.48,-.07],[.64,-.2],[.78,-.07],[.64,.17]],null,ink,.055);
 }
 if(tier===2){
  // A taller armored mane and layered flank plates distinguish the final rank.
  for(let i=0;i<4;i++){const xx=.51-i*.065,yy=-1.13+i*.23;shape([[xx,yy],[xx-.26,yy-.17],[xx-.21,yy+.15],[xx+.05,yy+.21]],shade,.04)}
  shape([[-.62,-.48],[-.32,-.61],[.15,-.54],[.28,-.3],[-.06,-.2],[-.45,-.27]],body);
  shape([[-.45,.14],[.07,.2],[.23,.44],[-.14,.68],[-.57,.48]],body);
  line(g,-.29,.32,-.14,.54,ink,.05);line(g,.54,.01,.73,.16,ink,.045);
 }
 // Etched borders, rivets and star seals remain subordinate to the horse and rider.
 for(const [x,y] of [[-.84,-.28],[-.69,.31],[-.27,-.32],[.14,.29],[.59,-.17],[.76,.14]]){
  disc(g,x,y,.025,ink);disc(g,x-.006,y-.006,.009,body);
 }
 poly(g,[[-.56,-.17],[-.27,-.27],[.04,-.13],[.02,.15],[-.24,.31],[-.54,.15]],null,ink,.025);
 g.save();g.translate(-.25,.02);g.scale(.13,.13);drawOriginalFamily(g,0,0,white);g.restore();
 for(let i=0;i<4;i++)line(g,.51+i*.07,-.63+i*.16,.75+i*.055,-.51+i*.16,ink,.025);
 shape([[1.06,-1.31],[1.14,-1.48],[1.24,-1.27],[1.15,-1.17]],shade,.025);
 line(g,1.26,-1.1,1.55,-.86,ink,.025);line(g,1.3,-1.02,1.52,-.83,ink,.025);
 // A small billowing mantle and saddle seat the actual Pawn III on the horse.
 shape([[-.36,-1.41],[-.72,-1.31],[-.87,-1.03+flow],[-1.16,-.75+flow],[-.82,-.67],[-.58,-.82],[-.22,-.81]],shade,.04);
 shape([[-.72,-.65],[-.57,-.8],[-.1,-.79],[.17,-.64],[.09,-.48],[-.56,-.48]],body,.04);
 line(g,-.5,-.5,-.55,.09,ink,.045);poly(g,[[-.64,.08],[-.43,.08],[-.43,.23],[-.64,.23]],null,ink,.035);
 g.save();g.translate(-.3,-1.28+stride*.14);g.scale(.49,.49);drawOriginalFamily(g,0,2,white,time);g.restore();
 // Reins visibly connect the rider to the bridled jaw.
 g.strokeStyle=ink;g.lineWidth=.035;g.beginPath();g.moveTo(.01,-1.03);g.quadraticCurveTo(.74,-.53,1.39,-.68);g.stroke();
 g.lineWidth=.018;g.beginPath();g.moveTo(-.02,-.96);g.quadraticCurveTo(.72,-.41,1.43,-.61);g.stroke();
 if(block>0){g.strokeStyle='#ead7a2';g.lineWidth=.08;g.beginPath();g.arc(.2,-.1,1.65,-1.1,1.1);g.stroke()}
}

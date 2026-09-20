const TAU=Math.PI*2;
const hash=(x,y)=>{let n=Math.imul(x+491,374761393)+Math.imul(y+97,668265263);n^=n>>>13;return((Math.imul(n,1274126177)^(n>>>16))>>>0)/4294967296};
function line(g,x,y,xx,yy,color,w=1){g.beginPath();g.moveTo(x,y);g.lineTo(xx,yy);g.strokeStyle=color;g.lineWidth=w;g.stroke()}
function glow(g,x,y,r,color,alpha=.15){g.save();g.globalAlpha*=alpha;const c=g.createRadialGradient(x,y,0,x,y,r);c.addColorStop(0,color);c.addColorStop(1,'transparent');g.fillStyle=c;g.fillRect(x-r,y-r,r*2,r*2);g.restore()}
 export function drawHydraFloor(g,art,images,time,{left,right,top,bottom}){
  g.fillStyle='#060c0c';g.fillRect(left,top,right-left,bottom-top);
  if(images.hydra.complete&&images.hydra.naturalWidth){g.globalAlpha=.19;for(let x=Math.floor(left/1600)*1600;x<right;x+=1600)for(let y=Math.floor(top/900)*900;y<bottom;y+=900)g.drawImage(images.hydra,x,y,1600,900);g.globalAlpha=1}
  // Fixed world positions; only local brightness varies with time.
  for(let ix=Math.floor(left/440)-1;ix<=Math.ceil(right/440);ix++)for(let iy=Math.floor(top/440)-1;iy<=Math.ceil(bottom/440);iy++){
   const n=hash(ix,iy),x=ix*440+n*140,y=iy*440+hash(iy,ix)*120,pulse=.5+.5*Math.sin(time*.85+n*TAU);glow(g,x,y,170,'#55906a',.04+pulse*.055);
   g.save();g.translate(x,y);g.rotate(n*TAU);g.strokeStyle=`rgba(91,139,94,${.11+pulse*.13})`;g.lineWidth=2;g.beginPath();g.moveTo(-85,15);g.bezierCurveTo(-45,-30,25,45,90,-10);g.stroke();for(let j=0;j<7;j++){const xx=-65+j*22,yy=Math.sin(j*.8)*11;line(g,xx,yy,xx-15,yy-30,'#4c79512a',2);line(g,xx,yy,xx+12,yy+32,'#4c79512a',2)}art.glyph(g,0,0,27,Math.floor(n*6),`rgba(145,161,96,${.17+pulse*.22})`);g.restore()
  }
 }
 export function drawLegacy(g,e){
  // Source silhouettes: ProceduralSpriteFactory.HydraPopulation.cs.
  g.save();g.translate(e.x,e.y);g.rotate(e.angle+Math.PI/2);const s=e.type===3?46:32;g.scale(s,s);g.fillStyle=e.hit>0?'#cdd6a3':'#151e14';g.strokeStyle='#9fa970';g.lineWidth=.045;
  const poly=pts=>{g.beginPath();for(let i=0;i<pts.length;i+=2)i?g.lineTo(pts[i],pts[i+1]):g.moveTo(pts[i],pts[i+1]);g.closePath();g.fill();g.stroke()};const eye=(x,y,r)=>{g.beginPath();g.arc(x,y,r,0,TAU);g.fillStyle='#d0c98b';g.fill();g.fillStyle='#151e14'};
  if(e.type===0){for(const side of [-1,1]){g.save();g.scale(side,1);poly([.05,-.52,.29,-.83,.36,-.39,.79,-.49,.57,-.12,.87,.16,.46,.25,.40,.73,.15,.44,.02,.52,.13,.12,.02,-.13]);eye(.37,0,.12);g.restore()}}
  else if(e.type===2){for(const side of [-1,1])for(let i=0;i<3;i++){g.save();g.scale(side,1);g.translate(0,-.3+i*.3);poly([.1,-.09,.48,-.24,.76,-.03,.31,.03,.12,.14]);g.restore()}line(g,0,-.71,0,.72,'#9fa970',.05);poly([0,-.89,.21,-.58,.12,-.42,-.12,-.42,-.21,-.58]);eye(0,-.59,.09)}
  else if(e.type===1){poly([-.2,-.5,.03,-.72,.21,-.34,.4,-.22,.22,.13,.32,.43,-.03,.32,-.3,.68,-.32,.2,-.56,.03,-.32,-.15]);poly([.19,-.2,.35,-.6,.77,-.82,.99,-.42,.9,.07,.58,.23,.74,-.14,.64,-.38,.51,-.4,.46,-.12]);eye(-.04,-.07,.13)}
  else if(e.type===3){poly([-.35,-.21,-.67,.04,-.64,.49,-.32,.75,.21,.79,.58,.51,.67,.07,.38,-.26,0,-.38]);poly([-.3,-.39,-.4,-.69,-.15,-.6,0,-.92,.16,-.61,.4,-.71,.29,-.37,0,-.25]);eye(0,.22,.2);eye(0,-.52,.085)}
  else if(e.type===4){for(let i=0;i<5;i++){g.save();g.rotate(i*TAU/5);line(g,0,-.27,.06,-.57,'#9fa970',.05);poly([-.1,-.33,.11,-.32,.15,-.54,.02,-.63,-.11,-.5]);line(g,.02,-.57,.01,-.91,'#9fa970',.05);g.restore()}poly([0,-.43,.15,-.2,.4,-.13,.25,.1,.25,.35,0,.25,-.25,.35,-.25,.1,-.4,-.13,-.15,-.2]);eye(0,0,.13)}
  else if(e.type===5){poly([-.6,-.45,-.22,-.68,.4,-.64,.69,-.2,.58,.45,.08,.68,-.55,.48,-.72,0]);poly([-.15,-.55,-.15,-1,.15,-1,.15,-.55]);eye(0,0,.14)}
  else if(e.type===6){poly([0,-.72,.35,-.12,.22,.52,0,.32,-.22,.52,-.35,-.12]);g.beginPath();g.arc(0,-.04,.79,Math.PI*1.1,Math.PI*1.9);g.stroke();line(g,0,.4,0,.85,'#9fa970',.05);eye(0,-.03,.14)}
  else if(e.type===7){for(const side of [-1,1]){g.save();g.scale(side,1);poly([.04,-.8,.53,.47,.13,.32,.05,.62]);eye(.25,.1,.14);line(g,.37,.63,.55,.82,'#9fa970',.05);g.restore()}}
  else if(e.type===8){poly([-.48,-.65,-.13,-.39,.15,-.39,.49,-.65,.61,.25,.34,.65,-.36,.65,-.62,.24]);poly([-.14,.15,-.18,-.88,.18,-.88,.14,.15]);eye(0,.33,.14);for(let i=0;i<3;i++)line(g,-.13+i*.13,.75,-.13+i*.13,.9,'#9fa970',.05)}
  else{poly([-.57,-.37,0,-.64,.57,-.3,.59,.45,0,.65,-.6,.31]);poly([-.78,-.05,-.32,-.05,-.32,.42,-.78,.42]);poly([.32,-.05,.78,-.05,.78,.42,.32,.42]);line(g,0,-.45,0,-.95,'#9fa970',.05);eye(0,-.05,.14)}g.restore()
 }

export const CELL=129.6,BOARD_HALF=CELL*14;
export const PIECES=['Pawn','Rook','Bishop','Knight','Queen','Armored Knight'];
export const TIER_NAMES=['Initiate','Ascendant','Sovereign'];
const BASE=[{hp:30,speed:69,r:20},{hp:140,speed:42,r:30},{hp:65,speed:37,r:24},{hp:65,speed:78,r:24},{hp:180,speed:32,r:32},{hp:60,speed:69*1.2,r:40}];
export function pieceStats(type,tier){tier=Math.max(0,Math.min(type===1?3:2,tier));const b=BASE[type];return {...b,hp:Math.round(b.hp*[1,1.65,2.5,4][tier]),speed:b.speed*[1,1.09,1.17,1.12][tier],r:b.r*(1+tier*.1),damage:8+tier*3,cooldown:[4.5,4.2,3.8,4.2][tier],warning:[1.2,1.05,.95,1.25][tier]}}
export function directorTier(age,roll){if(age<35)return 0;if(age<80)return roll<.55?0:1;return roll<.3?0:roll<.8?1:2}
export function pointSegmentDistance(p,a,b){const dx=b.x-a.x,dy=b.y-a.y,t=Math.max(0,Math.min(1,((p.x-a.x)*dx+(p.y-a.y)*dy)/(dx*dx+dy*dy||1)));return Math.hypot(p.x-a.x-t*dx,p.y-a.y-t*dy)}
export function planKnightPath(start,target,obstacles=[]){
 const paths=[];
 for(const horizontal of [true,false])for(const sx of [-1,1])for(const sy of [-1,1]){
  const corner={x:start.x+(horizontal?2*CELL*sx:0),y:start.y+(horizontal?0:2*CELL*sy)};
  const end={x:corner.x+(horizontal?0:CELL*sx),y:corner.y+(horizontal?CELL*sy:0)};
  if([corner,end].some(p=>Math.abs(p.x)>BOARD_HALF-60||Math.abs(p.y)>BOARD_HALF-60))continue;
  if(obstacles.some(o=>!o.dead&&(pointSegmentDistance(o,start,corner)<o.r+30||pointSegmentDistance(o,corner,end)<o.r+30)))continue;
  paths.push([{x:start.x,y:start.y},corner,end]);
 }
 paths.sort((a,b)=>Math.hypot(a[2].x-target.x,a[2].y-target.y)-Math.hypot(b[2].x-target.x,b[2].y-target.y));return paths[0]||null;
}
export function floorPhase(t){const age=t%7,cycle=Math.floor(t/7);return {cycle,color:cycle%2,age,stage:age<3?'warning':age<3.45?'burst':'rest'}}
export function warnedCells(player,color){const cx=Math.floor(player.x/CELL),cy=Math.floor(player.y/CELL),cells=[];for(let x=cx-2;x<=cx+2;x++)for(let y=cy-2;y<=cy+2;y++)if(x>=-14&&x<14&&y>=-14&&y<14&&((x+y)%2+2)%2===color)cells.push({x,y});return cells}
export function inWarnedCell(p,cells){return cells.some(c=>Math.floor(p.x/CELL)===c.x&&Math.floor(p.y/CELL)===c.y)}
export function promotePawn(p){if(p.dead||p.type!==0||p.tier>=2)return false;const previous=p.maxHp;p.tier++;const stats=pieceStats(0,p.tier);Object.assign(p,stats,{maxHp:stats.hp,hp:Math.min(stats.hp,p.hp+stats.hp-previous)});return true}

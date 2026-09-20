import {CELL} from './court-rules.js';
export const SENTINEL_HP=2400,SENTINEL_SHIELD=24;
export function sentinelCells(rook){
 const cx=Math.max(-12,Math.min(12,Math.round(rook.x/CELL))),cy=Math.max(-12,Math.min(12,Math.round(rook.y/CELL)));
 const cells=[];for(let x=cx-2;x<cx+2;x++)for(let y=cy-2;y<cy+2;y++)cells.push({x,y});return cells;
}
export function insideTerritory(actor,rook){return !rook.dead&&sentinelCells(rook).some(c=>Math.floor(actor.x/CELL)===c.x&&Math.floor(actor.y/CELL)===c.y)}
export function sentinelPhase(time,rook){
 const elapsed=time-(rook.delay??1);if(rook.dead||elapsed<0)return {cycle:-1,age:100,stage:'rest'};
 const age=elapsed%14,cycle=Math.floor(elapsed/14);return {cycle,age,stage:age<3?'warning':age<3.45?'burst':'rest'};
}

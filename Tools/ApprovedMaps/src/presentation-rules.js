export const TRANSIT_LEFT=(279-800)*1.6;
export const TRANSIT_RIGHT=(1320-800)*1.6;
export const TRANSIT_Y=(230-450)*1.6;
export const TRANSIT_WIDTH=190;
export function transitFrame(time){
 const speed=96,journey=(TRANSIT_RIGHT-TRANSIT_LEFT+TRANSIT_WIDTH)/speed,cycle=journey+2;
 const phase=((time%cycle)+cycle)%cycle,x=TRANSIT_LEFT-TRANSIT_WIDTH/2+Math.min(phase,journey)*speed;
 return {x,y:TRANSIT_Y,visible:phase<journey,clipLeft:TRANSIT_LEFT,clipRight:TRANSIT_RIGHT,
  visibleLeft:Math.max(TRANSIT_LEFT,x-TRANSIT_WIDTH/2),visibleRight:Math.min(TRANSIT_RIGHT,x+TRANSIT_WIDTH/2)};
}
export function roadShake(time,active,reducedMotion=false){return !active||reducedMotion?0:Math.sin(time*73)*3.2+Math.sin(time*119)*1.5}

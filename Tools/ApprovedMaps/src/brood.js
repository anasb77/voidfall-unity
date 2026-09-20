export const BROOD_INTERVAL=3;
export const BROOD_SIZE=5;
export function createHives(random=Math.random){
 return [[290,30],[-650,-460],[690,650]].map(([x,y],id)=>({id,x:x+(random()-.5)*100,y:y+(random()-.5)*100,hp:180,maxHp:180,clock:BROOD_INTERVAL,dead:false,guardianReleased:false,angle:random()*Math.PI*2,phase:random()*6,hit:0,waves:0}));
}
export function stepBrood(hives,dt,spawn){
 for(const h of hives){h.hit=Math.max(0,h.hit-dt);if(h.dead)continue;h.clock-=dt;while(h.clock<=1e-9){h.clock+=BROOD_INTERVAL;h.waves++;for(let i=0;i<BROOD_SIZE;i++)spawn(h,i,h.waves)}}
}
export function damageHive(h,amount,release){
 if(h.dead)return false;h.hp=Math.max(0,h.hp-amount);h.hit=.15;if(h.hp>0)return false;
 h.dead=true;if(!h.guardianReleased){h.guardianReleased=true;release(h)}return true;
}

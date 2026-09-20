// Five bodies share the Hydra palette; regular insects retain 90% of their previous scale.
export const INSECT_SCALE=.9;
export const EXPLOSION_RADIUS=100;
export function broodMember(hive,wave,index){
 return index<3?{legacy:true,type:(hive*3+wave*3+index)%10}:{kind:(hive*2+wave*2+index-3)%5};
}
export function armExploder(enemy,delay=.9){
 if(enemy.legacy||enemy.guardian||enemy.kind!==2||enemy.exploded)return false;
 enemy.fuse=enemy.fuse==null?delay:Math.min(enemy.fuse,delay);return true;
}
export function stepExploder(enemy,dt,explode){
 if(enemy.fuse==null||enemy.exploded)return false;
 enemy.fuse-=dt;if(enemy.fuse>0)return false;
 enemy.exploded=true;enemy.hp=0;explode(enemy);return true;
}

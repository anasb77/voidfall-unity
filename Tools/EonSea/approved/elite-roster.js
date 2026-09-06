/* Browser-only elite roster proposals. Tier I uses the existing Unity elite
 * baseline; II–IV are new browser designs. draw() is centered and faces UP.
 */
(function(root,factory){
  const api=factory(typeof module!=='undefined'&&module.exports?require('./roster.js'):root.EonRoster);
  if(typeof module!=='undefined'&&module.exports)module.exports=api;
  else root.EonEliteRoster=api;
})(typeof globalThis!=='undefined'?globalThis:this,function(R){
  'use strict';
  const types=['exploder','mortar','gunner'];
  const names=['','I','II','III','IV'];
  const families={
    exploder:{name:'Elite Exploder',baseName:'Exploder',accent:'#fb923c',hp:1.6,r:1.35,damage:1.15,speed:[1.2,1.22,1.25,1.28],cooldown:[0,0,0,0]},
    mortar:{name:'Siege Mortar',baseName:'Mortar',accent:'#fbbf24',hp:1.4,r:1.25,damage:1.1,speed:[.92,.96,1,1.04],cooldown:[6,5.6,5.1,4.7]},
    gunner:{name:'Curved Gunner',baseName:'Gunner',accent:'#f87171',hp:1.4,r:1.3,damage:1,speed:[1.05,1.1,1.15,1.2],cooldown:[4,3.7,3.4,3.1]}
  };
  // Geometry uses world pixels, time uses seconds, spread uses radians, and
  // curvatureAcceleration is lateral world pixels / second squared per slot.
  const definitions={
    exploder:[
      ['Elite rupture','The shared amber body carries a hot core and quiet gold halo.','Stops at close range, flashes for 1.1 seconds, then ruptures over a larger radius.',{blastCount:1,blastRadius:95,blastDelay:1.1,blastSpacing:0,proximityRadius:70}],
      ['Breach heart','A fuller four-lobed shell cradles one enlarged molten heart.','Warns for 1.2 seconds before a wider single blast; triggers from slightly farther away.',{blastCount:1,blastRadius:108,blastDelay:1.2,blastSpacing:0,proximityRadius:82}],
      ['Twin heart','Two bright chambers swell from one connected waist.','Marks a pair of overlapping breach zones along its approach, with time to cross their edge.',{blastCount:2,blastRadius:112,blastDelay:1.25,blastSpacing:92,proximityRadius:92}],
      ['Eruption crown','A broad three-lobed crown surrounds a dominant white-hot heart.','Marks three spaced rupture zones with a longer opening warning; resilience rises by half over III.',{blastCount:3,blastRadius:116,blastDelay:1.35,blastSpacing:104,proximityRadius:100}]
    ],
    mortar:[
      ['Drifting siege','The shared firing body gains a restrained gold halo.','A single impact drifts during its 1.5-second warning, then locks for the final 0.45 seconds.',{blastCount:1,blastRadius:58.5,blastDelay:1.5,blastSpacing:0,driftRadius:26,lockSeconds:.45}],
      ['Twin siege','Two broad firing throats grow from a compact glowing body.','Two drifting impacts straddle the target, then both lock before landing.',{blastCount:2,blastRadius:68,blastDelay:1.55,blastSpacing:100,driftRadius:30,lockSeconds:.45}],
      ['Crown siege','Three rounded firing lobes rise above one large amber core.','Three separated impact zones drift farther, then freeze for the same readable final lock.',{blastCount:3,blastRadius:76,blastDelay:1.65,blastSpacing:112,driftRadius:36,lockSeconds:.45}],
      ['Caldera siege','A wide volcanic crown and open central throat frame an amber heart.','Keeps three impacts, widens their spacing and drift, and gives a longer warning before the final lock.',{blastCount:3,blastRadius:84,blastDelay:1.75,blastSpacing:130,driftRadius:42,lockSeconds:.45}]
    ],
    gunner:[
      ['Curved gap','The shared red firing body carries a quiet elite halo.','Fires four curved projectiles from five curvature slots; the missing slot rotates each volley.',{shotCount:4,shotSpread:.1,curvatureAcceleration:210,curveGapSlots:5,aimWindup:.7}],
      ['Swept gap','Two hooked shoulders curl around a bright central red core.','Keeps four projectiles and a rotating gap, with a wider fan and a slightly quicker next volley.',{shotCount:4,shotSpread:.13,curvatureAcceleration:230,curveGapSlots:5,aimWindup:.75}],
      ['Crescent gap','A broad crescent body carries four luminous firing tips.','Opens the four-shot fan farther and bends its paths more strongly after a longer aim warning.',{shotCount:4,shotSpread:.16,curvatureAcceleration:250,curveGapSlots:5,aimWindup:.8}],
      ['Orbit gap','Four curled lobes join a heavy heart around a deep forward notch.','Keeps the five-slot rotating escape gap and four shots, with broad curves and the longest aim warning.',{shotCount:4,shotSpread:.19,curvatureAcceleration:270,curveGapSlots:5,aimWindup:.85}]
    ]
  };
  const entries={};
  for(const type of types){
    const family=families[type];
    entries[type]=definitions[type].map((d,index)=>Object.freeze({
      type,tier:index+1,elite:true,name:family.name+' '+names[index+1],baseName:family.baseName,
      trait:d[0],description:d[1],behavior:d[2],traits:Object.freeze(d[3]),accent:family.accent,
      provenance:index===0?'Existing Unity elite baseline · browser rendering':'Browser elite tier proposal'
    }));
  }
  function entry(type,tier=1){
    if(!Object.prototype.hasOwnProperty.call(entries,type))throw new RangeError('Unknown elite: '+type);
    return entries[type][(tier===2||tier===3||tier===4?tier:1)-1];
  }
  const health=[1,1.45,2.9,4.35],size=[1,1.05,1.1,1.15],damage=[1,1.12,1.42,1.65],projectile=[1,1.02,1.05,1.07];
  function stats(base,type,tier=1){
    const e=entry(type,tier),family=families[type],i=e.tier-1;
    const value=(key,fallback)=>Math.max(0,Number.isFinite(base[key])?base[key]:fallback);
    return {...base,hp:value('hp',30)*family.hp*health[i],speed:value('speed',70)*family.speed[i],
      r:value('r',15)*family.r*size[i],damage:value('damage',8)*family.damage*damage[i],
      cooldown:family.cooldown[i],projectileSpeed:value('projectileSpeed',240)*projectile[i],
      traits:e.traits,tier:e.tier,elite:true};
  }

  function draw(ctx,type,tier,x,y,size,time=0,images={}){
    const e=entry(type,tier),accent=e.accent,gold='#f5cf72',dark='#080c18';
    ctx.save();
    try{
      ctx.translate(x,y);ctx.scale(size/100,size/100);
      ctx.lineJoin='round';ctx.lineCap='round';
      function circle(x,y,r,fill){ctx.beginPath();ctx.arc(x,y,r,0,Math.PI*2);ctx.fillStyle=fill;ctx.fill();}
      function poly(points,fill=dark,stroke=accent,width=2.8){
        ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(p[0],p[1]):ctx.moveTo(p[0],p[1]));ctx.closePath();
        if(fill){ctx.fillStyle=fill;ctx.fill();}
        if(stroke){ctx.shadowColor=stroke;ctx.shadowBlur=3.5;ctx.strokeStyle=stroke;ctx.lineWidth=width;ctx.stroke();ctx.shadowBlur=0;}
      }
      function line(points,color=accent,width=2.2){ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(p[0],p[1]):ctx.moveTo(p[0],p[1]));ctx.strokeStyle=color;ctx.lineWidth=width;ctx.stroke();}
      function arc(x,y,r,start,end,color=accent,width=2.8){ctx.beginPath();ctx.arc(x,y,r,start,end);ctx.strokeStyle=color;ctx.lineWidth=width;ctx.stroke();}
      function core(x,y,r,color=accent){
        circle(x,y,r*2,'#111827');circle(x,y,r*1.6,color+'14');
        ctx.shadowColor=color;ctx.shadowBlur=8;circle(x,y,r,color);ctx.shadowBlur=0;
        circle(x-.8,y-1,r*.43,'#fff3db');
      }
      // Gold is an elite marker, kept outside the shared saturated role body.
      for(let i=0;i<7;i++)circle(0,0,45-i*2.8,accent+(i<3?'03':'04'));
      arc(0,0,39,-2.8,-1.92,gold+'85',1.5);
      arc(0,0,39,-1.22,-.34,gold+'85',1.5);
      for(let i=0;i<e.tier;i++)circle((i-(e.tier-1)/2)*5.5,38,1.25,gold);
      if(e.tier===1){R.draw(ctx,type,1,0,0,100,time,images);return;}
      ctx.scale(.9,.9);
      const three=e.tier===3,four=e.tier===4;
      if(type==='exploder'){
        poly(four?
          [[-8,-30],[6,-31],[14,-22],[26,-21],[32,-9],[27,3],[32,15],[20,27],[7,24],[0,31],[-12,27],[-22,29],[-33,15],[-28,2],[-32,-10],[-25,-22],[-14,-21]]:
          three?[[-25,-23],[-12,-28],[0,-18],[12,-28],[25,-21],[31,-8],[28,9],[21,23],[7,26],[0,19],[-10,27],[-24,21],[-31,6],[-30,-9]]:
          [[-12,-28],[10,-29],[18,-20],[28,-12],[30,5],[21,15],[13,28],[-6,30],[-18,21],[-29,12],[-30,-7],[-20,-17]]);
        if(four){core(0,7,8.5,'#fb7185');core(-16,-10,4.7,'#fb923c');core(16,-10,4.7,'#fb923c');line([[-5,-22],[0,-30],[7,-23]],'#fed7aa',2.3);}
        else if(three){core(-12,1,7.3,'#fb7185');core(12,1,7.3,'#fb7185');circle(0,14,2.5,'#fed7aa');}
        else{core(0,2,9,'#fb7185');arc(0,2,17,-2.7,-.6,accent,3.2);}
        line([[-5,-25],[-2,-33],[7,-36]],accent,2.2);circle(9,-36,2.2,'#fef08a');
      }else if(type==='mortar'){
        poly(four?
          [[-30,11],[-31,-4],[-28,-15],[-24,-27],[-17,-31],[-12,-23],[-10,-10],[-6,-17],[-6,-31],[0,-36],[7,-31],[7,-16],[11,-9],[13,-23],[20,-32],[27,-27],[30,-11],[34,2],[29,19],[15,29],[0,31],[-18,27]]:
          three?[[-28,14],[-30,-3],[-24,-11],[-23,-27],[-16,-31],[-10,-26],[-10,-9],[-6,-13],[-5,-32],[1,-36],[7,-30],[7,-12],[12,-8],[13,-26],[20,-30],[26,-24],[27,-8],[31,3],[26,20],[12,28],[-11,29]]:
          [[-26,14],[-28,-2],[-21,-9],[-20,-25],[-13,-31],[-5,-27],[-5,-9],[4,-9],[5,-26],[13,-31],[21,-26],[22,-10],[28,-2],[27,15],[12,28],[-10,27]]);
        core(0,12,four?8:7);
        const mouths=four?[[-21,-16,4.6],[0,-24,5.3],[22,-16,4.6]]:three?[[-16,-20,3.9],[1,-25,4.4],[20,-19,3.9]]:[[-13,-20,4.7],[13,-20,4.7]];
        for(const [px,py,r] of mouths){circle(px,py,r,'#fed7aa');circle(px,py,r*.46,dark);}
        if(four)arc(0,9,18,.22,2.9,'#fde68a',2.7);
      }else if(type==='gunner'){
        poly(four?
          [[-31,14],[-34,0],[-30,-18],[-25,-31],[-18,-34],[-18,-23],[-21,-15],[-15,-11],[-10,-29],[-4,-33],[-3,-17],[0,-9],[4,-17],[5,-32],[12,-29],[17,-11],[22,-15],[19,-25],[20,-35],[28,-30],[33,-17],[36,1],[29,18],[14,28],[1,32],[-15,27]]:
          three?[[-29,14],[-32,-1],[-26,-20],[-20,-29],[-13,-30],[-14,-17],[-8,-10],[-5,-27],[1,-31],[6,-25],[8,-10],[16,-16],[15,-29],[23,-27],[30,-17],[33,0],[27,17],[11,28],[-9,28]]:
          [[-26,13],[-29,-1],[-24,-16],[-18,-28],[-11,-30],[-10,-20],[-15,-11],[-5,-6],[0,-17],[6,-7],[15,-12],[11,-22],[14,-31],[23,-25],[29,-10],[30,7],[20,23],[2,29],[-16,24]]);
        core(0,10,four?8:7);
        if(four){arc(-2,-8,23,3.2,4.04,'#fda4af',2.5);arc(4,-8,23,-.94,-.05,'#fda4af',2.5);}
        else{line([[-21,-15],[-16,-7]],'#fda4af',2.7);line([[22,-14],[17,-6]],'#fda4af',2.7);}
        if(three||four)for(const [px,py] of (four?[[-24,-24],[-9,-24],[10,-24],[25,-24]]:[[-20,-20],[-4,-21],[4,-21],[22,-19]]))circle(px,py,1.8,'#fecdd3');
      }
    }finally{ctx.restore();}
  }
  return Object.freeze({types:Object.freeze(types),names:Object.freeze(names),entry,stats,draw,extraSprites:Object.freeze({})});
});

/* Shared VoidFall enemies, browser prototype roster completion proposals.
 * Existing roster II sprite files are unmodified copies; no Unity assets are changed.
 * draw() uses a full-square size and returns an UP-facing sprite, ready for angle + PI/2.
 */
(function(root,factory){
  const api=factory();
  if(typeof module!=='undefined'&&module.exports)module.exports=api;
  else root.EonRoster=api;
})(typeof globalThis!=='undefined'?globalThis:this,function(){
  'use strict';
  const types=['chaser','runner','gunner','twinGunner','dasher','brute','exploder','guard','technician','mortar','splitter','bulwark','harvester','carrier'];
  const names=['','I','II','III','IV'];
  const titles={chaser:'Regular',runner:'Runner',gunner:'Gunner',twinGunner:'Twin Gunner',dasher:'Dasher',brute:'Brute',exploder:'Exploder',guard:'Guard',technician:'Technician',mortar:'Mortar',splitter:'Splitter',bulwark:'Bulwark',harvester:'Harvester',carrier:'Carrier'};
  const accents={chaser:'#fb7185',runner:'#a78bfa',gunner:'#f87171',twinGunner:'#fb923c',dasher:'#e879f9',brute:'#fb923c',exploder:'#fbbf24',guard:'#60a5fa',technician:'#2dd4bf',mortar:'#fb923c',splitter:'#f472b6',bulwark:'#38bdf8',harvester:'#34d399',carrier:'#facc15'};
  const extraSprites={};
  for(const type of ['chaser','exploder','guard','gunner'])for(const frame of [0,1])extraSprites['roster2-'+type+'_'+frame]='assets/roster2-'+type+'_'+frame+'.png';

  // These parameters drive the existing pursuit, attack, support and death hooks.
  // Distances are world pixels, durations seconds, angles radians, reductions fractions.
  const definitions={
    chaser:[
      ['Direct pursuit','A simple dark void mass around a rose core.','Runs directly toward you.',{}],
      ['Persistent pursuit','A thicker, uneven shell around one bright core.','A stronger direct pursuer; no dash or ranged attack.',{}],
      ['Relentless pursuit','A dense, lobed void body and stronger core.','Sustains heavier contact pressure through direct pursuit.',{}]
    ],
    runner:[
      ['Zigzag','A small angular dart around a violet core.','Weaves quickly through gaps.',{zigzagAmplitude:.6,zigzagFrequency:6,sprintEvery:0,sprintDuration:0,sprintMultiplier:1}],
      ['Skitter burst','A longer dart with swept stabilizer fins.','Weaves more sharply and gains a brief sprint every four seconds.',{zigzagAmplitude:.72,zigzagFrequency:7,sprintEvery:4,sprintDuration:.7,sprintMultiplier:1.25}],
      ['Forked sprint','A forked prow and two separated rear vanes.','Uses a wider weave and a stronger, brief sprint every three seconds.',{zigzagAmplitude:.9,zigzagFrequency:8,sprintEvery:3,sprintDuration:.85,sprintMultiplier:1.4}]
    ],
    gunner:[
      ['Aimed shot','Single forward barrel and red core.','Fires one aimed projectile while holding range.',{shotCount:1,shotSpread:0}],
      ['Battery volley','Authored battery body with multiple firing prongs.','Fires a tight three-shot aimed battery.',{shotCount:3,shotSpread:.12}],
      ['Quad battery','Four separate barrels and wide armored shoulders.','Fires a denser four-shot battery with a readable central aim.',{shotCount:4,shotSpread:.11}]
    ],
    twinGunner:[
      ['Paired shots','Paired barrel ports flank an orange reactor.','Keeps range and fires two adjacent shots.',{shotCount:2,shotSpread:.13}],
      ['Crossfire array','Twin long cannon housings flank a narrow chassis.','Projects a three-shot fan from its paired battery.',{shotCount:3,shotSpread:.16}],
      ['Split broadside','Two independent gun towers carry four barrels.','Projects four shots over a wider crossfire angle.',{shotCount:4,shotSpread:.2}]
    ],
    dasher:[
      ['Warned charge','A sharp, split forward blade.','Shows its aim before a committed charge.',{dashSpeed:340,dashDuration:.55,dashWindup:.85,dashCount:1}],
      ['Rail charge','A narrow rail prow with swept rear blades.','Commits to a slightly longer charge after a clear warning.',{dashSpeed:360,dashDuration:.6,dashWindup:.8,dashCount:1}],
      ['Twin rail','A twin-point prow and detached-looking blade outriggers.','Follows its first charge with a second warned commitment.',{dashSpeed:385,dashDuration:.55,dashWindup:.7,dashCount:2}]
    ],
    brute:[
      ['Heavy pursuit','A dense polygonal shell and orange reactor.','Advances slowly with heavy contact pressure.',{}],
      ['Ground slam','Layered hexagonal armor and heavy shoulder plates.','Adds a small warned slam to its heavy pursuit.',{blastRadius:64,blastCount:1,blastSpacing:0,blastDelay:1}],
      ['Siege slam','Separate blocky pauldrons enclose a reinforced reactor.','Creates a wider warned explosive slam that accelerates glacier melting.',{blastRadius:100,blastCount:1,blastSpacing:0,blastDelay:.85}]
    ],
    exploder:[
      ['Proximity burst','A red danger core inside an amber shell.','Stops nearby, warns, then detonates.',{blastRadius:80,blastCount:1,blastSpacing:0,blastDelay:.8,proximityRadius:70}],
      ['Breach charge','Authored reinforced amber detonation body.','Triggers from farther out and produces a larger warned blast.',{blastRadius:100,blastCount:1,blastSpacing:0,blastDelay:.85,proximityRadius:80}],
      ['Twin breach','Two volatile chambers joined by a dark central brace.','Marks two adjacent blast zones before its paired detonation.',{blastRadius:110,blastCount:2,blastSpacing:100,blastDelay:.85,proximityRadius:90}]
    ],
    guard:[
      ['Armored front','Blue shield arc around an off-center core.','Reduces incoming frontal damage.',{shieldReduction:.37,shieldArc:1.8}],
      ['Reinforced shield','Authored larger shield silhouette and blue core.','Blocks more damage across a broader frontal arc.',{shieldReduction:.55,shieldArc:2.1}],
      ['Citadel shield','Two offset shield crescents protect a narrow inner body.','Overlapping plates provide stronger, wider frontal protection.',{shieldReduction:.68,shieldArc:2.6}]
    ],
    technician:[
      ['Nearby repairs','A teal utility chassis and recessed core.','Repairs nearby allies in regular pulses.',{healRadius:150,healAmount:8,healCount:2}],
      ['Relay repair','A cross-shaped chassis with three repair nodes.','Repairs up to three nearby allies with a stronger relay pulse.',{healRadius:185,healAmount:12,healCount:3}],
      ['Repair lattice','Four detached node arms surround a diamond reactor.','Repairs up to five allies over a wider radius.',{healRadius:220,healAmount:18,healCount:5}]
    ],
    mortar:[
      ['Artillery warning','A long orange barrel on a dark firing platform.','Marks one blast zone before impact.',{blastRadius:65,blastCount:1,blastSpacing:0,blastDelay:1.25}],
      ['Paired barrage','Two parallel mortar tubes on a braced base.','Marks two separated artillery impacts.',{blastRadius:75,blastCount:2,blastSpacing:95,blastDelay:1.25}],
      ['Siege battery','Three staggered tubes over a broad triangular base.','Marks three spaced blast zones with a longer shared warning.',{blastRadius:82,blastCount:3,blastSpacing:105,blastDelay:1.35}]
    ],
    splitter:[
      ['Splits on death','A cracked magenta diamond shell.','Releases two roster I runners when destroyed.',{splitCount:2,childTier:1}],
      ['Tripartite shell','Three joined angular lobes with bright fracture seams.','Releases three roster I runners when destroyed.',{splitCount:3,childTier:1}],
      ['Fracture crown','Separated diamond plates ring a hollow fractured center.','Releases four roster II runners when destroyed.',{splitCount:4,childTier:2}]
    ],
    bulwark:[
      ['Heavy volley','A broad cyan frontal plate and square rear body.','Advances slowly behind a three-shot heavy fan.',{shotCount:3,shotSpread:.22,shotSpeed:150,projectileRadius:8,shieldReduction:0,shieldArc:0}],
      ['Bastion fan','A crenellated frontal wall with two rear braces.','Adds light frontal protection and a four-shot heavy fan.',{shotCount:4,shotSpread:.2,shotSpeed:165,projectileRadius:9,shieldReduction:.18,shieldArc:1.8}],
      ['Fortress volley','A stepped wall spans two reinforced shield pylons.','Fires four larger, wider-spaced shots behind a stronger frontal wall.',{shotCount:4,shotSpread:.3,shotSpeed:180,projectileRadius:11,shieldReduction:.35,shieldArc:2.2}]
    ],
    harvester:[
      ['Harvest wounded','Paired green hooks surround a dark core.','Restores itself near a wounded ally.',{harvestRadius:80,harvestAmount:10,harvestTargets:1}],
      ['Reaping hooks','Two longer sickles project ahead of the body.','Harvests from up to two nearby wounded allies.',{harvestRadius:120,harvestAmount:16,harvestTargets:2}],
      ['Reaping crown','Four hooked arms form an open predatory crown.','Harvests more health from up to three wounded allies at greater range.',{harvestRadius:160,harvestAmount:22,harvestTargets:3}]
    ],
    carrier:[
      ['Runner release','A yellow reactor beside a square launch bay.','Periodically releases one roster I runner.',{spawnCount:1,childTier:1}],
      ['Twin hangar','A broad chassis with two open launch bays.','Periodically releases two roster I runners.',{spawnCount:2,childTier:1}],
      ['Armored brood','Separate hangar pods attach to a long central reactor hull.','Periodically releases three roster II runners.',{spawnCount:3,childTier:2}]
    ]
  };
  const finalDefinitions={"chaser":["Unrelenting pursuit","The final compact pursuit form.","The strongest Regular: direct pursuit with greater durability and contact damage.",{}],"runner":["Rift sprint","A four-point dart with split trailing light.","Weaves tightly and surges through a longer burst of speed.",{"zigzagAmplitude":1,"zigzagFrequency":8,"sprintEvery":2.8,"sprintDuration":1,"sprintMultiplier":1.42}],"gunner":["Crown battery","Four bright firing tips surround a deep core.","Delivers four faster shots from a wider, warned battery.",{"shotCount":4,"shotSpread":0.14}],"twinGunner":["Convergent battery","A broad, single void body with paired firing crowns.","Fires four faster shots across a wider crossfire angle.",{"shotCount":4,"shotSpread":0.25}],"dasher":["Rift cleaver","A split, blade-like void mass with twin internal streaks.","Chains two stronger charges; each direction gets a fresh warning.",{"dashSpeed":410,"dashDuration":0.6,"dashWindup":0.75,"dashCount":2}],"brute":["Rupture slam","A dense, fractured shell around twin luminous cores.","A larger warned explosive slam cracks nearby glacier formations.",{"blastRadius":120,"blastCount":1,"blastSpacing":0,"blastDelay":1}],"exploder":["Threefold breach","Three unstable cores inside a single cracked shell.","Marks three consecutive blast zones before a staggered detonation.",{"blastRadius":115,"blastCount":3,"blastSpacing":95,"blastDelay":1.1,"proximityRadius":100}],"guard":["Absolute front","Three nested arcs wrap a compact shield body.","Stronger frontal protection leaves the rear vulnerable.",{"shieldReduction":0.72,"shieldArc":2.8}],"technician":["Restoration pulse","A compact six-node void body, anchored by a bright core.","Repairs up to six wounded allies with a wider, stronger pulse.",{"healRadius":260,"healAmount":23,"healCount":6}],"mortar":["Crown barrage","A reinforced dark lobe with four bright apertures.","Marks four spaced blast sites with a generous shared warning.",{"blastRadius":87,"blastCount":4,"blastSpacing":110,"blastDelay":1.6}],"splitter":["Fracture bloom","A split shell with several luminous fracture cores.","Releases five roster III runners when destroyed.",{"splitCount":5,"childTier":3}],"bulwark":["Citadel fan","A wide layered front with one dominant luminous core.","Projects four heavy shots behind stronger frontal protection.",{"shotCount":4,"shotSpread":0.34,"shotSpeed":190,"projectileRadius":12,"shieldReduction":0.48,"shieldArc":2.45}],"harvester":["Hunger crown","Four broad hooked edges around a dense feeding core.","Harvests from up to four wounded allies over a larger radius.",{"harvestRadius":190,"harvestAmount":28,"harvestTargets":4}],"carrier":["Void brood","A single swollen void shell with four release apertures.","Periodically releases four roster III runners.",{"spawnCount":4,"childTier":3}]};
  for(const type of types)definitions[type].push(finalDefinitions[type]);
  const entries={};
  for(const type of types)entries[type]=definitions[type].map((d,index)=>Object.freeze({
    type,tier:index+1,name:titles[type]+' '+names[index+1],baseName:titles[type],trait:d[0],description:d[1],behavior:d[2],accent:accents[type],traits:Object.freeze(d[3]),
    provenance:index===0?'Shared original':index===1&&['chaser','gunner','exploder','guard'].includes(type)?'Existing roster II art · browser behavior':'Browser prototype proposal'
  }));
  function normalizeTier(tier){return tier===2||tier===3||tier===4?tier:1;}
  function entry(type,tier=1){if(!entries[type])throw new RangeError('Unknown enemy: '+type);return entries[type][normalizeTier(tier)-1];}
  const multipliers=[null,{hp:1,speed:1,r:1,damage:1,cooldown:1,projectileSpeed:1},{hp:1.3,speed:1.06,r:1.08,damage:1.12,cooldown:.82,projectileSpeed:1.06},{hp:2.6,speed:1.12,r:1.16,damage:1.5,cooldown:.65,projectileSpeed:1.15},{hp:4.2,speed:1.16,r:1.23,damage:1.9,cooldown:.58,projectileSpeed:1.18}];
  function stats(base,type,tier=1){
    const e=entry(type,tier),m=multipliers[e.tier],result={...base,tier:e.tier,traits:e.traits};
    for(const key of ['hp','speed','r','damage','cooldown','projectileSpeed']){
      const value=Number.isFinite(base[key])?base[key]:({hp:30,speed:70,r:15,damage:8,cooldown:3,projectileSpeed:240})[key];
      result[key]=Math.max(0,value)*m[key];
    }
    return result;
  }
  function chooseTier(totalRunSeconds,roll){
    const seconds=Number.isFinite(totalRunSeconds)?Math.max(0,totalRunSeconds):0;
    const chance=Number.isFinite(roll)?Math.max(0,Math.min(1,roll)):1;
    if(seconds>=2400)return 4;
    if(seconds>2040)return chance<(seconds-2040)/360?4:3;
    if(seconds>=1800)return 3;
    if(seconds>1440)return chance<(seconds-1440)/360?3:2;
    if(seconds>=900)return 2;
    if(seconds>540)return chance<(seconds-540)/360?2:1;
    return 1;
  }

  function variants(type,tier){return ['default'];}
  function draw(ctx,type,tier,x,y,size,time=0,images={},variant='default'){
    const e=entry(type,tier),three=e.tier>=3,four=e.tier===4;
    const original=type==='chaser'?(e.tier===1?images.chaser_0:null):e.tier===1?images[type+'_0']:e.tier===2?images['roster2-'+type+'_0']:null;
    ctx.save();
    try{
      ctx.translate(x,y);
      if(original){ctx.rotate(-Math.PI/2);ctx.drawImage(original,-size/2,-size/2,size,size);return;}
      ctx.scale(size/100,size/100);
      // Match the shared enemySprite language: one dark mass, saturated edge,
      // one dominant core, and diffuse falloff. Keep the evolved silhouette.
      const accent=e.accent,dark='#080c18',inner='#111827';
      ctx.lineJoin='round';ctx.lineCap='round';
      function circle(x,y,r,fill,stroke,width=1){ctx.beginPath();ctx.arc(x,y,r,0,Math.PI*2);if(fill){ctx.fillStyle=fill;ctx.fill();}if(stroke){ctx.strokeStyle=stroke;ctx.lineWidth=width;ctx.stroke();}}
      function poly(points,fill=dark,stroke=accent,width=2.8){if(four&&fill===dark&&points.length>=8)points=points.map(([px,py],i)=>[px*(i%3===0?1.1:1),py*(i%4===0?1.1:1)]);ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(p[0],p[1]):ctx.moveTo(p[0],p[1]));ctx.closePath();if(fill){ctx.fillStyle=fill;ctx.fill();}if(stroke){ctx.shadowColor=accent;ctx.shadowBlur=3.5;ctx.strokeStyle=stroke;ctx.lineWidth=width;ctx.stroke();ctx.shadowBlur=0;}}
      function line(points,color=accent,width=2){ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(p[0],p[1]):ctx.moveTo(p[0],p[1]));ctx.strokeStyle=color;ctx.lineWidth=width;ctx.stroke();}
      function core(x=0,y=1,r=5,color=accent){circle(x,y,r*2.05,inner);circle(x,y,r*1.65,color+'12');ctx.shadowColor=color;ctx.shadowBlur=7;circle(x,y,r,color);ctx.shadowBlur=0;circle(x-.8,y-1,r*.42,'#eef4ff');}
      function arc(x,y,r,a,b,color=accent,width=3){ctx.beginPath();ctx.arc(x,y,r,a,b);ctx.strokeStyle=color;ctx.lineWidth=width;ctx.stroke();}
      for(let i=0;i<8;i++)circle(0,0,43-i*2.7,accent+(i<3?'03':i<6?'04':'05'));
      ctx.scale(four?.96:.87,four?.96:.87);
      if(type==='chaser'){
        const radius=[0,17,23,27,30][e.tier],tips=e.tier<=2?7:e.tier===3?8:9,points=[];for(let i=0;i<tips*2;i++){const a=i/(tips*2)*Math.PI*2-Math.PI/2,rr=radius*(i%2===0?(1+(i%3===0?.05:-.03)):.63);points.push([Math.cos(a)*rr,Math.sin(a)*rr]);}poly(points,dark,accent,e.tier===1?2.5:2.8);core(0,0,e.tier===1?4.8:5.7);if(e.tier>1)arc(0,0,radius*.73,.35,1.75,accent,2);if(three)arc(0,0,radius*.76,3.45,4.65,accent,2);
      }else if(type==='runner'){
        poly(three?[[-7,-27],[0,-13],[9,-26],[18,-7],[13,4],[22,12],[7,10],[0,22],[-7,12],[-20,15],[-13,1],[-18,-9]]:[[0,-25],[15,-6],[11,3],[20,12],[4,11],[-3,22],[-15,10],[-10,0],[-17,-6]]);
        core(0,0,4);line([[-7,8],[0,-10],[7,6]],'#e2d6ff',1.8);
      }else if(type==='gunner'){
        const points=[[-22,12],[-22,-5]];
        for(const px of (three?[-15,-5,5,15]:[-12,0,12]))points.push([px-3,-7],[px-3,-21],[px,-25],[px+3,-21],[px+3,-7]);
        points.push([23,-3],[23,13],[11,23],[-11,24]);poly(points);
        core(0,9,5.5);for(const px of (three?[-15,-5,5,15]:[-12,0,12]))line([[px,-20],[px,-8]],'#fda4af',2.5);
      }else if(type==='twinGunner'){
        poly(three?[[-24,12],[-24,-5],[-20,-8],[-20,-24],[-15,-27],[-12,-23],[-12,-8],[-6,-12],[0,-6],[6,-12],[12,-8],[12,-23],[16,-27],[21,-24],[21,-8],[26,-3],[25,15],[10,24],[-8,24]]:[[-21,12],[-21,-7],[-15,-10],[-15,-23],[-9,-26],[-7,-11],[5,-13],[10,-24],[16,-23],[17,-9],[23,-3],[22,16],[9,24],[-9,22]]);
        core(0,9,5.5);for(const side of [-1,1]){line([[side*16,-18],[side*16,0]],'#fdba74',3);if(three)line([[side*22,-5],[side*22,5]],'#fdba74',2.5);}
      }else if(type==='dasher'){
        poly(three?[[-7,-28],[0,-14],[8,-28],[14,-7],[23,18],[7,10],[0,24],[-9,12],[-24,19],[-16,-3]]:[[0,-29],[11,-7],[24,18],[5,8],[-1,23],[-19,14],[-10,2],[-15,-8]]);
        poly([[-5,7],[0,-13],[5,4],[0,14]],accent,null);if(three){line([[-15,4],[-9,-10]],'#f5d0fe',2);line([[15,4],[10,-10]],'#f5d0fe',2);}
      }else if(type==='brute'){
        poly(three?[[-9,-29],[8,-28],[17,-19],[28,-14],[31,1],[24,10],[23,24],[10,25],[1,31],[-13,27],[-20,16],[-30,10],[-29,-6],[-21,-15],[-20,-24]]:[[-11,-25],[9,-24],[24,-12],[27,8],[13,24],[-7,28],[-24,16],[-27,-4]]);
        core(0,1,7);line([[-17,-10],[-9,-17]],'#fed7aa',3);line([[10,17],[19,8]],'#fed7aa',3);if(three)arc(0,0,18,3.3,4.3,accent,3);
      }else if(type==='exploder'){
        poly(three?[[-21,-18],[-8,-23],[1,-15],[13,-24],[25,-14],[29,0],[22,17],[9,23],[0,18],[-13,24],[-27,13],[-29,-2]]:[[-13,-22],[11,-24],[24,-11],[27,9],[12,25],[-12,25],[-26,8],[-25,-9]]);
        if(three){core(-11,1,6,'#fb7185');core(12,1,6,'#fb7185');}else core(0,2,8,'#f87171');
        line([[-4,-21],[0,-29],[9,-31]],accent,2);circle(10,-31,2.3,'#fef08a');
      }else if(type==='guard'){
        poly([[-16,-23],[3,-28],[22,-19],[28,0],[22,20],[3,27],[-19,19],[-28,1]]);
        core(0,7,5);arc(0,0,18,-2.85,-.25,'#93c5fd',4);if(three)arc(0,0,24,-2.75,-.4,'#bfdbfe',2.8);
      }else if(type==='technician'){
        poly(three?[[-8,-25],[8,-24],[12,-12],[25,-8],[26,7],[12,12],[7,26],[-8,24],[-12,12],[-26,7],[-24,-8],[-12,-12]]:[[-12,-22],[10,-23],[22,-8],[24,12],[7,24],[-15,22],[-25,3]]);
        core(0,0,5);line([[-7,0],[7,0]],'#ccfbf1',2.2);line([[0,-7],[0,7]],'#ccfbf1',2.2);
        for(let i=0;i<(three?4:3);i++){const a=i*Math.PI*2/(three?4:3)-Math.PI/2;circle(Math.cos(a)*19,Math.sin(a)*19,2.3,accent);}
      }else if(type==='mortar'){
        const points=[[-24,15],[-24,-4]];for(const px of (three?[-14,0,14]:[-9,9]))points.push([px-5,-9],[px-5,px===0?-25:-20],[px,px===0?-29:-24],[px+5,px===0?-25:-20],[px+5,-8]);
        points.push([26,0],[24,17],[9,26],[-10,24]);poly(points);core(0,11,5.5);
        for(const px of (three?[-14,0,14]:[-9,9])){circle(px,-13,3.3,'#fed7aa');circle(px,-13,1.5,dark);}
      }else if(type==='splitter'){
        poly(three?[[0,-29],[14,-19],[12,-10],[27,-4],[28,11],[14,17],[11,28],[-5,25],[-13,16],[-27,8],[-24,-7],[-12,-13],[-13,-22]]:[[0,-27],[21,-14],[26,6],[13,24],[-3,27],[-23,15],[-27,-4],[-14,-17]]);
        line([[0,-24],[-5,-9],[6,1],[-2,12],[1,23]],accent,2.7);line([[-5,-9],[-21,-10]],accent,2.2);if(three)line([[6,1],[22,9]],accent,2.2);
        core(-11,5,4.5);core(11,-6,three?4.5:3);if(three)circle(8,15,2.5,'#fbcfe8');
      }else if(type==='bulwark'){
        poly(three?[[-29,3],[-24,-19],[-11,-26],[0,-22],[12,-27],[27,-16],[31,4],[24,21],[9,28],[-14,25],[-27,15]]:[[-26,3],[-21,-20],[0,-25],[23,-17],[28,7],[17,25],[-10,27],[-25,16]]);
        core(0,8,6);line([[-20,-5],[-15,-15],[0,-19],[16,-14],[21,-4]],'#7dd3fc',4.5);if(three)arc(0,0,26,-2.75,-.35,accent,2.7);
      }else if(type==='harvester'){
        poly(three?[[-23,-25],[-12,-16],[-9,-6],[0,-11],[9,-6],[15,-17],[25,-23],[22,-5],[15,3],[26,13],[23,25],[13,19],[7,24],[-7,23],[-14,17],[-25,23],[-25,10],[-14,3],[-23,-5]]:[[-23,-25],[-11,-13],[-8,-3],[0,-9],[10,-4],[15,-16],[24,-22],[20,0],[13,10],[2,24],[-12,18],[-23,5],[-17,-5]]);
        core(0,5,5.4);line([[-14,-9],[-18,-18]],'#bbf7d0',2);line([[14,-9],[20,-17]],'#bbf7d0',2);
      }else if(type==='carrier'){
        poly(three?[[-15,-26],[6,-29],[22,-20],[21,-9],[30,-2],[27,16],[16,22],[1,29],[-15,25],[-26,14],[-28,-2],[-21,-11]]:[[-15,-23],[8,-25],[23,-15],[27,5],[19,23],[-2,27],[-22,17],[-27,-4]]);
        core(0,-6,6);const count=three?3:2;for(let i=0;i<count;i++){const px=(i-(count-1)/2)*13;poly([[px-4,7],[px+4,7],[px+4,16],[px-4,16]],'#060a12',accent,1.7);circle(px,9,1.4,'#fef08a');}
      }
      if(four){
        if(type==='chaser'){circle(-12,0,2.4,'#f2d8ff');circle(12,0,2.4,accent);}
        if(type==='runner'||type==='dasher'){line([[-10,15],[-14,25]],accent,3);line([[10,15],[14,25]],accent,3);}
        if(type==='gunner'||type==='twinGunner'){arc(0,9,12,.2,2.95,accent,2.5);}
        if(type==='brute'){line([[-20,-3],[-11,3],[-17,11]],accent,3);core(9,-9,3);}
        if(type==='exploder')core(0,-12,4,'#fb7185');
        if(type==='guard'||type==='bulwark')arc(0,0,30,-2.72,-.4,accent,2.3);
        if(type==='technician'){circle(-17,-17,2.4,'#ccfbf1');circle(17,17,2.4,'#ccfbf1');}
        if(type==='mortar'){circle(-7,2,2.3,'#fed7aa');circle(7,2,2.3,'#fed7aa');}
        if(type==='splitter'){line([[-15,13],[0,17],[15,9]],accent,2.2);core(1,18,3);}
        if(type==='harvester'){core(-10,5,3);core(10,5,3);}
        if(type==='carrier'){poly([[-5,14],[5,14],[5,23],[-5,23]],dark,accent,2);}
      }
    }finally{ctx.restore();}
  }
  return Object.freeze({types:Object.freeze(types),names:Object.freeze(names),entry,stats,chooseTier,variants,draw,extraSprites:Object.freeze(extraSprites)});
});

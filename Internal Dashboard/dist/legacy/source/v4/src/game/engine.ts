import { makeSprites, type SpriteSet } from "./sprites";
import { Sfx } from "./audio";
import { Input } from "./input";
import { WEAPONS, WEAPON_MAP, weaponStats } from "./weapons";
import { PASSIVES, PASSIVE_MAP } from "./passives";
import { ENEMIES, ENEMY_DOT } from "./enemies";
import { FORM_MAP, type FormId } from "./forms";
import { loadSave, saveSave, type SaveData, type DailyChallenge } from "./save";
import type { EngineEvents, GamePhase, HudRefs, ToastKind, ArenaId, PassiveId, WeaponId, EnemyId, HighScore, PerfData } from "./types";

const CS=64,TAU=Math.PI*2,SK="voidfall_hscores_v2";
interface E{x:number;y:number;vx:number;vy:number;kx:number;ky:number;hp:number;maxHp:number;shield:number;r:number;type:EnemyId;speed:number;dmg:number;xp:number;age:number;hit:number;bladeCd:number;atkCd:number;rot:number;spin:number;state:number;stateT:number;dashX:number;dashY:number;seed:number;elite:boolean;eliteMods:string[];boss:boolean;burn:number;burnT:number;stunT:number;shootCd:number;burrowT:number;burrowed:boolean;summonCd:number;healCd:number;slowT:number;markT:number;summoned:boolean;summonT:number;minionDmg:number;}
interface B{x:number;y:number;vx:number;vy:number;dmg:number;pierce:number;life:number;weapon:WeaponId;h0:number;h1:number;h2:number;h3:number;chain:number;status:string;homing:boolean;explosive:boolean;explodeR:number;slow:boolean;slowAmt:number;}
interface G{x:number;y:number;vx:number;vy:number;val:number;tier:number;age:number;pull:boolean;spd:number;kind:string;}
interface C{x:number;y:number;tier:string;age:number;}
interface P{x:number;y:number;vx:number;vy:number;life:number;max:number;size:number;drag:number;spr:HTMLCanvasElement;ring:boolean;grow:number;}
interface F{x:number;y:number;life:number;max:number;text:string;color:string;size:number;}
interface M{x:number;y:number;life:number;r:number;dmg:number;area:number;armed:boolean;armedT:number;}
interface D{x:number;y:number;fireT:number;dmg:number;range:number;cd:number;pierce:number;summoned:boolean;life:number;}
interface Boss{id:string;x:number;y:number;hp:number;maxHp:number;r:number;speed:number;dmg:number;color:string;phase:number;age:number;hit:number;introT:number;dead:boolean;deathT:number;atkT:number;moveT:number;pattern:string;patternT:number;enrage:number;projs:{x:number;y:number;vx:number;vy:number;dmg:number;r:number;life:number}[];}
interface TempBuff{id:string;t:number;endT:number;}

const ARENA:Record<string,{hp:number;spd:number;eliteF:number;grid:string;bg:[string,string,string];name:string}>={
  neon:{hp:1,spd:1,eliteF:1,grid:"rgba(99,120,246,0.075)",bg:["#0c1132","#070a1c","#04050d"],name:"Neon District"},
  ash:{hp:1.15,spd:1,eliteF:1.2,grid:"rgba(120,80,50,0.065)",bg:["#1c1310","#0f0b08","#070504"],name:"Ash Wastes"},
  reactor:{hp:1.25,spd:1.08,eliteF:1.5,grid:"rgba(100,255,180,0.07)",bg:["#0a1c16","#061010","#030808"],name:"Reactor Core"},
};

export class Game {
  private cv:HTMLCanvasElement;private cx:CanvasRenderingContext2D;
  w=0;h=0;private dpr=1;private sp:SpriteSet;private inp:Input;
  readonly sfx=new Sfx();private ev:EngineEvents;private hud:HudRefs|null=null;private save:SaveData;
  phase:GamePhase="menu";private raf=0;private lt=0;private aT=0;
  private q=2;private fEma=16;private fCnt=0;
  private dbg=false;private dbgD:PerfData={fps:0,frameTime:0,enemyCount:0,projCount:0,particleCount:0,gemCount:0,difficulty:1,wave:0,seed:0};
  private mode:"standard"|"endless"="standard";private arenaId="neon";private formId="sentinel";
  private _daily:DailyChallenge|null=null;private arenaM=ARENA.neon;
  px=0;py=0;pvx=0;pvy=0;php=100;pmaxHp=100;psh=0;pshMax=0;
  plv=1;pxp=0;pxpN=12;pifr=0;pbl=0;ptt=0;palive=true;wave=0;
  ens:E[]=[];buls:B[]=[];gems:G[]=[];chst:C[]=[];pts:P[]=[];pPool:P[]=[];
  flts:F[]=[];fPool:F[]=[];mines:M[]=[];drones:D[]=[];boss:Boss|null=null;
  cam={x:0,y:0};time=0;kills=0;eKills=0;bKills=0;
  combo=0;comboT=0;maxCombo=0;dDmg=0;tDmg=0;cEarn=0;usedRevive=false;usedIronWill=false;usedPhoenixHeart=false;lastStandActive=false;vwt=0;
  trauma=0;ts=1;tts=1;fzT=0;luT=-1;dyingT=-1;
  rFl=0;cFl=0;gFl=0;hpG=1;
  uc:Record<string,number>={};wlv:Record<WeaponId,number>={} as any;wevo:Record<WeaponId,boolean>={} as any;
  activeW:WeaponId[]=[]; // 3 active weapon slots
  spT=0.9;elT=40;swT=25;bossT:number[]=[300,600,900];bIdx=0;tIdx=0;km=50;
  copts:string[]=[];coptT:("weapon"|"passive"|"heal"|"reroll"|"banish")[]=[];banished:string[]=[];victory=false;
  grid=new Map<number,number[]>();gPool:number[][]=[];projGrid=new Map<number,number[]>();projGridPool:number[][]=[];
  bgC:HTMLCanvasElement|null=null;vig:HTMLCanvasElement|null=null;rVig:HTMLCanvasElement|null=null;
  nebs:HTMLCanvasElement[]=[];dust:{x:number;y:number;s:number;p:number;f:number}[]=[];
  lH={hp:-1,mhp:-1,xp:-1,lv:-1,t:-1,sc:-1,k:-1,cb:-1,cr:-1,sh:-1,mg:-1};
  private onR=()=>this.resize();private onV=()=>{if(document.hidden&&this.phase==="playing")this.setPhase("paused");};
  private _cd:Record<string,number>={};
  // magic system
  magicCd=0;magicOn=false;magicT=0;magicId="";
  magicBuffs:TempBuff[]=[];
  private markMult=1;private slowMult=1;
  private shieldActive=false;shieldBlock=0;shieldT=0;

  constructor(cv:HTMLCanvasElement,ev:EngineEvents){
    this.cv=cv;this.cx=cv.getContext("2d",{alpha:false})!;this.ev=ev;
    this.sp=makeSprites();this.inp=new Input(cv,(c,e)=>this.hk(c,e));
    this.save=loadSave();this.buildBg();this.resize();
    window.addEventListener("resize",this.onR);document.addEventListener("visibilitychange",this.onV);
    this.ev.onScores(this.lHS());this.ev.onMute(this.sfx.muted);
    this.lt=performance.now();this.raf=requestAnimationFrame(t=>this.loop(t));
  }
  setHud(h:HudRefs){this.hud=h;}
  setRun(mode:"standard"|"endless",ar:ArenaId,form:FormId,dl:DailyChallenge|null){
    this.mode=mode;this.arenaId=ar;this.formId=form;this._daily=dl;
    this.arenaM=ARENA[ar]||ARENA.neon;
  }
  destroy(){cancelAnimationFrame(this.raf);this.inp.destroy();window.removeEventListener("resize",this.onR);document.removeEventListener("visibilitychange",this.onV);}
  setPhase(p:GamePhase){this.phase=p;this.ev.onPhaseChange(p);}
  start(){this.sfx.init();this.sfx.ui();this.reset();this.setPhase("playing");this.toast(this.mode==="endless"?"ENDLESS MODE":"SURVIVE THE SWARM","info");}
  restart(){this.sfx.init();this.sfx.ui();this.reset();this.setPhase("playing");this.toast(this.mode==="endless"?"ENDLESS MODE":"SURVIVE THE SWARM","info");}
  toMenu(){this.sfx.ui();this.ens.length=0;this.buls.length=0;this.gems.length=0;this.boss=null;this.setPhase("menu");}
  togglePause(){if(this.phase==="playing"){this.sfx.pause();this.setPhase("paused");}else if(this.phase==="paused"){this.sfx.ui();this.tts=1;this.setPhase("playing");}}
  toggleMute(){this.sfx.init();this.sfx.setMuted(!this.sfx.muted);this.ev.onMute(this.sfx.muted);}
  toggleDbg(){this.dbg=!this.dbg;}
  get score(){const cm=this.stat("comboMaster");const m=1+Math.floor(this.combo/50)*(0.15+cm);return Math.round((this.kills*10+Math.floor(this.time)*5+this.eKills*250+this.bKills*500+(this.plv-1)*40)*m);}
  get form(){return FORM_MAP[this.formId as FormId];}

  stat(n:string){
    const pl=(id:PassiveId)=>this.uc[id]||0;const pu=this.save.permUpgrades;const cb=this.chB();
    switch(n){
      case"maxHp":return Math.round(100*cb.hp*(1+pl("maxHp")*0.2));
      case"baseHp":return cb.hp;
      case"dmg":{let d=cb.dmg*(1+pl("damage")*0.12)*(1+(pu.startDmg||0)*0.05);if(this.lastStandActive)d*=2;return d;}
      case"fr":{const fr=Math.pow(0.92,pl("fireRate"));const oc=pl("overcharge");if(oc>1)return fr*(1+oc*0.2);return fr;}
      case"crit":return cb.crit+pl("critChance")*0.07;
      case"critDmg":return 2.2+pl("critDmg")*0.25;
      case"speed":return 235*cb.speed*Math.pow(1.08,pl("moveSpeed"))*(1+(pu.moveSpd||0)*0.03);
      case"magnet":return 95*cb.mag*Math.pow(1.35,pl("magnet"))*(1+(pu.pickup||0)*0.05);
      case"armor":return cb.armor+pl("armor")*3;
      case"dodge":return pl("dodge")*0.05;
      case"regen":return cb.regen+pl("regen")*0.005;
      case"shieldMax":return pl("shield")*15;
      case"bossDmg":return 1+pl("bossDmg")*0.2;
      case"closeDmg":return cb.closeDmg*(1+pl("closeDmg")*0.15);
      case"longDmg":return 1+pl("longDmg")*0.15;
      case"statusDmg":return 1+pl("statusDmg")*0.2;
      case"areaSize":return 1+pl("areaSize")*0.12;
      case"pierce":return pl("pierce");
      case"projSpd":return 1+pl("projSpeed")*0.15;
      case"xpGain":return 1+pl("xpGain")*0.15;
      case"extra":return pl("extraChoices");
      case"luck":return pl("luck")*0.1;
      case"magicPow":return 1+pl("magicPow")*0.25;
      case"killHeal":return pl("killHeal")*0.5;
      case"eliteDmg":return 1+pl("eliteDmg")*0.15;
      case"thorns":return pl("thorns")*8;
      case"goldFind":return 1+pl("goldFind")*0.2;
      case"deathBurst":return pl("deathBurst")*0.15;
      case"vampiric":return pl("vampiric")*0.02;
      case"ricochet":return pl("ricochet")*0.1;
      case"soulHarvest":return pl("soulHarvest")*0.2;
      case"timeDilation":return pl("timeDilation")*0.15;
      case"voidGrasp":return pl("voidGrasp")*2;
      case"ironWill":return pl("ironWill")||0;
      case"echoShot":return pl("echoShot")*0.12;
      case"secondWind":return pl("secondWind")*0.01;
      case"bloodPact":return 1+pl("bloodPact")*0.25;
      case"magneticField":return 1+pl("magneticField")*0.3;
      case"overcharge":return 1+pl("overcharge")*0.2;
      case"desperado":return 1+pl("desperado")*0.18;
      case"executioner":return 1+pl("executioner")*0.3;
      case"multishot":return pl("multishot");
      case"frostbite":return pl("frostbite")*0.08;
      case"chainLightning":return pl("chainLightning");
      case"lastStand":return pl("lastStand")||0;
      case"comboMaster":return pl("comboMaster")*0.03;
      case"phoenixHeart":return pl("phoenixHeart")||0;
      case"voidWalker":return pl("voidWalker")*0.15;
      case"twinSouls":return pl("twinSouls")||0;
      default:return 1;
    }
  }
  private chB(){
    const f=FORM_MAP[this.formId as FormId]||FORM_MAP.sentinel;
    const b={hp:f.hp,speed:f.speed,dmg:f.dmg,crit:f.crit,mag:f.magnet,closeDmg:1,droneDmg:1,droneN:0,armor:f.armor,dodge:0,regen:f.regen,shield:0};
    const pu=this.save.permUpgrades;b.hp*=1+(pu.startHp||0)*0.05;b.dmg*=1+(pu.startDmg||0)*0.05;
    b.mag*=1+(pu.pickup||0)*0.05;b.speed*=1+(pu.moveSpd||0)*0.03;
    if(this.formId==="overseer")b.droneN=1;if(this.formId==="overseer")b.droneDmg=1.25;
    if(this.formId==="missile")b.speed=1.2;
    if(this.formId==="spider")b.mag=1.05;
    if(this.formId==="tank")b.armor=6;return b;
  }

  private reset(){
    let hp=Math.round(this.stat("maxHp"));
    // BloodPact: sacrifice HP for damage
    const bp=this.stat("bloodPact")-1;if(bp>0){hp=Math.round(hp*(1-bp*0.1));}
    this.px=0;this.py=0;this.pvx=0;this.pvy=0;this.php=hp;this.pmaxHp=hp;
    this.psh=Math.round(this.stat("shieldMax"));this.pshMax=Math.round(this.stat("shieldMax"));
    this.plv=1;this.pxp=0;this.pxpN=12;this.pifr=0;this.pbl=0;this.ptt=0;this.palive=true;
    this.magicCd=0;this.magicOn=false;this.magicT=0;this.markMult=1;this.slowMult=1;
    this.shieldActive=false;this.shieldBlock=0;this.shieldT=0;this.magicBuffs=[];
    this.ens.length=0;this.buls.length=0;this.gems.length=0;this.chst.length=0;
    this.pts.length=0;this.flts.length=0;this.mines.length=0;this.drones.length=0;this.boss=null;
    this.cam.x=0;this.cam.y=0;this.time=0;this.wave=0;
    this.kills=0;this.eKills=0;this.bKills=0;this.combo=0;this.comboT=0;this.maxCombo=0;
    this.dDmg=0;this.tDmg=0;this.cEarn=0;this.usedRevive=false;this.usedIronWill=false;this.usedPhoenixHeart=false;this.lastStandActive=false;this.vwt=0;
    this.trauma=0;this.ts=1;this.tts=1;this.fzT=0;this.luT=-1;this.dyingT=-1;
    this.rFl=0;this.cFl=0;this.gFl=0;this.hpG=1;this.victory=false;
    this.uc={};this.wlv={} as any;this.wevo={} as any;this.banished=[];this._cd={};
    this.spT=0.9;this.elT=this.arenaM.eliteF*40;this.swT=25;
    this.bossT=[300,600,900];this.bIdx=0;this.tIdx=0;this.km=50;
    if(this._daily){
      if(this._daily.modifiers.includes("double_elites")){this.elT*=0.5;this.bossT=this.bossT.map(t=>Math.round(t*0.7));}
      if(this._daily.modifiers.includes("frequent_bosses"))this.bossT=this.bossT.map(t=>Math.round(t*0.35));
    }
    const sw=this.form.startWeapon;this.wlv[sw]=1;
    this.activeW=[sw];
    const bn=this.chB().droneN;for(let i=0;i<bn;i++)this.drones.push(this.mkDrone());
    this.ev.onUpgrades({...this.wlv,_active:this.activeW} as any,this.pLv());
    this.save.totalRuns++;const fi=this.formId as FormId;this.save.favChar[fi]=(this.save.favChar[fi]||0)+1;
    saveSave(this.save);for(let i=0;i<4;i++)this.spE("chaser" as EnemyId);
  }
  private pLv(){const r:Record<string,number>={};for(const k of Object.keys(this.uc))if(PASSIVE_MAP[k as PassiveId])r[k]=this.uc[k];return r as Record<PassiveId,number>;}

  private hk(code:string,e:KeyboardEvent){
    if(["Space","ArrowUp","ArrowDown","ArrowLeft","ArrowRight"].includes(code))e.preventDefault();
    switch(code){
      case"Escape":case"KeyP":if(this.phase==="playing"||this.phase==="paused")this.togglePause();break;
      case"KeyM":this.toggleMute();break;
      case"Enter":case"Space":if(this.phase==="menu")this.start();else if(this.phase==="gameover"||this.phase==="victory")this.restart();else if(this.phase==="paused")this.togglePause();break;
      case"KeyR":if(this.phase==="gameover"||this.phase==="paused"||this.phase==="victory")this.restart();break;
      case"KeyD":if(this.dbg)this.toggleDbg();break;
      case"KeyQ":case"KeyE":this.activateMagic();break;
      case"Digit1":case"Digit2":case"Digit3":case"Digit4":case"Digit5":this.pkOpt(parseInt(code[5])-1);break;
    }
  }

  // ── Magic ──
  activateMagic(){
    if(!this.palive||this.magicCd>0||this.phase!=="playing")return;
    const f=this.form;if(!f)return;
    this.magicCd=f.magic.cooldown;this.magicT=f.magic.duration;this.magicOn=true;this.magicId=f.id;
    this.sfx.magic();
    switch(f.id){
      case"sentinel":{this.shieldActive=true;this.shieldBlock=50;this.shieldT=2;this.sfx.shieldUp();this.ring(this.px,this.py,30,300,0.5);this.burst(this.px,this.py,"cyan",18,250,0.5,0.8);break;}
      case"phantom":{const ax={x:0,y:0};this.inp.axis(ax);if(ax.x===0&&ax.y===0){ax.x=1;ax.y=0;}
        const d=200;const nx=ax.x/Math.hypot(ax.x,ax.y)||1;const ny=ax.y/Math.hypot(ax.x,ax.y)||1;
        this.px+=nx*d;this.py+=ny*d;this.pifr=0.5;this.burst(this.px,this.py,"fuchsia",14,200,0.4,0.7);this.shake(0.15);break;}
      case"bastion":{const range=200*this.stat("areaSize");for(const e of this.ens){const dd=Math.hypot(e.x-this.px,e.y-this.py);if(dd<range+e.r){const dx2=e.x-this.px,dy2=e.y-this.py;
        this.dmgE(e,60*this.stat("dmg"),dx2,dy2,350,false);}}this.shake(0.5);this.fzT=0.06;this.ring(this.px,this.py,40,range*1.5,0.4);this.burst(this.px,this.py,"orange",22,300,0.5,1);break;}
      case"overseer":{for(let i=0;i<3;i++){const a=Math.random()*TAU;const d=this.drones.length<9?this.mkDrone():null;if(d){d.x=this.px+Math.cos(a)*80;d.y=this.py+Math.sin(a)*80;d.summoned=true;d.life=f.magic.duration;this.drones.push(d);}}this.burst(this.px,this.py,"violet",16,200,0.5,0.7);break;}
      case"wraith":{this.markMult=1.5;for(const e of this.ens)e.markT=f.magic.duration;this.toast("DEATH MARKED","danger");this.burst(this.px,this.py,"rose",12,180,0.4,0.6);this.sfx.mark();break;}
      case"elemental":{const range=250*this.stat("areaSize");for(const e of this.ens){const dd=Math.hypot(e.x-this.px,e.y-this.py);if(dd<range)e.stunT=f.magic.duration;this.burst(e.x,e.y,"cyan",3,100,0.3,0.5);}this.ring(this.px,this.py,30,range*2,0.5);this.burst(this.px,this.py,"cyan",25,280,0.6,0.9);break;}
      case"chronomancer":{this.slowMult=0.4;this.toast("TIME WARP","info");this.burst(this.px,this.py,"yellow",20,200,0.5,0.7);this.ring(this.px,this.py,20,400,0.8);break;}
      case"necromancer":{const toSummon=Math.min(4,Math.floor(this.kills/10)+1);for(let i=0;i<toSummon;i++){const e2:E={x:this.px+(Math.random()-0.5)*120,y:this.py+(Math.random()-0.5)*120,vx:0,vy:0,kx:0,ky:0,hp:30,maxHp:30,shield:0,r:10,type:"swarmer",speed:110,dmg:12*this.stat("dmg"),xp:0,age:0.5,hit:0,bladeCd:0,atkCd:0,rot:0,spin:2,state:0,stateT:0,dashX:0,dashY:0,seed:Math.random()*100,elite:false,eliteMods:[],boss:false,burn:0,burnT:0,stunT:0,shootCd:0,burrowT:0,burrowed:false,summonCd:0,healCd:0,summoned:true,summonT:f.magic.duration,minionDmg:12*this.stat("dmg"),slowT:0,markT:0};
        this.ens.push(e2);}this.burst(this.px,this.py,"emerald",14,200,0.5,0.7);break;}
      case"missile":{for(let i=0;i<8;i++){const a=(i/8)*TAU;const sp=400;this.buls.push({x:this.px+Math.cos(a)*20,y:this.py+Math.sin(a)*20,vx:Math.cos(a)*sp,vy:Math.sin(a)*sp,dmg:this.stat("dmg")*30,pierce:0,life:2.5,weapon:"rocket",h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:true,explosive:true,explodeR:40,slow:false,slowAmt:0});}this.shake(0.3);this.burst(this.px,this.py,"crimson",20,300,0.5,0.8);this.ring(this.px,this.py,20,200,0.4);this.sfx.rocket();break;}
      case"spider":{this.magicT=f.magic.duration;this.ring(this.px,this.py,20,250*this.stat("areaSize"),0.5);this.burst(this.px,this.py,"purple",12,100,0.4,0.6);for(const e of this.ens){const dd=Math.hypot(e.x-this.px,e.y-this.py);if(dd<250){e.slowT=f.magic.duration;e.burnT=f.magic.duration*0.5;e.burn=this.stat("dmg")*0.3;}}}break;
      case"tank":{this.psh+=50;this.pshMax=Math.max(this.pshMax,50);this.pifr=4;this.shieldActive=true;this.shieldBlock=999;this.shieldT=4;this.toast("FORTIFIED!","info");this.ring(this.px,this.py,20,300,0.5);this.burst(this.px,this.py,"slate",15,150,0.4,0.7);this.sfx.shieldUp();break;}
    }
  }

  // ── Upgrades ──
  private roll():string[]{
    const avail:{id:string;w:number}[]=[];
    for(const w of WEAPONS){const lv=this.wlv[w.id]||0;if(lv>0&&lv<8)avail.push({id:`w_${w.id}`,w:6});if(lv>=8&&!this.wevo[w.id])avail.push({id:`w_${w.id}`,w:3});}
    for(const p of PASSIVES){if(this.banished.includes(p.id))continue;const lv=this.uc[p.id]||0;if(lv>=p.max&&p.max<90)continue;avail.push({id:`p_${p.id}`,w:p.weight*(1+this.stat("luck"))*(lv===0?1.3:1)});}
    if(this.php<this.stat("maxHp")*0.8)avail.push({id:"heal",w:4});
    if((this.uc.rerolls||0)<3)avail.push({id:"rerolls",w:2});
    if((this.uc.banishes||0)<3)avail.push({id:"banishes",w:2});
    const picked:string[]=[];const pool=[...avail];const want=3+this.stat("extra");
    while(picked.length<want&&pool.length>0){const tw=pool.reduce((s,u)=>s+u.w,0);let r=Math.random()*tw;let idx=0;
      for(let i=0;i<pool.length;i++){r-=pool[i].w;if(r<=0){idx=i;break;}}picked.push(pool[idx].id);pool.splice(idx,1);}
    while(picked.length<3)picked.push("heal");return picked;
  }
  private parseId(id:string){if(id==="heal")return{t:"heal" as const,k:""};if(id==="rerolls")return{t:"reroll" as const,k:""};if(id==="banishes")return{t:"banish" as const,k:""};if(id.startsWith("w_"))return{t:"weapon" as const,k:id.slice(2)};return{t:"passive" as const,k:id.slice(2)};}

  applyUpgrade(id:string){
    if(this.phase!=="levelup")return;const p=this.parseId(id);
    if(p.t==="heal"){this.php=Math.min(this.stat("maxHp"),this.php+40);this.sfx.pick();this.tts=1;this.setPhase("playing");this.ev.onUpgrades({...this.wlv,_active:this.activeW} as any,this.pLv());return;}
    if(p.t==="reroll"){if((this.uc.rerolls||0)>0){this.uc.rerolls!--;this.copts=this.roll();this.coptT=this.copts.map(x=>this.parseId(x).t);this.sfx.ui();this.ev.onLevelUp(this.copts,this.coptT);}return;}
    if(p.t==="banish"){if((this.uc.banishes||0)>0&&this.copts.length>=2){this.uc.banishes!--;const i=Math.floor(Math.random()*this.copts.length);this.banished.push(this.copts[i]);this.copts.splice(i,1);this.sfx.ui();this.ev.onLevelUp(this.copts,this.coptT);}return;}
    if(p.t==="weapon"){this.wlv[p.k as WeaponId]=(this.wlv[p.k as WeaponId]||0)+1;this.save.favWeapon[p.k as WeaponId]=(this.save.favWeapon[p.k as WeaponId]||0)+1;
      // Add to active slots if not already equipped
      const wk=p.k as WeaponId;
      if(!this.activeW.includes(wk)){
        if(this.activeW.length<3){this.activeW.push(wk);}
        else{this.activeW[0]=wk;this.toast(`Weapon swapped: ${WEAPONS.find(w=>w.id===wk)?.name||wk} equipped`,"info");}
      }
    }
    if(p.t==="passive"){this.uc[p.k]=(this.uc[p.k]||0)+1;this.save.mostUsedUpgrade[p.k as PassiveId]=(this.save.mostUsedUpgrade[p.k as PassiveId]||0)+1;}
    const wlvCopy={...this.wlv,_active:this.activeW} as any;
    this.sfx.pick();this.ev.onUpgrades(wlvCopy,this.pLv());this.tts=1;this.setPhase("playing");
    if(this.pxp>=this.pxpN)this.addXp(0);this.checkEvo();
  }
  pkOpt(i:number){if(this.phase==="levelup"&&this.copts[i])this.applyUpgrade(this.copts[i]);}

  private checkEvo(){
    for(const w of WEAPONS){if(this.wevo[w.id]||!w.evolution)continue;const lv=this.wlv[w.id]||0;if(lv<8)continue;
      const c=w.evolution.condition;if(c.passive){const pl=this.pLv();if((pl[c.passive]||0)<(c.passiveLevel||5))continue;}
      this.wevo[w.id]=true;this.wlv[w.id]=1;this.save.unlockedEvolutions.push(w.evolution!.id);saveSave(this.save);
      this.toast(w.evolution!.name,"evo");this.cFl=1;this.shake(0.6);this.fzT=0.12;
      this.ring(this.px,this.py,20,520,0.8);this.burst(this.px,this.py,"cyan",35,380,0.8,1.1);this.sfx.evolution();}
  }

  // ── Spawning ──
  private hpM(){const t=this.time;return(1+t/200)*this.arenaM.hp;}
  private spdM(){return Math.min(1.4,1+this.time/600)*this.arenaM.spd;}
  private dmgM(){return 1+this.time/500;}
  private spE(type:EnemyId,x?:number,y?:number,hpMul=1,elite=false,mods:string[]=[]){
    const d=ENEMIES[type];if(!d)return;const a=Math.random()*TAU;const r=Math.hypot(this.w,this.h)/2+80+Math.random()*140;
    const hp=d.hp*this.hpM()*hpMul;const sh=mods.includes("shielded")?hp*0.3:0;
    this.ens.push({x:x??this.px+Math.cos(a)*r,y:y??this.py+Math.sin(a)*r,vx:0,vy:0,kx:0,ky:0,hp,maxHp:hp,shield:sh,r:d.r*(mods.includes("giant")?1.6:1),type,
      speed:d.speed*(mods.includes("frenzied")?1.4:1)*(0.9+Math.random()*0.2)*this.spdM(),dmg:d.dmg*this.dmgM()*(elite?1.5:1),xp:d.xp*(elite?5:1),
      age:0,hit:0,bladeCd:0,atkCd:0,rot:Math.random()*TAU,spin:(Math.random()-0.5)*2.4,state:0,stateT:0,dashX:0,dashY:0,seed:Math.random()*100,elite,eliteMods:mods,
      boss:false,burn:0,burnT:0,stunT:0,shootCd:2+Math.random()*2,burrowT:0,burrowed:false,summonCd:0,healCd:0,summoned:false,summonT:0,minionDmg:0,slowT:0,markT:0,});
  }
  private spDir(dt:number){
    this.spT-=dt;const cap=Math.min(this.mode==="endless"?400:280,26+this.time*0.65);
    if(this.spT<=0&&this.ens.length<cap){this.spT+=Math.max(0.12,0.85-this.time*0.0025);const n=1+Math.floor(this.time/40);for(let i=0;i<n;i++)this.spE(this.pkType());}
    if(this.time>=this.swT){this.swT+=28;const cnt=Math.min(30,8+Math.floor(this.time/15));
      const t:EnemyId=this.time>80&&Math.random()<0.4?"runner":this.time>60&&Math.random()<0.3?"swarmer":"chaser";
      const base=Math.random()*TAU;for(let i=0;i<cnt;i++){const a=base+(i/cnt)*TAU;const rr=Math.hypot(this.w,this.h)/2+60;this.spE(t,this.px+Math.cos(a)*rr,this.py+Math.sin(a)*rr);}
      this.toast("SWARM INBOUND","danger");this.sfx.warn();}
    if(this.time>=this.elT){this.elT+=42*this.arenaM.eliteF;const t=["chaser","tank","charger","shielded"][Math.floor(Math.random()*4)];
      const mods=this.rollEM();this.spE(t as EnemyId,undefined,undefined,1.5,true,mods);this.toast(`ELITE ${t.toUpperCase()} RISES`,"danger");this.sfx.elite();}
    if(this.bIdx<this.bossT.length&&this.time>=this.bossT[this.bIdx]){this.bIdx++;this.spBoss(this.bIdx);}
    const toasts=[{t:50,x:"THE VOID DEEPENS"},{t:110,x:"THEY GROW HUNGRY"},{t:180,x:"NO ESCAPE"},{t:400,x:"THE WARDEN AWAKENS"},{t:550,x:"DARKNESS CONVERGES"}];
    if(this.tIdx<toasts.length&&this.time>=toasts[this.tIdx].t){this.toast(toasts[this.tIdx].x,"info");this.tIdx++;}
  }
  private rollEM(){const all=["giant","frenzied","shielded","regen","explosive","teleport","vampiric","electrified"];const n=1+(this.time>200?1:0);const m:string[]=[];for(let i=0;i<n;i++)m.push(all[Math.floor(Math.random()*all.length)]);return m;}
  private pkType():EnemyId{const t=this.time;const r=Math.random();if(t<12)return"chaser";if(t<30)return r<0.5?"chaser":r<0.7?"runner":r<0.85?"crawler":"spider";if(t<55)return r<0.35?"chaser":r<0.5?"runner":r<0.6?"spider":r<0.7?"shooter":r<0.8?"wisp":r<0.9?"crawler":"toxin";if(t<80)return r<0.25?"chaser":r<0.38?"runner":r<0.48?"spider":r<0.56?"shooter":r<0.64?"wisp":r<0.72?"crawler":r<0.8?"toxin":r<0.88?"charger":"blink";if(t<120)return r<0.18?"chaser":r<0.3?"runner":r<0.38?"spider":r<0.46?"shooter":r<0.54?"wisp":r<0.62?"crawler":r<0.7?"toxin":r<0.78?"blink":r<0.84?"exploder":r<0.89?"phantom":"bomber";
    const pool:EnemyId[]=["chaser","runner","spider","shooter","wisp","crawler","toxin","blink","exploder","phantom","bomber","leech","mirror","swarmlord","pulsar","lancer","necrofiend","warper","colossus","rift","anchor","golem","broodmother","jumper","sniper","phaser","vortex","hydra","mimic","siren","wraithlord"];
    const weights=[6,5,5,4,4,5,3,4,4,3,3,3,3,3,3,3,3,2,2,2,2,3,2,3,2,2,2,2,2,1,1];
    const tw=weights.reduce((a:number,b:number)=>a+b,0);let rr=Math.random()*tw;for(let i=0;i<pool.length;i++){rr-=weights[i];if(rr<=0)return pool[i];}
    return"chaser";}
  private spBoss(idx:number){
    const bs=[{id:"warden",nm:"THE WARDEN",c:"#ef4444",hp:3000,spd:55,dm:18},{id:"hive",nm:"THE HIVE MIND",c:"#a78bfa",hp:5000,spd:45,dm:15},{id:"gunslinger",nm:"THE GUNSLINGER",c:"#fb923c",hp:7500,spd:65,dm:20},{id:"voidEngine",nm:"THE VOID ENGINE",c:"#22d3ee",hp:12000,spd:40,dm:25}];
    const b=bs[Math.min(idx-1,bs.length-1)];if(!b)return;const a=Math.random()*TAU;const rr=380;const hp=b.hp*this.hpM();
    this.boss={id:b.id,x:this.px+Math.cos(a)*rr,y:this.py+Math.sin(a)*rr,hp,maxHp:hp,r:65,speed:b.spd,dmg:b.dm*this.dmgM(),color:b.c,phase:0,age:0,hit:0,introT:2.0,dead:false,deathT:0,atkT:0,moveT:0,pattern:"chase",patternT:0,enrage:0,projs:[]};
    this.toast(b.nm,"danger");this.sfx.boss();this.shake(0.5);this.fzT=0.06;
  }

  // ── Drops ──
  private spGems(x:number,y:number,total:number){
    let g=24;while(total>0&&g-->0){const r=Math.random();let v=1;if(total>=20&&r<0.3)v=20;else if(total>=5&&r<0.55)v=5;total-=v;
      if(this.gems.length>500){const gg=this.gems[(Math.random()*this.gems.length)|0];gg.val+=v;if(gg.val>=20)gg.tier=2;else if(gg.val>=5)gg.tier=1;continue;}
      const a=Math.random()*TAU;const sp=40+Math.random()*90;this.gems.push({x,y,vx:Math.cos(a)*sp,vy:Math.sin(a)*sp,val:v,tier:v>=20?2:v>=5?1:0,age:Math.random()*10,pull:false,spd:0,kind:"xp"});}
    if(this.gems.length<500&&Math.random()<0.5)this.gems.push({x:x+(Math.random()-0.5)*40,y:y+(Math.random()-0.5)*40,vx:0,vy:0,val:5+Math.floor(Math.random()*10),tier:1,age:0,pull:false,spd:0,kind:"credit"});
  }
  private spChest(x:number,y:number,tier:string){this.chst.push({x,y,tier,age:0});}

  // ── FX ──
  private shake(a:number){this.trauma=Math.min(1,this.trauma+a);}
  private burst(x:number,y:number,dot:string,n:number,spd:number,life:number,sz:number){
    if(this.q===0)n=Math.ceil(n*0.4);else if(this.q===1)n=Math.ceil(n*0.65);const s=this.sp.dot[dot];if(!s)return;
    for(let i=0;i<n;i++){if(this.pts.length>=400)return;const p=this.pPool.pop()??{}as P;const a=Math.random()*TAU;const sp=spd*(0.3+Math.random()*0.9);
      p.x=x;p.y=y;p.vx=Math.cos(a)*sp;p.vy=Math.sin(a)*sp;p.life=p.max=life*(0.6+Math.random()*0.7);p.size=sz*(0.6+Math.random()*0.8);p.drag=3.2;p.spr=s;p.ring=false;p.grow=0;this.pts.push(p);}}
  private ring(x:number,y:number,ss:number,gr:number,life:number){const p=this.pPool.pop()??{}as P;p.x=x;p.y=y;p.vx=0;p.vy=0;p.life=p.max=life;p.size=ss;p.drag=0;p.spr=this.sp.ring;p.ring=true;p.grow=gr;this.pts.push(p);}
  private ft(x:number,y:number,text:string,color:string,sz:number){if(this.flts.length>=50)return;const f=this.fPool.pop()??{}as F;f.x=x+(Math.random()-0.5)*14;f.y=y-8;f.life=f.max=0.7;f.text=text;f.color=color;f.size=sz;this.flts.push(f);}
  private toast(text:string,kind:ToastKind){this.ev.onToast(text,kind);}
  private mkDrone():D{return{x:this.px,y:this.py,fireT:0.3+Math.random()*0.3,dmg:8*this.chB().droneDmg,range:420,cd:0.55,pierce:0,summoned:false,life:999};}

  // ── Weapons ──
  private fireW(dt:number){
    const ms=this.stat("multishot"); // extra projectiles
    const tw=this.stat("twinSouls"); // echo shots
    for(const wId of this.activeW){const lv=this.wlv[wId]||0;if(lv<=0)continue;const w=WEAPON_MAP[wId];if(!w)continue;
      const evo=this.wevo[wId]||false;const ws=weaponStats(wId,lv,evo);
      const dmg=ws.damage*this.stat("dmg");this._cd[wId]=(this._cd[wId]||0)-dt;
      if((this._cd[wId]||0)<=0){this._cd[wId]=ws.cooldown*this.stat("fr");
        const origCount=ws.count;
        // Multishot: add extra projectiles
        if(ms>0){ws.count=origCount+ms;this.fireOne(wId,dmg,ws,evo,dt);ws.count=origCount;}
        else this.fireOne(wId,dmg,ws,evo,dt);
        // Twin Souls: echo shot at 50% damage
        if(tw>0){this.fireOne(wId,dmg*0.5,{...ws,count:origCount},evo,dt);}
      }
    }
  }

  private fireOne(id:WeaponId,dmg:number,ws:{range:number;pierce:number;area:number;count:number},evo:boolean,dt:number){
    const tgt=this.nrEnemy(ws.range||500);const ang=tgt?Math.atan2(tgt.y-this.py,tgt.x-this.px):Math.random()*TAU;
    switch(id){
      case"pistol":for(let i=0;i<ws.count;i++){const a=ang+(i-(ws.count-1)/2)*0.12+(Math.random()-0.5)*0.04;const c=Math.cos(a),s=Math.sin(a);const sp=640*this.stat("projSpd");this.buls.push({x:this.px+c*16,y:this.py+s*16,vx:c*sp,vy:s*sp,dmg,pierce:ws.pierce+this.stat("pierce"),life:1,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:evo?3:0,status:"none",homing:false,explosive:false,explodeR:0,slow:false,slowAmt:0});}this.sfx.shoot();this.burst(this.px+Math.cos(ang)*20,this.py+Math.sin(ang)*20,"cyan",2,130,0.16,0.55);break;
      case"shotgun":{const pellets=ws.count;const spr=(0.5+ws.area*0.05)*this.stat("areaSize");for(let i=0;i<pellets;i++){const a=ang+(Math.random()-0.5)*spr;const c=Math.cos(a),s=Math.sin(a);const sp=500*this.stat("projSpd");this.buls.push({x:this.px+c*16,y:this.py+s*16,vx:c*sp,vy:s*sp,dmg:dmg*0.7,pierce:0,life:0.35,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:false,explosive:evo,explodeR:evo?40:0,slow:false,slowAmt:0});}this.sfx.shotgun();this.shake(0.15);break;}
      case"smg":{const c=Math.cos(ang),s=Math.sin(ang);const sp=700*this.stat("projSpd");this.buls.push({x:this.px+c*16,y:this.py+s*16,vx:c*sp+(Math.random()-0.5)*sp*0.12,vy:s*sp+(Math.random()-0.5)*sp*0.12,dmg,pierce:ws.pierce,life:0.6,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:evo?2:0,status:Math.random()<0.15?"burn":"none",homing:evo,explosive:false,explodeR:0,slow:false,slowAmt:0});if(Math.random()<0.3)this.sfx.shoot();break;}
      case"railgun":for(let i=0;i<ws.count;i++){const a=ang+(i-(ws.count-1)/2)*0.06;const c=Math.cos(a),s=Math.sin(a);const sp=900*this.stat("projSpd");this.buls.push({x:this.px+c*20,y:this.py+s*20,vx:c*sp,vy:s*sp,dmg,pierce:ws.pierce+this.stat("pierce"),life:1.2,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:false,explosive:evo,explodeR:evo?60:0,slow:false,slowAmt:0});}this.sfx.railgun();this.shake(0.2);this.fzT=0.03;break;
      case"arc":{if(!tgt)break;let cx=this.px,cy=this.py;let ct:E=tgt;for(let i=0;i<ws.count;i++){this.buls.push({x:cx,y:cy,vx:0,vy:0,dmg:dmg*(1-i*0.08),pierce:0,life:0.25,weapon:id,h0:this.ens.indexOf(ct),h1:-1,h2:-1,h3:-1,chain:0,status:"chain",homing:false,explosive:false,explodeR:0,slow:false,slowAmt:0});cx=ct.x;cy=ct.y;let best:E|null=null,bd=ws.range*1.5;for(const e of this.ens){if(e===ct||e.age<0.1)continue;const dd2=Math.hypot(e.x-cx,e.y-cy);if(dd2<bd){bd=dd2;best=e;}}if(!best)break;ct=best;}this.sfx.arc();break;}
      case"flame":for(let i=0;i<3;i++){const a=ang+(Math.random()-0.5)*0.6*ws.area*this.stat("areaSize");const dist=30+Math.random()*ws.range;this.buls.push({x:this.px+Math.cos(a)*dist,y:this.py+Math.sin(a)*dist,vx:Math.cos(a)*40,vy:Math.sin(a)*40,dmg:dmg*0.3,pierce:0,life:0.35,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"burn",homing:false,explosive:false,explodeR:0,slow:false,slowAmt:0});}if(Math.random()<0.4)this.sfx.flame();break;
      case"rocket":for(let i=0;i<ws.count;i++){const a=ang+(i-(ws.count-1)/2)*0.3;const c=Math.cos(a),s=Math.sin(a);const sp=380*this.stat("projSpd");this.buls.push({x:this.px+c*18,y:this.py+s*18,vx:c*sp,vy:s*sp,dmg,pierce:0,life:2,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:false,explosive:true,explodeR:50*ws.area*this.stat("areaSize"),slow:false,slowAmt:0});}this.sfx.rocket();this.shake(0.12);break;
      case"blades":this.pbl+=dt*3.1;break;
      case"drone":break;
      case"mines":for(let i=0;i<ws.count;i++){const bh=Math.atan2(-this.pvy,-this.pvx)+(Math.random()-0.5)*0.8;this.mines.push({x:this.px+Math.cos(bh)*50,y:this.py+Math.sin(bh)*50,life:8,r:ws.area*this.stat("areaSize")*30,dmg:dmg*2,area:ws.area*this.stat("areaSize"),armed:false,armedT:0.5});}this.sfx.mine();break;
      case"ice":for(let i=0;i<ws.count;i++){const a=ang+(i-(ws.count-1)/2)*0.08;const c=Math.cos(a),s=Math.sin(a);const sp=580*this.stat("projSpd");this.buls.push({x:this.px+c*16,y:this.py+s*16,vx:c*sp,vy:s*sp,dmg,pierce:ws.pierce+this.stat("pierce"),life:1.1,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:false,explosive:false,explodeR:0,slow:true,slowAmt:0.45});}this.sfx.ice();this.burst(this.px+Math.cos(ang)*20,this.py+Math.sin(ang)*20,"cyan",4,100,0.2,0.6);break;
      case"nova":{const range=180*ws.area*this.stat("areaSize");for(const e of this.ens){const dd=Math.hypot(e.x-this.px,e.y-this.py);if(dd<range+e.r){const dx2=e.x-this.px,dy2=e.y-this.py;this.dmgE(e,dmg,dx2,dy2,120,false);}}if(evo){for(let j=0;j<8;j++){const ja=(j/8)*TAU;this.buls.push({x:this.px+Math.cos(ja)*range*0.5,y:this.py+Math.sin(ja)*range*0.5,vx:Math.cos(ja)*200,vy:Math.sin(ja)*200,dmg:dmg*0.3,pierce:0,life:1.5,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:false,explosive:false,explodeR:0,slow:false,slowAmt:0});}}this.shake(0.3);this.fzT=0.04;this.ring(this.px,this.py,20,range*2.5,0.4);this.burst(this.px,this.py,"orange",18,260,0.5,0.9);this.sfx.nova();break;}
      case"crossbow":for(let i=0;i<ws.count;i++){const a=ang+(i-(ws.count-1)/2)*0.2;const c=Math.cos(a),s=Math.sin(a);const sp=800*this.stat("projSpd");this.buls.push({x:this.px+c*20,y:this.py+s*20,vx:c*sp,vy:s*sp,dmg,pierce:ws.pierce+this.stat("pierce"),life:1.5,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:false,explosive:false,explodeR:0,slow:false,slowAmt:0});}this.sfx.crossbow();break;
      case"tendrils":for(let i=0;i<3;i++){const a=ang+(Math.random()-0.5)*ws.area*this.stat("areaSize");const dist=40+Math.random()*ws.range;this.buls.push({x:this.px+Math.cos(a)*dist,y:this.py+Math.sin(a)*dist,vx:Math.cos(a)*30,vy:Math.sin(a)*30,dmg:dmg*0.25,pierce:0,life:0.5,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"burn",homing:false,explosive:false,explodeR:0,slow:false,slowAmt:0});}this.sfx.tendrils();break;
      case"plasma":{const c=Math.cos(ang),s=Math.sin(ang);const sp=500*this.stat("projSpd");this.buls.push({x:this.px+c*14,y:this.py+s*14,vx:c*sp,vy:s*sp,dmg,pierce:0,life:0.8,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:false,explosive:false,explodeR:0,slow:false,slowAmt:0});this.sfx.plasma();break;}
      case"thunder":{const targets=evo?4:1;const candidates=this.ens.filter(e=>e.age>0.2&&!e.burrowed);for(let i=0;i<Math.min(targets,candidates.length);i++){const t=candidates[Math.floor(Math.random()*candidates.length)];this.buls.push({x:t.x+(Math.random()-0.5)*30,y:t.y-200,vx:0,vy:400,dmg,pierce:0,life:0.5,weapon:id,h0:this.ens.indexOf(t),h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:true,explosive:false,explodeR:0,slow:false,slowAmt:0});}this.sfx.thunder();break;}
      case"chain":{for(let i=0;i<ws.count;i++){const a=ang+(i-(ws.count-1)/2)*0.15;const c=Math.cos(a),s=Math.sin(a);const sp=450*this.stat("projSpd");this.buls.push({x:this.px+c*18,y:this.py+s*18,vx:c*sp,vy:s*sp,dmg,pierce:0,life:1.8,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:false,explosive:false,explodeR:0,slow:false,slowAmt:0});}this.sfx.chain();break;}
      case"voidorb":{for(let i=0;i<ws.count;i++){const a=ang+(i-(ws.count-1)/2)*0.25;const c=Math.cos(a),s=Math.sin(a);const sp=180*this.stat("projSpd");this.buls.push({x:this.px+c*20,y:this.py+s*20,vx:c*sp,vy:s*sp,dmg,pierce:0,life:4,weapon:id,h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:false,explosive:evo,explodeR:evo?80:0,slow:true,slowAmt:0.2});}this.sfx.voidorb();this.burst(this.px+Math.cos(ang)*30,this.py+Math.sin(ang)*30,"violet",6,100,0.3,0.7);break;}
    }
  }

  private nrEnemy(md:number){let best:E|null=null,bd=md*md;for(const e of this.ens){if(e.age<0.15||e.burrowed)continue;const dx=e.x-this.px,dy=e.y-this.py;const d=dx*dx+dy*dy;if(d<bd){bd=d;best=e;}}return best;}

  // ── Update ──
  private update(dt:number,dr:number){
    this.time+=dt;this.wave=Math.floor(this.time/30)+1;
    const ax={x:0,y:0};if(this.palive)this.inp.axis(ax);
    const k=1-Math.exp(-14*dt);this.pvx+=(ax.x*this.stat("speed")-this.pvx)*k;this.pvy+=(ax.y*this.stat("speed")-this.pvy)*k;
    this.px+=this.pvx*dt;this.py+=this.pvy*dt;if(this.pifr>0)this.pifr-=dt;
    const rg=this.stat("regen");if(rg>0&&this.php<this.pmaxHp)this.php=Math.min(this.pmaxHp,this.php+this.pmaxHp*rg*dt);
    if(this.pshMax>0&&this.psh<this.pshMax)this.psh=Math.min(this.pshMax,this.psh+this.pshMax*0.02*dt);
    // magic timers
    if(this.magicCd>0)this.magicCd-=dr;
    if(this.magicOn){this.magicT-=dr;if(this.magicT<=0){this.magicOn=false;this.markMult=1;this.slowMult=1;}}
    if(this.shieldActive){this.shieldT-=dr;if(this.shieldT<=0)this.shieldActive=false;}
    // shield absorbs damage
    this.ptt-=dt;if(Math.abs(this.pvx)+Math.abs(this.pvy)>40&&this.ptt<=0&&this.q>0){this.ptt=0.055;
      const p=this.pPool.pop()??{}as P;p.x=this.px;p.y=this.py;p.vx=-this.pvx*0.08+(Math.random()-0.5)*20;p.vy=-this.pvy*0.08+(Math.random()-0.5)*20;
      p.life=p.max=0.32;p.size=0.5;p.drag=1;p.spr=this.sp.dot.cyan;p.ring=false;p.grow=0;if(this.pts.length<400)this.pts.push(p);else this.pPool.push(p);}
    const ck=1-Math.exp(-6*dt);this.cam.x+=(this.px-this.cam.x)*ck;this.cam.y+=(this.py-this.cam.y)*ck;
    if(this.palive)this.fireW(dt);
    // Passive effects
    if(this.palive){
      // Time Dilation - slow enemies when low HP
      const td=this.stat("timeDilation")-1;if(td>0&&this.php<this.pmaxHp*0.3){this.ts=Math.max(0.6,1-td*0.3);}
      // Void Grasp - pull distant enemies
      const vg=this.stat("voidGrasp");if(vg>0){for(const e of this.ens){const dd=Math.hypot(e.x-this.px,e.y-this.py);if(dd>200&&dd<500){e.kx+=(this.px-e.x)/dd*vg*30*dt;e.ky+=(this.py-e.y)/dd*vg*30*dt;}}}
      // Second Wind - regen when standing still
      const sw=this.stat("secondWind");if(sw>0&&Math.abs(this.pvx)<10&&Math.abs(this.pvy)<10){this.php=Math.min(this.pmaxHp,this.php+this.pmaxHp*sw*dt);}
      // Last Stand - trigger at 1 HP
      const ls=this.stat("lastStand");if(ls>0&&!this.lastStandActive&&this.php<=5){this.lastStandActive=true;this.toast("LAST STAND!","danger");this.rFl=0.5;this.cFl=0.5;this.shake(0.3);this.ring(this.px,this.py,20,300,0.6);this.burst(this.px,this.py,"red",20,200,0.5,0.8);setTimeout(()=>{this.lastStandActive=false;},5000);}
      // Void Walker - speed boost and damaging trail
      const vw=this.stat("voidWalker");if(vw>0&&Math.abs(this.pvx)+Math.abs(this.pvy)>30){this.vwt-=dt;if(this.vwt<=0){this.vwt=0.08;const p=this.pPool.pop()??{}as P;p.x=this.px;p.y=this.py;p.vx=(Math.random()-0.5)*50;p.vy=(Math.random()-0.5)*50;p.life=p.max=0.5;p.size=0.6;p.drag=1.5;p.spr=this.sp.dot.violet;p.ring=false;p.grow=0;if(this.pts.length<400)this.pts.push(p);else this.pPool.push(p);}}}
    this.upDrones(dt);this.upMines(dt);this.spDir(dt);this.upEns(dt);this.upBuls(dt);this.upBoss(dt);this.upGems(dt);this.upChests(dt);this.upFx(dt);
    if(this.comboT>0){this.comboT-=dr;if(this.comboT<=0)this.combo=0;}
    if(this.luT>0){this.luT-=dr;if(this.luT<=0){this.luT=-1;this.copts=this.roll();this.coptT=this.copts.map(x=>this.parseId(x).t);this.setPhase("levelup");this.ev.onLevelUp(this.copts,this.coptT);}}
    if(this.dyingT>0){this.dyingT-=dr;if(this.dyingT<=0){this.dyingT=-1;this.endRun(false);}}
    if(this.mode==="standard"&&this.time>=900&&!this.victory&&!this.boss){this.victory=true;this.endRun(true);}
    this.trauma=Math.max(0,this.trauma-dr*1.7);this.rFl=Math.max(0,this.rFl-dr*2.4);this.cFl=Math.max(0,this.cFl-dr*2.2);this.gFl=Math.max(0,this.gFl-dr*2.0);
    this.ts+=(this.tts-this.ts)*(1-Math.exp(-9*dr));
  }

  private upDrones(dt:number){
    const want=(this.wlv.drone||0)+this.chB().droneN;while(this.drones.length<want&&this.drones.length<9)this.drones.push(this.mkDrone());
    while(this.drones.length>want)this.drones.pop();
    for(let i=this.drones.length-1;i>=0;i--){const d=this.drones[i];d.fireT-=dt;
      if(d.summoned){d.life-=dt;if(d.life<=0){this.drones.splice(i,1);continue;}}
      const idx=i;const orbitR=100;const ta=this.pbl+idx*(TAU/Math.max(want,1));
      d.x+=(this.px+Math.cos(ta)*orbitR-d.x)*(1-Math.exp(-8*dt));d.y+=(this.py+Math.sin(ta)*orbitR-d.y)*(1-Math.exp(-8*dt));
      if(d.fireT<=0&&this.palive){const t=this.nrEnemy(d.range);if(t){d.fireT=d.cd*this.stat("fr");const a=Math.atan2(t.y-d.y,t.x-d.x);const sp=550*this.stat("projSpd");
        this.buls.push({x:d.x,y:d.y,vx:Math.cos(a)*sp,vy:Math.sin(a)*sp,dmg:d.dmg*this.stat("dmg")*this.chB().droneDmg,pierce:d.pierce+this.stat("pierce"),life:0.8,weapon:"drone",h0:-1,h1:-1,h2:-1,h3:-1,chain:0,status:"none",homing:this.wevo.drone,explosive:false,explodeR:0,slow:false,slowAmt:0});}}}
  }
  private upMines(dt:number){for(let i=this.mines.length-1;i>=0;i--){const m=this.mines[i];m.life-=dt;if(m.armedT>0)m.armedT-=dt;else m.armed=true;
      if(m.life<=0){this.mines.splice(i,1);continue;}for(const e of this.ens){if(e.burrowed)continue;if(Math.hypot(e.x-m.x,e.y-m.y)<m.r+e.r&&m.armed){this.exMine(m);this.mines.splice(i,1);break;}}}}
  private exMine(m:M){for(const e of this.ens){const d=Math.hypot(e.x-m.x,e.y-m.y);if(d<m.r+e.r)this.dmgE(e,m.dmg,m.x-e.x,m.y-e.y,300,false);}
    this.burst(m.x,m.y,"orange",15,300,0.5,0.9);this.ring(m.x,m.y,10,m.r*2,0.4);this.sfx.die();this.shake(0.2);}

  private upEns(dt:number){
    for(const a of this.grid.values()){a.length=0;this.gPool.push(a);}this.grid.clear();
    for(let i=0;i<this.ens.length;i++){const e=this.ens[i];const k=Math.floor(e.x/CS)*100000+Math.floor(e.y/CS);let c=this.grid.get(k);if(!c){c=this.gPool.pop()??[];this.grid.set(k,c);}c.push(i);}
    for(let i=this.ens.length-1;i>=0;i--){const e=this.ens[i];e.age+=dt;if(e.hit>0)e.hit-=dt;if(e.atkCd>0)e.atkCd-=dt;if(e.bladeCd>0)e.bladeCd-=dt;
      if(e.stunT>0){e.stunT-=dt;continue;}
      if(e.burnT>0){e.burnT-=dt;e.hp-=e.burn*dt;if(e.hp<=0){this.killE(e,i);continue;}}
      if(e.summoned){e.summonT-=dt;if(e.summonT<=0){this.ens.splice(i,1);continue;}}
      e.rot+=e.spin*dt;
      const sm=e.slowT>0?this.slowMult:1;e.slowT=Math.max(0,e.slowT-dt);
      const dx=this.px-e.x,dy=this.py-e.y,dist=Math.hypot(dx,dy)||1,nx=dx/dist,ny=dy/dist;
      const spd=e.speed*sm;
      switch(e.type){
        case"charger":if(e.state===0){e.vx=nx*spd;e.vy=ny*spd;if(dist<220&&e.age>0.5){e.state=1;e.stateT=0.3;}}else if(e.state===1){e.vx*=0.9;e.vy*=0.9;e.stateT-=dt;if(e.stateT<=0){e.state=2;e.stateT=0.35;e.dashX=nx;e.dashY=ny;}}else if(e.state===2){e.vx=e.dashX*520;e.vy=e.dashY*520;e.stateT-=dt;if(e.stateT<=0){e.state=3;e.stateT=0.6;}}else{e.vx=nx*e.speed*0.3;e.vy=ny*e.speed*0.3;e.stateT-=dt;if(e.stateT<=0)e.state=0;}break;
        case"shooter":e.shootCd-=dt;if(dist>180){e.vx=nx*spd;e.vy=ny*spd;}else if(dist<140){e.vx=-nx*spd*0.5;e.vy=-ny*spd*0.5;}else{e.vx*=0.9;e.vy*=0.9;}
          if(e.shootCd<=0){e.shootCd=3.0;if(this.boss)this.boss.projs.push({x:e.x,y:e.y,vx:nx*280,vy:ny*280,dmg:e.dmg*0.5,r:5,life:3});}break;
        case"healer":e.healCd-=dt;if(e.healCd<=0){e.healCd=5;for(const o of this.ens)if(o!==e&&Math.hypot(o.x-e.x,o.y-e.y)<200){o.hp=Math.min(o.maxHp,o.hp+o.maxHp*0.15);}this.burst(e.x,e.y,"rose",8,120,0.5,0.6);}e.vx=nx*spd*0.5;e.vy=ny*spd*0.5;break;
        case"summoner":e.summonCd-=dt;if(e.summonCd<=0&&this.ens.length<200){e.summonCd=8;for(let s=0;s<3;s++)this.spE("swarmer",e.x+(Math.random()-0.5)*60,e.y+(Math.random()-0.5)*60);}e.vx=nx*spd*0.4;e.vy=ny*spd*0.4;break;
        case"ambusher":if(e.burrowed){e.burrowT-=dt;if(e.burrowT<=0){e.burrowed=false;e.age=0.3;}}else{e.vx=nx*spd;e.vy=ny*spd;if(dist>350){e.burrowed=true;e.burrowT=2;}}break;
        case"spider":{e.shootCd-=dt;if(dist>120){e.vx=nx*spd;e.vy=ny*spd;}else{e.vx*=0.85;e.vy*=0.85;}if(e.shootCd<=0){e.shootCd=6;this.boss?.projs.push({x:e.x,y:e.y,vx:nx*180,vy:ny*180,dmg:e.dmg*0.4,r:4,life:4});}}break;
        case"phantom":e.stateT-=dt;if(e.stateT<=0){e.stateT=1.5;this.burst(e.x,e.y,"indigo",4,60,0.3,0.4);this.shake(0.05);}e.vx=nx*spd*1.15;e.vy=ny*spd*1.15;break;
        case"golem":{e.stateT-=dt;if(e.stateT<=0&&dist<300){e.stateT=5;for(const o of this.ens){if(o!==e&&Math.hypot(o.x-e.x,o.y-e.y)<150){const kd2=Math.hypot(o.x-e.x,o.y-e.y)||1;o.kx+=(o.x-e.x)/kd2*200;o.ky+=(o.y-e.y)/kd2*200;}}this.shake(0.3);this.ring(e.x,e.y,20,180,0.3);this.burst(e.x,e.y,"slate",12,200,0.4,0.8);}}e.vx=nx*spd*0.7;e.vy=ny*spd*0.7;break;
        case"wisp":{if(dist<120){const drain=e.dmg*0.3*dt;if(this.palive&&this.php<this.pmaxHp){this.php=Math.min(this.pmaxHp,this.php+drain);this.cEarn+=1;}}}e.vx=nx*spd;e.vy=ny*spd;break;
        case"vortex":{e.stateT-=dt;if(e.stateT<=0){e.stateT=3;for(const o of this.ens){const dd=Math.hypot(o.x-e.x,o.y-e.y);if(dd<300&&dd>1){const dd2=Math.hypot(o.x-e.x,o.y-e.y);o.kx+=(e.x-o.x)/dd2*150;o.ky+=(e.y-o.y)/dd2*150;}}this.burst(e.x,e.y,"vortex",8,80,0.5,0.6);}e.vx=nx*spd*0.5;e.vy=ny*spd*0.5;}break;
        case"broodmother":{e.summonCd-=dt;if(e.summonCd<=0&&this.ens.length<250){e.summonCd=8;for(let s=0;s<3;s++)this.spE("swarmer",e.x+(Math.random()-0.5)*40,e.y+(Math.random()-0.5)*40);}e.vx=nx*spd*0.7;e.vy=ny*spd*0.7;}break;
        case"jumper":{e.stateT-=dt;if(e.state<=0){e.state=1;e.stateT=1.2;e.vx*=0;e.vy*=0;}else if(e.state===1){e.stateT-=dt;if(e.stateT<=0){e.state=2;e.stateT=0.4;const ja=Math.atan2(this.py-e.y,this.px-e.x);e.vx=Math.cos(ja)*400;e.vy=Math.sin(ja)*400;}}else{e.stateT-=dt;e.vx*=0.95;e.vy*=0.95;if(e.stateT<=0)e.state=0;}}break;
        case"sniper":{e.shootCd-=dt;if(dist>200){e.vx*=0.95;e.vy*=0.95;}else if(dist<150){e.vx=-nx*spd*0.5;e.vy=-ny*spd*0.5;}else{e.vx*=0.9;e.vy*=0.9;}if(e.shootCd<=0){e.shootCd=2.5;const sa=Math.atan2(this.py-e.y,this.px-e.x);this.boss?.projs.push({x:e.x,y:e.y,vx:Math.cos(sa)*350,vy:Math.sin(sa)*350,dmg:e.dmg,r:5,life:4});this.burst(e.x,e.y,"crimson",3,60,0.2,0.4);}}break;
        case"phaser":{e.stateT-=dt;if(e.stateT<=0){e.stateT=3+Math.random()*2;e.burrowed=!e.burrowed;if(!e.burrowed)e.age=0.3;}if(!e.burrowed){e.vx=nx*spd;e.vy=ny*spd;}else{e.vx*=0.9;e.vy*=0.9;}}break;
        case"bomber":{e.shootCd-=dt;if(dist<250){e.vx=-nx*spd*0.5;e.vy=-ny*spd*0.5;}else{e.vx=nx*spd;e.vy=ny*spd;}if(e.shootCd<=0){e.shootCd=3;this.boss?.projs.push({x:e.x,y:e.y,vx:(Math.random()-0.5)*40,vy:(Math.random()-0.5)*40,dmg:e.dmg*0.6,r:8,life:5});this.burst(e.x,e.y,"gold",4,60,0.3,0.5);}}break;
        case"leech":{if(dist<200){const drain=e.dmg*dt;if(this.palive&&this.php>0){const actual=Math.min(this.php,drain);this.php-=actual;this.dDmg-=actual;e.hp=Math.min(e.maxHp,e.hp+actual*0.3);this.burst(this.px,this.py,"blood",1,40,0.2,0.4);}}e.vx=nx*spd;e.vy=ny*spd;}break;
        case"mirror":e.vx=nx*spd*0.8;e.vy=ny*spd*0.8;break;
        case"swarmlord":{e.stateT-=dt;if(e.stateT<=0){e.stateT=6;for(let i=0;i<4;i++){const a=(i/4)*Math.PI*2;this.spE("swarmer",e.x+Math.cos(a)*60,e.y+Math.sin(a)*60);}}e.vx=nx*spd*0.6;e.vy=ny*spd*0.6;}break;
        case"crawler":{if(dist<50){e.vx=-nx*spd;e.vy=-ny*spd;}else{e.vx=nx*spd;e.vy=ny*spd;}}break;
        case"wall":{for(const o of this.ens){const dd=Math.hypot(o.x-e.x,o.y-e.y);if(dd<100&&dd>1){o.shield=Math.min(o.maxHp*0.3,o.shield+0.5*dt);}}e.vx=nx*spd*0.6;e.vy=ny*spd*0.6;}break;
        case"pulsar":{e.stateT-=dt;if(e.stateT<=0){e.stateT=2;const pRange=150;for(const o of this.ens){if(o===e)continue;const dd=Math.hypot(o.x-e.x,o.y-e.y);if(dd<pRange+o.r){const dd2=Math.hypot(o.x-e.x,o.y-e.y);o.kx+=(o.x-e.x)/dd2*200;o.ky+=(o.y-e.y)/dd2*200;}}if(Math.hypot(this.px-e.x,this.py-e.y)<pRange+14)this.dmgP(e.dmg*0.4,(this.px-e.x)/Math.hypot(this.px-e.x,this.py-e.y)||1,(this.py-e.y)/Math.hypot(this.px-e.x,this.py-e.y)||1);this.ring(e.x,e.y,20,pRange*2,0.4);this.burst(e.x,e.y,"pinkGlow",10,150,0.4,0.6);this.shake(0.15);}}e.vx=nx*spd*0.7;e.vy=ny*spd*0.7;break;
        case"lancer":{e.stateT-=dt;if(e.state===0){e.vx=nx*spd*0.5;e.vy=ny*spd*0.5;if(dist<250&&e.age>0.5){e.state=1;e.stateT=0.3;}}else if(e.state===1){e.vx*=0.9;e.vy*=0.9;e.stateT-=dt;if(e.stateT<=0){e.state=2;e.stateT=0.35;e.dashX=nx;e.dashY=ny;}}else if(e.state===2){e.vx=e.dashX*550;e.vy=e.dashY*550;e.stateT-=dt;if(e.stateT<=0){e.state=3;e.stateT=0.6;}}else{e.vx=nx*e.speed*0.3;e.vy=ny*e.speed*0.3;e.stateT-=dt;if(e.stateT<=0)e.state=0;}}break;
        case"necrofiend":{e.shootCd-=dt;if(e.shootCd<=0&&this.kills>5){e.shootCd=10;const rx=this.px+(Math.random()-0.5)*200;const ry=this.py+(Math.random()-0.5)*200;this.spE("chaser",rx,ry,0.5);this.burst(rx,ry,"voidPurple",6,80,0.4,0.5);}}e.vx=nx*spd;e.vy=ny*spd;break;
        case"warper":{e.stateT-=dt;if(e.stateT<=0){e.stateT=3;this.shake(0.15);this.ring(e.x,e.y,20,200,0.4);this.burst(e.x,e.y,"warp",8,100,0.3,0.6);for(const o of this.ens){const dd=Math.hypot(o.x-e.x,o.y-e.y);if(dd<200){const dd2=Math.hypot(o.x-e.x,o.y-e.y)||1;o.kx+=(o.x-e.x)/dd2*100;o.ky+=(o.y-e.y)/dd2*100;}}}e.vx=nx*spd*0.7;e.vy=ny*spd*0.7;break;}
        case"toxin":{if(Math.random()<0.3){this.boss?.projs.push({x:e.x+(Math.random()-0.5)*10,y:e.y+(Math.random()-0.5)*10,vx:(Math.random()-0.5)*20,vy:(Math.random()-0.5)*20,dmg:e.dmg*0.2,r:6,life:4});}e.vx=nx*spd;e.vy=ny*spd;}break;
        case"anchor":{if(dist<30&&this.palive&&this.pifr<=0){this.pvx*=0.2;this.pvy*=0.2;this.toast("ANCHORED","danger");}e.vx=nx*spd*0.6;e.vy=ny*spd*0.6;}break;
        case"blink":{e.stateT-=dt;if(e.stateT<=0){e.stateT=1.2;this.burst(e.x,e.y,"blink",6,80,0.3,0.5);this.shake(0.1);for(const o of this.ens){const dd=Math.hypot(o.x-e.x,o.y-e.y);if(dd<120&&dd>1){const dd2=Math.hypot(o.x-e.x,o.y-e.y)||1;o.kx+=(o.x-e.x)/dd2*100;o.ky+=(o.y-e.y)/dd2*100;}}}e.vx=nx*spd*0.85;e.vy=ny*spd*0.85;}break;
        case"colossus":{e.stateT-=dt;if(e.stateT<=0){e.stateT=4;for(let i=0;i<3;i++){const a=Math.random()*Math.PI*2;this.boss?.projs.push({x:e.x,y:e.y,vx:Math.cos(a)*200,vy:Math.sin(a)*200,dmg:e.dmg*0.4,r:6,life:3});}this.shake(0.3);this.burst(e.x,e.y,"colossus",15,200,0.5,0.8);}}e.vx=nx*spd;e.vy=ny*spd;break;
        case"rift":{e.shootCd-=dt;if(e.shootCd<=0){e.shootCd=5;this.spE("chaser",e.x+(Math.random()-0.5)*40,e.y+(Math.random()-0.5)*40,0.8);this.burst(e.x,e.y,"riftPurple",8,100,0.4,0.6);}}e.vx=nx*spd*0.4;e.vy=ny*spd*0.4;break;
        case"hydra":{e.shootCd-=dt;if(e.shootCd<=0){e.shootCd=3;for(let h=0;h<3;h++){const ha=Math.atan2(this.py-e.y,this.px-e.x)+((h-1)*0.3);this.boss?.projs.push({x:e.x,y:e.y,vx:Math.cos(ha)*220,vy:Math.sin(ha)*220,dmg:e.dmg*0.4,r:5,life:3});}this.burst(e.x,e.y,"hydra",6,80,0.3,0.5);}e.vx=nx*spd;e.vy=ny*spd;break;}
        case"mimic":{if(dist<200&&e.age>1){e.speed*=1.8;e.dmg*=2;e.r=14;e.shootCd=999;}e.vx=nx*spd;e.vy=ny*spd;break;}
        case"siren":{e.stateT-=dt;if(e.stateT<=0){e.stateT=6;if(dist<300&&this.palive){this.pvx*=0.3;this.pvy*=0.3;this.toast("CHARMED!","danger");this.burst(this.px,this.py,"siren",5,40,0.3,0.4);}}e.vx=nx*spd;e.vy=ny*spd;break;}
        case"wraithlord":{e.stateT-=dt;if(e.stateT<=0){e.stateT=8;for(const o of this.ens){if(o!==e&&Math.hypot(o.x-e.x,o.y-e.y)<200){o.hp=Math.min(o.maxHp,o.hp+o.maxHp*0.2);}this.burst(e.x,e.y,"wraithlord",10,100,0.5,0.6);}this.toast("WRAITH LORD DRAINS LIFE","danger");}e.vx=nx*spd*0.8;e.vy=ny*spd*0.8;break;}
        default:e.vx=nx*spd;e.vy=ny*spd;
      }
      const kd=Math.exp(-8*dt);e.kx*=kd;e.ky*=kd;e.x+=(e.vx+e.kx)*dt;e.y+=(e.vy+e.ky)*dt;
      if(!e.elite&&!e.boss&&!e.summoned&&dist>1750){const a=Math.random()*TAU;const rr=Math.hypot(this.w,this.h)/2+90;e.x=this.px+Math.cos(a)*rr;e.y=this.py+Math.sin(a)*rr;e.age=0;}
      if(this.palive&&e.age>0.35&&e.atkCd<=0&&this.pifr<=0&&!e.burrowed){if(dist<e.r+14){e.atkCd=0.7;this.dmgP(e.dmg,nx,ny);if(e.type==="exploder")this.killE(e,i);}}
      if((this.wlv.blades||0)>0&&this.palive){const bn=weaponStats("blades",this.wlv.blades,this.wevo.blades).count;const br=weaponStats("blades",this.wlv.blades,this.wevo.blades).range;const bd=weaponStats("blades",this.wlv.blades,this.wevo.blades).damage*this.stat("dmg");
        for(let bi=0;bi<bn;bi++){if(e.bladeCd>0||e.age<0.2)continue;const ba=this.pbl+(bi/bn)*TAU;const bx=this.px+Math.cos(ba)*br;const by=this.py+Math.sin(ba)*br;
          if(Math.hypot(e.x-bx,e.y-by)<e.r+17){e.bladeCd=0.35;this.dmgE(e,bd,bx-e.x,by-e.y,180,false);}}}
    }
    for(let i=0;i<this.ens.length;i++){const a=this.ens[i];const cx=Math.floor(a.x/CS),cy=Math.floor(a.y/CS);
      for(let gx=cx-1;gx<=cx+1;gx++)for(let gy=cy-1;gy<=cy+1;gy++){const cell=this.grid.get(gx*100000+gy);if(!cell)continue;
        for(let c=0;c<cell.length;c++){const j=cell[c];if(j<=i)continue;const b=this.ens[j];let sdx=b.x-a.x,sdy=b.y-a.y;const mn=a.r+b.r-2;const d2=sdx*sdx+sdy*sdy;
          if(d2>=mn*mn||d2<0.0001)continue;const d=Math.sqrt(d2);const push=((mn-d)/d)*0.4;sdx*=push;sdy*=push;const wa=b.r/(a.r+b.r);a.x-=sdx*wa;a.y-=sdy*wa;b.x+=sdx*(1-wa);b.y+=sdy*(1-wa);}}}
  }

  private upBuls(dt:number){
    for(let i=this.buls.length-1;i>=0;i--){const b=this.buls[i];
      if(b.homing&&this.ens.length>0){const t=this.nrEnemy(400);if(t){const a=Math.atan2(t.y-b.y,t.x-b.x);const sp=Math.hypot(b.vx,b.vy);const ca=Math.atan2(b.vy,b.vx);
        let df=a-ca;while(df>Math.PI)df-=TAU;while(df<-Math.PI)df+=TAU;const na=ca+Math.sign(df)*Math.min(Math.abs(df),4*dt);b.vx=Math.cos(na)*sp;b.vy=Math.sin(na)*sp;}}
      b.x+=b.vx*dt;b.y+=b.vy*dt;b.life-=dt;if(b.life<=0){this.buls[i]=this.buls[this.buls.length-1];this.buls.pop();continue;}
      const bcx=Math.floor(b.x/CS),bcy=Math.floor(b.y/CS);let hit=false;
      for(let gx=bcx-1;gx<=bcx+1&&!hit;gx++)for(let gy=bcy-1;gy<=bcy+1&&!hit;gy++){const cell=this.grid.get(gx*100000+gy);if(!cell)continue;
        for(let c=0;c<cell.length&&!hit;c++){const ei=cell[c];if(ei>=this.ens.length)continue;const e=this.ens[ei];if(!e||e.age<0.12||e.burrowed)continue;
          if(ei===b.h0||ei===b.h1||ei===b.h2||ei===b.h3)continue;
          const dx=e.x-b.x,dy=e.y-b.y,rr=e.r+6;if(dx*dx+dy*dy<rr*rr){
            b.h3=b.h2;b.h2=b.h1;b.h1=b.h0;b.h0=ei;const crit=Math.random()<this.stat("crit");const dd=crit?b.dmg*this.stat("critDmg"):b.dmg;
            this.dmgE(e,dd,b.vx,b.vy,150,crit);if(b.chain>0)this.chainLt(e,b.chain-1,b.dmg*0.6);
            if(b.status==="burn"){e.burn=b.dmg*0.15*this.stat("statusDmg");e.burnT=2;}
            if(b.slow)e.slowT=Math.max(e.slowT,1.5);
            if(b.explosive)this.exBul(b);hit=true;break;}}}
      if(hit){b.pierce--;if(b.pierce<0){this.buls[i]=this.buls[this.buls.length-1];this.buls.pop();}}}
  }
  private chainLt(from:E,n:number,d:number){if(n<=0)return;let best:E|null=null,bd=180;for(const e of this.ens){if(e===from||e.burrowed)continue;const d2=Math.hypot(e.x-from.x,e.y-from.y);if(d2<bd){bd=d2;best=e;}}
    if(best){const crit=Math.random()<this.stat("crit");this.dmgE(best,d*(crit?this.stat("critDmg"):1),best.x-from.x,best.y-from.y,80,crit);this.chainLt(best,n-1,d*0.75);}}
  private exBul(b:B){for(const e of this.ens){if(e.burrowed)continue;const d=Math.hypot(e.x-b.x,e.y-b.y);if(d<(b.explodeR||40)+e.r)this.dmgE(e,b.dmg*0.5,b.x-e.x,b.y-e.y,120,false);}this.burst(b.x,b.y,"cyan",8,200,0.4,0.7);}

  private dmgE(e:E,d:number,dx:number,dy:number,knock:number,crit:boolean){
    const mark=e.markT>0?this.markMult:1;d*=mark;
    if(e.elite)d*=this.stat("eliteDmg");
    // Executioner: bonus damage to low HP enemies
    if(e.hp/e.maxHp<0.25)d*=this.stat("executioner");
    // Desperado: bonus damage when player is low HP
    if(this.php<this.pmaxHp*0.4)d*=this.stat("desperado");
    // Overcharge bonus
    const oc=this.stat("overcharge");if(oc>1)d*=oc;
    this.dDmg+=d;
    // Frostbite: slow enemies on hit
    const fb=this.stat("frostbite");if(fb>0)e.slowT=Math.max(e.slowT,1);
    // Chain Lightning: chain to nearby enemies
    const cl=this.stat("chainLightning");if(cl>0)this.chainLt(e,cl,d*0.5);
    const dl=Math.hypot(dx,dy)||1;const res=e.elite?0.08:ENEMIES[e.type]?.r>20?0.25:1;
    if(e.shield>0){const absorb=Math.min(e.shield,d);e.shield-=absorb;d-=absorb;if(absorb>0)this.burst(e.x,e.y,"cyan",4,100,0.3,0.5);}
    if(d>0)e.hp-=d;e.hit=0.09;e.kx+=(dx/dl)*knock*res;e.ky+=(dy/dl)*knock*res;
    this.ft(e.x,e.y-e.r,String(Math.round(d)),crit?"#facc15":"#e2e8f0",crit?17:12.5);
    this.burst(e.x,e.y,ENEMY_DOT[e.type]||"pink",crit?5:3,190,0.35,0.7);this.sfx.hit();if(crit)this.sfx.crit();
    if(e.hp<=0)this.killE(e,this.ens.indexOf(e));}
  private killE(e:E,idx:number){if(idx<0||idx>=this.ens.length)return;this.ens[idx]=this.ens[this.ens.length-1];this.ens.pop();
    this.kills++;this.combo++;this.comboT=3;this.maxCombo=Math.max(this.maxCombo,this.combo);
    // kill heal
    const kh=this.stat("killHeal");if(kh>0&&this.palive){this.php=Math.min(this.pmaxHp,this.php+kh);this.burst(this.px,this.py,"emerald",3,60,0.3,0.5);}
    // vampiric life steal
    const vp=this.stat("vampiric");if(vp>0&&this.palive){this.php=Math.min(this.pmaxHp,this.php+e.maxHp*vp);}
    // death burst
    const db=this.stat("deathBurst");if(db>0){for(const o of this.ens){const dd=Math.hypot(o.x-e.x,o.y-e.y);if(dd<80+e.r&&o!==e){this.dmgE(o,e.maxHp*db,o.x-e.x,o.y-e.y,80,false);}}this.burst(e.x,e.y,"yellow",6,120,0.3,0.6);}
    // thorns
    if(e.elite){const th=this.stat("thorns");if(th>0){for(const o of this.ens){if(o!==e&&Math.hypot(o.x-e.x,o.y-e.y)<200){this.dmgE(o,th*0.5,o.x-e.x,o.y-e.y,50,false);}}this.burst(e.x,e.y,"rose",5,80,0.3,0.5);}}
    if(this.combo>=25&&this.combo%25===0)this.toast(`${this.combo} COMBO`,"combo");
    if(e.elite){this.eKills++;this.shake(0.7);this.fzT=Math.max(this.fzT,0.09);this.ring(e.x,e.y,30,420,0.6);this.burst(e.x,e.y,"red",30,380,0.8,1.1);this.burst(e.x,e.y,"orange",18,300,0.7,0.9);this.sfx.eliteDie();this.toast("ELITE SLAIN +250","gem");}
    else{this.shake(0.07);this.burst(e.x,e.y,ENEMY_DOT[e.type]||"pink",e.type==="tank"?18:9,260,0.55,0.9);if(e.type==="tank")this.ring(e.x,e.y,14,220,0.4);this.sfx.die();}
    this.spGems(e.x,e.y,e.xp);if(e.elite)this.spChest(e.x,e.y,"elite");
    if(this.kills>=this.km){this.toast(`${this.km} KILLS`,"gem");this.km*=2;}}

  private upBoss(dt:number){if(!this.boss)return;const b=this.boss;
    if(b.introT>0){b.introT-=dt;if(b.introT<=0)b.introT=0;return;}
    if(b.dead){b.deathT-=dt;if(b.deathT<=0)this.boss=null;b.projs=b.projs.filter(p=>{p.x+=p.vx*dt;p.y+=p.vy*dt;p.life-=dt;return p.life>0;});return;}
    b.age+=dt;b.hit=Math.max(0,b.hit-dt);b.enrage-=dt;if(b.enrage<=0){b.enrage=30;b.phase=(b.phase+1)%3;}
    const dx=this.px-b.x,dy=this.py-b.y,dist=Math.hypot(dx,dy)||1;b.x+=(dx/dist)*b.speed*dt;b.y+=(dy/dist)*b.speed*dt;
    b.atkT-=dt;if(b.atkT<=0){b.atkT=Math.max(0.5,2-b.phase*0.4);
      if(b.phase===0)for(let i=0;i<8;i++){const a=(i/8)*TAU;b.projs.push({x:b.x,y:b.y,vx:Math.cos(a)*200,vy:Math.sin(a)*200,dmg:b.dmg*0.3,r:5,life:3});}
      else if(b.phase===1){const a=Math.atan2(this.py-b.y,this.px-b.x);for(let i=-2;i<=2;i++){const oa=a+i*0.15;b.projs.push({x:b.x,y:b.y,vx:Math.cos(oa)*300,vy:Math.sin(oa)*300,dmg:b.dmg*0.4,r:6,life:2.5});}}
      else for(let i=0;i<8;i++){const a=Math.random()*TAU;const sp=150+Math.random()*100;b.projs.push({x:b.x,y:b.y,vx:Math.cos(a)*sp,vy:Math.sin(a)*sp,dmg:b.dmg*0.25,r:4,life:2.5});}this.sfx.bossAtk();}
    b.projs=b.projs.filter(p=>{p.x+=p.vx*dt;p.y+=p.vy*dt;p.life-=dt;if(this.palive&&this.pifr<=0&&Math.hypot(p.x-this.px,p.y-this.py)<p.r+14){this.dmgP(p.dmg,Math.sign(p.x-this.px),Math.sign(p.y-this.py));return false;}return p.life>0;});
    if(this.palive&&this.pifr<=0&&dist<b.r+14)this.dmgP(b.dmg,dx/dist,dy/dist);
    if(b.hp<=0){b.dead=true;b.deathT=1.5;this.bKills++;this.shake(0.8);this.fzT=0.15;this.burst(b.x,b.y,b.color,40,400,1.0,1.3);this.ring(b.x,b.y,40,600,1.0);
      this.spGems(b.x,b.y,60);this.spChest(b.x,b.y,"boss");this.toast(`${b.id.toUpperCase()} SLAIN`,"gem");this.sfx.bossDie();}}

  private dmgP(d:number,nx:number,ny:number){
    const armor=this.stat("armor");d=Math.max(1,d-armor);if(Math.random()<this.stat("dodge")){this.ft(this.px,this.py-20,"DODGE","#a78bfa",14);return;}
    if(this.shieldActive&&this.shieldBlock>0){const absorbed=Math.min(this.shieldBlock,d);this.shieldBlock-=absorbed;d-=absorbed;this.burst(this.px,this.py,"cyan",6,150,0.3,0.5);}
    if(d<=0)return;
    // Iron Will - survive fatal hit once per run
    const iw=this.stat("ironWill");if(iw>0&&!this.usedIronWill&&this.php-d<=0){this.usedIronWill=true;this.php=1;this.pifr=2;this.cFl=1;this.shake(0.5);this.toast("IRON WILL","info");this.ring(this.px,this.py,20,400,0.6);this.burst(this.px,this.py,"cyan",20,300,0.6,0.8);this.sfx.shieldUp();this.tDmg+=d;return;}
    // Phoenix Heart - revive once per run at 50% HP
    const ph=this.stat("phoenixHeart");if(ph>0&&!this.usedPhoenixHeart&&this.php-d<=0){this.usedPhoenixHeart=true;this.php=this.pmaxHp*0.5;this.pifr=3;this.cFl=1;this.gFl=0.8;this.shake(0.6);this.toast("PHOENIX RISES","evo");this.ring(this.px,this.py,20,500,0.8);this.burst(this.px,this.py,"yellow",30,300,0.7,0.9);this.sfx.evolution();this.tDmg+=d;return;}
    this.php-=d;this.tDmg+=d;this.pifr=0.65;this.rFl=1;this.shake(0.5);this.fzT=Math.max(this.fzT,0.045);this.sfx.hurt();this.burst(this.px,this.py,"red",10,260,0.5,0.9);
    this.pvx+=nx*260;this.pvy+=ny*260;
    if(this.php<=0&&this.palive){this.php=0;this.palive=false;this.dyingT=1.15;this.tts=0.22;this.shake(1);this.fzT=0.09;this.sfx.gameover();
      this.burst(this.px,this.py,"cyan",42,420,0.9,1.2);this.burst(this.px,this.py,"white",20,300,0.7,0.9);this.ring(this.px,this.py,20,340,0.7);}}

  private upGems(dt:number){const mg2=this.stat("magnet")*this.stat("magnet");
    for(let i=this.gems.length-1;i>=0;i--){const g=this.gems[i];g.age+=dt;const dx=this.px-g.x,dy=this.py-g.y,d2=dx*dx+dy*dy;
      if(this.palive&&(g.pull||d2<mg2)){g.pull=true;g.spd=Math.min(950,g.spd+2900*dt);const d=Math.sqrt(d2)||1;g.vx=(dx/d)*g.spd;g.vy=(dy/d)*g.spd;}else{const kd=Math.exp(-5*dt);g.vx*=kd;g.vy*=kd;}
      g.x+=g.vx*dt;g.y+=g.vy*dt;if(this.palive&&d2<22*22){this.gems[i]=this.gems[this.gems.length-1];this.gems.pop();
        if(g.kind==="credit"){const v=Math.round(g.val*this.stat("goldFind"));this.cEarn+=v;this.sfx.coin();this.ft(this.px,this.py-10,`+${v}$`,"#facc15",13);this.gFl=0.5;}
        else{this.addXp(Math.round(g.val*this.stat("xpGain")));this.sfx.gem(0);this.burst(this.px,this.py,"emerald",2,120,0.25,0.6);}}}}
  private upChests(dt:number){for(let i=this.chst.length-1;i>=0;i--){const c=this.chst[i];c.age+=dt;const d=Math.hypot(this.px-c.x,this.py-c.y);
      if(this.palive&&d<30){this.openChest(c.tier);this.chst.splice(i,1);}}}
  private openChest(tier:string){const r=Math.random();
    if(tier==="boss"){if(r<0.3)this.toast("★ EVOLUTION MATERIAL ★","evo");else if(r<0.6){this.spGems(this.px,this.py,20);this.toast("TREASURE","gold");}else this.toast("★ LEGENDARY FIND ★","evo");}
    else if(tier==="elite"){if(r<0.5)this.spGems(this.px,this.py,10);else this.toast("ELITE BOON","gem");}
    else{if(r<0.3)this.spGems(this.px,this.py,5);else this.toast("CHEST OPENED","gold");}this.sfx.chest();this.burst(this.px,this.py,"yellow",12,200,0.5,0.8);}
  private addXp(v:number){this.pxp+=v;if(this.pxp>=this.pxpN&&this.luT<0&&this.dyingT<0){this.pxp-=this.pxpN;this.plv++;this.pxpN=Math.floor(6+this.plv*4+Math.pow(this.plv,1.7)*2);
    this.luT=0.42;this.tts=0.12;this.cFl=0.9;this.ring(this.px,this.py,18,460,0.65);this.burst(this.px,this.py,"cyan",22,330,0.7,1);this.sfx.levelup();}}
  private upFx(dt:number){
    for(let i=this.pts.length-1;i>=0;i--){const p=this.pts[i];p.life-=dt;if(p.life<=0){this.pts[i]=this.pts[this.pts.length-1];this.pts.pop();this.pPool.push(p);continue;}
      if(p.ring)p.size+=p.grow*dt;else{const kd=Math.exp(-p.drag*dt);p.vx*=kd;p.vy*=kd;p.x+=p.vx*dt;p.y+=p.vy*dt;}}
    for(let i=this.flts.length-1;i>=0;i--){const f=this.flts[i];f.life-=dt;f.y-=46*dt;if(f.life<=0){this.flts[i]=this.flts[this.flts.length-1];this.flts.pop();this.fPool.push(f);}}}

  private endRun(victory:boolean){
    const stats={score:this.score,kills:this.kills,eliteKills:this.eKills,bossKills:this.bKills,time:Math.floor(this.time),level:this.plv,maxCombo:this.maxCombo,damageDealt:Math.round(this.dDmg),damageTaken:Math.round(this.tDmg),creditsEarned:this.cEarn,weapons:{...this.wlv}};
    const prevBest=this.lHS().length>0?this.lHS()[0].score:-1;
    this.save.totalPlaytime+=this.time;this.save.totalKills+=this.kills;this.save.totalEliteKills+=this.eKills;this.save.totalBossKills+=this.bKills;
    this.save.totalCreditsEarned+=this.cEarn;this.save.totalDamageDealt+=stats.damageDealt;this.save.totalDamageTaken+=stats.damageTaken;
    this.save.highestScore=Math.max(this.save.highestScore,stats.score);this.save.longestSurvival=Math.max(this.save.longestSurvival,stats.time);
    this.save.highestCombo=Math.max(this.save.highestCombo,this.maxCombo);
    if(victory){this.save.victories++;this.save.completedRuns++;this.save.unlockedEndless=true;if(this.time>=300)this.save.fiveMinRuns++;if(!this.usedRevive)this.save.noReviveWins++;this.toast("VICTORY!","gem");this.sfx.victory();}
    else this.sfx.gameover();
    const cr=Math.round(stats.score*0.1*(this._daily?this._daily.rewardMult:1));this.save.credits+=cr;
    if(this.save.totalKills>=500&&!this.save.unlockedChars.includes("phantom"))this.save.unlockedChars.push("phantom" as any);
    if(this.save.fiveMinRuns>=1&&!this.save.unlockedChars.includes("bastion"))this.save.unlockedChars.push("bastion" as any);
    if(this.save.completedRuns>=3&&!this.save.unlockedChars.includes("overseer"))this.save.unlockedChars.push("overseer" as any);
    this.saveHS({score:stats.score,kills:stats.kills,time:stats.time,level:stats.level,character:this.formId as any,arena:this.arenaId as any,date:Date.now(),victory});
    saveSave(this.save);this.ev.onGameOver(stats,0,stats.score>prevBest,victory);
    if(victory)this.setPhase("victory");else this.setPhase("gameover");
  }

  private lHS():HighScore[]{try{const r=localStorage.getItem(SK);if(!r)return[];const a=JSON.parse(r);return Array.isArray(a)?a.slice(0,8):[];}catch{return[];}}
  private saveHS(e:HighScore){const s=this.lHS();s.push(e);s.sort((a,b)=>b.score-a.score);const hs=s.slice(0,8);try{localStorage.setItem(SK,JSON.stringify(hs));}catch{}}

  private resize(){this.w=window.innerWidth;this.h=window.innerHeight;const c=this.q===0?1.25:2;this.dpr=Math.min(window.devicePixelRatio||1,c);
    this.cv.width=Math.round(this.w*this.dpr);this.cv.height=Math.round(this.h*this.dpr);this.buildGrads();}
  private buildBg(){const cols=["#312e81","#4c1d95","#155e75","#3b0764"];for(let i=0;i<4;i++){const c=document.createElement("canvas");c.width=c.height=300;const ctx=c.getContext("2d")!;
      const g=ctx.createRadialGradient(150,150,10,150,150,150);g.addColorStop(0,cols[i]+"55");g.addColorStop(0.6,cols[i]+"26");g.addColorStop(1,cols[i]+"00");ctx.fillStyle=g;ctx.fillRect(0,0,300,300);this.nebs.push(c);}
    for(let i=0;i<80;i++)this.dust.push({x:Math.random()*2200,y:Math.random()*2200,s:0.5+Math.random()*1.6,p:0.35+Math.random()*0.5,f:0.4+Math.random()*1.6});}
  private buildGrads(){
    const bg=document.createElement("canvas");bg.width=512;bg.height=288;const c=bg.getContext("2d")!;
    const g=c.createRadialGradient(256,128,20,256,144,300);g.addColorStop(0,this.arenaM.bg[0]);g.addColorStop(0.55,this.arenaM.bg[1]);g.addColorStop(1,this.arenaM.bg[2]);
    c.fillStyle=g;c.fillRect(0,0,512,288);this.bgC=bg;
    const v=document.createElement("canvas");v.width=512;v.height=288;const vc=v.getContext("2d")!;
    const vg=vc.createRadialGradient(256,144,90,256,144,320);vg.addColorStop(0,"rgba(0,0,0,0)");vg.addColorStop(0.75,"rgba(2,3,10,0.32)");vg.addColorStop(1,"rgba(1,2,8,0.66)");
    vc.fillStyle=vg;vc.fillRect(0,0,512,288);this.vig=v;
    const r=document.createElement("canvas");r.width=512;r.height=288;const rc=r.getContext("2d")!;
    const rg=rc.createRadialGradient(256,144,80,256,144,310);rg.addColorStop(0,"rgba(239,68,68,0)");rg.addColorStop(0.8,"rgba(239,68,68,0.22)");rg.addColorStop(1,"rgba(190,18,60,0.5)");
    rc.fillStyle=rg;rc.fillRect(0,0,512,288);this.rVig=r;}

  private render(){
    const ctx=this.cx;const W=this.w,H=this.h;
    ctx.setTransform(this.dpr,0,0,this.dpr,0,0);
    if(this.bgC)ctx.drawImage(this.bgC,0,0,W,H);else{ctx.fillStyle="#05060f";ctx.fillRect(0,0,W,H);}
    const sm=this.trauma*this.trauma*15;const shX=(Math.random()*2-1)*sm;const shY=(Math.random()*2-1)*sm;
    const camX=this.cam.x-W/2+shX;const camY=this.cam.y-H/2+shY;
    if(this.q>0){const nw=W+700,nh=H+700;for(let i=0;i<this.nebs.length;i++){
      const px=((i*977+500-camX*0.22)%nw+nw)%nw-350;const py=((i*613+300-camY*0.22)%nh+nh)%nh-350;const sc=2.4+(i%3);
      ctx.globalAlpha=0.75;ctx.drawImage(this.nebs[i],px-150*sc,py-150*sc,300*sc,300*sc);}ctx.globalAlpha=1;}
    const GS=96;ctx.strokeStyle=this.arenaM.grid;ctx.lineWidth=1;ctx.beginPath();
    const gx0=Math.floor(camX/GS)*GS,gy0=Math.floor(camY/GS)*GS;
    for(let x=gx0;x<camX+W+GS;x+=GS){ctx.moveTo(x-camX,0);ctx.lineTo(x-camX,H);}
    for(let y=gy0;y<camY+H+GS;y+=GS){ctx.moveTo(0,y-camY);ctx.lineTo(W,y-camY);}ctx.stroke();
    if(this.q>0){const dw=W+200,dh=H+200;const spr=this.sp.dot.white;for(let i=0;i<this.dust.length;i++){const d=this.dust[i];
      const px=((d.x-camX*d.p)%dw+dw)%dw-100;const py=((d.y-camY*d.p)%dh+dh)%dh-100;const tw=0.35+0.3*Math.sin(this.aT*d.f+i);
      ctx.globalAlpha=tw*0.5;const s=d.s*8;ctx.drawImage(spr,px-s/2,py-s/2,s,s);}ctx.globalAlpha=1;}
    ctx.save();ctx.translate(-camX,-camY);
    const gspr=[this.sp.gemS,this.sp.gemM,this.sp.gemL];for(let i=0;i<this.gems.length;i++){const g=this.gems[i];const s=gspr[g.tier];const pulse=1+Math.sin(g.age*5)*0.12;const sz=s.width*pulse;ctx.drawImage(s,g.x-sz/2,g.y-sz/2,sz,sz);}
    for(const c of this.chst){const s=24+Math.sin(c.age*4)*2;ctx.fillStyle=c.tier==="boss"?"#facc15":c.tier==="elite"?"#fb923c":"#a78bfa";ctx.globalAlpha=0.8;ctx.fillRect(c.x-s/2,c.y-s/2,s,s);ctx.globalAlpha=1;ctx.strokeStyle="#fff";ctx.lineWidth=1;ctx.strokeRect(c.x-s/2,c.y-s/2,s,s);}
    for(let i=0;i<this.ens.length;i++){const e=this.ens[i];const s=this.sp.enemy[e.type];if(!s)continue;const grow=e.age<0.35?Math.min(1,e.age/0.35):1;const sz=s.width*grow;
      ctx.save();ctx.translate(e.x,e.y);ctx.rotate(e.rot);ctx.globalAlpha=e.burrowed?0.3:e.summoned?0.7:1;ctx.drawImage(s,-sz/2,-sz/2,sz,sz);ctx.globalAlpha=1;ctx.restore();
      if(e.elite){const pl=1+Math.sin(this.aT*4)*0.12;const rs=150*pl;ctx.globalAlpha=0.28+0.1*Math.sin(this.aT*4);ctx.drawImage(this.sp.ring,e.x-rs/2,e.y-rs/2,rs,rs);ctx.globalAlpha=1;}
      if((e.type==="tank"||e.elite)&&e.hp<e.maxHp){const bw=e.r*2;const frac=Math.max(0,e.hp/e.maxHp);ctx.fillStyle="rgba(2,6,18,0.7)";ctx.fillRect(e.x-bw/2,e.y-e.r-12,bw,4);ctx.fillStyle=e.elite?"#ef4444":"#fb923c";ctx.fillRect(e.x-bw/2,e.y-e.r-12,bw*frac,4);}
      if(e.shield>0){ctx.strokeStyle="rgba(96,165,250,0.6)";ctx.lineWidth=2;ctx.beginPath();ctx.arc(e.x,e.y,e.r+4,0,TAU);ctx.stroke();}}
    for(const m of this.mines){const a=m.armed?0.8:0.3+Math.sin(this.aT*8)*0.2;ctx.globalAlpha=a;ctx.drawImage(this.sp.mine,m.x-11,m.y-11);ctx.globalAlpha=1;}
    for(const d of this.drones){ctx.drawImage(this.sp.dot.cyan,d.x-6,d.y-6,12,12);if(d.summoned){ctx.globalAlpha=0.4;ctx.beginPath();ctx.arc(d.x,d.y,10,0,TAU);ctx.fillStyle=this.form.color;ctx.fill();ctx.globalAlpha=1;}}
    if(this.boss){const b=this.boss;if(!b.dead){ctx.globalAlpha=b.introT>0?0.5:1;ctx.beginPath();ctx.arc(b.x,b.y,b.r,0,TAU);ctx.fillStyle=b.color;ctx.globalAlpha*=0.3;ctx.fill();
      ctx.globalAlpha=b.introT>0?0.8:1;ctx.beginPath();ctx.arc(b.x,b.y,b.r*0.8,0,TAU);const bg2=ctx.createRadialGradient(b.x,b.y,0,b.x,b.y,b.r*0.8);bg2.addColorStop(0,"#fff");bg2.addColorStop(0.4,b.color);bg2.addColorStop(1,b.color+"88");
      ctx.fillStyle=bg2;ctx.fill();ctx.strokeStyle="#fff";ctx.lineWidth=3;ctx.stroke();ctx.globalAlpha=1;
      const bw=b.r*3;const frac=Math.max(0,b.hp/b.maxHp);ctx.fillStyle="rgba(2,6,18,0.8)";ctx.fillRect(b.x-bw/2,b.y-b.r-20,bw,8);ctx.fillStyle=b.color;ctx.fillRect(b.x-bw/2,b.y-b.r-20,bw*frac,8);
      ctx.fillStyle="#fff";ctx.font='bold 10px "Chakra Petch"';ctx.textAlign="center";ctx.fillText(b.id.toUpperCase(),b.x,b.y-b.r-25);}
      for(const p of b.projs){ctx.beginPath();ctx.arc(p.x,p.y,p.r,0,TAU);ctx.fillStyle="#ef4444";ctx.fill();}}
    if(this.palive){const f=this.form;const blink=this.pifr>0&&Math.sin(this.aT*34)>0;ctx.globalAlpha=blink?0.35:1;ctx.save();ctx.translate(this.px,this.py);ctx.rotate(this.aT*1.4);
      // form-colored ring glow
      ctx.globalAlpha=0.15;ctx.fillStyle=f.glowColor;ctx.beginPath();ctx.arc(0,0,38,0,TAU);ctx.fill();ctx.globalAlpha=blink?0.35:1;
      const prKey=this.formId||"sentinel";
      const pr=this.sp.playerRings[prKey]||this.sp.playerRing;
      ctx.drawImage(pr,-31,-31,62,62);ctx.restore();
      // form-colored player
      const pl=this.sp.players[prKey]||this.sp.player;const ps=pl.width;ctx.drawImage(pl,this.px-ps/2,this.py-ps/2);
      // form color glow under player
      ctx.globalAlpha=0.2;ctx.fillStyle=f.glowColor;ctx.beginPath();ctx.arc(this.px,this.py,22,0,TAU);ctx.fill();ctx.globalAlpha=1;
      // magic cooldown indicator
      if(this.magicCd>0){const pct=1-this.magicCd/(f.magic.cooldown||20);ctx.strokeStyle=f.glowColor+"88";ctx.lineWidth=2;ctx.beginPath();ctx.arc(this.px,this.py+28,12,-Math.PI/2,-Math.PI/2+TAU*pct);ctx.stroke();}
      ctx.globalAlpha=1;}
    if((this.wlv.blades||0)>0&&this.palive){const bn=weaponStats("blades",this.wlv.blades,this.wevo.blades).count;const br=weaponStats("blades",this.wlv.blades,this.wevo.blades).range;
      for(let i=0;i<bn;i++){const a=this.pbl+(i/bn)*TAU;const bx=this.px+Math.cos(a)*br;const by=this.py+Math.sin(a)*br;ctx.save();ctx.translate(bx,by);ctx.rotate(a+Math.PI/2);ctx.drawImage(this.sp.blade,-22,-11);ctx.restore();}}
    for(let i=0;i<this.buls.length;i++){const b=this.buls[i];ctx.save();ctx.translate(b.x,b.y);ctx.rotate(Math.atan2(b.vy,b.vx));
      if(b.weapon==="arc"){ctx.globalAlpha=0.6;ctx.strokeStyle="#a78bfa";ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(-8,0);ctx.lineTo(8,0);ctx.stroke();ctx.globalAlpha=1;}
      else if(b.weapon==="flame"){ctx.globalAlpha=0.5;ctx.fillStyle="#fb923c";ctx.beginPath();ctx.arc(0,0,4,0,TAU);ctx.fill();ctx.globalAlpha=1;}
      else if(b.weapon==="ice")ctx.drawImage(this.sp.iceBolt,-18,-10);
      else if(b.weapon==="crossbow")ctx.drawImage(this.sp.bolt,-20,-8);
      else if(b.weapon==="plasma")ctx.drawImage(this.sp.plasma,-14,-14);
      else if(b.weapon==="nova")ctx.drawImage(this.sp.nova,-24,-24);
      else if(b.weapon==="tendrils"){ctx.globalAlpha=0.6;ctx.drawImage(this.sp.tendril,-14,-14);ctx.globalAlpha=1;}
      else if(b.weapon==="chain"){ctx.strokeStyle="#fbbf24";ctx.lineWidth=3;ctx.beginPath();ctx.moveTo(-12,0);ctx.lineTo(12,0);ctx.stroke();ctx.fillStyle="#fde047";ctx.beginPath();ctx.arc(0,0,4,0,Math.PI*2);ctx.fill();}
      else if(b.weapon==="voidorb"){ctx.globalAlpha=0.7;ctx.drawImage(this.sp.plasma,-14,-14);ctx.globalAlpha=1;}
      else ctx.drawImage(this.sp.bullet,-17,-9);ctx.restore();}
    ctx.globalCompositeOperation="lighter";for(let i=0;i<this.pts.length;i++){const p=this.pts[i];const t=p.life/p.max;ctx.globalAlpha=t;
      if(p.ring){const sz=p.size*2;ctx.drawImage(p.spr,p.x-p.size,p.y-p.size,sz,sz);}else{const sz=24*p.size*(0.4+t*0.6);ctx.drawImage(p.spr,p.x-sz/2,p.y-sz/2,sz,sz);}}
    ctx.globalAlpha=1;ctx.globalCompositeOperation="source-over";
    if(this.flts.length>0){ctx.textAlign="center";for(let i=0;i<this.flts.length;i++){const f=this.flts[i];const t=f.life/f.max;ctx.globalAlpha=Math.min(1,t*2);
      ctx.font=`700 ${f.size}px "Chakra Petch", monospace`;ctx.fillStyle=f.color;ctx.fillText(f.text,f.x,f.y);}ctx.globalAlpha=1;}
    ctx.restore();
    // Arena hazards
    if(this.arenaId==="reactor"&&this.phase==="playing"){
      const elecT=this.time*2.5;
      for(let i=0;i<3;i++){
        const ex=((Math.sin(elecT+i*2.1)*0.5+0.5)*W);
        const ey=((Math.cos(elecT*0.7+i*3.3)*0.5+0.5)*H);
        ctx.strokeStyle="rgba(100,255,180,0.15)";ctx.lineWidth=2;ctx.beginPath();
        for(let j=0;j<5;j++){const jx=ex+(Math.random()-0.5)*40;const jy=ey+(Math.random()-0.5)*40;j===0?ctx.moveTo(jx,jy):ctx.lineTo(jx,jy);}ctx.stroke();
      }
    }
    if(this.arenaId==="ash"&&this.phase==="playing"){
      ctx.globalAlpha=0.08;ctx.fillStyle="#8b7355";
      for(let i=0;i<30;i++){const px=((i*137+this.aT*20*(0.5+Math.sin(i)*0.3))%W);const py=((i*89+this.aT*8)%H);ctx.beginPath();ctx.arc(px,py,1+Math.sin(i)*0.5,0,Math.PI*2);ctx.fill();}
      ctx.globalAlpha=1;
    }
    // Magic visual effects
    if(this.magicOn&&this.formId){
      const f=this.form;
      if(f.id==="sentinel"&&this.shieldActive){ctx.globalAlpha=0.3+0.1*Math.sin(this.aT*8);ctx.strokeStyle=f.glowColor;ctx.lineWidth=3;ctx.beginPath();ctx.arc(this.px,this.py,30+4*Math.sin(this.aT*6),0,TAU);ctx.stroke();ctx.globalAlpha=1;}
      if(f.id==="chronomancer"&&this.slowMult<1){ctx.globalAlpha=0.06;ctx.fillStyle="#facc15";ctx.fillRect(0,0,W,H);ctx.globalAlpha=1;}
      if(f.id==="wraith"&&this.markMult>1){for(const e of this.ens){if(e.markT>0){ctx.globalAlpha=0.3+0.1*Math.sin(this.aT*6);ctx.strokeStyle="#f43f5e";ctx.lineWidth=1;ctx.beginPath();ctx.arc(e.x,e.y,e.r+6,0,TAU);ctx.stroke();ctx.globalAlpha=1;}}}
      if(f.id==="elemental"&&this.time>0){/* frost handled by stunT on enemies */}
    }
    if(this.vig)ctx.drawImage(this.vig,0,0,W,H);
    const lowHp=this.phase==="playing"&&this.palive&&this.php<this.pmaxHp*0.3;const rA=this.rFl*0.85+(lowHp?0.35+0.2*Math.sin(this.aT*5):0);
    if(rA>0.01&&this.rVig){ctx.globalAlpha=Math.min(1,rA);ctx.drawImage(this.rVig,0,0,W,H);ctx.globalAlpha=1;}
    if(this.cFl>0.01){ctx.fillStyle=`rgba(103,232,249,${(this.cFl*0.16).toFixed(3)})`;ctx.fillRect(0,0,W,H);}
    if(this.gFl>0.01){ctx.fillStyle=`rgba(250,204,21,${(this.gFl*0.1).toFixed(3)})`;ctx.fillRect(0,0,W,H);}
    if(this.inp.joyActive&&this.phase==="playing"){const bx=this.inp.joyBaseX,by=this.inp.joyBaseY;ctx.beginPath();ctx.arc(bx,by,52,0,TAU);ctx.strokeStyle="rgba(103,232,249,0.35)";ctx.lineWidth=2;ctx.stroke();
      ctx.beginPath();ctx.arc(bx,by,52,0,TAU);ctx.fillStyle="rgba(34,211,238,0.08)";ctx.fill();const kx=bx+this.inp.joyX*40,ky=by+this.inp.joyY*40;
      ctx.beginPath();ctx.arc(kx,ky,22,0,TAU);ctx.fillStyle="rgba(103,232,249,0.45)";ctx.fill();ctx.beginPath();ctx.arc(kx,ky,22,0,TAU);ctx.strokeStyle="rgba(165,243,252,0.8)";ctx.stroke();}
    if(this.dbg){const d=this.dbgD;ctx.fillStyle="rgba(0,0,0,0.7)";ctx.fillRect(8,8,220,175);ctx.fillStyle="#0f0";ctx.font="11px monospace";ctx.textAlign="left";
      ctx.fillText(`FPS: ${d.fps}`,16,24);ctx.fillText(`Frame: ${d.frameTime.toFixed(1)}ms`,16,38);ctx.fillText(`Enemies: ${d.enemyCount}`,16,52);
      ctx.fillText(`Bullets: ${d.projCount}`,16,66);ctx.fillText(`Particles: ${d.particleCount}`,16,80);ctx.fillText(`Gems: ${d.gemCount}`,16,94);
      ctx.fillText(`Diff: ${d.difficulty.toFixed(2)}`,16,108);ctx.fillText(`Wave: ${d.wave}`,16,122);ctx.fillText(`Seed: ${d.seed}`,16,136);
      ctx.fillText(`Quality: ${this.q}`,16,150);ctx.fillText(`Mode: ${this.mode}`,16,164);ctx.fillText(`Arena: ${this.arenaM.name}`,16,178);}
  }

  private upHud(dr:number){const h=this.hud;if(!h)return;const L=this.lH;const hpF=Math.max(0,this.php/this.pmaxHp);
    if(this.php!==L.hp||this.pmaxHp!==L.mhp){L.hp=this.php;L.mhp=this.pmaxHp;if(h.hpBar)h.hpBar.style.transform=`scaleX(${hpF.toFixed(3)})`;if(h.hpText)h.hpText.textContent=`${Math.ceil(Math.max(0,this.php))}`;}
    this.hpG+=(hpF-this.hpG)*(1-Math.exp(-3.2*dr));if(this.hpG<hpF)this.hpG=hpF;if(h.hpGhost)h.hpGhost.style.transform=`scaleX(${this.hpG.toFixed(3)})`;
    const xF=Math.min(1,this.pxp/this.pxpN);if(xF!==L.xp){L.xp=xF;if(h.xpBar)h.xpBar.style.transform=`scaleX(${xF.toFixed(3)})`;}
    if(this.plv!==L.lv){L.lv=this.plv;if(h.levelText)h.levelText.textContent=`LV ${this.plv}`;}
    const tsec=Math.floor(this.time);if(tsec!==L.t){L.t=tsec;if(h.timeText){const m=Math.floor(tsec/60);const s=tsec%60;h.timeText.textContent=`${m}:${s.toString().padStart(2,"0")}`;}}
    const sc=this.score;if(sc!==L.sc){L.sc=sc;if(h.scoreText)h.scoreText.textContent=sc.toLocaleString();}
    if(this.kills!==L.k){L.k=this.kills;if(h.killText)h.killText.textContent=String(this.kills);}
    if(this.combo!==L.cb){L.cb=this.combo;if(h.comboText)h.comboText.textContent=this.combo>5?`${this.combo}× COMBO`:"";}
    if(this.cEarn!==L.cr){L.cr=this.cEarn;if(h.creditsText)h.creditsText.textContent=`$${this.cEarn}`;}
    const shF=this.pshMax>0?this.psh/this.pshMax:0;if(shF!==L.sh){L.sh=shF;if(h.shieldBar)h.shieldBar.style.transform=`scaleX(${shF.toFixed(3)})`;if(h.shieldText)h.shieldText.textContent=this.psh>0?Math.ceil(this.psh).toString():"";}
    if(h.hurtFlash)h.hurtFlash.style.opacity=String(this.rFl*0.5);}

  private loop(now:number){this.raf=requestAnimationFrame(t=>this.loop(t));let dr=(now-this.lt)/1000;this.lt=now;if(dr>0.05)dr=0.05;if(dr<=0)dr=0.0001;this.aT+=dr;
    this.fEma=this.fEma*0.96+dr*1000*0.04;if(++this.fCnt%90===0){if(this.fEma>19.5&&this.q>0){this.q--;if(this.q===0)this.resize();}else if(this.fEma<13.5&&this.q<2)this.q++;}
    this.dbgD={fps:Math.round(1000/this.fEma),frameTime:this.fEma,enemyCount:this.ens.length,projCount:this.buls.length,particleCount:this.pts.length,gemCount:this.gems.length,difficulty:this.hpM(),wave:this.wave,seed:0};
    if(this.dbg)this.ev.onPerf(this.dbgD);
    let dt=dr*this.ts;if(this.fzT>0){this.fzT-=dr;dt=0;}
    if(this.phase==="playing")this.update(dt,dr);else if(this.phase==="menu"){this.cam.x=Math.cos(this.aT*0.07)*260;this.cam.y=Math.sin(this.aT*0.052)*260;this.upFx(dr);}
    else this.upFx(this.phase==="gameover"?dr*0.4:dr*0.12);this.render();this.upHud(dr);}
}

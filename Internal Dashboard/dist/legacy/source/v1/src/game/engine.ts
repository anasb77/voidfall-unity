import { makeSprites, type SpriteSet } from "./sprites";
import { Sfx } from "./audio";
import { Input } from "./input";
import { UPGRADES } from "./upgrades";
import type {
  EngineEvents,
  GamePhase,
  HighScore,
  HudRefs,
  RunStats,
} from "./types";

type EnemyType = "chaser" | "imp" | "dasher" | "brute" | "elite";

interface Enemy {
  x: number; y: number; vx: number; vy: number; kx: number; ky: number;
  hp: number; maxHp: number; r: number; type: EnemyType; speed: number;
  dmg: number; xp: number; age: number; hit: number; bladeCd: number;
  atkCd: number; rot: number; spin: number; state: number; stateT: number;
  dashX: number; dashY: number; seed: number; elite: boolean;
}

interface Bullet {
  x: number; y: number; vx: number; vy: number;
  dmg: number; pierce: number; life: number;
  h0: Enemy | null; h1: Enemy | null; h2: Enemy | null; h3: Enemy | null;
}

interface Gem {
  x: number; y: number; vx: number; vy: number;
  val: number; tier: number; age: number; pull: boolean; spd: number;
}

interface Particle {
  x: number; y: number; vx: number; vy: number;
  life: number; max: number; size: number; drag: number;
  spr: HTMLCanvasElement; ring: boolean; grow: number;
}

interface Floater {
  x: number; y: number; life: number; max: number;
  text: string; color: string; size: number;
}

const ENEMY_DEFS: Record<EnemyType, { hp: number; speed: number; dmg: number; r: number; xp: number }> = {
  chaser: { hp: 20, speed: 76, dmg: 8, r: 15, xp: 1 },
  imp: { hp: 9, speed: 135, dmg: 5, r: 10, xp: 1 },
  dasher: { hp: 16, speed: 98, dmg: 10, r: 12, xp: 2 },
  brute: { hp: 95, speed: 47, dmg: 16, r: 24, xp: 4 },
  elite: { hp: 850, speed: 54, dmg: 22, r: 38, xp: 40 },
};

const ENEMY_DOT: Record<EnemyType, string> = {
  chaser: "pink", imp: "violet", dasher: "fuchsia", brute: "orange", elite: "red",
};

const SCORE_KEY = "voidfall_scores_v1";
const TAU = Math.PI * 2;

function backOut(t: number): number {
  const c = 1.70158;
  const u = t - 1;
  return 1 + (c + 1) * u * u * u + c * u * u;
}

export class Game {
  private canvas: HTMLCanvasElement;
  private ctx: CanvasRenderingContext2D;
  private w = 0;
  private h = 0;
  private dpr = 1;
  private sprites: SpriteSet;
  private input: Input;
  readonly sfx = new Sfx();
  private events: EngineEvents;
  private hud: HudRefs | null = null;

  phase: GamePhase = "menu";
  private raf = 0;
  private lastT = 0;
  private ambientT = 0;

  // world state
  private player = this.freshPlayer();
  private enemies: Enemy[] = [];
  private bullets: Bullet[] = [];
  private gems: Gem[] = [];
  private parts: Particle[] = [];
  private floaters: Floater[] = [];
  private partPool: Particle[] = [];
  private floaterPool: Floater[] = [];
  private cam = { x: 0, y: 0 };
  private time = 0;
  private kills = 0;
  private eliteKills = 0;
  private spawnT = 1.2;
  private eliteT = 55;
  private swarmT = 30;
  private toastT: { t: number; text: string }[] = [
    { t: 60, text: "THE VOID DEEPENS" },
    { t: 120, text: "THEY GROW HUNGRY" },
    { t: 200, text: "NO ESCAPE. ONLY SURVIVAL." },
  ];
  private toastIdx = 0;
  private killMilestone = 50;

  // feel systems
  private trauma = 0;
  private timeScale = 1;
  private targetTimeScale = 1;
  private freezeT = 0;
  private levelUpT = -1;
  private dyingT = -1;
  private redFlash = 0;
  private cyanFlash = 0;
  private hpGhost = 1;

  private upCounts: Record<string, number> = {};
  private stats = this.baseStats();
  private currentOptions: string[] = [];
  private scores: HighScore[] = [];

  // fx
  private grid = new Map<number, number[]>();
  private gridPool: number[][] = [];
  private bgGrad: HTMLCanvasElement | null = null;
  private vignette: HTMLCanvasElement | null = null;
  private redVignette: HTMLCanvasElement | null = null;
  private nebulae: HTMLCanvasElement[] = [];
  private dust: { x: number; y: number; s: number; p: number; f: number }[] = [];
  private quality = 2;
  private frameEma = 16;
  private frameCount = 0;

  private lastHud = { hp: -1, maxHp: -1, xp: -1, lvl: -1, time: -1, score: -1, kills: -1 };

  private onResize = () => this.resize();
  private onVis = () => {
    if (document.hidden && this.phase === "playing") this.setPhase("paused");
  };

  constructor(canvas: HTMLCanvasElement, events: EngineEvents) {
    this.canvas = canvas;
    this.ctx = canvas.getContext("2d", { alpha: false })!;
    this.events = events;
    this.sprites = makeSprites();
    this.input = new Input(canvas, (code, e) => this.handleKey(code, e));
    this.scores = this.loadScores();
    this.buildBackdrop();
    this.resize();
    window.addEventListener("resize", this.onResize);
    document.addEventListener("visibilitychange", this.onVis);
    this.events.onScores(this.scores);
    this.events.onMute(this.sfx.muted);
    this.lastT = performance.now();
    this.raf = requestAnimationFrame((t) => this.loop(t));
  }

  setHud(hud: HudRefs) {
    this.hud = hud;
  }

  destroy() {
    cancelAnimationFrame(this.raf);
    this.input.destroy();
    window.removeEventListener("resize", this.onResize);
    document.removeEventListener("visibilitychange", this.onVis);
  }

  // ---------------------------------------------------------------- state

  private freshPlayer() {
    return {
      x: 0, y: 0, vx: 0, vy: 0,
      hp: 100, level: 1, xp: 0, xpNeed: this.xpNeed(1),
      iframes: 0, fireT: 0.25, bladeAng: 0, trailT: 0,
      gemStep: 0, gemStepT: 0, alive: true,
    };
  }

  private baseStats() {
    return {
      damage: 12, fireInterval: 0.42, projectiles: 1, pierce: 0,
      speed: 235, magnet: 95, maxHp: 100, crit: 0.05, blades: 0, bladeDmg: 16,
    };
  }

  private xpNeed(level: number) {
    return Math.floor(6 + level * 4 + Math.pow(level, 1.7) * 2);
  }

  get score(): number {
    return this.kills * 10 + Math.floor(this.time) * 5 + this.eliteKills * 250 + (this.player.level - 1) * 40;
  }

  private setPhase(p: GamePhase) {
    this.phase = p;
    this.events.onPhaseChange(p);
  }

  // ---------------------------------------------------------------- public actions

  start() {
    this.sfx.init();
    this.sfx.ui();
    this.reset();
    this.setPhase("playing");
    this.events.onToast("SURVIVE THE SWARM", "info");
  }

  restart() {
    this.sfx.init();
    this.sfx.ui();
    this.reset();
    this.setPhase("playing");
    this.events.onToast("SURVIVE THE SWARM", "info");
  }

  toMenu() {
    this.sfx.ui();
    this.enemies.length = 0;
    this.bullets.length = 0;
    this.gems.length = 0;
    this.setPhase("menu");
  }

  togglePause() {
    if (this.phase === "playing") {
      this.sfx.pause();
      this.setPhase("paused");
    } else if (this.phase === "paused") {
      this.sfx.ui();
      this.targetTimeScale = 1;
      this.setPhase("playing");
    }
  }

  toggleMute() {
    this.sfx.init();
    this.sfx.setMuted(!this.sfx.muted);
    this.events.onMute(this.sfx.muted);
  }

  applyUpgrade(id: string) {
    if (this.phase !== "levelup") return;
    this.upCounts[id] = (this.upCounts[id] || 0) + 1;
    this.recalc();
    if (id === "vitality") this.player.hp = Math.min(this.stats.maxHp, this.player.hp + 25);
    if (id === "patch") this.player.hp = Math.min(this.stats.maxHp, this.player.hp + 40);
    this.sfx.pick();
    this.events.onUpgrades(Object.entries(this.upCounts).flatMap(([k, v]) => Array(v).fill(k) as string[]));
    this.targetTimeScale = 1;
    this.setPhase("playing");
    // chain another level-up if XP still overflows
    if (this.player.xp >= this.player.xpNeed) this.addXp(0);
  }

  pickOption(i: number) {
    if (this.phase === "levelup" && this.currentOptions[i]) {
      this.applyUpgrade(this.currentOptions[i]);
    }
  }

  // ---------------------------------------------------------------- reset / persistence

  private reset() {
    this.player = this.freshPlayer();
    this.enemies.length = 0;
    this.bullets.length = 0;
    this.gems.length = 0;
    this.parts.length = 0;
    this.floaters.length = 0;
    this.cam.x = 0;
    this.cam.y = 0;
    this.time = 0;
    this.kills = 0;
    this.eliteKills = 0;
    this.spawnT = 0.9;
    this.eliteT = 55;
    this.swarmT = 30;
    this.trauma = 0;
    this.timeScale = 1;
    this.targetTimeScale = 1;
    this.freezeT = 0;
    this.levelUpT = -1;
    this.dyingT = -1;
    this.redFlash = 0;
    this.cyanFlash = 0;
    this.hpGhost = 1;
    this.upCounts = {};
    this.recalc();
    this.toastIdx = 0;
    this.killMilestone = 50;
    this.events.onUpgrades([]);
    this.lastHud = { hp: -1, maxHp: -1, xp: -1, lvl: -1, time: -1, score: -1, kills: -1 };
    // opening wave so the first ten seconds already pop
    for (let i = 0; i < 4; i++) this.spawnEnemy("chaser");
  }

  private loadScores(): HighScore[] {
    try {
      const raw = localStorage.getItem(SCORE_KEY);
      if (!raw) return [];
      const arr = JSON.parse(raw) as HighScore[];
      return Array.isArray(arr) ? arr.slice(0, 8) : [];
    } catch {
      return [];
    }
  }

  private saveScore(stats: RunStats): { rank: number; isBest: boolean } {
    const entry: HighScore = { ...stats, date: Date.now() };
    const prevBest = this.scores.length > 0 ? this.scores[0].score : -1;
    this.scores.push(entry);
    this.scores.sort((a, b) => b.score - a.score);
    this.scores = this.scores.slice(0, 8);
    try {
      localStorage.setItem(SCORE_KEY, JSON.stringify(this.scores));
    } catch { /* storage unavailable */ }
    const rank = this.scores.indexOf(entry);
    this.events.onScores(this.scores);
    return { rank, isBest: rank === 0 && stats.score > prevBest };
  }

  // ---------------------------------------------------------------- input

  private handleKey(code: string, e: KeyboardEvent) {
    if (["Space", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(code)) {
      e.preventDefault();
    }
    switch (code) {
      case "Escape":
      case "KeyP":
        if (this.phase === "playing" || this.phase === "paused") this.togglePause();
        break;
      case "KeyM":
        this.toggleMute();
        break;
      case "Enter":
      case "Space":
        if (this.phase === "menu") this.start();
        else if (this.phase === "gameover") this.restart();
        else if (this.phase === "paused") this.togglePause();
        break;
      case "KeyR":
        if (this.phase === "gameover" || this.phase === "paused") this.restart();
        break;
      case "Digit1":
        this.pickOption(0);
        break;
      case "Digit2":
        this.pickOption(1);
        break;
      case "Digit3":
        this.pickOption(2);
        break;
    }
  }

  // ---------------------------------------------------------------- upgrades

  private recalc() {
    const u = this.upCounts;
    this.stats = {
      damage: 12 * Math.pow(1.3, u.heavy || 0),
      fireInterval: Math.max(0.09, 0.42 * Math.pow(0.85, u.rapid || 0)),
      projectiles: 1 + (u.split || 0),
      pierce: u.pierce || 0,
      speed: 235 * Math.pow(1.1, u.swift || 0),
      magnet: 95 * Math.pow(1.45, u.magnet || 0),
      maxHp: 100 + 25 * (u.vitality || 0),
      crit: 0.05 + 0.1 * (u.crit || 0),
      blades: u.blades || 0,
      bladeDmg: 16 + 8 * (u.blades || 0),
    };
    this.player.hp = Math.min(this.player.hp, this.stats.maxHp);
  }

  private rollUpgrades(): string[] {
    const avail = UPGRADES.filter(
      (u) => u.id !== "patch" && (this.upCounts[u.id] || 0) < u.max
    );
    const picked: string[] = [];
    const pool = [...avail];
    while (picked.length < 3 && pool.length > 0) {
      const totalW = pool.reduce((s, u) => s + u.weight, 0);
      let r = Math.random() * totalW;
      let idx = 0;
      for (let i = 0; i < pool.length; i++) {
        r -= pool[i].weight;
        if (r <= 0) { idx = i; break; }
      }
      picked.push(pool[idx].id);
      pool.splice(idx, 1);
    }
    // low-health mercy card
    if (this.player.hp < this.stats.maxHp * 0.45) {
      if (picked.length >= 3) picked[2] = "patch";
      else picked.push("patch");
    }
    while (picked.length < 3) picked.push("patch");
    return picked;
  }

  // ---------------------------------------------------------------- spawning

  private hpMul() { return 1 + this.time / 85 + (this.time * this.time) / 50000; }
  private spdMul() { return Math.min(1.35, 1 + this.time / 420); }
  private dmgMul() { return 1 + this.time / 320; }

  private spawnEnemy(type: EnemyType, x?: number, y?: number, hpMul = 1) {
    const ang = Math.random() * TAU;
    const rad = Math.hypot(this.w, this.h) / 2 + 70 + Math.random() * 130;
    const d = ENEMY_DEFS[type];
    const mul = this.hpMul() * hpMul;
    const hp = d.hp * mul;
    this.enemies.push({
      x: x ?? this.player.x + Math.cos(ang) * rad,
      y: y ?? this.player.y + Math.sin(ang) * rad,
      vx: 0, vy: 0, kx: 0, ky: 0,
      hp, maxHp: hp, r: d.r, type,
      speed: d.speed * (0.9 + Math.random() * 0.2) * this.spdMul(),
      dmg: d.dmg * this.dmgMul(), xp: d.xp,
      age: 0, hit: 0, bladeCd: 0, atkCd: 0,
      rot: Math.random() * TAU, spin: (Math.random() - 0.5) * 2.4,
      state: 0, stateT: 0, dashX: 0, dashY: 0,
      seed: Math.random() * 100, elite: type === "elite",
    });
  }

  private spawnDirector(dt: number) {
    this.spawnT -= dt;
    const cap = Math.min(230, 26 + this.time * 0.62);
    if (this.spawnT <= 0 && this.enemies.length < cap) {
      this.spawnT += Math.max(0.15, 0.85 - this.time * 0.0023);
      const n = 1 + Math.floor(this.time / 50);
      for (let i = 0; i < n; i++) this.spawnEnemy(this.pickType());
    }

    if (this.time >= this.swarmT) {
      this.swarmT += 34;
      const count = Math.min(26, 10 + Math.floor(this.time / 18));
      const type: EnemyType = this.time > 70 && Math.random() < 0.5 ? "dasher" : "imp";
      const base = Math.random() * TAU;
      for (let i = 0; i < count; i++) {
        const a = base + (i / count) * TAU;
        const r = Math.hypot(this.w, this.h) / 2 + 50;
        this.spawnEnemy(type, this.player.x + Math.cos(a) * r, this.player.y + Math.sin(a) * r);
      }
      this.events.onToast("SWARM INBOUND", "danger");
      this.sfx.warn();
    }

    if (this.time >= this.eliteT) {
      this.eliteT += 58;
      this.spawnEnemy("elite", undefined, undefined, 1 + this.time / 130);
      this.events.onToast("AN ELITE HAS RISEN", "danger");
      this.sfx.warn();
    }

    if (this.toastIdx < this.toastT.length && this.time >= this.toastT[this.toastIdx].t) {
      this.events.onToast(this.toastT[this.toastIdx].text, "info");
      this.toastIdx++;
    }
  }

  private pickType(): EnemyType {
    const t = this.time;
    const r = Math.random();
    if (t < 15) return "chaser";
    if (t < 35) return r < 0.6 ? "chaser" : "imp";
    if (t < 60) return r < 0.45 ? "chaser" : r < 0.8 ? "imp" : "dasher";
    if (t < 100) return r < 0.4 ? "chaser" : r < 0.7 ? "imp" : r < 0.85 ? "dasher" : "brute";
    return r < 0.34 ? "chaser" : r < 0.62 ? "imp" : r < 0.8 ? "dasher" : "brute";
  }

  private spawnGems(x: number, y: number, total: number) {
    let guard = 24;
    while (total > 0 && guard-- > 0) {
      const r = Math.random();
      let val = 1;
      if (total >= 20 && r < 0.3) val = 20;
      else if (total >= 5 && r < 0.55) val = 5;
      total -= val;
      if (this.gems.length > 420) {
        const g = this.gems[(Math.random() * this.gems.length) | 0];
        g.val += val;
        if (g.val >= 20) g.tier = 2;
        else if (g.val >= 5) g.tier = 1;
        continue;
      }
      const a = Math.random() * TAU;
      const sp = 40 + Math.random() * 90;
      this.gems.push({
        x, y,
        vx: Math.cos(a) * sp, vy: Math.sin(a) * sp,
        val, tier: val >= 20 ? 2 : val >= 5 ? 1 : 0,
        age: Math.random() * 10, pull: false, spd: 0,
      });
    }
  }

  // ---------------------------------------------------------------- fx helpers

  private shake(amt: number) {
    this.trauma = Math.min(1, this.trauma + amt);
  }

  private burst(x: number, y: number, dot: string, n: number, speed: number, life: number, size: number) {
    if (this.quality === 0) n = Math.ceil(n * 0.4);
    else if (this.quality === 1) n = Math.ceil(n * 0.65);
    const spr = this.sprites.dot[dot];
    for (let i = 0; i < n; i++) {
      if (this.parts.length >= 380) return;
      const p = this.partPool.pop() ?? ({} as Particle);
      const a = Math.random() * TAU;
      const sp = speed * (0.3 + Math.random() * 0.9);
      p.x = x; p.y = y;
      p.vx = Math.cos(a) * sp; p.vy = Math.sin(a) * sp;
      p.life = p.max = life * (0.6 + Math.random() * 0.7);
      p.size = size * (0.6 + Math.random() * 0.8);
      p.drag = 3.2; p.spr = spr; p.ring = false; p.grow = 0;
      this.parts.push(p);
    }
  }

  private ringWave(x: number, y: number, startSize: number, grow: number, life: number) {
    const p = this.partPool.pop() ?? ({} as Particle);
    p.x = x; p.y = y; p.vx = 0; p.vy = 0;
    p.life = p.max = life; p.size = startSize; p.drag = 0;
    p.spr = this.sprites.ring; p.ring = true; p.grow = grow;
    this.parts.push(p);
  }

  private floatText(x: number, y: number, text: string, color: string, size: number) {
    if (this.floaters.length >= 48) return;
    const f = this.floaterPool.pop() ?? ({} as Floater);
    f.x = x + (Math.random() - 0.5) * 14;
    f.y = y - 8;
    f.life = f.max = 0.7;
    f.text = text; f.color = color; f.size = size;
    this.floaters.push(f);
  }

  // ---------------------------------------------------------------- update

  private update(dt: number, dtRaw: number) {
    const p = this.player;
    this.time += dt;

    // ----- player movement
    const ax = { x: 0, y: 0 };
    if (p.alive) this.input.axis(ax);
    const k = 1 - Math.exp(-14 * dt);
    p.vx += (ax.x * this.stats.speed - p.vx) * k;
    p.vy += (ax.y * this.stats.speed - p.vy) * k;
    p.x += p.vx * dt;
    p.y += p.vy * dt;
    if (p.iframes > 0) p.iframes -= dt;

    // engine trail while moving
    p.trailT -= dt;
    const moving = Math.abs(p.vx) + Math.abs(p.vy) > 40;
    if (moving && p.trailT <= 0 && this.quality > 0) {
      p.trailT = 0.055;
      const pt = this.partPool.pop() ?? ({} as Particle);
      pt.x = p.x; pt.y = p.y;
      pt.vx = -p.vx * 0.08 + (Math.random() - 0.5) * 20;
      pt.vy = -p.vy * 0.08 + (Math.random() - 0.5) * 20;
      pt.life = pt.max = 0.32; pt.size = 0.5; pt.drag = 1;
      pt.spr = this.sprites.dot.cyan; pt.ring = false; pt.grow = 0;
      if (this.parts.length < 380) this.parts.push(pt);
      else this.partPool.push(pt);
    }

    // camera
    const ck = 1 - Math.exp(-6 * dt);
    this.cam.x += (p.x - this.cam.x) * ck;
    this.cam.y += (p.y - this.cam.y) * ck;

    // ----- auto fire
    if (p.alive) {
      p.fireT -= dt;
      if (p.fireT <= 0) {
        const target = this.nearestEnemy(560);
        if (target) {
          p.fireT += this.stats.fireInterval;
          this.fire(target);
        } else {
          p.fireT = 0.05;
        }
      }
    }

    this.spawnDirector(dt);
    this.updateEnemies(dt);
    this.updateBlades(dt);
    this.updateBullets(dt);
    this.updateGems(dt);
    this.updateFx(dt);

    // ----- level up sequencing (real time so slow-mo feels right)
    if (this.levelUpT > 0) {
      this.levelUpT -= dtRaw;
      if (this.levelUpT <= 0) {
        this.levelUpT = -1;
        this.currentOptions = this.rollUpgrades();
        this.setPhase("levelup");
        this.events.onLevelUp(this.currentOptions);
      }
    }

    // ----- death sequencing
    if (this.dyingT > 0) {
      this.dyingT -= dtRaw;
      if (this.dyingT <= 0) {
        this.dyingT = -1;
        const stats: RunStats = {
          score: this.score, kills: this.kills,
          time: Math.floor(this.time), level: p.level,
        };
        const { rank, isBest } = this.saveScore(stats);
        this.setPhase("gameover");
        this.events.onGameOver(stats, rank, isBest);
      }
    }

    // decay feel timers
    this.trauma = Math.max(0, this.trauma - dtRaw * 1.7);
    this.redFlash = Math.max(0, this.redFlash - dtRaw * 2.4);
    this.cyanFlash = Math.max(0, this.cyanFlash - dtRaw * 2.2);
    const tsK = 1 - Math.exp(-9 * dtRaw);
    this.timeScale += (this.targetTimeScale - this.timeScale) * tsK;
  }

  private nearestEnemy(maxDist: number): Enemy | null {
    let best: Enemy | null = null;
    let bestD = maxDist * maxDist;
    const p = this.player;
    for (let i = 0; i < this.enemies.length; i++) {
      const e = this.enemies[i];
      if (e.age < 0.15) continue;
      const dx = e.x - p.x;
      const dy = e.y - p.y;
      const d = dx * dx + dy * dy;
      if (d < bestD) { bestD = d; best = e; }
    }
    return best;
  }

  private fire(target: Enemy) {
    const p = this.player;
    const baseAng = Math.atan2(target.y - p.y, target.x - p.x);
    const n = this.stats.projectiles;
    const spread = 0.15;
    for (let i = 0; i < n; i++) {
      const ang = baseAng + (i - (n - 1) / 2) * spread + (Math.random() - 0.5) * 0.04;
      const cos = Math.cos(ang);
      const sin = Math.sin(ang);
      this.bullets.push({
        x: p.x + cos * 16, y: p.y + sin * 16,
        vx: cos * 640, vy: sin * 640,
        dmg: this.stats.damage, pierce: this.stats.pierce, life: 0.9,
        h0: null, h1: null, h2: null, h3: null,
      });
    }
    this.burst(p.x + Math.cos(baseAng) * 20, p.y + Math.sin(baseAng) * 20, "cyan", 2, 130, 0.16, 0.55);
    this.sfx.shoot();
  }

  private updateEnemies(dt: number) {
    const p = this.player;
    const arr = this.enemies;

    // rebuild spatial grid for separation
    for (const cellArr of this.grid.values()) {
      cellArr.length = 0;
      this.gridPool.push(cellArr);
    }
    this.grid.clear();
    const CS = 56;
    for (let i = 0; i < arr.length; i++) {
      const e = arr[i];
      const key = Math.floor(e.x / CS) * 100000 + Math.floor(e.y / CS);
      let cellArr = this.grid.get(key);
      if (!cellArr) {
        cellArr = this.gridPool.pop() ?? [];
        this.grid.set(key, cellArr);
      }
      cellArr.push(i);
    }

    for (let i = arr.length - 1; i >= 0; i--) {
      const e = arr[i];
      e.age += dt;
      if (e.hit > 0) e.hit -= dt;
      if (e.atkCd > 0) e.atkCd -= dt;
      if (e.bladeCd > 0) e.bladeCd -= dt;
      e.rot += e.spin * dt;

      const dx = p.x - e.x;
      const dy = p.y - e.y;
      const dist = Math.hypot(dx, dy) || 1;
      const nx = dx / dist;
      const ny = dy / dist;

      // --- AI
      if (e.type === "dasher") {
        if (e.state === 0) {
          e.vx = nx * e.speed; e.vy = ny * e.speed;
          if (dist < 250 && e.age > 0.5) { e.state = 1; e.stateT = 0.45; }
        } else if (e.state === 1) {
          e.vx *= 1 - Math.min(1, 10 * dt); e.vy *= 1 - Math.min(1, 10 * dt);
          e.stateT -= dt;
          if (e.stateT <= 0) {
            e.state = 2; e.stateT = 0.38;
            e.dashX = nx; e.dashY = ny;
            this.sfx.dash();
          }
        } else if (e.state === 2) {
          e.vx = e.dashX * 570; e.vy = e.dashY * 570;
          e.stateT -= dt;
          if (this.quality > 0 && Math.random() < 0.6) {
            this.burst(e.x, e.y, "fuchsia", 1, 20, 0.25, 0.6);
          }
          if (e.stateT <= 0) { e.state = 3; e.stateT = 0.7; }
        } else {
          e.vx = nx * e.speed * 0.35; e.vy = ny * e.speed * 0.35;
          e.stateT -= dt;
          if (e.stateT <= 0) e.state = 0;
        }
      } else {
        const wob = Math.sin(this.time * 2.2 + e.seed) * (e.type === "imp" ? 0.45 : 0.18);
        const wx = nx * Math.cos(wob) - ny * Math.sin(wob);
        const wy = nx * Math.sin(wob) + ny * Math.cos(wob);
        e.vx = wx * e.speed;
        e.vy = wy * e.speed;
      }

      // knock decay + integrate
      const kd = Math.exp(-8 * dt);
      e.kx *= kd; e.ky *= kd;
      e.x += (e.vx + e.kx) * dt;
      e.y += (e.vy + e.ky) * dt;

      // recycle far-away stragglers back onto the ring
      if (!e.elite && dist > 1750) {
        const a = Math.random() * TAU;
        const r = Math.hypot(this.w, this.h) / 2 + 90;
        e.x = p.x + Math.cos(a) * r;
        e.y = p.y + Math.sin(a) * r;
        e.age = 0;
      }

      // --- touch damage
      if (p.alive && e.age > 0.4 && e.atkCd <= 0 && p.iframes <= 0) {
        if (dist < e.r + 15) {
          e.atkCd = 0.72;
          this.damagePlayer(e.dmg, nx, ny, e);
        }
      }
    }

    // --- separation (single pass over neighbour pairs)
    for (let i = 0; i < arr.length; i++) {
      const a = arr[i];
      const cx = Math.floor(a.x / CS);
      const cy = Math.floor(a.y / CS);
      for (let gx = cx - 1; gx <= cx + 1; gx++) {
        for (let gy = cy - 1; gy <= cy + 1; gy++) {
          const cellArr = this.grid.get(gx * 100000 + gy);
          if (!cellArr) continue;
          for (let c = 0; c < cellArr.length; c++) {
            const j = cellArr[c];
            if (j <= i) continue;
            const b = arr[j];
            let dx = b.x - a.x;
            let dy = b.y - a.y;
            const min = a.r + b.r - 3;
            const d2 = dx * dx + dy * dy;
            if (d2 >= min * min || d2 < 0.0001) continue;
            const d = Math.sqrt(d2);
            const push = ((min - d) / d) * 0.42;
            dx *= push; dy *= push;
            const wa = b.r / (a.r + b.r);
            a.x -= dx * wa; a.y -= dy * wa;
            b.x += dx * (1 - wa); b.y += dy * (1 - wa);
          }
        }
      }
    }
  }

  private damagePlayer(dmg: number, nx: number, ny: number, src: Enemy) {
    const p = this.player;
    p.hp -= dmg;
    p.iframes = 0.65;
    this.redFlash = 1;
    this.shake(0.5);
    this.freezeT = Math.max(this.freezeT, 0.045);
    this.sfx.hurt();
    this.burst(p.x, p.y, "red", 10, 260, 0.5, 0.9);
    // knock player & attacker apart
    p.vx += nx * 260;
    p.vy += ny * 260;
    const resist = src.elite ? 0.1 : src.type === "brute" ? 0.3 : 1;
    src.kx -= nx * 160 * resist;
    src.ky -= ny * 160 * resist;

    if (p.hp <= 0 && p.alive) {
      p.hp = 0;
      p.alive = false;
      this.dyingT = 1.15;
      this.targetTimeScale = 0.22;
      this.shake(1);
      this.freezeT = 0.09;
      this.sfx.gameover();
      this.burst(p.x, p.y, "cyan", 42, 420, 0.9, 1.2);
      this.burst(p.x, p.y, "white", 20, 300, 0.7, 0.9);
      this.ringWave(p.x, p.y, 20, 340, 0.7);
    }
  }

  private updateBlades(dt: number) {
    const n = this.stats.blades;
    if (n <= 0 || !this.player.alive) return;
    const p = this.player;
    p.bladeAng += dt * 3.1;
    const radius = 82;
    for (let i = 0; i < n; i++) {
      const a = p.bladeAng + (i / n) * TAU;
      const bx = p.x + Math.cos(a) * radius;
      const by = p.y + Math.sin(a) * radius;
      for (let j = 0; j < this.enemies.length; j++) {
        const e = this.enemies[j];
        if (e.bladeCd > 0 || e.age < 0.2) continue;
        const dx = e.x - bx;
        const dy = e.y - by;
        const rr = e.r + 17;
        if (dx * dx + dy * dy < rr * rr) {
          e.bladeCd = 0.38;
          const crit = Math.random() < this.stats.crit;
          this.hitEnemy(e, j, this.stats.bladeDmg * (crit ? 2.2 : 1), crit, dx, dy, 200);
        }
      }
    }
  }

  private updateBullets(dt: number) {
    const arr = this.bullets;
    for (let i = arr.length - 1; i >= 0; i--) {
      const b = arr[i];
      b.x += b.vx * dt;
      b.y += b.vy * dt;
      b.life -= dt;
      if (b.life <= 0) {
        arr[i] = arr[arr.length - 1];
        arr.pop();
        continue;
      }
      for (let j = 0; j < this.enemies.length; j++) {
        const e = this.enemies[j];
        if (e.age < 0.12) continue;
        if (e === b.h0 || e === b.h1 || e === b.h2 || e === b.h3) continue;
        const dx = e.x - b.x;
        const dy = e.y - b.y;
        const rr = e.r + 6;
        if (dx * dx + dy * dy < rr * rr) {
          b.h3 = b.h2; b.h2 = b.h1; b.h1 = b.h0; b.h0 = e;
          const crit = Math.random() < this.stats.crit;
          this.hitEnemy(e, j, b.dmg * (crit ? 2.2 : 1), crit, b.vx, b.vy, 150);
          if (crit) this.sfx.crit();
          b.pierce--;
          if (b.pierce < 0) {
            arr[i] = arr[arr.length - 1];
            arr.pop();
            break;
          }
        }
      }
    }
  }

  private hitEnemy(e: Enemy, idx: number, dmg: number, crit: boolean, dirX: number, dirY: number, knock: number) {
    e.hp -= dmg;
    e.hit = 0.09;
    const dl = Math.hypot(dirX, dirY) || 1;
    const resist = e.elite ? 0.08 : e.type === "brute" ? 0.25 : 1;
    e.kx += (dirX / dl) * knock * resist;
    e.ky += (dirY / dl) * knock * resist;
    this.floatText(
      e.x, e.y - e.r, String(Math.round(dmg)),
      crit ? "#facc15" : "#e2e8f0",
      crit ? 17 : 12.5
    );
    this.burst(e.x, e.y, ENEMY_DOT[e.type], crit ? 5 : 3, 190, 0.35, 0.7);
    this.sfx.hit();
    if (e.hp <= 0) this.killEnemy(e, idx);
  }

  private killEnemy(e: Enemy, idx: number) {
    // swap-remove (idx valid: bullets/blades may hold stale idx after removal —
    // they break out immediately after a kill via pierce/bladeCd, safe)
    this.enemies[idx] = this.enemies[this.enemies.length - 1];
    this.enemies.pop();
    this.kills++;
    if (e.elite) {
      this.eliteKills++;
      this.shake(0.7);
      this.freezeT = Math.max(this.freezeT, 0.09);
      this.ringWave(e.x, e.y, 30, 420, 0.6);
      this.burst(e.x, e.y, "red", 30, 380, 0.8, 1.1);
      this.burst(e.x, e.y, "orange", 18, 300, 0.7, 0.9);
      this.sfx.eliteDie();
      this.events.onToast("ELITE SLAIN  +250", "gem");
    } else {
      this.shake(0.07);
      this.burst(e.x, e.y, ENEMY_DOT[e.type], e.type === "brute" ? 18 : 9, 260, 0.55, 0.9);
      if (e.type === "brute") this.ringWave(e.x, e.y, 14, 220, 0.4);
      this.sfx.die();
    }
    this.spawnGems(e.x, e.y, e.xp);

    if (this.kills >= this.killMilestone) {
      this.events.onToast(`${this.killMilestone} KILLS`, "gem");
      this.killMilestone *= 2;
    }
  }

  private updateGems(dt: number) {
    const p = this.player;
    const arr = this.gems;
    const mag2 = this.stats.magnet * this.stats.magnet;
    for (let i = arr.length - 1; i >= 0; i--) {
      const g = arr[i];
      g.age += dt;
      const dx = p.x - g.x;
      const dy = p.y - g.y;
      const d2 = dx * dx + dy * dy;
      if (p.alive && (g.pull || d2 < mag2)) {
        g.pull = true;
        g.spd = Math.min(950, g.spd + 2900 * dt);
        const d = Math.sqrt(d2) || 1;
        g.vx = (dx / d) * g.spd;
        g.vy = (dy / d) * g.spd;
      } else {
        const kd = Math.exp(-5 * dt);
        g.vx *= kd; g.vy *= kd;
      }
      g.x += g.vx * dt;
      g.y += g.vy * dt;
      if (p.alive && d2 < 22 * 22) {
        // collect
        arr[i] = arr[arr.length - 1];
        arr.pop();
        this.addXp(g.val);
        p.gemStep = p.gemStepT > 0 ? p.gemStep + 1 : 0;
        p.gemStepT = 0.9;
        this.sfx.gem(p.gemStep);
        this.burst(p.x, p.y, "emerald", 2, 120, 0.25, 0.6);
      }
    }
    if (p.gemStepT > 0) p.gemStepT -= dt;
  }

  private addXp(val: number) {
    const p = this.player;
    p.xp += val;
    if (p.xp >= p.xpNeed && this.levelUpT < 0 && this.dyingT < 0) {
      p.xp -= p.xpNeed;
      p.level++;
      p.xpNeed = this.xpNeed(p.level);
      this.levelUpT = 0.42;
      this.targetTimeScale = 0.12;
      this.cyanFlash = 0.9;
      this.ringWave(p.x, p.y, 18, 460, 0.65);
      this.burst(p.x, p.y, "cyan", 22, 330, 0.7, 1);
      this.sfx.levelup();
    }
  }

  private updateFx(dt: number) {
    for (let i = this.parts.length - 1; i >= 0; i--) {
      const pt = this.parts[i];
      pt.life -= dt;
      if (pt.life <= 0) {
        this.parts[i] = this.parts[this.parts.length - 1];
        this.parts.pop();
        this.partPool.push(pt);
        continue;
      }
      if (pt.ring) {
        pt.size += pt.grow * dt;
      } else {
        const kd = Math.exp(-pt.drag * dt);
        pt.vx *= kd; pt.vy *= kd;
        pt.x += pt.vx * dt;
        pt.y += pt.vy * dt;
      }
    }
    for (let i = this.floaters.length - 1; i >= 0; i--) {
      const f = this.floaters[i];
      f.life -= dt;
      f.y -= 46 * dt;
      if (f.life <= 0) {
        this.floaters[i] = this.floaters[this.floaters.length - 1];
        this.floaters.pop();
        this.floaterPool.push(f);
      }
    }
  }

  // ---------------------------------------------------------------- loop

  private loop(now: number) {
    this.raf = requestAnimationFrame((t) => this.loop(t));
    let dtRaw = (now - this.lastT) / 1000;
    this.lastT = now;
    if (dtRaw > 0.05) dtRaw = 0.05;
    if (dtRaw <= 0) dtRaw = 0.0001;
    this.ambientT += dtRaw;

    // adaptive quality
    this.frameEma = this.frameEma * 0.96 + dtRaw * 1000 * 0.04;
    if (++this.frameCount % 90 === 0) {
      if (this.frameEma > 19.5 && this.quality > 0) {
        this.quality--;
        if (this.quality === 0) this.resize();
      } else if (this.frameEma < 13.5 && this.quality < 2) {
        this.quality++;
      }
    }

    let dt = dtRaw * this.timeScale;
    if (this.freezeT > 0) {
      this.freezeT -= dtRaw;
      dt = 0;
    }

    if (this.phase === "playing") {
      this.update(dt, dtRaw);
    } else if (this.phase === "menu") {
      // gentle ambient camera drift behind the menu
      this.cam.x = Math.cos(this.ambientT * 0.07) * 260;
      this.cam.y = Math.sin(this.ambientT * 0.052) * 260;
      this.updateFx(dtRaw);
    } else {
      // paused / levelup / gameover: keep particles settling, world frozen
      this.updateFx(this.phase === "gameover" ? dtRaw * 0.4 : dtRaw * 0.12);
      if (this.phase === "gameover") {
        this.trauma = Math.max(0, this.trauma - dtRaw * 1.7);
      }
    }

    this.render();
    this.updateHud(dtRaw);
  }

  // ---------------------------------------------------------------- render

  private resize() {
    this.w = window.innerWidth;
    this.h = window.innerHeight;
    const cap = this.quality === 0 ? 1.25 : 2;
    this.dpr = Math.min(window.devicePixelRatio || 1, cap);
    this.canvas.width = Math.round(this.w * this.dpr);
    this.canvas.height = Math.round(this.h * this.dpr);
    this.buildGradients();
  }

  private buildBackdrop() {
    // nebula blobs
    const cols = ["#312e81", "#4c1d95", "#155e75", "#3b0764"];
    for (let i = 0; i < 4; i++) {
      const c = document.createElement("canvas");
      c.width = c.height = 300;
      const ctx = c.getContext("2d")!;
      const g = ctx.createRadialGradient(150, 150, 10, 150, 150, 150);
      g.addColorStop(0, cols[i] + "55");
      g.addColorStop(0.6, cols[i] + "26");
      g.addColorStop(1, cols[i] + "00");
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, 300, 300);
      this.nebulae.push(c);
    }
    // dust field
    for (let i = 0; i < 80; i++) {
      this.dust.push({
        x: Math.random() * 2200,
        y: Math.random() * 2200,
        s: 0.5 + Math.random() * 1.6,
        p: 0.35 + Math.random() * 0.5,
        f: 0.4 + Math.random() * 1.6,
      });
    }
  }

  private buildGradients() {
    const bg = document.createElement("canvas");
    bg.width = 512; bg.height = 288;
    const bctx = bg.getContext("2d")!;
    const g = bctx.createRadialGradient(256, 128, 20, 256, 144, 300);
    g.addColorStop(0, "#0c1132");
    g.addColorStop(0.55, "#070a1c");
    g.addColorStop(1, "#04050d");
    bctx.fillStyle = g;
    bctx.fillRect(0, 0, 512, 288);
    this.bgGrad = bg;

    const v = document.createElement("canvas");
    v.width = 512; v.height = 288;
    const vctx = v.getContext("2d")!;
    const vg = vctx.createRadialGradient(256, 144, 90, 256, 144, 320);
    vg.addColorStop(0, "rgba(0,0,0,0)");
    vg.addColorStop(0.75, "rgba(2,3,10,0.32)");
    vg.addColorStop(1, "rgba(1,2,8,0.66)");
    vctx.fillStyle = vg;
    vctx.fillRect(0, 0, 512, 288);
    this.vignette = v;

    const r = document.createElement("canvas");
    r.width = 512; r.height = 288;
    const rctx = r.getContext("2d")!;
    const rg = rctx.createRadialGradient(256, 144, 80, 256, 144, 310);
    rg.addColorStop(0, "rgba(239,68,68,0)");
    rg.addColorStop(0.8, "rgba(239,68,68,0.22)");
    rg.addColorStop(1, "rgba(190,18,60,0.5)");
    rctx.fillStyle = rg;
    rctx.fillRect(0, 0, 512, 288);
    this.redVignette = r;
  }

  private render() {
    const ctx = this.ctx;
    const { w, h } = this;
    ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);

    // background
    if (this.bgGrad) ctx.drawImage(this.bgGrad, 0, 0, w, h);
    else { ctx.fillStyle = "#05060f"; ctx.fillRect(0, 0, w, h); }

    const shakeMag = this.trauma * this.trauma * 15;
    const shX = (Math.random() * 2 - 1) * shakeMag;
    const shY = (Math.random() * 2 - 1) * shakeMag;
    const camX = this.cam.x - w / 2 + shX;
    const camY = this.cam.y - h / 2 + shY;

    // nebulae (screen-space parallax wrap)
    if (this.quality > 0) {
      const nw = w + 700;
      const nh = h + 700;
      for (let i = 0; i < this.nebulae.length; i++) {
        const px = ((i * 977 + 500 - camX * 0.22) % nw + nw) % nw - 350;
        const py = ((i * 613 + 300 - camY * 0.22) % nh + nh) % nh - 350;
        const scale = 2.4 + (i % 3);
        ctx.globalAlpha = 0.75;
        ctx.drawImage(this.nebulae[i], px - 150 * scale, py - 150 * scale, 300 * scale, 300 * scale);
      }
      ctx.globalAlpha = 1;
    }

    // grid
    const GS = 96;
    ctx.strokeStyle = "rgba(99,120,246,0.075)";
    ctx.lineWidth = 1;
    ctx.beginPath();
    const gx0 = Math.floor(camX / GS) * GS;
    const gy0 = Math.floor(camY / GS) * GS;
    for (let x = gx0; x < camX + w + GS; x += GS) {
      ctx.moveTo(x - camX, 0);
      ctx.lineTo(x - camX, h);
    }
    for (let y = gy0; y < camY + h + GS; y += GS) {
      ctx.moveTo(0, y - camY);
      ctx.lineTo(w, y - camY);
    }
    ctx.stroke();

    // dust
    if (this.quality > 0) {
      const dw = w + 200;
      const dh = h + 200;
      const spr = this.sprites.dot.white;
      for (let i = 0; i < this.dust.length; i++) {
        const d = this.dust[i];
        const px = ((d.x - camX * d.p) % dw + dw) % dw - 100;
        const py = ((d.y - camY * d.p) % dh + dh) % dh - 100;
        const tw = 0.35 + 0.3 * Math.sin(this.ambientT * d.f + i);
        ctx.globalAlpha = tw * 0.5;
        const s = d.s * 8;
        ctx.drawImage(spr, px - s / 2, py - s / 2, s, s);
      }
      ctx.globalAlpha = 1;
    }

    // ---- world space
    ctx.save();
    ctx.translate(-camX, -camY);

    // gems
    const gemSpr = [this.sprites.gemS, this.sprites.gemM, this.sprites.gemL];
    for (let i = 0; i < this.gems.length; i++) {
      const g = this.gems[i];
      const s = gemSpr[g.tier];
      const pulse = 1 + Math.sin(g.age * 5) * 0.12;
      const sz = s.width * pulse;
      ctx.drawImage(s, g.x - sz / 2, g.y - sz / 2, sz, sz);
    }

    // enemies
    for (let i = 0; i < this.enemies.length; i++) {
      const e = this.enemies[i];
      const grow = e.age < 0.35 ? backOut(Math.min(1, e.age / 0.35)) : 1;
      const spr = e.hit > 0 || (e.type === "dasher" && e.state === 1 && Math.sin(this.ambientT * 40) > 0)
        ? this.sprites.enemyHit[e.type]
        : this.sprites.enemy[e.type];
      const sz = spr.width * grow;
      ctx.save();
      ctx.translate(e.x, e.y);
      ctx.rotate(e.rot);
      ctx.drawImage(spr, -sz / 2, -sz / 2, sz, sz);
      ctx.restore();
      if (e.elite) {
        const pulse = 1 + Math.sin(this.ambientT * 4) * 0.12;
        const rs = 150 * pulse;
        ctx.globalAlpha = 0.28 + 0.1 * Math.sin(this.ambientT * 4);
        ctx.drawImage(this.sprites.ring, e.x - rs / 2, e.y - rs / 2, rs, rs);
        ctx.globalAlpha = 1;
      }
      // mini hp bar for heavies
      if ((e.type === "brute" || e.elite) && e.hp < e.maxHp) {
        const bw = e.r * 2;
        const frac = Math.max(0, e.hp / e.maxHp);
        ctx.fillStyle = "rgba(2,6,18,0.7)";
        ctx.fillRect(e.x - bw / 2, e.y - e.r - 12, bw, 4);
        ctx.fillStyle = e.elite ? "#ef4444" : "#fb923c";
        ctx.fillRect(e.x - bw / 2, e.y - e.r - 12, bw * frac, 4);
      }
    }

    // orbit blades
    const p = this.player;
    if (this.stats.blades > 0 && p.alive) {
      const n = this.stats.blades;
      for (let i = 0; i < n; i++) {
        const a = p.bladeAng + (i / n) * TAU;
        const bx = p.x + Math.cos(a) * 82;
        const by = p.y + Math.sin(a) * 82;
        ctx.save();
        ctx.translate(bx, by);
        ctx.rotate(a + Math.PI / 2);
        ctx.drawImage(this.sprites.blade, -22, -11);
        ctx.restore();
      }
    }

    // player
    if (p.alive && this.phase !== "menu") {
      const blink = p.iframes > 0 && Math.sin(this.ambientT * 34) > 0;
      ctx.globalAlpha = blink ? 0.35 : 1;
      ctx.save();
      ctx.translate(p.x, p.y);
      ctx.rotate(this.ambientT * 1.4);
      const rs = 62;
      ctx.drawImage(this.sprites.playerRing, -rs / 2, -rs / 2, rs, rs);
      ctx.restore();
      const ps = this.sprites.player.width;
      ctx.drawImage(this.sprites.player, p.x - ps / 2, p.y - ps / 2);
      ctx.globalAlpha = 1;
    }

    // bullets
    const bs = this.sprites.bullet;
    for (let i = 0; i < this.bullets.length; i++) {
      const b = this.bullets[i];
      ctx.save();
      ctx.translate(b.x, b.y);
      ctx.rotate(Math.atan2(b.vy, b.vx));
      ctx.drawImage(bs, -17, -9);
      ctx.restore();
    }

    // particles (additive)
    ctx.globalCompositeOperation = "lighter";
    for (let i = 0; i < this.parts.length; i++) {
      const pt = this.parts[i];
      const t = pt.life / pt.max;
      ctx.globalAlpha = t;
      if (pt.ring) {
        const sz = pt.size * 2;
        ctx.drawImage(pt.spr, pt.x - pt.size, pt.y - pt.size, sz, sz);
      } else {
        const sz = 24 * pt.size * (0.4 + t * 0.6);
        ctx.drawImage(pt.spr, pt.x - sz / 2, pt.y - sz / 2, sz, sz);
      }
    }
    ctx.globalAlpha = 1;
    ctx.globalCompositeOperation = "source-over";

    // damage numbers
    if (this.floaters.length > 0) {
      ctx.textAlign = "center";
      for (let i = 0; i < this.floaters.length; i++) {
        const f = this.floaters[i];
        const t = f.life / f.max;
        ctx.globalAlpha = Math.min(1, t * 2);
        ctx.font = `700 ${f.size}px "Chakra Petch", monospace`;
        ctx.fillStyle = f.color;
        ctx.fillText(f.text, f.x, f.y);
      }
      ctx.globalAlpha = 1;
    }

    ctx.restore();

    // ---- screen space fx
    if (this.vignette) ctx.drawImage(this.vignette, 0, 0, w, h);

    const lowHp = this.phase === "playing" && p.alive && p.hp < this.stats.maxHp * 0.3;
    const redA = this.redFlash * 0.85 + (lowHp ? 0.35 + 0.2 * Math.sin(this.ambientT * 5) : 0);
    if (redA > 0.01 && this.redVignette) {
      ctx.globalAlpha = Math.min(1, redA);
      ctx.drawImage(this.redVignette, 0, 0, w, h);
      ctx.globalAlpha = 1;
    }
    if (this.cyanFlash > 0.01) {
      ctx.fillStyle = `rgba(103,232,249,${(this.cyanFlash * 0.16).toFixed(3)})`;
      ctx.fillRect(0, 0, w, h);
    }

    // touch joystick
    if (this.input.joyActive && this.phase === "playing") {
      const bx = this.input.joyBaseX;
      const by = this.input.joyBaseY;
      ctx.beginPath();
      ctx.arc(bx, by, 52, 0, TAU);
      ctx.strokeStyle = "rgba(103,232,249,0.35)";
      ctx.lineWidth = 2;
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(bx, by, 52, 0, TAU);
      ctx.fillStyle = "rgba(34,211,238,0.08)";
      ctx.fill();
      const kx = bx + this.input.joyX * 40;
      const ky = by + this.input.joyY * 40;
      ctx.beginPath();
      ctx.arc(kx, ky, 22, 0, TAU);
      ctx.fillStyle = "rgba(103,232,249,0.45)";
      ctx.fill();
      ctx.beginPath();
      ctx.arc(kx, ky, 22, 0, TAU);
      ctx.strokeStyle = "rgba(165,243,252,0.8)";
      ctx.stroke();
    }
  }

  // ---------------------------------------------------------------- HUD (direct DOM, no react re-render)

  private updateHud(dtRaw: number) {
    const hud = this.hud;
    if (!hud) return;
    const p = this.player;
    const L = this.lastHud;

    const hpFrac = Math.max(0, p.hp / this.stats.maxHp);
    if (p.hp !== L.hp || this.stats.maxHp !== L.maxHp) {
      L.hp = p.hp; L.maxHp = this.stats.maxHp;
      if (hud.hpBar) hud.hpBar.style.transform = `scaleX(${hpFrac.toFixed(3)})`;
      if (hud.hpText) hud.hpText.textContent = `${Math.ceil(Math.max(0, p.hp))}`;
    }
    // ghost bar trails behind
    this.hpGhost += (hpFrac - this.hpGhost) * (1 - Math.exp(-3.2 * dtRaw));
    if (this.hpGhost < hpFrac) this.hpGhost = hpFrac;
    if (hud.hpGhost) hud.hpGhost.style.transform = `scaleX(${this.hpGhost.toFixed(3)})`;

    const xpFrac = Math.min(1, p.xp / p.xpNeed);
    if (xpFrac !== L.xp) {
      L.xp = xpFrac;
      if (hud.xpBar) hud.xpBar.style.transform = `scaleX(${xpFrac.toFixed(3)})`;
    }
    if (p.level !== L.lvl) {
      L.lvl = p.level;
      if (hud.levelText) hud.levelText.textContent = `LV ${p.level}`;
    }
    const tsec = Math.floor(this.time);
    if (tsec !== L.time) {
      L.time = tsec;
      if (hud.timeText) {
        const m = Math.floor(tsec / 60);
        const s = tsec % 60;
        hud.timeText.textContent = `${m}:${s.toString().padStart(2, "0")}`;
      }
    }
    const sc = this.score;
    if (sc !== L.score) {
      L.score = sc;
      if (hud.scoreText) hud.scoreText.textContent = sc.toLocaleString();
    }
    if (this.kills !== L.kills) {
      L.kills = this.kills;
      if (hud.killText) hud.killText.textContent = String(this.kills);
    }
    if (hud.hurtFlash) hud.hurtFlash.style.opacity = String(this.redFlash * 0.5);
  }
}

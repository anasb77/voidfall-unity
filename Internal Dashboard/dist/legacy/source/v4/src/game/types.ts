// ───────────────────────────────────────────────────────────────────
// VOIDFALL v1.0 — Complete type definitions
// ───────────────────────────────────────────────────────────────────

export type GamePhase = "menu" | "playing" | "levelup" | "paused" | "gameover" | "victory" | "evolve" | "chest" | "revive";
export type Rarity = "common" | "uncommon" | "rare" | "epic" | "legendary";
export type WeaponId = "pistol" | "shotgun" | "smg" | "railgun" | "arc" | "flame" | "rocket" | "blades" | "drone" | "mines" | "ice" | "nova" | "crossbow" | "tendrils" | "plasma" | "thunder" | "chain" | "voidorb";
export type PassiveId =
  | "damage" | "critChance" | "critDmg" | "fireRate" | "projSpeed" | "areaSize"
  | "pierce" | "statusDmg" | "bossDmg" | "closeDmg" | "longDmg"
  | "maxHp" | "armor" | "regen" | "shield" | "dodge" | "postHitDR" | "emergHeal"
  | "moveSpeed" | "magnet" | "xpGain" | "currencyGain" | "luck" | "rerolls"
  | "banishes" | "extraChoices" | "dashCD"
  | "magicPow" | "killHeal" | "eliteDmg" | "thorns" | "goldFind"
  | "deathBurst" | "vampiric" | "ricochet" | "soulHarvest" | "timeDilation"
  | "voidGrasp" | "ironWill" | "echoShot" | "secondWind" | "bloodPact"
  | "magneticField" | "overcharge" | "desperado" | "executioner"
  | "multishot" | "frostbite" | "chainLightning" | "lastStand"
  | "comboMaster" | "phoenixHeart" | "voidWalker" | "twinSouls";
export type EnemyId =
  | "chaser" | "runner" | "tank" | "shooter" | "charger" | "exploder"
  | "splitter" | "shielded" | "healer" | "summoner" | "ambusher" | "swarmer"
  | "spider" | "phantom" | "golem" | "wisp"
  | "vortex" | "broodmother" | "jumper" | "sniper" | "phaser" | "bomber"
  | "leech" | "mirror" | "swarmlord" | "crawler" | "wall" | "pulsar"
  | "lancer" | "necrofiend" | "warper" | "toxin" | "anchor" | "blink"
  | "colossus" | "rift"
  | "hydra" | "mimic" | "siren" | "wraithlord";
export type BossId = "warden" | "hive" | "gunslinger" | "voidEngine";
export type EliteMod = "giant" | "frenzied" | "shielded" | "regen" | "explosive" | "teleport" | "vampiric" | "split" | "electrified" | "summoner";
export type CharacterId = "sentinel" | "phantom" | "bastion" | "overseer";
export type ArenaId = "neon" | "ash" | "reactor";
export type ModeId = "standard" | "endless";
export type ToastKind = "info" | "danger" | "gem" | "gold" | "evo" | "combo";

export interface Vec2 { x: number; y: number }

/* ── Weapons ─────────────────────────────────────────────────────── */

export interface WeaponDef {
  id: WeaponId;
  name: string;
  icon: string;
  rarity: Rarity;
  baseDamage: number;
  baseCooldown: number;
  baseRange: number;
  basePierce: number;
  baseArea: number;
  description: string;
  evolution?: {
    id: string;
    name: string;
    condition: { passive?: PassiveId; weaponLevel?: number; passiveLevel?: number };
    description: string;
  };
}

export interface ActiveWeapon {
  id: WeaponId;
  level: number;
  evolved: boolean;
  cooldownTimer: number;
  chargeTimer: number;
  // runtime-only fields (not persisted between levels)
}

/* ── Passives ────────────────────────────────────────────────────── */

export interface PassiveDef {
  id: PassiveId;
  name: string;
  icon: string;
  rarity: Rarity;
  max: number;
  weight: number;
  description: (level: number) => string;
  category: "offensive" | "defensive" | "utility";
}

/* ── Characters ──────────────────────────────────────────────────── */

export interface CharacterDef {
  id: CharacterId;
  name: string;
  desc: string;
  startWeapon: WeaponId;
  passive: { id: string; desc: string };
  baseStats: { maxHp: number; speed: number; damage: number; crit: number; magnet: number };
  color: string;
  unlockCondition?: string;
  unlocked: boolean;
}

/* ── Enemies ─────────────────────────────────────────────────────── */

export interface EnemyDef {
  id: EnemyId;
  hp: number;
  speed: number;
  dmg: number;
  r: number;
  xp: number;
  color: string;
  behavior: string;
  interval: number;
}

/* ── Bosses ──────────────────────────────────────────────────────── */

export interface BossPhase {
  name: string;
  hpThreshold: number;
  attacks: string[];
  duration: number;
  attackInterval: number;
}

export interface BossDef {
  id: BossId;
  name: string;
  hp: number;
  r: number;
  speed: number;
  dmg: number;
  xp: number;
  color: string;
  phases: BossPhase[];
  enrageTime: number;
}

/* ── Arenas ──────────────────────────────────────────────────────── */

export interface ArenaDef {
  id: ArenaId;
  name: string;
  desc: string;
  bgColors: [string, string, string];
  gridColor: string;
  hpMod: number;
  spdMod: number;
  eliteFreqMod: number;
  unlockCondition?: string;
  unlocked: boolean;
}

/* ── Engine events ───────────────────────────────────────────────── */

export interface HudRefs {
  hpBar: HTMLElement | null;
  hpGhost: HTMLElement | null;
  hpText: HTMLElement | null;
  xpBar: HTMLElement | null;
  levelText: HTMLElement | null;
  timeText: HTMLElement | null;
  scoreText: HTMLElement | null;
  killText: HTMLElement | null;
  comboText: HTMLElement | null;
  hurtFlash: HTMLElement | null;
  weaponText: HTMLElement | null;
  creditsText: HTMLElement | null;
  shieldBar: HTMLElement | null;
  shieldText: HTMLElement | null;
}

export interface RunStats {
  score: number;
  kills: number;
  eliteKills: number;
  bossKills: number;
  time: number;
  level: number;
  maxCombo: number;
  damageDealt: number;
  damageTaken: number;
  creditsEarned: number;
  weapons: Record<WeaponId, number>;
}

export interface HighScore {
  score: number;
  kills: number;
  time: number;
  level: number;
  character: CharacterId;
  arena: ArenaId;
  date: number;
  victory: boolean;
}

export interface Toast {
  id: number;
  text: string;
  kind: ToastKind;
  sub?: string;
}

export interface PerfData {
  fps: number;
  frameTime: number;
  enemyCount: number;
  projCount: number;
  particleCount: number;
  gemCount: number;
  difficulty: number;
  wave: number;
  seed: number;
}

export interface EngineEvents {
  onPhaseChange: (phase: GamePhase) => void;
  onLevelUp: (optionIds: string[], optionTypes: ("weapon" | "passive" | "heal" | "reroll" | "banish")[]) => void;
  onGameOver: (stats: RunStats, rank: number, isBest: boolean, victory: boolean) => void;
  onToast: (text: string, kind: ToastKind, sub?: string) => void;
  onScores: (scores: HighScore[]) => void;
  onUpgrades: (weaponLevels: Record<WeaponId, number>, passiveLevels: Record<PassiveId, number>) => void;
  onMute: (muted: boolean) => void;
  onPerf: (data: PerfData) => void;
  onChestOpen: (tier: string) => void;
}

export const PHASE_OVERLAYS: GamePhase[] = ["levelup", "paused", "gameover", "victory", "evolve", "chest", "revive"];

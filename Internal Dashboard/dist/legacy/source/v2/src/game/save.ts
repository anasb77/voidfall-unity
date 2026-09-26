// ───────────────────────────────────────────────────────────────────
// VOIDFALL v1.0 — Versioned Save System with Migration
// ───────────────────────────────────────────────────────────────────
import type { CharacterId, ArenaId, PassiveId, WeaponId } from "./types";

const SAVE_KEY = "voidfall_v1";
const CURRENT_VERSION = 1;

export interface SaveData {
  version: number;
  credits: number;
  reviveTokens: number;
  // Permanent upgrades
  permUpgrades: Record<string, number>;
  // Unlocks
  unlockedChars: CharacterId[];
  unlockedArenas: ArenaId[];
  unlockedEvolutions: string[];
  unlockedEndless: boolean;
  // Stats
  totalRuns: number;
  totalPlaytime: number;
  totalKills: number;
  totalEliteKills: number;
  totalBossKills: number;
  highestScore: number;
  longestSurvival: number;
  highestCombo: number;
  totalCreditsEarned: number;
  victories: number;
  revivesUsed: number;
  totalDamageDealt: number;
  totalDamageTaken: number;
  favChar: Record<CharacterId, number>;
  favWeapon: Record<WeaponId, number>;
  mostUsedUpgrade: Record<PassiveId, number>;
  // Challenges
  completedRuns: number;
  bossDefeated: Record<string, number>;
  evolutionReached: Record<string, boolean>;
  fiveMinRuns: number;
  noReviveWins: number;
  // Achievements
  achievements: Record<string, boolean>;
  // Settings
  settings: {
    masterVol: number;
    sfxVol: number;
    musicVol: number;
    shakeIntensity: number;
    showDmgNumbers: boolean;
    flashIntensity: number;
    reducedMotion: boolean;
    highContrast: boolean;
    quality: number;
    joystickSize: number;
    joystickOpacity: number;
    vibration: boolean;
    tutorialDone: boolean;
    dailyChallengeSeed: number;
    lastDaily: string;
  };
  // Migration data preserved from old saves
  _oldScores?: unknown[];
  _migratedFrom?: string;
}

function freshSave(): SaveData {
  return {
    version: CURRENT_VERSION,
    credits: 0,
    reviveTokens: 1,
    permUpgrades: {},
    unlockedChars: ["sentinel"],
    unlockedArenas: ["neon"],
    unlockedEvolutions: [],
    unlockedEndless: false,
    totalRuns: 0,
    totalPlaytime: 0,
    totalKills: 0,
    totalEliteKills: 0,
    totalBossKills: 0,
    highestScore: 0,
    longestSurvival: 0,
    highestCombo: 0,
    totalCreditsEarned: 0,
    victories: 0,
    revivesUsed: 0,
    totalDamageDealt: 0,
    totalDamageTaken: 0,
    favChar: { sentinel: 0, phantom: 0, bastion: 0, overseer: 0 },
    favWeapon: { pistol: 0, shotgun: 0, smg: 0, railgun: 0, arc: 0, flame: 0, rocket: 0, blades: 0, drone: 0, mines: 0 },
    mostUsedUpgrade: {} as Record<PassiveId, number>,
    completedRuns: 0,
    bossDefeated: {},
    evolutionReached: {},
    fiveMinRuns: 0,
    noReviveWins: 0,
    achievements: {},
    settings: {
      masterVol: 1, sfxVol: 1, musicVol: 0.6, shakeIntensity: 1,
      showDmgNumbers: true, flashIntensity: 1, reducedMotion: false,
      highContrast: false, quality: 2, joystickSize: 1, joystickOpacity: 0.7,
      vibration: true, tutorialDone: false, dailyChallengeSeed: 0,
      lastDaily: "",
    },
  };
}

export function loadSave(): SaveData {
  try {
    const raw = localStorage.getItem(SAVE_KEY);
    if (!raw) return freshSave();
    const data = JSON.parse(raw) as SaveData;
    if (!data || typeof data !== "object") return freshSave();

    // Migrate from version 0 (original game)
    if (data.version === 0 || !data.version) {
      const migrated = migrateFromV0(data);
      data.version = CURRENT_VERSION;
      // merge preserved fields
      Object.assign(data, migrated);
    }

    // Ensure all fields exist
    const fresh = freshSave();
    const result: SaveData = { ...fresh, ...data };
    result.settings = { ...fresh.settings, ...data.settings };
    result.favChar = { ...fresh.favChar, ...data.favChar };
    result.favWeapon = { ...fresh.favWeapon, ...data.favWeapon };
    result.mostUsedUpgrade = { ...fresh.mostUsedUpgrade, ...data.mostUsedUpgrade };

    // Daily challenge seed
    const today = new Date().toISOString().slice(0, 10);
    if (result.settings.lastDaily !== today) {
      result.settings.lastDaily = today;
      result.settings.dailyChallengeSeed = dailySeed(today);
    }

    return result;
  } catch {
    return freshSave();
  }
}

function migrateFromV0(old: Partial<SaveData>): Partial<SaveData> {
  const result: Partial<SaveData> = {};
  if (Array.isArray(old._oldScores)) result._oldScores = old._oldScores;
  result._migratedFrom = "v0";
  return result;
}

export function saveSave(data: SaveData): void {
  try {
    localStorage.setItem(SAVE_KEY, JSON.stringify(data));
  } catch { /* storage full or unavailable */ }
}

export function exportSave(data: SaveData): string {
  return btoa(JSON.stringify(data));
}

export function importSave(raw: string): SaveData | null {
  try {
    const json = atob(raw);
    const data = JSON.parse(json) as SaveData;
    if (data && typeof data === "object") return data;
    return null;
  } catch {
    return null;
  }
}

export function resetSave(): SaveData {
  try { localStorage.removeItem(SAVE_KEY); } catch {}
  return freshSave();
}

function dailySeed(dateStr: string): number {
  let hash = 0;
  for (let i = 0; i < dateStr.length; i++) {
    hash = ((hash << 5) - hash + dateStr.charCodeAt(i)) | 0;
  }
  return Math.abs(hash);
}

export interface DailyChallenge {
  name: string;
  desc: string;
  modifiers: string[];
  rewardMult: number;
  seed: number;
}

export function getDailyChallenge(): DailyChallenge {
  const save = loadSave();
  const seed = save.settings.dailyChallengeSeed;
  const rng = seededRng(seed);
  const challenges: Omit<DailyChallenge, "seed">[] = [
    { name: "Shotgun Only", desc: "Start with only the Scatter Shotgun.", modifiers: ["shotgun_start", "no_reroll"], rewardMult: 1.5 },
    { name: "Blitz", desc: "Enemies are 30% faster but deal 20% less damage.", modifiers: ["fast_enemies", "weak_hits"], rewardMult: 1.3 },
    { name: "Elite Storm", desc: "Double elite frequency. Double rewards.", modifiers: ["double_elites", "double_rewards"], rewardMult: 1.5 },
    { name: "Glass Cannon", desc: "Start with 50% HP but deal 50% more damage.", modifiers: ["low_hp", "high_dmg"], rewardMult: 1.4 },
    { name: "One-Hit Shield", desc: "Take one free hit. No healing pickups.", modifiers: ["one_shield", "no_healing"], rewardMult: 1.6 },
    { name: "Credit Rush", desc: "Earn 3× credits but enemies are tougher.", modifiers: ["triple_credits", "tough_enemies"], rewardMult: 1.3 },
    { name: "Boss Rush", desc: "Bosses appear 3× more often.", modifiers: ["frequent_bosses"], rewardMult: 1.5 },
    { name: "No Meta", desc: "Permanent upgrades disabled for this run.", modifiers: ["no_meta"], rewardMult: 1.4 },
  ];
  const pick = challenges[Math.floor(rng() * challenges.length)];
  return { ...pick, seed };
}

function seededRng(seed: number): () => number {
  let s = seed | 0;
  return () => {
    s = (s * 1664525 + 1013904223) | 0;
    return (s >>> 0) / 4294967296;
  };
}

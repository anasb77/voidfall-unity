// ───────────────────────────────────────────────────────────────────
// VOIDFALL v1.0 — 12 Enemy Archetypes
// ───────────────────────────────────────────────────────────────────
import type { EnemyDef, EnemyId } from "./types";

export const ENEMIES: Record<EnemyId, EnemyDef> = {
  chaser: { id: "chaser", hp: 20, speed: 76, dmg: 8, r: 15, xp: 1, color: "#fb7185", behavior: "direct", interval: 0 },
  runner: { id: "runner", hp: 8, speed: 155, dmg: 5, r: 8, xp: 1, color: "#a78bfa", behavior: "zigzag", interval: 0 },
  tank: { id: "tank", hp: 120, speed: 38, dmg: 18, r: 28, xp: 5, color: "#fb923c", behavior: "direct", interval: 0 },
  shooter: { id: "shooter", hp: 35, speed: 52, dmg: 12, r: 14, xp: 3, color: "#ef4444", behavior: "ranged", interval: 3.0 },
  charger: { id: "charger", hp: 30, speed: 90, dmg: 22, r: 14, xp: 3, color: "#e879f9", behavior: "charge", interval: 4.0 },
  exploder: { id: "exploder", hp: 16, speed: 88, dmg: 30, r: 12, xp: 2, color: "#f59e0b", behavior: "approach_explode", interval: 0 },
  splitter: { id: "splitter", hp: 55, speed: 62, dmg: 10, r: 22, xp: 3, color: "#34d399", behavior: "direct", interval: 0 },
  shielded: { id: "shielded", hp: 40, speed: 65, dmg: 9, r: 18, xp: 3, color: "#60a5fa", behavior: "shielded", interval: 0 },
  healer: { id: "healer", hp: 45, speed: 50, dmg: 6, r: 16, xp: 4, color: "#f9a8d4", behavior: "heal", interval: 5.0 },
  summoner: { id: "summoner", hp: 60, speed: 42, dmg: 8, r: 20, xp: 5, color: "#c084fc", behavior: "summon", interval: 8.0 },
  ambusher: { id: "ambusher", hp: 25, speed: 140, dmg: 16, r: 11, xp: 2, color: "#fbbf24", behavior: "burrow", interval: 5.0 },
  swarmer: { id: "swarmer", hp: 5, speed: 105, dmg: 3, r: 6, xp: 1, color: "#86efac", behavior: "swarm", interval: 0 },
};

export const ENEMY_DOT: Record<EnemyId, string> = {
  chaser: "pink", runner: "violet", tank: "orange", shooter: "red",
  charger: "fuchsia", exploder: "yellow", splitter: "emerald", shielded: "cyan",
  healer: "rose", summoner: "purple", ambusher: "amber", swarmer: "lime",
};

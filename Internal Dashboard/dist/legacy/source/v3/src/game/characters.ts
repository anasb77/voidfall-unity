// ───────────────────────────────────────────────────────────────────
// VOIDFALL v1.0 — 4 Playable Characters
// ───────────────────────────────────────────────────────────────────
import type { CharacterDef } from "./types";

export const CHARACTERS: CharacterDef[] = [
  {
    id: "sentinel",
    name: "Sentinel",
    desc: "Balanced warrior. Starts with the Pulse Pistol.",
    startWeapon: "pistol",
    passive: { id: "sturdy", desc: "+15% max HP, +5% damage" },
    baseStats: { maxHp: 115, speed: 230, damage: 1.05, crit: 0.05, magnet: 95 },
    color: "#22d3ee",
    unlocked: true,
  },
  {
    id: "phantom",
    name: "Phantom",
    desc: "Fast crit specialist. Low HP but deadly strikes. Starts with the Rapid SMG.",
    startWeapon: "smg",
    passive: { id: "critmaster", desc: "+10% crit chance, +30% crit damage, -15% max HP" },
    baseStats: { maxHp: 85, speed: 275, damage: 0.92, crit: 0.15, magnet: 110 },
    color: "#e879f9",
    unlockCondition: "Kill 500 enemies in total runs",
    unlocked: false,
  },
  {
    id: "bastion",
    name: "Bastion",
    desc: "Slow, armored tank. Heavy damage at close range. Starts with the Scatter Shotgun.",
    startWeapon: "shotgun",
    passive: { id: "fortress", desc: "+30% max HP, -15% move speed, +25% close-range damage" },
    baseStats: { maxHp: 155, speed: 195, damage: 1.0, crit: 0.03, magnet: 75 },
    color: "#fb923c",
    unlockCondition: "Survive for 5 minutes in a single run",
    unlocked: false,
  },
  {
    id: "overseer",
    name: "Overseer",
    desc: "Summoner who commands drones. Passive: +1 starting drone. Starts with Combat Drone.",
    startWeapon: "drone",
    passive: { id: "commander", desc: "+1 starting drone, drones deal +25% damage" },
    baseStats: { maxHp: 95, speed: 220, damage: 1.0, crit: 0.04, magnet: 120 },
    color: "#a78bfa",
    unlockCondition: "Complete 3 runs (any outcome)",
    unlocked: false,
  },
];

export const CHAR_MAP = Object.fromEntries(CHARACTERS.map((c) => [c.id, c])) as Record<string, CharacterDef>;

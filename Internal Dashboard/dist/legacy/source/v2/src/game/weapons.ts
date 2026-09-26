// ───────────────────────────────────────────────────────────────────
// VOIDFALL v1.0 — 10 Weapons with evolution paths
// ───────────────────────────────────────────────────────────────────
import type { WeaponDef } from "./types";

export const WEAPONS: WeaponDef[] = [
  {
    id: "pistol",
    name: "Pulse Pistol",
    icon: "⊕",
    rarity: "common",
    baseDamage: 14,
    baseCooldown: 0.45,
    baseRange: 520,
    basePierce: 0,
    baseArea: 1,
    description: "Reliable single-target. Fires at nearest enemy.",
    evolution: {
      id: "starbreaker",
      name: "★ Starbreaker",
      condition: { weaponLevel: 8, passive: "damage", passiveLevel: 5 },
      description: "Every 3rd shot fires a massive piercing beam that explodes.",
    },
  },
  {
    id: "shotgun",
    name: "Scatter Shotgun",
    icon: "◆",
    rarity: "common",
    baseDamage: 10,
    baseCooldown: 0.9,
    baseRange: 200,
    basePierce: 0,
    baseArea: 6,
    description: "Short-range cone with heavy knockback.",
    evolution: {
      id: "cataclysm",
      name: "★ Cataclysm Cannon",
      condition: { weaponLevel: 8, passive: "areaSize", passiveLevel: 5 },
      description: "Fires a devastating wide cone with explosive pellets.",
    },
  },
  {
    id: "smg",
    name: "Rapid SMG",
    icon: "≡",
    rarity: "common",
    baseDamage: 5,
    baseCooldown: 0.09,
    baseRange: 400,
    basePierce: 0,
    baseArea: 1,
    description: "High fire rate, low damage. Crit chance +10%.",
    evolution: {
      id: "bulletstorm",
      name: "★ Bullet Storm",
      condition: { weaponLevel: 8, passive: "critChance", passiveLevel: 5 },
      description: "Every bullet has +15% crit. Crits chain to nearby enemies.",
    },
  },
  {
    id: "railgun",
    name: "Railgun",
    icon: "║",
    rarity: "rare",
    baseDamage: 85,
    baseCooldown: 2.0,
    baseRange: 700,
    basePierce: 10,
    baseArea: 1,
    description: "Slow, devastating, pierces everything in a line.",
    evolution: {
      id: "eventhorizon",
      name: "★ Event Horizon",
      condition: { weaponLevel: 8, passive: "pierce", passiveLevel: 5 },
      description: "Creates a gravity well at the endpoint, pulling and damaging enemies.",
    },
  },
  {
    id: "arc",
    name: "Arc Cannon",
    icon: "⚡",
    rarity: "rare",
    baseDamage: 20,
    baseCooldown: 0.7,
    baseRange: 300,
    basePierce: 0,
    baseArea: 1,
    description: "Lightning that chains between nearby enemies (3 chains).",
    evolution: {
      id: "thunderdominion",
      name: "★ Thunder Dominion",
      condition: { weaponLevel: 8, passive: "areaSize", passiveLevel: 4 },
      description: "Chains to 7 enemies, each chain deals +20% damage.",
    },
  },
  {
    id: "flame",
    name: "Flamethrower",
    icon: "🔥",
    rarity: "rare",
    baseDamage: 3,
    baseCooldown: 0.06,
    baseRange: 140,
    basePierce: 0,
    baseArea: 3,
    description: "Continuous short-range damage that applies burning.",
    evolution: {
      id: "dragonsbreath",
      name: "★ Dragon's Breath",
      condition: { weaponLevel: 8, passive: "statusDmg", passiveLevel: 5 },
      description: "Burns spread to nearby enemies and deal 3× damage.",
    },
  },
  {
    id: "rocket",
    name: "Rocket Launcher",
    icon: "▲",
    rarity: "rare",
    baseDamage: 45,
    baseCooldown: 1.5,
    baseRange: 450,
    basePierce: 0,
    baseArea: 5,
    description: "Explosive projectile with area damage.",
    evolution: {
      id: "apocalypse",
      name: "★ Apocalypse Engine",
      condition: { weaponLevel: 8, passive: "areaSize", passiveLevel: 6 },
      description: "Fires 3 rockets in a spread. Explosions leave lingering fire.",
    },
  },
  {
    id: "blades",
    name: "Orbit Blades",
    icon: "◎",
    rarity: "common",
    baseDamage: 18,
    baseCooldown: 0,
    baseRange: 0,
    basePierce: 0,
    baseArea: 1,
    description: "3 orbiting blades damage nearby enemies continuously.",
    evolution: {
      id: "solarguillotine",
      name: "★ Solar Guillotine",
      condition: { weaponLevel: 8, passive: "damage", passiveLevel: 5 },
      description: "6 massive orbiting blades + periodic outward slash wave.",
    },
  },
  {
    id: "drone",
    name: "Combat Drone",
    icon: "✦",
    rarity: "epic",
    baseDamage: 8,
    baseCooldown: 0.5,
    baseRange: 450,
    basePierce: 0,
    baseArea: 1,
    description: "Autonomous drone fires at enemies independently.",
    evolution: {
      id: "warswarm",
      name: "★ Autonomous War Swarm",
      condition: { weaponLevel: 8, passive: "extraChoices", passiveLevel: 3 },
      description: "4 drones fire in synchronized barrages with homing.",
    },
  },
  {
    id: "mines",
    name: "Mine Layer",
    icon: "◈",
    rarity: "epic",
    baseDamage: 55,
    baseCooldown: 1.8,
    baseRange: 80,
    basePierce: 0,
    baseArea: 3,
    description: "Drops proximity mines behind you. Detonate on contact.",
    evolution: {
      id: "deadzone",
      name: "★ Dead Zone Protocol",
      condition: { weaponLevel: 8, passive: "areaSize", passiveLevel: 5 },
      description: "Mines create a persistent minefield. Chain detonations.",
    },
  },
];

export const WEAPON_MAP = Object.fromEntries(WEAPONS.map((w) => [w.id, w])) as Record<string, WeaponDef>;

// Weapon stat scaling per level
export function weaponStats(id: string, level: number, evolved: boolean) {
  const w = WEAPON_MAP[id];
  if (!w) return { damage: 10, cooldown: 0.5, range: 300, pierce: 0, area: 1, count: 1 };
  const m = evolved ? 1.8 : 1;
  const l = level - 1;
  switch (id) {
    case "pistol":
      return {
        damage: w.baseDamage * (1 + l * 0.22) * m,
        cooldown: w.baseCooldown * Math.pow(0.94, l),
        range: w.baseRange * (1 + l * 0.04),
        pierce: evolved ? 3 + Math.floor(l * 0.5) : Math.floor(l / 3),
        area: 1,
        count: evolved ? 3 : 1 + Math.floor(l / 3),
      };
    case "shotgun":
      return {
        damage: w.baseDamage * (1 + l * 0.18) * m,
        cooldown: w.baseCooldown * Math.pow(0.95, l),
        range: w.baseRange * (1 + l * 0.06),
        pierce: 0,
        area: 1 + l * 0.15,
        count: evolved ? 12 : 5 + Math.floor(l / 2),
      };
    case "smg":
      return {
        damage: w.baseDamage * (1 + l * 0.12) * m,
        cooldown: w.baseCooldown * Math.pow(0.96, l),
        range: w.baseRange * (1 + l * 0.03),
        pierce: evolved ? 1 : 0,
        area: 1,
        count: 1,
      };
    case "railgun":
      return {
        damage: w.baseDamage * (1 + l * 0.28) * m,
        cooldown: w.baseCooldown * Math.pow(0.93, l),
        range: w.baseRange * (1 + l * 0.05),
        pierce: w.basePierce + l * 2,
        area: 1,
        count: evolved ? 2 : 1,
      };
    case "arc":
      return {
        damage: w.baseDamage * (1 + l * 0.2) * m,
        cooldown: w.baseCooldown * Math.pow(0.95, l),
        range: w.baseRange * (1 + l * 0.08),
        pierce: 0,
        area: 1,
        count: evolved ? 7 : 3 + Math.floor(l / 2),
      };
    case "flame":
      return {
        damage: w.baseDamage * (1 + l * 0.15) * m,
        cooldown: Math.max(0.03, w.baseCooldown * Math.pow(0.96, l)),
        range: w.baseRange * (1 + l * 0.08),
        pierce: 0,
        area: 1 + l * 0.2,
        count: 1,
      };
    case "rocket":
      return {
        damage: w.baseDamage * (1 + l * 0.25) * m,
        cooldown: w.baseCooldown * Math.pow(0.94, l),
        range: w.baseRange * (1 + l * 0.04),
        pierce: 0,
        area: 1 + l * 0.2,
        count: evolved ? 3 : 1,
      };
    case "blades":
      return {
        damage: w.baseDamage * (1 + l * 0.2) * m,
        cooldown: 0,
        range: 82 * (1 + l * 0.05),
        pierce: 0,
        area: 1,
        count: evolved ? 6 : 3 + Math.floor(l / 3),
      };
    case "drone":
      return {
        damage: w.baseDamage * (1 + l * 0.18) * m,
        cooldown: w.baseCooldown * Math.pow(0.93, l),
        range: w.baseRange * (1 + l * 0.04),
        pierce: evolved ? 2 : 0,
        area: 1,
        count: evolved ? 4 : 1 + Math.floor(l / 4),
      };
    case "mines":
      return {
        damage: w.baseDamage * (1 + l * 0.22) * m,
        cooldown: w.baseCooldown * Math.pow(0.94, l),
        range: w.baseRange * (1 + l * 0.1),
        pierce: 0,
        area: 1 + l * 0.2,
        count: evolved ? 2 : 1,
      };
    default:
      return { damage: 10, cooldown: 0.5, range: 300, pierce: 0, area: 1, count: 1 };
  }
}

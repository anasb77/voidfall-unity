// ───────────────────────────────────────────────────────────────────
// VOIDFALL v1.1 — 16 Weapons with evolution paths
// ───────────────────────────────────────────────────────────────────
import type { WeaponDef } from "./types";

export const WEAPONS: WeaponDef[] = [
  {
    id: "pistol", name: "Pulse Pistol", icon: "⊕", rarity: "common",
    baseDamage: 14, baseCooldown: 0.45, baseRange: 520, basePierce: 0, baseArea: 1,
    description: "Reliable single-target. Fires at nearest enemy.",
    evolution: { id: "starbreaker", name: "★ Starbreaker",
      condition: { weaponLevel: 8, passive: "damage", passiveLevel: 5 },
      description: "Every 3rd shot fires a massive piercing beam that explodes." },
  },
  {
    id: "shotgun", name: "Scatter Shotgun", icon: "◆", rarity: "common",
    baseDamage: 10, baseCooldown: 0.9, baseRange: 200, basePierce: 0, baseArea: 6,
    description: "Short-range cone with heavy knockback.",
    evolution: { id: "cataclysm", name: "★ Cataclysm Cannon",
      condition: { weaponLevel: 8, passive: "areaSize", passiveLevel: 5 },
      description: "Fires a devastating wide cone with explosive pellets." },
  },
  {
    id: "smg", name: "Rapid SMG", icon: "≡", rarity: "common",
    baseDamage: 5, baseCooldown: 0.09, baseRange: 400, basePierce: 0, baseArea: 1,
    description: "High fire rate, low damage. Crit chance +10%.",
    evolution: { id: "bulletstorm", name: "★ Bullet Storm",
      condition: { weaponLevel: 8, passive: "critChance", passiveLevel: 5 },
      description: "Every bullet has +15% crit. Crits chain to nearby enemies." },
  },
  {
    id: "railgun", name: "Railgun", icon: "║", rarity: "rare",
    baseDamage: 85, baseCooldown: 2.0, baseRange: 700, basePierce: 10, baseArea: 1,
    description: "Slow, devastating, pierces everything in a line.",
    evolution: { id: "eventhorizon", name: "★ Event Horizon",
      condition: { weaponLevel: 8, passive: "pierce", passiveLevel: 5 },
      description: "Creates a gravity well at the endpoint, pulling and damaging enemies." },
  },
  {
    id: "arc", name: "Arc Cannon", icon: "⚡", rarity: "rare",
    baseDamage: 20, baseCooldown: 0.7, baseRange: 300, basePierce: 0, baseArea: 1,
    description: "Lightning that chains between nearby enemies (3 chains).",
    evolution: { id: "thunderdominion", name: "★ Thunder Dominion",
      condition: { weaponLevel: 8, passive: "areaSize", passiveLevel: 4 },
      description: "Chains to 7 enemies, each chain deals +20% damage." },
  },
  {
    id: "flame", name: "Flamethrower", icon: "🔥", rarity: "rare",
    baseDamage: 3, baseCooldown: 0.06, baseRange: 140, basePierce: 0, baseArea: 3,
    description: "Continuous short-range damage that applies burning.",
    evolution: { id: "dragonsbreath", name: "★ Dragon's Breath",
      condition: { weaponLevel: 8, passive: "statusDmg", passiveLevel: 5 },
      description: "Burns spread to nearby enemies and deal 3× damage." },
  },
  {
    id: "rocket", name: "Rocket Launcher", icon: "▲", rarity: "rare",
    baseDamage: 45, baseCooldown: 1.5, baseRange: 450, basePierce: 0, baseArea: 5,
    description: "Explosive projectile with area damage.",
    evolution: { id: "apocalypse", name: "★ Apocalypse Engine",
      condition: { weaponLevel: 8, passive: "areaSize", passiveLevel: 6 },
      description: "Fires 3 rockets in a spread. Explosions leave lingering fire." },
  },
  {
    id: "blades", name: "Orbit Blades", icon: "◎", rarity: "common",
    baseDamage: 18, baseCooldown: 0, baseRange: 0, basePierce: 0, baseArea: 1,
    description: "3 orbiting blades damage nearby enemies continuously.",
    evolution: { id: "solarguillotine", name: "★ Solar Guillotine",
      condition: { weaponLevel: 8, passive: "damage", passiveLevel: 5 },
      description: "6 massive orbiting blades + periodic outward slash wave." },
  },
  {
    id: "drone", name: "Combat Drone", icon: "✦", rarity: "epic",
    baseDamage: 8, baseCooldown: 0.5, baseRange: 450, basePierce: 0, baseArea: 1,
    description: "Autonomous drone fires at enemies independently.",
    evolution: { id: "warswarm", name: "★ Autonomous War Swarm",
      condition: { weaponLevel: 8, passive: "extraChoices", passiveLevel: 3 },
      description: "4 drones fire in synchronized barrages with homing." },
  },
  {
    id: "mines", name: "Mine Layer", icon: "◈", rarity: "epic",
    baseDamage: 55, baseCooldown: 1.8, baseRange: 80, basePierce: 0, baseArea: 3,
    description: "Drops proximity mines behind you. Detonate on contact.",
    evolution: { id: "deadzone", name: "★ Dead Zone Protocol",
      condition: { weaponLevel: 8, passive: "areaSize", passiveLevel: 5 },
      description: "Mines create a persistent minefield. Chain detonations." },
  },
  {
    id: "ice", name: "Ice Lance", icon: "❄", rarity: "rare",
    baseDamage: 25, baseCooldown: 0.65, baseRange: 480, basePierce: 2, baseArea: 1,
    description: "Piercing frost bolt that slows enemies on hit.",
    evolution: { id: "glacier", name: "★ Glacier Shard",
      condition: { weaponLevel: 8, passive: "statusDmg", passiveLevel: 5 },
      description: "Frozen enemies shatter on kill, launching shrapnel." },
  },
  {
    id: "nova", name: "Void Nova", icon: "✸", rarity: "epic",
    baseDamage: 35, baseCooldown: 2.5, baseRange: 0, basePierce: 0, baseArea: 8,
    description: "Radial burst damages all nearby enemies. Area scales.",
    evolution: { id: "supernova", name: "★ Supernova Collapse",
      condition: { weaponLevel: 8, passive: "areaSize", passiveLevel: 5 },
      description: "Pulls enemies inward then explodes with massive force." },
  },
  {
    id: "crossbow", name: "Void Crossbow", icon: "⟋", rarity: "uncommon",
    baseDamage: 40, baseCooldown: 1.2, baseRange: 600, basePierce: 3, baseArea: 1,
    description: "Slow-firing heavy bolt with high pierce.",
    evolution: { id: "arbalest", name: "★ Void Arbalest",
      condition: { weaponLevel: 8, passive: "pierce", passiveLevel: 4 },
      description: "Fires 3 bolts in a spread. Each bolt pins enemies briefly." },
  },
  {
    id: "tendrils", name: "Void Tendrils", icon: "〰", rarity: "epic",
    baseDamage: 12, baseCooldown: 0.35, baseRange: 160, basePierce: 0, baseArea: 2,
    description: "Persistent damaging zones that pulse around you.",
    evolution: { id: "abyssal", name: "★ Abyssal Garden",
      condition: { weaponLevel: 8, passive: "areaSize", passiveLevel: 6 },
      description: "Tendrils follow you and drain enemy HP to heal you." },
  },
  {
    id: "plasma", name: "Plasma Beam", icon: "║", rarity: "rare",
    baseDamage: 4, baseCooldown: 0.02, baseRange: 350, basePierce: 0, baseArea: 1,
    description: "Continuous beam with ramping damage the longer it hits one target.",
    evolution: { id: "annihilator", name: "★ Annihilator Beam",
      condition: { weaponLevel: 8, passive: "damage", passiveLevel: 5 },
      description: "Beam splits into 3 parallel beams at max charge." },
  },
  {
    id: "thunder", name: "Thunderstrike", icon: "☇", rarity: "uncommon",
    baseDamage: 50, baseCooldown: 2.0, baseRange: 500, basePierce: 0, baseArea: 1,
    description: "Lightning strikes random enemies from above.",
    evolution: { id: "stormlord", name: "★ Storm Lord",
      condition: { weaponLevel: 8, passive: "critChance", passiveLevel: 4 },
      description: "Strikes create chain lightning between all struck enemies." },
  },
  {
    id: "chain", name: "Chain Spike", icon: "⛓", rarity: "rare",
    baseDamage: 35, baseCooldown: 1.2, baseRange: 280, basePierce: 0, baseArea: 1,
    description: "Throws a spike that chains back to you, hitting enemies twice.",
    evolution: { id: "harpoon", name: "★ Void Harpoon",
      condition: { weaponLevel: 8, passive: "pierce", passiveLevel: 4 },
      description: "Pulls hit enemies toward you on return. Chains 3×." },
  },
  {
    id: "voidorb", name: "Void Orb", icon: "●", rarity: "epic",
    baseDamage: 22, baseCooldown: 1.8, baseRange: 350, basePierce: 0, baseArea: 2,
    description: "Launches a slow-moving orb that orbits and damages enemies.",
    evolution: { id: "blackhole", name: "★ Black Hole",
      condition: { weaponLevel: 8, passive: "areaSize", passiveLevel: 5 },
      description: "Orb becomes a black hole, pulling enemies inward before exploding." },
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
    case "pistol": return {
      damage: w.baseDamage * (1 + l * 0.22) * m,
      cooldown: w.baseCooldown * Math.pow(0.94, l),
      range: w.baseRange * (1 + l * 0.04),
      pierce: evolved ? 3 + Math.floor(l * 0.5) : Math.floor(l / 3),
      area: 1, count: evolved ? 3 : 1 + Math.floor(l / 3),
    };
    case "shotgun": return {
      damage: w.baseDamage * (1 + l * 0.18) * m,
      cooldown: w.baseCooldown * Math.pow(0.95, l),
      range: w.baseRange * (1 + l * 0.06),
      pierce: 0, area: 1 + l * 0.15,
      count: evolved ? 12 : 5 + Math.floor(l / 2),
    };
    case "smg": return {
      damage: w.baseDamage * (1 + l * 0.12) * m,
      cooldown: w.baseCooldown * Math.pow(0.96, l),
      range: w.baseRange * (1 + l * 0.03),
      pierce: evolved ? 1 : 0, area: 1, count: 1,
    };
    case "railgun": return {
      damage: w.baseDamage * (1 + l * 0.28) * m,
      cooldown: w.baseCooldown * Math.pow(0.93, l),
      range: w.baseRange * (1 + l * 0.05),
      pierce: w.basePierce + l * 2, area: 1, count: evolved ? 2 : 1,
    };
    case "arc": return {
      damage: w.baseDamage * (1 + l * 0.2) * m,
      cooldown: w.baseCooldown * Math.pow(0.95, l),
      range: w.baseRange * (1 + l * 0.08),
      pierce: 0, area: 1, count: evolved ? 7 : 3 + Math.floor(l / 2),
    };
    case "flame": return {
      damage: w.baseDamage * (1 + l * 0.15) * m,
      cooldown: Math.max(0.03, w.baseCooldown * Math.pow(0.96, l)),
      range: w.baseRange * (1 + l * 0.08),
      pierce: 0, area: 1 + l * 0.2, count: 1,
    };
    case "rocket": return {
      damage: w.baseDamage * (1 + l * 0.25) * m,
      cooldown: w.baseCooldown * Math.pow(0.94, l),
      range: w.baseRange * (1 + l * 0.04),
      pierce: 0, area: 1 + l * 0.2, count: evolved ? 3 : 1,
    };
    case "blades": return {
      damage: w.baseDamage * (1 + l * 0.2) * m,
      cooldown: 0,
      range: 82 * (1 + l * 0.05),
      pierce: 0, area: 1, count: evolved ? 6 : 3 + Math.floor(l / 3),
    };
    case "drone": return {
      damage: w.baseDamage * (1 + l * 0.18) * m,
      cooldown: w.baseCooldown * Math.pow(0.93, l),
      range: w.baseRange * (1 + l * 0.04),
      pierce: evolved ? 2 : 0, area: 1, count: evolved ? 4 : 1 + Math.floor(l / 4),
    };
    case "mines": return {
      damage: w.baseDamage * (1 + l * 0.22) * m,
      cooldown: w.baseCooldown * Math.pow(0.94, l),
      range: w.baseRange * (1 + l * 0.1),
      pierce: 0, area: 1 + l * 0.2, count: evolved ? 2 : 1,
    };
    case "ice": return {
      damage: w.baseDamage * (1 + l * 0.2) * m,
      cooldown: w.baseCooldown * Math.pow(0.95, l),
      range: w.baseRange * (1 + l * 0.05),
      pierce: w.basePierce + Math.floor(l / 2), area: 1, count: evolved ? 3 : 1,
    };
    case "nova": return {
      damage: w.baseDamage * (1 + l * 0.22) * m,
      cooldown: w.baseCooldown * Math.pow(0.94, l),
      range: 0, area: 1 + l * 0.25, pierce: 0, count: 1,
    };
    case "crossbow": return {
      damage: w.baseDamage * (1 + l * 0.26) * m,
      cooldown: w.baseCooldown * Math.pow(0.95, l),
      range: w.baseRange * (1 + l * 0.03),
      pierce: w.basePierce + l, area: 1, count: evolved ? 3 : 1,
    };
    case "tendrils": return {
      damage: w.baseDamage * (1 + l * 0.15) * m,
      cooldown: w.baseCooldown * Math.pow(0.96, l),
      range: w.baseRange * (1 + l * 0.08),
      pierce: 0, area: 1 + l * 0.15, count: 1,
    };
    case "plasma": return {
      damage: w.baseDamage * (1 + l * 0.1) * m,
      cooldown: w.baseCooldown,
      range: w.baseRange * (1 + l * 0.05),
      pierce: 0, area: 1, count: evolved ? 3 : 1,
    };
    case "thunder": return {
      damage: w.baseDamage * (1 + l * 0.25) * m,
      cooldown: w.baseCooldown * Math.pow(0.94, l),
      range: w.baseRange, pierce: 0, area: 1, count: evolved ? 4 : 1,
    };
    case "chain": return {
      damage: w.baseDamage * (1 + l * 0.2) * m,
      cooldown: w.baseCooldown * Math.pow(0.95, l),
      range: w.baseRange * (1 + l * 0.06),
      pierce: 0, area: 1, count: evolved ? 3 : 1,
    };
    case "voidorb": return {
      damage: w.baseDamage * (1 + l * 0.22) * m,
      cooldown: w.baseCooldown * Math.pow(0.95, l),
      range: w.baseRange * (1 + l * 0.08),
      pierce: 0, area: 1 + l * 0.15, count: evolved ? 2 : 1,
    };
    default: return { damage: 10, cooldown: 0.5, range: 300, pierce: 0, area: 1, count: 1 };
  }
}

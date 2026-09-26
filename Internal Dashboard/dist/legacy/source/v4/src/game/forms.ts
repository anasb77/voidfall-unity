// ───────────────────────────────────────────────────────────────────
// VOIDFALL v1.1 — Player Forms (replacing Characters)
// Each form has unique stats, a starting weapon, and a magic ability.
// ───────────────────────────────────────────────────────────────────
import type { WeaponId } from "./types";

export type FormId = "sentinel" | "phantom" | "bastion" | "overseer" | "wraith" | "elemental" | "chronomancer" | "necromancer" | "missile" | "spider" | "tank";

export interface MagicDef {
  name: string;
  desc: string;
  cooldown: number;
  duration: number;
  icon: string;
  color: string;
  // what the magic actually does is coded in the engine
}

export interface FormDef {
  id: FormId;
  name: string;
  desc: string;
  // stat multipliers (base HP=100, base speed=235, base dmg=1)
  hp: number;
  speed: number;
  dmg: number;
  crit: number;   // base crit chance
  armor: number;  // flat damage reduction
  regen: number;  // % HP/s
  magnet: number;
  // weapon & magic
  startWeapon: WeaponId;
  magic: MagicDef;
  // visuals
  color: string;
  glowColor: string;
  // unlock
  unlocked: boolean;
  unlockCondition?: string;
}

export const FORMS: FormDef[] = [
  {
    id: "sentinel", name: "Sentinel", desc: "Balanced warrior. The void's steadfast guardian.",
    hp: 1.15, speed: 1.0, dmg: 1.05, crit: 0.05, armor: 0, regen: 0, magnet: 1.0,
    startWeapon: "pistol",
    magic: { name: "Void Shield", desc: "Creates an absorbing barrier (2s, blocks 50 dmg).", cooldown: 22, duration: 2, icon: "◇", color: "#22d3ee" },
    color: "#22d3ee", glowColor: "#67e8f9",
    unlocked: true,
  },
  {
    id: "phantom", name: "Phantom", desc: "Fast and elusive. Critical strike specialist.",
    hp: 0.82, speed: 1.15, dmg: 0.92, crit: 0.14, armor: 0, regen: 0, magnet: 1.12,
    startWeapon: "smg",
    magic: { name: "Phase Dash", desc: "Teleport 200 units in movement direction (0.5s iframes).", cooldown: 10, duration: 0.5, icon: "⟐", color: "#e879f9" },
    color: "#e879f9", glowColor: "#f0abfc",
    unlocked: true,
  },
  {
    id: "bastion", name: "Bastion", desc: "Armored juggernaut. Devastating at close range.",
    hp: 1.32, speed: 0.82, dmg: 1.0, crit: 0.03, armor: 4, regen: 0, magnet: 0.8,
    startWeapon: "shotgun",
    magic: { name: "Shockwave", desc: "Massive radial damage + knockback (8s cooldown).", cooldown: 8, duration: 0.15, icon: "◎", color: "#fb923c" },
    color: "#fb923c", glowColor: "#fdba74",
    unlocked: true,
  },
  {
    id: "overseer", name: "Overseer", desc: "Commander of the void swarm. Drone master.",
    hp: 0.92, speed: 0.98, dmg: 1.0, crit: 0.04, armor: 0, regen: 0, magnet: 1.2,
    startWeapon: "drone",
    magic: { name: "Swarm Surge", desc: "Summon 3 temporary void drones for 8s.", cooldown: 30, duration: 8, icon: "✦", color: "#a78bfa" },
    color: "#a78bfa", glowColor: "#c4b5fd",
    unlocked: true,
  },
  {
    id: "wraith", name: "Wraith", desc: "Death incarnate. Leaves mines and curses.",
    hp: 0.9, speed: 1.08, dmg: 1.08, crit: 0.06, armor: 0, regen: 0, magnet: 0.95,
    startWeapon: "mines",
    magic: { name: "Death Mark", desc: "Mark all enemies on screen; they take +50% damage for 5s.", cooldown: 25, duration: 5, icon: "☠", color: "#f43f5e" },
    color: "#f43f5e", glowColor: "#fda4af",
    unlocked: true,
  },
  {
    id: "elemental", name: "Elemental", desc: "Master of frost and flame. Area control specialist.",
    hp: 1.0, speed: 1.0, dmg: 1.02, crit: 0.06, armor: 0, regen: 0, magnet: 1.0,
    startWeapon: "ice",
    magic: { name: "Frost Nova", desc: "Freeze all enemies within range for 3s.", cooldown: 18, duration: 3, icon: "❄", color: "#67e8f9" },
    color: "#67e8f9", glowColor: "#a5f3fc",
    unlocked: true,
  },
  {
    id: "chronomancer", name: "Chronomancer", desc: "Bends time itself. Slows enemies and accelerates self.",
    hp: 0.88, speed: 1.05, dmg: 0.95, crit: 0.07, armor: 0, regen: 0, magnet: 1.05,
    startWeapon: "crossbow",
    magic: { name: "Time Warp", desc: "Slow all enemies by 60% for 4s. You move normally.", cooldown: 20, duration: 4, icon: "⏳", color: "#facc15" },
    color: "#facc15", glowColor: "#fde047",
    unlocked: true,
  },
  {
    id: "necromancer", name: "Necromancer", desc: "Raises the dead. Summons minions from fallen foes.",
    hp: 0.95, speed: 0.95, dmg: 1.0, crit: 0.05, armor: 0, regen: 0.003, magnet: 1.1,
    startWeapon: "tendrils",
    magic: { name: "Raise Dead", desc: "Summon 2-4 void minions from killed enemies (lasts 10s).", cooldown: 28, duration: 10, icon: "♱", color: "#34d399" },
    color: "#34d399", glowColor: "#6ee7b7",
    unlocked: true,
  },
  {
    id: "missile", name: "Missile", desc: "Glass cannon. Massive speed and projectile speed but fragile.",
    hp: 0.7, speed: 1.2, dmg: 1.15, crit: 0.08, armor: 0, regen: 0, magnet: 0.9,
    startWeapon: "rocket",
    magic: { name: "Rocket Barrage", desc: "Fire 8 homing missiles in all directions.", cooldown: 20, duration: 0.5, icon: "🚀", color: "#ef4444" },
    color: "#ef4444", glowColor: "#fca5a5",
    unlocked: true,
  },
  {
    id: "spider", name: "Spider", desc: "Webs of death. Slows enemies and traps them.",
    hp: 0.95, speed: 1.05, dmg: 0.95, crit: 0.06, armor: 0, regen: 0, magnet: 1.05,
    startWeapon: "mines",
    magic: { name: "Web Trap", desc: "Create a web zone that slows and damages enemies for 6s.", cooldown: 18, duration: 6, icon: "🕸", color: "#8b5cf6" },
    color: "#8b5cf6", glowColor: "#c4b5fd",
    unlocked: true,
  },
  {
    id: "tank", name: "Tank", desc: "Indestructible fortress. Slow but unkillable.",
    hp: 1.5, speed: 0.75, dmg: 0.85, crit: 0.03, armor: 6, regen: 0.005, magnet: 0.8,
    startWeapon: "railgun",
    magic: { name: "Fortify", desc: "Gain +50 armor and immovable stance for 4s.", cooldown: 25, duration: 4, icon: "🛡", color: "#64748b" },
    color: "#64748b", glowColor: "#94a3b8",
    unlocked: true,
  },
];

export const FORM_MAP = Object.fromEntries(FORMS.map((f) => [f.id, f])) as Record<FormId, FormDef>;

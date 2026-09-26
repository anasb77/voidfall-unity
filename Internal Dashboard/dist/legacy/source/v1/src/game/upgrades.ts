import {
  Split,
  Zap,
  Crosshair,
  ChevronsRight,
  Wind,
  Magnet,
  Heart,
  Orbit,
  Sparkles,
  HeartPulse,
  type LucideIcon,
} from "lucide-react";

export interface UpgradeDef {
  id: string;
  name: string;
  icon: LucideIcon;
  max: number;
  weight: number;
  desc: (nextLevel: number) => string;
  tag?: string;
}

export const UPGRADES: UpgradeDef[] = [
  {
    id: "split",
    name: "Split Shot",
    icon: Split,
    max: 3,
    weight: 10,
    desc: (l) => `Fire ${l + 1} projectiles per volley`,
    tag: "WEAPON",
  },
  {
    id: "rapid",
    name: "Overclock",
    icon: Zap,
    max: 5,
    weight: 10,
    desc: () => "+18% fire rate",
    tag: "WEAPON",
  },
  {
    id: "heavy",
    name: "Void Rounds",
    icon: Crosshair,
    max: 5,
    weight: 10,
    desc: () => "+30% bullet damage",
    tag: "WEAPON",
  },
  {
    id: "pierce",
    name: "Phase Rounds",
    icon: ChevronsRight,
    max: 3,
    weight: 7,
    desc: (l) => `Bullets pierce ${l} extra ${l === 1 ? "enemy" : "enemies"}`,
    tag: "WEAPON",
  },
  {
    id: "crit",
    name: "Crit Matrix",
    icon: Sparkles,
    max: 3,
    weight: 7,
    desc: () => "+10% critical chance (2.2× dmg)",
    tag: "WEAPON",
  },
  {
    id: "blades",
    name: "Orbit Blades",
    icon: Orbit,
    max: 3,
    weight: 8,
    desc: (l) => (l === 1 ? "Summon an orbiting blade" : `+1 orbiting blade (${l + 1} total)`),
    tag: "SUMMON",
  },
  {
    id: "swift",
    name: "Slipstream",
    icon: Wind,
    max: 4,
    weight: 7,
    desc: () => "+10% move speed",
    tag: "BODY",
  },
  {
    id: "magnet",
    name: "Magnet Core",
    icon: Magnet,
    max: 3,
    weight: 7,
    desc: () => "+45% gem pickup radius",
    tag: "BODY",
  },
  {
    id: "vitality",
    name: "Vital Surge",
    icon: Heart,
    max: 4,
    weight: 8,
    desc: () => "+25 max HP and restore 25 HP",
    tag: "BODY",
  },
  {
    id: "patch",
    name: "Patch Kit",
    icon: HeartPulse,
    max: 99,
    weight: 0, // only offered as a fallback
    desc: () => "Restore 40 HP right now",
    tag: "INSTANT",
  },
];

export const UPGRADE_MAP: Record<string, UpgradeDef> = Object.fromEntries(
  UPGRADES.map((u) => [u.id, u])
);

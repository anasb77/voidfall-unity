export type GamePhase = "menu" | "playing" | "levelup" | "paused" | "gameover";

export interface HudRefs {
  hpBar: HTMLElement | null;
  hpGhost: HTMLElement | null;
  hpText: HTMLElement | null;
  xpBar: HTMLElement | null;
  levelText: HTMLElement | null;
  timeText: HTMLElement | null;
  scoreText: HTMLElement | null;
  killText: HTMLElement | null;
  hurtFlash: HTMLElement | null;
}

export interface HighScore {
  score: number;
  kills: number;
  time: number;
  level: number;
  date: number;
}

export interface RunStats {
  score: number;
  kills: number;
  time: number;
  level: number;
}

export interface Toast {
  id: number;
  text: string;
  kind: "info" | "danger" | "gem";
}

export interface EngineEvents {
  onPhaseChange: (phase: GamePhase) => void;
  onLevelUp: (optionIds: string[]) => void;
  onGameOver: (stats: RunStats, rank: number, isBest: boolean) => void;
  onToast: (text: string, kind: Toast["kind"]) => void;
  onScores: (scores: HighScore[]) => void;
  onUpgrades: (ids: string[]) => void;
  onMute: (muted: boolean) => void;
}

export const PHASE_IDLE_RENDER: GamePhase[] = ["menu", "paused", "levelup", "gameover"];

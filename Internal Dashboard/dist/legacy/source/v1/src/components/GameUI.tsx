import { useEffect, useRef, useState } from "react";
import {
  Play,
  Pause,
  RotateCcw,
  Home,
  Volume2,
  VolumeX,
  Trophy,
  Skull,
  Timer,
  Heart,
  Keyboard,
  Smartphone,
  ChevronRight,
  Crown,
  type LucideIcon,
} from "lucide-react";
import { Game } from "../game/engine";
import { UPGRADE_MAP } from "../game/upgrades";
import type { GamePhase, HighScore, HudRefs, RunStats, Toast } from "../game/types";

function fmtTime(sec: number) {
  const m = Math.floor(sec / 60);
  const s = Math.floor(sec % 60);
  return `${m}:${s.toString().padStart(2, "0")}`;
}

/* ------------------------------------------------------------------ */
/* Score table                                                         */
/* ------------------------------------------------------------------ */
function ScoreTable({ scores, highlight }: { scores: HighScore[]; highlight?: number }) {
  if (scores.length === 0) {
    return (
      <div className="py-6 text-center text-[13px] tracking-widest text-slate-500 font-display">
        NO RECORDED RUNS — BE THE FIRST TO FALL
      </div>
    );
  }
  return (
    <div className="overflow-hidden rounded-lg border border-cyan-200/10">
      <table className="w-full text-left text-[13px]">
        <thead>
          <tr className="bg-cyan-400/5 font-display text-[10px] tracking-[0.2em] text-cyan-200/50">
            <th className="px-3 py-2 font-semibold">#</th>
            <th className="px-3 py-2 font-semibold">SCORE</th>
            <th className="px-3 py-2 font-semibold">KILLS</th>
            <th className="px-3 py-2 font-semibold">TIME</th>
            <th className="px-3 py-2 font-semibold text-right">LV</th>
          </tr>
        </thead>
        <tbody>
          {scores.slice(0, 5).map((s, i) => (
            <tr
              key={s.date + "-" + i}
              className={
                highlight === i
                  ? "bg-yellow-300/10 text-yellow-200"
                  : i === 0
                    ? "text-cyan-100"
                    : "text-slate-400"
              }
            >
              <td className="px-3 py-1.5 font-display">
                <span className="inline-flex items-center gap-1">
                  {i === 0 && <Crown size={12} className="text-yellow-300" />}
                  {i + 1}
                </span>
              </td>
              <td className="px-3 py-1.5 font-display font-bold">{s.score.toLocaleString()}</td>
              <td className="px-3 py-1.5">{s.kills}</td>
              <td className="px-3 py-1.5">{fmtTime(s.time)}</td>
              <td className="px-3 py-1.5 text-right">{s.level}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/* Icon button                                                         */
/* ------------------------------------------------------------------ */
function IconBtn({
  icon: Icon,
  label,
  onClick,
}: {
  icon: LucideIcon;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      aria-label={label}
      className="pointer-events-auto flex h-11 w-11 items-center justify-center rounded-lg border border-slate-400/20 bg-slate-900/60 text-slate-300 backdrop-blur-md transition hover:border-cyan-300/50 hover:text-cyan-200 active:scale-95"
    >
      <Icon size={18} />
    </button>
  );
}

/* ------------------------------------------------------------------ */
/* Main component                                                      */
/* ------------------------------------------------------------------ */
export default function GameUI() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const engineRef = useRef<Game | null>(null);
  const hudRef = useRef<HudRefs>({
    hpBar: null,
    hpGhost: null,
    hpText: null,
    xpBar: null,
    levelText: null,
    timeText: null,
    scoreText: null,
    killText: null,
    hurtFlash: null,
  });
  const toastId = useRef(0);

  const [phase, setPhase] = useState<GamePhase>("menu");
  const [options, setOptions] = useState<string[]>([]);
  const [over, setOver] = useState<{ stats: RunStats; rank: number; isBest: boolean } | null>(null);
  const [scores, setScores] = useState<HighScore[]>([]);
  const [upgIds, setUpgIds] = useState<string[]>([]);
  const [muted, setMuted] = useState(false);
  const [toasts, setToasts] = useState<Toast[]>([]);

  useEffect(() => {
    const engine = new Game(canvasRef.current!, {
      onPhaseChange: (p) => setPhase(p),
      onLevelUp: (opts) => setOptions(opts),
      onGameOver: (stats, rank, isBest) => setOver({ stats, rank, isBest }),
      onToast: (text, kind) => {
        const id = ++toastId.current;
        setToasts((t) => [...t.slice(-2), { id, text, kind }]);
        window.setTimeout(() => {
          setToasts((t) => t.filter((x) => x.id !== id));
        }, 2450);
      },
      onScores: (s) => setScores([...s]),
      onUpgrades: (ids) => setUpgIds(ids),
      onMute: (m) => setMuted(m),
    });
    engine.setHud(hudRef.current);
    engineRef.current = engine;
    return () => engine.destroy();
  }, []);

  const eng = () => engineRef.current!;
  const inRun = phase === "playing" || phase === "levelup" || phase === "paused";

  const upgCounts = upgIds.reduce<Record<string, number>>((acc, id) => {
    acc[id] = (acc[id] || 0) + 1;
    return acc;
  }, {});

  return (
    <div className="scanlines relative h-full w-full overflow-hidden bg-void">
      <canvas ref={canvasRef} className="absolute inset-0 h-full w-full" />

      {/* hurt flash overlay (driven by engine) */}
      <div
        ref={(el) => {
          hudRef.current.hurtFlash = el;
        }}
        className="pointer-events-none absolute inset-0 opacity-0"
        style={{
          background:
            "radial-gradient(ellipse at center, transparent 35%, rgba(239,68,68,0.5) 100%)",
        }}
      />

      {/* --------------------------------------------------------- HUD */}
      <div
        className={`pointer-events-none absolute inset-0 transition-opacity duration-300 ${
          inRun ? "opacity-100" : "opacity-0"
        }`}
        aria-hidden={!inRun}
      >
        {/* xp bar */}
        <div className="absolute inset-x-0 top-0 h-[7px] bg-slate-950/70">
          <div
            ref={(el) => {
              hudRef.current.xpBar = el;
            }}
            className="bar-fill h-full w-full bg-gradient-to-r from-emerald-500 via-emerald-400 to-lime-300 shadow-[0_0_12px_rgba(52,211,153,0.7)]"
            style={{ transform: "scaleX(0)" }}
          />
        </div>

        {/* top row */}
        <div className="absolute inset-x-0 top-[7px] flex items-start justify-between gap-3 px-3 pt-2 sm:px-5 sm:pt-3">
          {/* hp */}
          <div className="w-[38vw] max-w-56 sm:w-56">
            <div className="mb-1 flex items-center justify-between font-display text-[10px] tracking-[0.2em] text-cyan-100/70">
              <span className="flex items-center gap-1">
                <Heart size={11} className="text-rose-400" />
                HP
              </span>
              <span
                ref={(el) => {
                  hudRef.current.hpText = el;
                }}
              >
                100
              </span>
            </div>
            <div className="bar-shell relative h-3.5 overflow-hidden rounded-sm">
              <div
                ref={(el) => {
                  hudRef.current.hpGhost = el;
                }}
                className="bar-fill absolute inset-0 bg-rose-200/50"
                style={{ transform: "scaleX(1)" }}
              />
              <div
                ref={(el) => {
                  hudRef.current.hpBar = el;
                }}
                className="bar-fill absolute inset-0 bg-gradient-to-r from-rose-500 via-rose-400 to-orange-300 shadow-[0_0_10px_rgba(251,113,133,0.6)]"
                style={{ transform: "scaleX(1)" }}
              />
            </div>
          </div>

          {/* timer */}
          <div className="flex flex-col items-center">
            <div
              ref={(el) => {
                hudRef.current.timeText = el;
              }}
              className="font-display text-2xl font-bold tracking-widest text-slate-100 text-glow-cyan sm:text-3xl"
            >
              0:00
            </div>
            <div
              ref={(el) => {
                hudRef.current.levelText = el;
              }}
              className="mt-0.5 rounded-sm border border-emerald-300/30 bg-emerald-400/10 px-2 py-px font-display text-[10px] font-semibold tracking-[0.25em] text-emerald-300"
            >
              LV 1
            </div>
          </div>

          {/* score + buttons */}
          <div className="flex items-start gap-2">
            <div className="text-right">
              <div className="font-display text-[9px] tracking-[0.3em] text-cyan-100/60">SCORE</div>
              <div
                ref={(el) => {
                  hudRef.current.scoreText = el;
                }}
                className="font-display text-xl font-bold leading-none text-cyan-200 text-glow-cyan sm:text-2xl"
              >
                0
              </div>
              <div className="mt-1 flex items-center justify-end gap-1 font-display text-[11px] text-rose-200/80">
                <Skull size={11} />
                <span
                  ref={(el) => {
                    hudRef.current.killText = el;
                  }}
                >
                  0
                </span>
              </div>
            </div>
            <IconBtn icon={Pause} label="Pause" onClick={() => eng().togglePause()} />
          </div>
        </div>

        {/* build icons */}
        {upgIds.length > 0 && (
          <div className="absolute bottom-3 left-3 flex flex-wrap gap-1.5 sm:bottom-4 sm:left-5">
            {Object.entries(upgCounts).map(([id, n]) => {
              const def = UPGRADE_MAP[id];
              if (!def || id === "patch") return null;
              const Ic = def.icon;
              return (
                <div
                  key={id}
                  title={`${def.name} ${n}`}
                  className="relative flex h-9 w-9 items-center justify-center rounded-md border border-cyan-200/20 bg-slate-950/60 text-cyan-300 backdrop-blur-sm"
                >
                  <Ic size={15} />
                  <span className="absolute -bottom-1 -right-1 rounded-sm bg-cyan-400 px-1 font-display text-[9px] font-bold leading-3 text-slate-950">
                    {n}
                  </span>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* --------------------------------------------------------- toasts */}
      <div className="pointer-events-none absolute inset-x-0 top-[18%] flex flex-col items-center gap-2">
        {toasts.map((t) => (
          <div
            key={t.id}
            className={`anim-toast font-display text-lg font-bold tracking-[0.3em] sm:text-2xl ${
              t.kind === "danger"
                ? "text-rose-400 text-glow-red"
                : t.kind === "gem"
                  ? "text-emerald-300 text-glow-gem"
                  : "text-cyan-200 text-glow-cyan"
            }`}
          >
            {t.text}
          </div>
        ))}
      </div>

      {/* --------------------------------------------------------- menu */}
      {phase === "menu" && (
        <div className="anim-fade-in absolute inset-0 flex items-center justify-center overflow-y-auto px-4 py-6">
          <div className="pointer-events-auto flex w-full max-w-xl flex-col items-center">
            <div className="anim-title-float flex flex-col items-center">
              <div className="mb-2 font-display text-[10px] tracking-[0.55em] text-cyan-300/70 sm:text-xs">
                THE SWARM IS ENDLESS — YOU ARE NOT
              </div>
              <h1 className="title-shimmer font-display text-6xl font-bold tracking-[0.12em] sm:text-8xl">
                VOIDFALL
              </h1>
              <div className="mt-2 font-display text-[11px] tracking-[0.5em] text-slate-400 sm:text-sm">
                SURVIVOR&nbsp;OF&nbsp;THE&nbsp;SWARM
              </div>
            </div>

            <button
              onClick={() => eng().start()}
              className="btn-neon anim-pulse-glow mt-8 flex items-center gap-3 rounded-md px-10 py-4 text-lg font-bold sm:text-xl"
            >
              <Play size={22} className="fill-current" />
              ENTER THE VOID
            </button>

            <div className="mt-6 grid w-full grid-cols-2 gap-3">
              <div className="panel rounded-lg p-3.5">
                <div className="mb-2 flex items-center gap-2 font-display text-[10px] tracking-[0.25em] text-cyan-200/70">
                  <Keyboard size={13} /> DESKTOP
                </div>
                <div className="space-y-1 text-[12px] leading-snug text-slate-400">
                  <div>
                    <span className="text-slate-200">WASD / Arrows</span> — move
                  </div>
                  <div>
                    <span className="text-slate-200">Auto-fire</span> — just dodge
                  </div>
                  <div>
                    <span className="text-slate-200">P</span> pause · <span className="text-slate-200">M</span> mute
                  </div>
                </div>
              </div>
              <div className="panel rounded-lg p-3.5">
                <div className="mb-2 flex items-center gap-2 font-display text-[10px] tracking-[0.25em] text-cyan-200/70">
                  <Smartphone size={13} /> TOUCH
                </div>
                <div className="space-y-1 text-[12px] leading-snug text-slate-400">
                  <div>
                    <span className="text-slate-200">Drag anywhere</span> — move
                  </div>
                  <div>
                    <span className="text-slate-200">Auto-fire</span> — just dodge
                  </div>
                  <div>Grab gems · level up · survive</div>
                </div>
              </div>
            </div>

            <div className="panel mt-3 w-full rounded-lg p-3.5">
              <div className="mb-2 flex items-center gap-2 font-display text-[10px] tracking-[0.25em] text-yellow-200/80">
                <Trophy size={13} /> HALL OF THE FALLEN
              </div>
              <ScoreTable scores={scores} />
            </div>

            <div className="mt-4 flex items-center gap-3">
              <IconBtn icon={muted ? VolumeX : Volume2} label="Toggle sound" onClick={() => eng().toggleMute()} />
              <div className="font-display text-[10px] tracking-[0.3em] text-slate-600">
                v1.0 — RUNS ENTIRELY IN YOUR BROWSER
              </div>
            </div>
          </div>
        </div>
      )}

      {/* --------------------------------------------------------- level up */}
      {phase === "levelup" && (
        <div className="anim-fade-in absolute inset-0 flex items-center justify-center bg-slate-950/55 px-4 backdrop-blur-[3px]">
          <div className="pointer-events-auto flex w-full max-w-3xl flex-col items-center">
            <div className="anim-rise-in mb-1 font-display text-[11px] tracking-[0.5em] text-emerald-300 text-glow-gem">
              POWER SURGE
            </div>
            <h2 className="anim-rise-in mb-6 font-display text-3xl font-bold tracking-[0.2em] text-slate-100 sm:text-4xl">
              CHOOSE AN UPGRADE
            </h2>
            <div className="flex w-full flex-col justify-center gap-3 sm:flex-row sm:gap-4">
              {options.map((id, i) => {
                const def = UPGRADE_MAP[id];
                if (!def) return null;
                const Ic = def.icon;
                const cur = upgCounts[id] || 0;
                const isMax = cur >= def.max - 1 && def.max < 90;
                return (
                  <button
                    key={id + i}
                    onClick={() => eng().applyUpgrade(id)}
                    className="upg-card anim-card-in relative flex flex-1 flex-row items-center gap-4 rounded-xl p-4 text-left sm:flex-col sm:items-center sm:p-6 sm:text-center"
                    style={{ animationDelay: `${i * 70}ms` }}
                  >
                    <span className="absolute right-3 top-3 hidden font-display text-[10px] tracking-widest text-slate-500 sm:block">
                      [{i + 1}]
                    </span>
                    <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full border border-cyan-300/40 bg-cyan-400/10 text-cyan-300 shadow-[0_0_20px_rgba(34,211,238,0.25)]">
                      <Ic size={26} />
                    </span>
                    <span className="flex min-w-0 flex-col sm:items-center">
                      <span className="font-display text-[9px] tracking-[0.35em] text-cyan-400/70">
                        {def.tag}
                      </span>
                      <span className="font-display text-lg font-bold tracking-wider text-slate-100">
                        {def.name}
                      </span>
                      <span className="mt-1 text-[12.5px] leading-snug text-slate-400">
                        {def.desc(cur + 1)}
                      </span>
                      {def.max < 90 && (
                        <span className="mt-2 flex gap-1">
                          {Array.from({ length: def.max }).map((_, pi) => (
                            <span
                              key={pi}
                              className={`h-1 w-4 rounded-full ${
                                pi < cur ? "bg-cyan-400" : pi === cur ? "bg-cyan-200 animate-pulse" : "bg-slate-700"
                              }`}
                            />
                          ))}
                        </span>
                      )}
                      {isMax && (
                        <span className="mt-1 font-display text-[9px] tracking-[0.3em] text-yellow-300">
                          FINAL RANK
                        </span>
                      )}
                    </span>
                    <ChevronRight size={16} className="ml-auto shrink-0 text-cyan-300/60 sm:hidden" />
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      )}

      {/* --------------------------------------------------------- pause */}
      {phase === "paused" && (
        <div className="anim-fade-in absolute inset-0 flex items-center justify-center bg-slate-950/60 px-4 backdrop-blur-[3px]">
          <div className="panel anim-rise-in pointer-events-auto flex w-full max-w-sm flex-col items-center rounded-2xl p-7">
            <h2 className="font-display text-3xl font-bold tracking-[0.35em] text-slate-100 text-glow-cyan">
              PAUSED
            </h2>
            <div className="mb-6 mt-1 font-display text-[10px] tracking-[0.4em] text-slate-500">
              THE SWARM WAITS FOR NO ONE
            </div>

            {upgIds.length > 0 && (
              <div className="mb-5 flex flex-wrap justify-center gap-1.5">
                {Object.entries(upgCounts).map(([id, n]) => {
                  const def = UPGRADE_MAP[id];
                  if (!def || id === "patch") return null;
                  const Ic = def.icon;
                  return (
                    <div
                      key={id}
                      title={`${def.name} ${n}`}
                      className="relative flex h-9 w-9 items-center justify-center rounded-md border border-cyan-200/20 bg-slate-950/60 text-cyan-300"
                    >
                      <Ic size={15} />
                      <span className="absolute -bottom-1 -right-1 rounded-sm bg-cyan-400 px-1 font-display text-[9px] font-bold leading-3 text-slate-950">
                        {n}
                      </span>
                    </div>
                  );
                })}
              </div>
            )}

            <div className="flex w-full flex-col gap-2.5">
              <button
                onClick={() => eng().togglePause()}
                className="btn-neon flex items-center justify-center gap-2 rounded-md px-6 py-3 text-sm font-bold"
              >
                <Play size={16} className="fill-current" /> RESUME
              </button>
              <button
                onClick={() => eng().restart()}
                className="btn-ghost flex items-center justify-center gap-2 rounded-md px-6 py-3 text-sm"
              >
                <RotateCcw size={15} /> RESTART RUN
              </button>
              <button
                onClick={() => eng().toMenu()}
                className="btn-ghost flex items-center justify-center gap-2 rounded-md px-6 py-3 text-sm"
              >
                <Home size={15} /> ABANDON TO MENU
              </button>
            </div>

            <div className="mt-5">
              <IconBtn icon={muted ? VolumeX : Volume2} label="Toggle sound" onClick={() => eng().toggleMute()} />
            </div>
          </div>
        </div>
      )}

      {/* --------------------------------------------------------- game over */}
      {phase === "gameover" && over && (
        <div className="anim-fade-in absolute inset-0 flex items-center justify-center overflow-y-auto bg-slate-950/55 px-4 py-6 backdrop-blur-[2px]">
          <div className="pointer-events-auto flex w-full max-w-md flex-col items-center">
            <h2 className="anim-rise-in text-center font-display text-4xl font-bold tracking-[0.18em] text-rose-400 text-glow-red sm:text-5xl">
              THE VOID
              <br />
              CLAIMS YOU
            </h2>
            {over.isBest && (
              <div className="anim-best anim-rise-in mt-3 flex items-center gap-2 rounded-full border border-yellow-300/60 bg-yellow-300/10 px-4 py-1.5 font-display text-[11px] font-bold tracking-[0.3em] text-yellow-200">
                <Crown size={13} /> NEW BEST RUN
              </div>
            )}
            {!over.isBest && over.rank >= 0 && (
              <div className="anim-rise-in mt-3 font-display text-[11px] tracking-[0.35em] text-cyan-200/80">
                RANK #{over.rank + 1} ALL TIME
              </div>
            )}

            <div className="anim-rise-in mt-5 grid w-full grid-cols-2 gap-2.5" style={{ animationDelay: "80ms" }}>
              <div className="panel col-span-2 flex flex-col items-center rounded-xl p-4">
                <div className="font-display text-[9px] tracking-[0.4em] text-cyan-200/60">FINAL SCORE</div>
                <div className="font-display text-4xl font-bold text-cyan-200 text-glow-cyan">
                  {over.stats.score.toLocaleString()}
                </div>
              </div>
              {[
                { icon: Timer, label: "SURVIVED", value: fmtTime(over.stats.time) },
                { icon: Skull, label: "KILLS", value: String(over.stats.kills) },
              ].map((s) => (
                <div key={s.label} className="panel flex items-center gap-3 rounded-xl p-3.5">
                  <s.icon size={18} className="shrink-0 text-cyan-300/70" />
                  <div>
                    <div className="font-display text-[9px] tracking-[0.3em] text-slate-500">{s.label}</div>
                    <div className="font-display text-xl font-bold text-slate-100">{s.value}</div>
                  </div>
                </div>
              ))}
            </div>

            <div className="anim-rise-in mt-4 flex w-full gap-2.5" style={{ animationDelay: "140ms" }}>
              <button
                onClick={() => eng().restart()}
                className="btn-neon flex flex-1 items-center justify-center gap-2 rounded-md px-6 py-3.5 text-sm font-bold"
              >
                <RotateCcw size={16} /> RETRY&nbsp;
                <span className="hidden text-[10px] opacity-60 sm:inline">[R]</span>
              </button>
              <button
                onClick={() => eng().toMenu()}
                className="btn-ghost flex flex-1 items-center justify-center gap-2 rounded-md px-6 py-3.5 text-sm"
              >
                <Home size={15} /> MENU
              </button>
            </div>

            <div className="anim-rise-in panel mt-4 w-full rounded-xl p-3.5" style={{ animationDelay: "200ms" }}>
              <div className="mb-2 flex items-center gap-2 font-display text-[10px] tracking-[0.25em] text-yellow-200/80">
                <Trophy size={13} /> HALL OF THE FALLEN
              </div>
              <ScoreTable scores={scores} highlight={over.rank >= 0 && over.rank < 5 ? over.rank : undefined} />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

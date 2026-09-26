import { useEffect, useRef, useState } from "react";
import {
  Play, Pause, RotateCcw, Home, Volume2, VolumeX, Trophy, Skull,
  Heart, Crown, ChevronRight, Zap, Shield, Star,
  type LucideIcon,
} from "lucide-react";
import { Game } from "../game/engine";
import { WEAPON_MAP } from "../game/weapons";
import { PASSIVE_MAP } from "../game/passives";
import { CHARACTERS } from "../game/characters";
import type { GamePhase, HighScore, HudRefs, RunStats, Toast, PassiveId } from "../game/types";

function fmtTime(sec: number) { const m = Math.floor(sec / 60); const s = Math.floor(sec % 60); return `${m}:${s.toString().padStart(2, "0")}`; }

function ScoreTable({ scores, highlight }: { scores: HighScore[]; highlight?: number }) {
  if (!scores.length) return <div className="py-6 text-center text-[13px] tracking-widest text-slate-500 font-display">NO RECORDED RUNS</div>;
  return (
    <div className="overflow-hidden rounded-lg border border-cyan-200/10">
      <table className="w-full text-left text-[13px]">
        <thead>
          <tr className="bg-cyan-400/5 font-display text-[10px] tracking-[0.2em] text-cyan-200/50">
            <th className="px-3 py-2 font-semibold">#</th><th className="px-3 py-2 font-semibold">SCORE</th>
            <th className="px-3 py-2 font-semibold">KILLS</th><th className="px-3 py-2 font-semibold">TIME</th>
            <th className="px-3 py-2 font-semibold text-right">LV</th>
          </tr>
        </thead>
        <tbody>
          {scores.slice(0, 5).map((s, i) => (
            <tr key={s.date + "-" + i} className={highlight === i ? "bg-yellow-300/10 text-yellow-200" : i === 0 ? "text-cyan-100" : "text-slate-400"}>
              <td className="px-3 py-1.5 font-display">{i === 0 && <Crown size={12} className="inline text-yellow-300 mr-1" />}{i + 1}</td>
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

function IconBtn({ icon: Icon, label, onClick }: { icon: LucideIcon; label: string; onClick: () => void }) {
  return (
    <button onClick={onClick} aria-label={label}
      className="pointer-events-auto flex h-11 w-11 items-center justify-center rounded-lg border border-slate-400/20 bg-slate-900/60 text-slate-300 backdrop-blur-md transition hover:border-cyan-300/50 hover:text-cyan-200 active:scale-95">
      <Icon size={18} />
    </button>
  );
}

export default function GameUI() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const engineRef = useRef<Game | null>(null);
  const hudRef = useRef<HudRefs>({
    hpBar: null, hpGhost: null, hpText: null, xpBar: null, levelText: null,
    timeText: null, scoreText: null, killText: null, hurtFlash: null,
    comboText: null, weaponText: null, creditsText: null, shieldBar: null, shieldText: null,
  });
  const toastId = useRef(0);

  const [phase, setPhase] = useState<GamePhase>("menu");
  const [opts, setOpts] = useState<string[]>([]);
  const [_optTypes, setOptTypes] = useState<("weapon" | "passive" | "heal" | "reroll" | "banish")[]>([]);
  const [over, setOver] = useState<{ stats: RunStats; rank: number; isBest: boolean; victory: boolean } | null>(null);
  const [scores, setScores] = useState<HighScore[]>([]);
  const [wLv, setWlv] = useState<Record<string, number>>({});
  const [muted, setMuted] = useState(false);
  const [toasts, setToasts] = useState<Toast[]>([]);
  const [selChar, setSelChar] = useState("sentinel");
  const [selArena, setSelArena] = useState("neon");
  const [selMode, setSelMode] = useState<"standard" | "endless">("standard");
  const [showChar, setShowChar] = useState(false);
  const [showArena, setShowArena] = useState(false);
  const [hasEndless, setHasEndless] = useState(false);

  useEffect(() => {
    const eng = new Game(canvasRef.current!, {
      onPhaseChange: (p) => setPhase(p),
      onLevelUp: (ids, types) => { setOpts(ids); setOptTypes(types); },
      onGameOver: (stats, rank, isBest, victory) => setOver({ stats, rank, isBest, victory }),
      onToast: (text, kind) => {
        const id = ++toastId.current;
        setToasts((t) => [...t.slice(-2), { id, text, kind }]);
        setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 2450);
      },
      onScores: (s) => { setScores([...s]); if (s[0]) setHasEndless(s.length > 0); },
      onUpgrades: (wl, _pl) => setWlv({ ...wl }),
      onMute: (m) => setMuted(m),
      onPerf: () => {},
      onChestOpen: () => {},
    });
    eng.setHud(hudRef.current);
    eng.setRun("standard", "neon", "sentinel", null);
    engineRef.current = eng;
    return () => eng.destroy();
  }, []);

  const eng = () => engineRef.current!;
  const inRun = phase === "playing" || phase === "levelup" || phase === "paused";

  const startRun = () => {
    eng().setRun(selMode, selArena as any, selChar as any, null);
    eng().start();
  };

  const upgList = Object.entries(wLv).filter(([, n]) => n > 0);

  return (
    <div className="scanlines relative h-full w-full overflow-hidden bg-void">
      <canvas ref={canvasRef} className="absolute inset-0 h-full w-full" />
      <div ref={(el) => { hudRef.current.hurtFlash = el; }}
        className="pointer-events-none absolute inset-0 opacity-0"
        style={{ background: "radial-gradient(ellipse at center, transparent 35%, rgba(239,68,68,0.5) 100%)" }}
      />

      {/* HUD */}
      <div className={`pointer-events-none absolute inset-0 transition-opacity duration-300 ${inRun ? "opacity-100" : "opacity-0"}`} aria-hidden={!inRun}>
        <div className="absolute inset-x-0 top-0 h-[7px] bg-slate-950/70">
          <div ref={(el) => { hudRef.current.xpBar = el; }}
            className="bar-fill h-full w-full bg-gradient-to-r from-emerald-500 via-emerald-400 to-lime-300 shadow-[0_0_12px_rgba(52,211,153,0.7)]" style={{ transform: "scaleX(0)" }} />
        </div>
        <div className="absolute inset-x-0 top-[7px] flex items-start justify-between gap-3 px-3 pt-2 sm:px-5 sm:pt-3">
          <div className="w-[38vw] max-w-56 sm:w-56">
            <div className="mb-1 flex items-center justify-between font-display text-[10px] tracking-[0.2em] text-cyan-100/70">
              <span className="flex items-center gap-1"><Heart size={11} className="text-rose-400" /> HP</span>
              <span ref={(el) => { hudRef.current.hpText = el; }}>100</span>
            </div>
            <div className="bar-shell relative h-3.5 overflow-hidden rounded-sm">
              <div ref={(el) => { hudRef.current.hpGhost = el; }} className="bar-fill absolute inset-0 bg-rose-200/50" style={{ transform: "scaleX(1)" }} />
              <div ref={(el) => { hudRef.current.hpBar = el; }} className="bar-fill absolute inset-0 bg-gradient-to-r from-rose-500 via-rose-400 to-orange-300 shadow-[0_0_10px_rgba(251,113,133,0.6)]" style={{ transform: "scaleX(1)" }} />
            </div>
          </div>
          <div className="flex flex-col items-center">
            <div ref={(el) => { hudRef.current.timeText = el; }}
              className="font-display text-2xl font-bold tracking-widest text-slate-100 text-glow-cyan sm:text-3xl">0:00</div>
            <div ref={(el) => { hudRef.current.levelText = el; }}
              className="mt-0.5 rounded-sm border border-emerald-300/30 bg-emerald-400/10 px-2 py-px font-display text-[10px] font-semibold tracking-[0.25em] text-emerald-300">LV 1</div>
          </div>
          <div className="flex items-start gap-2">
            <div className="text-right">
              <div className="font-display text-[9px] tracking-[0.3em] text-cyan-100/60">SCORE</div>
              <div ref={(el) => { hudRef.current.scoreText = el; }}
                className="font-display text-xl font-bold leading-none text-cyan-200 text-glow-cyan sm:text-2xl">0</div>
              <div className="mt-1 flex items-center justify-end gap-1 font-display text-[11px] text-rose-200/80">
                <Skull size={11} /><span ref={(el) => { hudRef.current.killText = el; }}>0</span>
              </div>
              <div ref={(el) => { hudRef.current.comboText = el; }}
                className="font-display text-[10px] tracking-wider text-yellow-300 text-glow-gem mt-0.5 min-h-[14px]"></div>
            </div>
            <IconBtn icon={Pause} label="Pause" onClick={() => eng().togglePause()} />
          </div>
        </div>
        {upgList.length > 0 && (
          <div className="absolute bottom-3 left-3 flex flex-wrap gap-1.5 sm:bottom-4 sm:left-5">
            {upgList.map(([id, n]) => {
              const def = WEAPON_MAP[id]; if (!def) return null;
              return (
                <div key={id} title={`${def.name} Lv${n}`}
                  className="relative flex h-9 w-9 items-center justify-center rounded-md border border-cyan-200/20 bg-slate-950/60 text-cyan-300 backdrop-blur-sm">
                  <span className="text-xs">{def.icon}</span>
                  <span className="absolute -bottom-1 -right-1 rounded-sm bg-cyan-400 px-1 font-display text-[9px] font-bold leading-3 text-slate-950">{n}</span>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Toasts */}
      <div className="pointer-events-none absolute inset-x-0 top-[18%] flex flex-col items-center gap-2">
        {toasts.map((t) => (
          <div key={t.id} className={`anim-toast font-display text-lg font-bold tracking-[0.3em] sm:text-2xl ${
            t.kind === "danger" ? "text-rose-400 text-glow-red" : t.kind === "gem" ? "text-emerald-300 text-glow-gem" : t.kind === "gold" ? "text-yellow-300" : t.kind === "evo" ? "text-fuchsia-300" : t.kind === "combo" ? "text-amber-300" : "text-cyan-200 text-glow-cyan"
          }`}>{t.text}</div>
        ))}
      </div>

      {/* MENU */}
      {phase === "menu" && (
        <div className="anim-fade-in absolute inset-0 flex items-center justify-center overflow-y-auto px-4 py-6">
          <div className="pointer-events-auto flex w-full max-w-xl flex-col items-center">
            <div className="anim-title-float flex flex-col items-center mb-6">
              <div className="mb-2 font-display text-[10px] tracking-[0.55em] text-cyan-300/70 sm:text-xs">THE SWARM IS ENDLESS — YOU ARE NOT</div>
              <h1 className="title-shimmer font-display text-6xl font-bold tracking-[0.12em] sm:text-8xl">VOIDFALL</h1>
              <div className="mt-2 font-display text-[11px] tracking-[0.5em] text-slate-400 sm:text-sm">SURVIVOR OF THE SWARM</div>
            </div>

            <div className="flex gap-2 mb-4">
              <button onClick={() => setShowChar(!showChar)}
                className={`btn-neon px-4 py-2 rounded-md text-xs font-display tracking-wider ${showChar ? "bg-cyan-400/30" : ""}`}>
                ⚔ CHARACTER
              </button>
              <button onClick={() => setShowArena(!showArena)}
                className={`btn-neon px-4 py-2 rounded-md text-xs font-display tracking-wider ${showArena ? "bg-cyan-400/30" : ""}`}>
                🗺 ARENA
              </button>
              {hasEndless && (
                <button onClick={() => setSelMode(selMode === "standard" ? "endless" : "standard")}
                  className={`btn-neon px-4 py-2 rounded-md text-xs font-display tracking-wider ${selMode === "endless" ? "bg-cyan-400/30" : ""}`}>
                  ∞ {selMode.toUpperCase()}
                </button>
              )}
            </div>

            {showChar && (
              <div className="anim-rise-in w-full grid grid-cols-2 gap-2 mb-4">
                {CHARACTERS.map((ch) => (
                  <button key={ch.id} onClick={() => { setSelChar(ch.id); setShowChar(false); }}
                    className={`panel rounded-lg p-3 text-left transition ${selChar === ch.id ? "border-cyan-300/60 ring-1 ring-cyan-300/30" : ""} ${!ch.unlocked ? "opacity-40" : ""}`}>
                    <div className="font-display text-sm font-bold" style={{ color: ch.color }}>{ch.name}</div>
                    <div className="text-[11px] text-slate-400 mt-1">{ch.desc}</div>
                    {!ch.unlocked && <div className="text-[10px] text-yellow-300 mt-1">🔒 {ch.unlockCondition}</div>}
                  </button>
                ))}
              </div>
            )}

            {showArena && (
              <div className="anim-rise-in w-full grid grid-cols-2 gap-2 mb-4">
                {[
                  { id: "neon", name: "Neon District", desc: "Balanced", unlocked: true, color: "#22d3ee" },
                  { id: "ash", name: "Ash Wastes", desc: "+15% enemy HP", unlocked: true, color: "#fb923c" },
                  { id: "reactor", name: "Reactor Core", desc: "+25% HP, +50% elites", unlocked: false, color: "#34d399" },
                ].map((a) => (
                  <button key={a.id} onClick={() => { setSelArena(a.id); setShowArena(false); }}
                    className={`panel rounded-lg p-3 text-left transition ${selArena === a.id ? "border-cyan-300/60 ring-1 ring-cyan-300/30" : ""} ${!a.unlocked ? "opacity-40" : ""}`}>
                    <div className="font-display text-sm font-bold" style={{ color: a.color }}>{a.name}</div>
                    <div className="text-[11px] text-slate-400 mt-1">{a.desc}</div>
                  </button>
                ))}
              </div>
            )}

            <button onClick={startRun}
              className="btn-neon anim-pulse-glow flex items-center gap-3 rounded-md px-10 py-4 text-lg font-bold sm:text-xl">
              <Play size={22} className="fill-current" />
              {selMode === "endless" ? "ENDLESS RUN" : "ENTER THE VOID"}
            </button>

            <div className="panel mt-4 w-full rounded-lg p-3.5">
              <div className="mb-2 flex items-center gap-2 font-display text-[10px] tracking-[0.25em] text-yellow-200/80">
                <Trophy size={13} /> HALL OF THE FALLEN
              </div>
              <ScoreTable scores={scores} />
            </div>

            <div className="mt-4 flex items-center gap-3">
              <IconBtn icon={muted ? VolumeX : Volume2} label="Toggle sound" onClick={() => eng().toggleMute()} />
              <div className="font-display text-[10px] tracking-[0.3em] text-slate-600">v1.0 — Press D for debug</div>
            </div>
          </div>
        </div>
      )}

      {/* LEVEL UP */}
      {phase === "levelup" && (
        <div className="anim-fade-in absolute inset-0 flex items-center justify-center bg-slate-950/55 px-4 backdrop-blur-[3px]">
          <div className="pointer-events-auto flex w-full max-w-4xl flex-col items-center">
            <div className="anim-rise-in mb-1 font-display text-[11px] tracking-[0.5em] text-emerald-300 text-glow-gem">POWER SURGE</div>
            <h2 className="anim-rise-in mb-6 font-display text-3xl font-bold tracking-[0.2em] text-slate-100 sm:text-4xl">CHOOSE AN UPGRADE</h2>
            <div className="flex w-full flex-col justify-center gap-3 sm:flex-row sm:gap-4">
              {opts.map((id, i) => {
                const parsed = id.startsWith("w_") ? { type: "weapon" as const, key: id.slice(2) } :
                  id.startsWith("p_") ? { type: "passive" as const, key: id.slice(2) } :
                  { type: id as "heal" | "reroll" | "banish", key: "" };
                const isHeal = parsed.type === "heal";
                const isReroll = parsed.type === "reroll";
                const isBanish = parsed.type === "banish";
                const wDef = parsed.type === "weapon" ? WEAPON_MAP[parsed.key] : null;
                const pDef = parsed.type === "passive" ? PASSIVE_MAP[parsed.key as PassiveId] : null;
                const Icon = isHeal ? Heart : isReroll ? RotateCcw : isBanish ? Shield : wDef ? (Star as any) : pDef ? (Zap as any) : Star;
                return (
                  <button key={id + i} onClick={() => eng().applyUpgrade(id)}
                    className="upg-card anim-card-in relative flex flex-1 flex-col items-center gap-2 rounded-xl p-4 sm:p-6 text-center"
                    style={{ animationDelay: `${i * 70}ms` }}>
                    <span className="absolute right-3 top-3 font-display text-[10px] tracking-widest text-slate-500">[{i + 1}]</span>
                    <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full border border-cyan-300/40 bg-cyan-400/10 text-cyan-300 shadow-[0_0_20px_rgba(34,211,238,0.25)]">
                      <Icon size={26} />
                    </span>
                    <span className="flex min-w-0 flex-col items-center">
                      <span className="font-display text-[9px] tracking-[0.35em] text-cyan-400/70">{parsed.type.toUpperCase()}</span>
                      <span className="font-display text-lg font-bold tracking-wider text-slate-100">
                        {isHeal ? "HEAL" : isReroll ? "REROLL" : isBanish ? "BANISH" : wDef?.name || pDef?.name || id}
                      </span>
                      <span className="mt-1 text-[12.5px] leading-snug text-slate-400">
                        {isHeal ? "Restore 40 HP" : isReroll ? "+1 reroll at level up" : isBanish ? "Remove one upgrade choice" :
                          wDef ? `Lv ${wLv[parsed.key] || 1} → ${wDef.evolution && (wLv[parsed.key] || 0) >= 7 ? "EVOLUTION" : `Lv ${(wLv[parsed.key] || 0) + 1}`}` :
                          pDef ? pDef.description((wLv[parsed.key] || 0) + 1) : ""}
                      </span>
                    </span>
                    <ChevronRight size={16} className="text-cyan-300/60 sm:hidden" />
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      )}

      {/* PAUSE */}
      {phase === "paused" && (
        <div className="anim-fade-in absolute inset-0 flex items-center justify-center bg-slate-950/60 px-4 backdrop-blur-[3px]">
          <div className="panel anim-rise-in pointer-events-auto flex w-full max-w-sm flex-col items-center rounded-2xl p-7">
            <h2 className="font-display text-3xl font-bold tracking-[0.35em] text-slate-100 text-glow-cyan">PAUSED</h2>
            <div className="mb-6 mt-1 font-display text-[10px] tracking-[0.4em] text-slate-500">THE SWARM WAITS FOR NO ONE</div>
            <div className="flex w-full flex-col gap-2.5">
              <button onClick={() => eng().togglePause()}
                className="btn-neon flex items-center justify-center gap-2 rounded-md px-6 py-3 text-sm font-bold">
                <Play size={16} className="fill-current" /> RESUME
              </button>
              <button onClick={() => eng().restart()}
                className="btn-ghost flex items-center justify-center gap-2 rounded-md px-6 py-3 text-sm">
                <RotateCcw size={15} /> RESTART RUN
              </button>
              <button onClick={() => eng().toMenu()}
                className="btn-ghost flex items-center justify-center gap-2 rounded-md px-6 py-3 text-sm">
                <Home size={15} /> ABANDON TO MENU
              </button>
            </div>
            <div className="mt-5">
              <IconBtn icon={muted ? VolumeX : Volume2} label="Toggle sound" onClick={() => eng().toggleMute()} />
            </div>
          </div>
        </div>
      )}

      {/* GAME OVER / VICTORY */}
      {(phase === "gameover" || phase === "victory") && over && (
        <div className="anim-fade-in absolute inset-0 flex items-center justify-center overflow-y-auto bg-slate-950/55 px-4 py-6 backdrop-blur-[2px]">
          <div className="pointer-events-auto flex w-full max-w-md flex-col items-center">
            {over.victory ? (
              <h2 className="anim-rise-in text-center font-display text-4xl font-bold tracking-[0.18em] text-emerald-300 text-glow-gem sm:text-5xl">
                VICTORY
                <br />
                <span className="text-2xl text-slate-300">THE SWARM IS VANQUISHED</span>
              </h2>
            ) : (
              <h2 className="anim-rise-in text-center font-display text-4xl font-bold tracking-[0.18em] text-rose-400 text-glow-red sm:text-5xl">
                THE VOID
                <br />
                CLAIMS YOU
              </h2>
            )}
            {over.isBest && (
              <div className="anim-best anim-rise-in mt-3 flex items-center gap-2 rounded-full border border-yellow-300/60 bg-yellow-300/10 px-4 py-1.5 font-display text-[11px] font-bold tracking-[0.3em] text-yellow-200">
                <Crown size={13} /> NEW BEST RUN
              </div>
            )}
            <div className="anim-rise-in mt-5 grid w-full grid-cols-2 gap-2.5" style={{ animationDelay: "80ms" }}>
              <div className="panel col-span-2 flex flex-col items-center rounded-xl p-4">
                <div className="font-display text-[9px] tracking-[0.4em] text-cyan-200/60">FINAL SCORE</div>
                <div className="font-display text-4xl font-bold text-cyan-200 text-glow-cyan">{over.stats.score.toLocaleString()}</div>
              </div>
              {[
                { icon: Skull, label: "KILLS", value: String(over.stats.kills) },
                { label: "ELITES", value: String(over.stats.eliteKills || 0) },
                { label: "BOSSES", value: String(over.stats.bossKills || 0) },
                { label: "MAX COMBO", value: String(over.stats.maxCombo || 0) },
              ].map((s) => (
                <div key={s.label} className="panel flex flex-col items-center rounded-xl p-3">
                  {s.icon && <s.icon size={16} className="text-cyan-300/70 mb-1" />}
                  <div className="font-display text-[9px] tracking-[0.3em] text-slate-500">{s.label}</div>
                  <div className="font-display text-lg font-bold text-slate-100">{s.value}</div>
                </div>
              ))}
            </div>
            <div className="anim-rise-in mt-4 flex w-full gap-2.5" style={{ animationDelay: "140ms" }}>
              <button onClick={() => eng().restart()}
                className="btn-neon flex flex-1 items-center justify-center gap-2 rounded-md px-6 py-3.5 text-sm font-bold">
                <RotateCcw size={16} /> RETRY
              </button>
              <button onClick={() => eng().toMenu()}
                className="btn-ghost flex flex-1 items-center justify-center gap-2 rounded-md px-6 py-3.5 text-sm">
                <Home size={15} /> MENU
              </button>
            </div>
            <div className="anim-rise-in panel mt-4 w-full rounded-xl p-3.5" style={{ animationDelay: "200ms" }}>
              <div className="mb-2 flex items-center gap-2 font-display text-[10px] tracking-[0.25em] text-yellow-200/80">
                <Trophy size={13} /> HIGH SCORES
              </div>
              <ScoreTable scores={scores} highlight={over.rank >= 0 && over.rank < 5 ? over.rank : undefined} />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

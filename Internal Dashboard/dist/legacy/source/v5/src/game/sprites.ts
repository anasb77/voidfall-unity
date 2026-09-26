// Pre-rendered glow sprites. Everything expensive (radial gradients, glow) is baked
// once here so the runtime loop only does cheap drawImage calls.

export interface SpriteSet {
  player: HTMLCanvasElement; playerRing: HTMLCanvasElement;
  players: Record<string, HTMLCanvasElement>; // form-specific players
  playerRings: Record<string, HTMLCanvasElement>; // form-specific rings
  bullet: HTMLCanvasElement; blade: HTMLCanvasElement;
  iceBolt: HTMLCanvasElement; bolt: HTMLCanvasElement;
  plasma: HTMLCanvasElement; nova: HTMLCanvasElement; tendril: HTMLCanvasElement; mine: HTMLCanvasElement;
  gemS: HTMLCanvasElement; gemM: HTMLCanvasElement; gemL: HTMLCanvasElement;
  ring: HTMLCanvasElement;
  enemy: Record<string, HTMLCanvasElement>;
  enemyHit: Record<string, HTMLCanvasElement>;
  dot: Record<string, HTMLCanvasElement>;
}

function cv(size: number): [HTMLCanvasElement, CanvasRenderingContext2D] {
  const c = document.createElement("canvas");
  c.width = c.height = Math.ceil(size);
  const ctx = c.getContext("2d")!;
  ctx.translate(size / 2, size / 2);
  return [c, ctx];
}

function glow(ctx: CanvasRenderingContext2D, r: number, color: string, alpha = 0.5) {
  const g = ctx.createRadialGradient(0, 0, r * 0.1, 0, 0, r);
  g.addColorStop(0, hexA(color, alpha));
  g.addColorStop(0.55, hexA(color, alpha * 0.28));
  g.addColorStop(1, hexA(color, 0));
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.arc(0, 0, r, 0, Math.PI * 2);
  ctx.fill();
}

export function hexA(hex: string, a: number): string {
  const n = parseInt(hex.slice(1), 16);
  const r = (n >> 16) & 255;
  const g = (n >> 8) & 255;
  const b = n & 255;
  return `rgba(${r},${g},${b},${a})`;
}

function poly(ctx: CanvasRenderingContext2D, points: [number, number][]) {
  ctx.beginPath();
  ctx.moveTo(points[0][0], points[0][1]);
  for (let i = 1; i < points.length; i++) ctx.lineTo(points[i][0], points[i][1]);
  ctx.closePath();
}

function starPath(ctx: CanvasRenderingContext2D, spikes: number, outer: number, inner: number) {
  ctx.beginPath();
  for (let i = 0; i < spikes * 2; i++) {
    const r = i % 2 === 0 ? outer : inner;
    const a = (i / (spikes * 2)) * Math.PI * 2 - Math.PI / 2;
    const x = Math.cos(a) * r;
    const y = Math.sin(a) * r;
    if (i === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  }
  ctx.closePath();
}

function ngon(ctx: CanvasRenderingContext2D, n: number, r: number, rot = 0) {
  ctx.beginPath();
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2 + rot - Math.PI / 2;
    const x = Math.cos(a) * r;
    const y = Math.sin(a) * r;
    if (i === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  }
  ctx.closePath();
}

type ShapeFn = (ctx: CanvasRenderingContext2D, r: number) => void;

function enemySprite(r: number, color: string, shape: ShapeFn, white: boolean): HTMLCanvasElement {
  const pad = r * 1.1 + 14;
  const [c, ctx] = cv((r + pad) * 2);
  if (!white) glow(ctx, r + pad, color, 0.55);
  // body
  shape(ctx, r);
  const bodyGrad = ctx.createRadialGradient(-r * 0.3, -r * 0.3, r * 0.1, 0, 0, r * 1.2);
  if (white) {
    bodyGrad.addColorStop(0, "#ffffff");
    bodyGrad.addColorStop(1, "#dbeafe");
  } else {
    bodyGrad.addColorStop(0, "#131735");
    bodyGrad.addColorStop(0.72, "#0a0d22");
    bodyGrad.addColorStop(1, hexA(color, 0.9));
  }
  ctx.fillStyle = bodyGrad;
  ctx.fill();
  ctx.lineWidth = Math.max(2, r * 0.14);
  ctx.strokeStyle = white ? "#ffffff" : color;
  ctx.stroke();
  // core eye
  ctx.beginPath();
  ctx.arc(0, 0, r * 0.34, 0, Math.PI * 2);
  ctx.fillStyle = white ? "#ffffff" : hexA(color, 0.95);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(0, 0, r * 0.16, 0, Math.PI * 2);
  ctx.fillStyle = white ? "#e0f2fe" : "#ffffff";
  ctx.fill();
  return c;
}

function playerSprite(): HTMLCanvasElement {
  const r = 15; const pad = 22;
  const [c, ctx] = cv((r + pad) * 2);
  glow(ctx, r + pad, "#22d3ee", 0.6);
  const g = ctx.createRadialGradient(-4, -5, 2, 0, 0, r);
  g.addColorStop(0, "#ecfeff"); g.addColorStop(0.45, "#67e8f9"); g.addColorStop(1, "#0e7490");
  ctx.beginPath(); ctx.arc(0, 0, r, 0, Math.PI * 2); ctx.fillStyle = g; ctx.fill();
  ctx.lineWidth = 2.5; ctx.strokeStyle = "#a5f3fc"; ctx.stroke();
  ctx.beginPath(); ctx.arc(0, 0, r * 0.45, 0, Math.PI * 2); ctx.fillStyle = "#05060f"; ctx.fill();
  ctx.beginPath(); ctx.arc(0, 0, r * 0.2, 0, Math.PI * 2); ctx.fillStyle = "#e0faff"; ctx.fill();
  return c;
}

// Form-specific player sprites — all inspired by the standard void orb
function formPlayerSprite(color: string, accent: string, shapeFn?: (ctx: CanvasRenderingContext2D, r: number) => void): HTMLCanvasElement {
  const r = 15; const pad = 22;
  const [c, ctx] = cv((r + pad) * 2);
  glow(ctx, r + pad, color, 0.55);
  if (shapeFn) {
    shapeFn(ctx, r);
    const g = ctx.createRadialGradient(-r * 0.3, -r * 0.3, r * 0.1, 0, 0, r * 1.15);
    g.addColorStop(0, accent); g.addColorStop(0.7, hexA(color, 0.85)); g.addColorStop(1, hexA(color, 0.35));
    ctx.fillStyle = g; ctx.fill();
    ctx.lineWidth = 2; ctx.strokeStyle = accent; ctx.stroke();
    ctx.beginPath(); ctx.arc(0, 0, r * 0.38, 0, Math.PI * 2); ctx.fillStyle = hexA(color, 0.85); ctx.fill();
    ctx.beginPath(); ctx.arc(0, 0, r * 0.16, 0, Math.PI * 2); ctx.fillStyle = accent; ctx.fill();
  } else {
    const g = ctx.createRadialGradient(-4, -5, 2, 0, 0, r);
    g.addColorStop(0, accent); g.addColorStop(0.45, color); g.addColorStop(1, hexA(color, 0.7));
    ctx.beginPath(); ctx.arc(0, 0, r, 0, Math.PI * 2); ctx.fillStyle = g; ctx.fill();
    ctx.lineWidth = 2.5; ctx.strokeStyle = accent; ctx.stroke();
    ctx.beginPath(); ctx.arc(0, 0, r * 0.45, 0, Math.PI * 2); ctx.fillStyle = "#05060f"; ctx.fill();
    ctx.beginPath(); ctx.arc(0, 0, r * 0.2, 0, Math.PI * 2); ctx.fillStyle = accent; ctx.fill();
  }
  return c;
}

function formRingSprite(color: string, accent: string): HTMLCanvasElement {
  const r = 25;
  const [c, ctx] = cv(r * 2 + 12);
  ctx.translate(0, 0);
  for (let i = 0; i < 10; i++) {
    const a = (i / 10) * Math.PI * 2;
    const x = Math.cos(a) * r;
    const y = Math.sin(a) * r;
    ctx.beginPath();
    ctx.arc(x, y, i % 2 === 0 ? 2.2 : 1.2, 0, Math.PI * 2);
    ctx.fillStyle = i % 2 === 0 ? accent : hexA(color, 0.5);
    ctx.fill();
  }
  ctx.beginPath();
  ctx.arc(0, 0, r, 0, Math.PI * 2);
  ctx.lineWidth = 1;
  ctx.strokeStyle = hexA(color, 0.25);
  ctx.stroke();
  return c;
}

function playerRingSprite(): HTMLCanvasElement {
  const r = 25;
  const [c, ctx] = cv(r * 2 + 12);
  ctx.translate(0, 0);
  for (let i = 0; i < 10; i++) {
    const a = (i / 10) * Math.PI * 2;
    const x = Math.cos(a) * r;
    const y = Math.sin(a) * r;
    ctx.beginPath();
    ctx.arc(x, y, i % 2 === 0 ? 2.2 : 1.2, 0, Math.PI * 2);
    ctx.fillStyle = hexA("#67e8f9", i % 2 === 0 ? 0.9 : 0.5);
    ctx.fill();
  }
  ctx.beginPath();
  ctx.arc(0, 0, r, 0, Math.PI * 2);
  ctx.lineWidth = 1;
  ctx.strokeStyle = hexA("#22d3ee", 0.28);
  ctx.stroke();
  return c;
}

function bulletSprite(): HTMLCanvasElement {
  const c = document.createElement("canvas");
  c.width = 34; c.height = 18;
  const ctx = c.getContext("2d")!; ctx.translate(17, 9);
  const g = ctx.createRadialGradient(2, 0, 0.5, 2, 0, 16);
  g.addColorStop(0, "rgba(255,255,255,0.95)"); g.addColorStop(0.25, "rgba(165,243,252,0.75)");
  g.addColorStop(0.6, "rgba(34,211,238,0.28)"); g.addColorStop(1, "rgba(34,211,238,0)");
  ctx.fillStyle = g; ctx.beginPath(); ctx.ellipse(0, 0, 16, 8, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#ffffff"; ctx.beginPath(); ctx.ellipse(3, 0, 6, 2.4, 0, 0, Math.PI * 2); ctx.fill();
  return c;
}

function bladeSprite(): HTMLCanvasElement {
  const c = document.createElement("canvas");
  c.width = 44; c.height = 22;
  const ctx = c.getContext("2d")!; ctx.translate(22, 11);
  const g = ctx.createRadialGradient(0, 0, 1, 0, 0, 21);
  g.addColorStop(0, "rgba(255,255,255,0.95)"); g.addColorStop(0.3, "rgba(103,232,249,0.8)");
  g.addColorStop(0.7, "rgba(34,211,238,0.25)"); g.addColorStop(1, "rgba(34,211,238,0)");
  ctx.fillStyle = g; ctx.beginPath(); ctx.ellipse(0, 0, 21, 7.5, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#ffffff"; ctx.beginPath(); ctx.ellipse(0, 0, 12, 2.2, 0, 0, Math.PI * 2); ctx.fill();
  return c;
}

function iceBoltSprite(): HTMLCanvasElement {
  const c = document.createElement("canvas"); c.width = 36; c.height = 20;
  const ctx = c.getContext("2d")!; ctx.translate(18, 10);
  // ice crystal shape
  ctx.fillStyle = "rgba(255,255,255,0.3)"; ctx.beginPath();
  ctx.moveTo(14, 0); ctx.lineTo(2, -5); ctx.lineTo(-12, -2); ctx.lineTo(-14, 0); ctx.lineTo(-12, 2); ctx.lineTo(2, 5); ctx.closePath(); ctx.fill();
  ctx.fillStyle = "rgba(165,243,252,0.7)"; ctx.beginPath(); ctx.ellipse(2, 0, 10, 3, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#e0f7fa"; ctx.beginPath(); ctx.ellipse(6, 0, 5, 2, 0, 0, Math.PI * 2); ctx.fill();
  return c;
}

function boltSprite(): HTMLCanvasElement {
  const c = document.createElement("canvas"); c.width = 40; c.height = 16;
  const ctx = c.getContext("2d")!; ctx.translate(20, 8);
  const g = ctx.createRadialGradient(2, 0, 0.5, 2, 0, 18);
  g.addColorStop(0, "rgba(255,255,255,0.95)"); g.addColorStop(0.2, "rgba(250,204,21,0.8)");
  g.addColorStop(0.5, "rgba(250,204,21,0.25)"); g.addColorStop(1, "rgba(250,204,21,0)");
  ctx.fillStyle = g; ctx.beginPath(); ctx.ellipse(0, 0, 18, 7, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#fef9c3"; ctx.beginPath(); ctx.ellipse(4, 0, 7, 2, 0, 0, Math.PI * 2); ctx.fill();
  return c;
}

function plasmaSprite(): HTMLCanvasElement {
  const c = document.createElement("canvas"); c.width = 28; c.height = 28;
  const ctx = c.getContext("2d")!; ctx.translate(14, 14);
  const g = ctx.createRadialGradient(0, 0, 1, 0, 0, 13);
  g.addColorStop(0, "rgba(255,255,255,0.95)"); g.addColorStop(0.25, "rgba(165,243,252,0.85)");
  g.addColorStop(0.6, "rgba(103,232,249,0.3)"); g.addColorStop(1, "rgba(103,232,249,0)");
  ctx.fillStyle = g; ctx.beginPath(); ctx.arc(0, 0, 13, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#ffffff"; ctx.beginPath(); ctx.arc(0, 0, 4, 0, Math.PI * 2); ctx.fill();
  return c;
}

function novaSprite(): HTMLCanvasElement {
  const c = document.createElement("canvas"); c.width = 48; c.height = 48;
  const ctx = c.getContext("2d")!; ctx.translate(24, 24);
  const g = ctx.createRadialGradient(0, 0, 2, 0, 0, 22);
  g.addColorStop(0, "rgba(255,255,255,0.95)"); g.addColorStop(0.15, "rgba(250,204,21,0.85)");
  g.addColorStop(0.45, "rgba(251,146,60,0.35)"); g.addColorStop(0.75, "rgba(239,68,68,0.12)");
  g.addColorStop(1, "rgba(239,68,68,0)");
  ctx.fillStyle = g; ctx.beginPath(); ctx.arc(0, 0, 22, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#fef08a"; ctx.beginPath(); ctx.arc(0, 0, 5, 0, Math.PI * 2); ctx.fill();
  return c;
}

function tendrilSprite(): HTMLCanvasElement {
  const c = document.createElement("canvas"); c.width = 28; c.height = 28;
  const ctx = c.getContext("2d")!; ctx.translate(14, 14);
  ctx.strokeStyle = "rgba(236,72,153,0.65)"; ctx.lineWidth = 2.5;
  for (let i = 0; i < 4; i++) {
    const a = (i / 4) * Math.PI * 2;
    ctx.beginPath(); ctx.moveTo(0, 0);
    ctx.quadraticCurveTo(Math.cos(a) * 6, Math.sin(a) * 6, Math.cos(a) * 12, Math.sin(a) * 12);
    ctx.stroke();
  }
  ctx.fillStyle = "rgba(236,72,153,0.8)"; ctx.beginPath(); ctx.arc(0, 0, 4, 0, Math.PI * 2); ctx.fill();
  return c;
}

function mineSprite(): HTMLCanvasElement {
  const c = document.createElement("canvas"); c.width = 22; c.height = 22;
  const ctx = c.getContext("2d")!; ctx.translate(11, 11);
  const g = ctx.createRadialGradient(0, 0, 1, 0, 0, 9);
  g.addColorStop(0, "rgba(251,191,36,0.95)"); g.addColorStop(0.5, "rgba(245,158,11,0.55)");
  g.addColorStop(1, "rgba(245,158,11,0)");
  ctx.fillStyle = g; ctx.beginPath(); ctx.arc(0, 0, 9, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#fef3c7"; ctx.beginPath(); ctx.arc(0, 0, 3, 0, Math.PI * 2); ctx.fill();
  return c;
}

function gemSprite(r: number, color: string): HTMLCanvasElement {
  const pad = r + 12;
  const [c, ctx] = cv((r + pad) * 2);
  glow(ctx, r + pad, color, 0.55);
  poly(ctx, [
    [0, -r],
    [r * 0.72, 0],
    [0, r],
    [-r * 0.72, 0],
  ]);
  const g = ctx.createLinearGradient(0, -r, 0, r);
  g.addColorStop(0, "#ffffff");
  g.addColorStop(0.35, color);
  g.addColorStop(1, hexA(color, 0.55));
  ctx.fillStyle = g;
  ctx.fill();
  ctx.lineWidth = 1.5;
  ctx.strokeStyle = hexA("#ffffff", 0.8);
  ctx.stroke();
  return c;
}

function ringSprite(): HTMLCanvasElement {
  const [c, ctx] = cv(128);
  ctx.beginPath();
  ctx.arc(0, 0, 52, 0, Math.PI * 2);
  ctx.lineWidth = 7;
  ctx.strokeStyle = "rgba(255,255,255,0.9)";
  ctx.stroke();
  ctx.beginPath();
  ctx.arc(0, 0, 52, 0, Math.PI * 2);
  ctx.lineWidth = 16;
  ctx.strokeStyle = "rgba(255,255,255,0.22)";
  ctx.stroke();
  return c;
}

function dotSprite(color: string): HTMLCanvasElement {
  const [c, ctx] = cv(24);
  const g = ctx.createRadialGradient(0, 0, 0.5, 0, 0, 12);
  g.addColorStop(0, "rgba(255,255,255,0.95)");
  g.addColorStop(0.3, hexA(color, 0.9));
  g.addColorStop(1, hexA(color, 0));
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.arc(0, 0, 12, 0, Math.PI * 2);
  ctx.fill();
  return c;
}

export function makeSprites(): SpriteSet {
  const enemy: Record<string, HTMLCanvasElement> = {};
  const enemyHit: Record<string, HTMLCanvasElement> = {};
  const defs: [string, number, string, ShapeFn][] = [
    ["chaser", 15, "#fb7185", (ctx, r) => starPath(ctx, 7, r, r * 0.62)],
    ["runner", 8, "#a78bfa", (ctx, r) => { ctx.beginPath(); ctx.moveTo(0, -r); ctx.lineTo(r * 0.6, r * 0.8); ctx.lineTo(-r * 0.6, r * 0.8); ctx.closePath(); }],
    ["tank", 28, "#fb923c", (ctx, r) => ngon(ctx, 6, r)],
    ["shooter", 14, "#ef4444", (ctx, r) => poly(ctx, [[0, -r], [r, 0], [0, r], [-r, 0]])],
    ["charger", 14, "#e879f9", (ctx, r) => poly(ctx, [[r, 0], [-r * 0.6, r * 0.8], [-r * 0.3, 0], [-r * 0.6, -r * 0.8]])],
    ["exploder", 12, "#f59e0b", (ctx, r) => starPath(ctx, 4, r, r * 0.55)],
    ["splitter", 22, "#34d399", (ctx, r) => { ctx.beginPath(); for (let i = 0; i < 8; i++) { const a = (i / 8) * Math.PI * 2; const rr = i % 2 === 0 ? r : r * 0.7; i === 0 ? ctx.moveTo(Math.cos(a) * rr, Math.sin(a) * rr) : ctx.lineTo(Math.cos(a) * rr, Math.sin(a) * rr); } ctx.closePath(); }],
    ["shielded", 18, "#60a5fa", (ctx, r) => { ctx.beginPath(); ctx.arc(0, 0, r, 0, Math.PI * 2); ctx.moveTo(r * 0.5, -r); ctx.lineTo(-r * 0.5, -r); ctx.lineTo(-r * 0.5, -r * 1.3); ctx.lineTo(r * 0.5, -r * 1.3); ctx.closePath(); }],
    ["healer", 16, "#f9a8d4", (ctx, r) => { ctx.beginPath(); const w = r * 0.35; ctx.moveTo(-w, -r); ctx.lineTo(w, -r); ctx.lineTo(w, -w); ctx.lineTo(r, -w); ctx.lineTo(r, w); ctx.lineTo(w, w); ctx.lineTo(w, r); ctx.lineTo(-w, r); ctx.lineTo(-w, w); ctx.lineTo(-r, w); ctx.lineTo(-r, -w); ctx.lineTo(-w, -w); ctx.closePath(); }],
    ["summoner", 20, "#c084fc", (ctx, r) => starPath(ctx, 5, r, r * 0.5)],
    ["ambusher", 11, "#fbbf24", (ctx, r) => { ctx.beginPath(); ctx.moveTo(r, 0); ctx.lineTo(-r * 0.5, r); ctx.lineTo(-r * 0.2, 0); ctx.lineTo(-r * 0.5, -r); ctx.closePath(); }],
    ["swarmer", 6, "#86efac", (ctx, r) => starPath(ctx, 6, r, r * 0.6)],
    // All enemies use the void-orb style: dark core + colored glow + eye
    // Shapes define the OUTER silhouette only; enemySprite() handles fill/stroke/eye
    // All enemies use the void-orb-with-glow style; shapes define the OUTER silhouette
    ["spider", 13, "#c084fc", (ctx, r) => { ctx.beginPath(); ctx.arc(0,0,r*0.75,0,Math.PI*2); for(let i=0;i<8;i++){const a=(i/8)*Math.PI*2;ctx.moveTo(Math.cos(a)*r*0.75,Math.sin(a)*r*0.75);ctx.lineTo(Math.cos(a)*r,Math.sin(a)*r);} }],
    ["phantom", 16, "#818cf8", (ctx, r) => { ctx.beginPath();ctx.moveTo(0,-r);ctx.lineTo(r*0.5,-r*0.3);ctx.lineTo(r,0.2*r);ctx.lineTo(r*0.3,r);ctx.lineTo(-r*0.3,r);ctx.lineTo(-r,0.2*r);ctx.lineTo(-r*0.5,-r*0.3);ctx.closePath(); }],
    ["golem", 32, "#94a3b8", (ctx, r) => { ctx.beginPath();ctx.moveTo(0,-r);ctx.lineTo(r*0.8,-r*0.4);ctx.lineTo(r,r*0.2);ctx.lineTo(r*0.6,r);ctx.lineTo(-r*0.6,r);ctx.lineTo(-r,r*0.2);ctx.lineTo(-r*0.8,-r*0.4);ctx.closePath(); }],
    ["wisp", 8, "#38bdf8", (ctx, r) => { ctx.beginPath(); ctx.arc(0, 0, r, 0, Math.PI * 2); }],
    // 20 NEW ENEMIES — distinct recognizable silhouettes
    ["vortex", 20, "#a855f7", (ctx, r) => { ctx.beginPath(); for(let i=0;i<24;i++){const a=(i/24)*Math.PI*4;const rr=r*(0.3+0.7*(1-i/24));ctx.lineTo(Math.cos(a)*rr,Math.sin(a)*rr);}ctx.closePath(); }],
    ["broodmother", 24, "#22c55e", (ctx, r) => { ctx.beginPath();ctx.arc(0,0,r*0.7,0,Math.PI*2);for(let i=0;i<6;i++){const a=(i/6)*Math.PI*2;ctx.moveTo(0,0);ctx.lineTo(Math.cos(a)*r,Math.sin(a)*r);ctx.lineTo(Math.cos(a+0.3)*r*0.5,Math.sin(a+0.3)*r*0.5);} }],
    ["jumper", 10, "#f97316", (ctx, r) => { ctx.beginPath();ctx.moveTo(0,-r);ctx.quadraticCurveTo(r*0.6,-r*0.2,r*0.5,r*0.7);ctx.lineTo(0,r*0.4);ctx.lineTo(-r*0.5,r*0.7);ctx.quadraticCurveTo(-r*0.6,-r*0.2,0,-r);ctx.closePath(); }],
    ["sniper", 12, "#dc2626", (ctx, r) => { ctx.beginPath();ctx.arc(0,0,r*0.45,0,Math.PI*2);ctx.moveTo(-r,0);ctx.lineTo(-r*0.5,-r*0.15);ctx.lineTo(-r*0.5,r*0.15);ctx.closePath();ctx.moveTo(r*0.3,-r*0.1);ctx.lineTo(r,-r*0.15);ctx.lineTo(r,r*0.15);ctx.lineTo(r*0.3,r*0.1);ctx.closePath(); }],
    ["phaser", 14, "#6366f1", (ctx, r) => { ctx.beginPath();ctx.moveTo(r,0);ctx.lineTo(r*0.2,r*0.5);ctx.lineTo(-r*0.5,r*0.5);ctx.lineTo(-r,0);ctx.lineTo(-r*0.5,-r*0.5);ctx.lineTo(r*0.2,-r*0.5);ctx.closePath(); }],
    ["bomber", 14, "#eab308", (ctx, r) => { ctx.beginPath();ctx.arc(0,0,r*0.6,0,Math.PI*2);for(let i=0;i<4;i++){const a=(i/4)*Math.PI*2;ctx.moveTo(Math.cos(a)*r*0.6,Math.sin(a)*r*0.6);ctx.lineTo(Math.cos(a)*r,Math.sin(a)*r);} }],
    ["leech", 10, "#e11d48", (ctx, r) => { ctx.beginPath();ctx.moveTo(r,0);ctx.quadraticCurveTo(r*0.5,r*0.6,0,r*0.7);ctx.quadraticCurveTo(-r*0.5,r*0.8,-r*0.8,r*0.5);ctx.quadraticCurveTo(-r,r*0.2,-r,0);ctx.quadraticCurveTo(-r,-r*0.3,-r*0.7,-r*0.5);ctx.quadraticCurveTo(-r*0.3,-r*0.6,0,-r*0.5);ctx.quadraticCurveTo(r*0.3,-r*0.4,r*0.8,-r*0.2);ctx.quadraticCurveTo(r*0.9,0,r,0); }],
    ["mirror", 16, "#e5e7eb", (ctx, r) => { ctx.beginPath();ctx.moveTo(0,-r);ctx.lineTo(r*0.6,0);ctx.lineTo(0,r);ctx.lineTo(-r*0.6,0);ctx.closePath();ctx.moveTo(r*0.3,-r*0.5);ctx.lineTo(r,0);ctx.lineTo(r*0.3,r*0.5);ctx.closePath(); }],
    ["swarmlord", 26, "#059669", (ctx, r) => { ctx.beginPath();ctx.arc(0,0,r*0.65,0,Math.PI*2);for(let i=0;i<8;i++){const a=(i/8)*Math.PI*2;ctx.moveTo(Math.cos(a)*r*0.65,Math.sin(a)*r*0.65);ctx.lineTo(Math.cos(a)*r,Math.sin(a)*r);ctx.lineTo(Math.cos(a+0.2)*r*0.85,Math.sin(a+0.2)*r*0.85);} }],
    ["crawler", 7, "#84cc16", (ctx, r) => { ctx.beginPath();ctx.ellipse(0,0,r*1.4,r*0.5,0,0,Math.PI*2); }],
    ["wall", 22, "#60a5fa", (ctx, r) => { ctx.beginPath();ctx.rect(-r,-r*0.3,r*2,r*0.6); }],
    ["pulsar", 18, "#e879f9", (ctx, r) => { ctx.beginPath();ctx.arc(0,0,r*0.5,0,Math.PI*2);for(let i=0;i<12;i++){const a=(i/12)*Math.PI*2;ctx.moveTo(Math.cos(a)*r*0.5,Math.sin(a)*r*0.5);ctx.lineTo(Math.cos(a)*r,Math.sin(a)*r);} }],
    ["lancer", 12, "#06b6d4", (ctx, r) => { ctx.beginPath();ctx.moveTo(r*1.2,0);ctx.lineTo(r*0.1,r*0.4);ctx.lineTo(-r*0.3,r*0.8);ctx.lineTo(-r*0.2,0);ctx.lineTo(-r*0.3,-r*0.8);ctx.lineTo(r*0.1,-r*0.4);ctx.closePath(); }],
    ["necrofiend", 18, "#7c3aed", (ctx, r) => { ctx.beginPath();ctx.moveTo(0,-r);ctx.lineTo(r*0.4,-r*0.5);ctx.lineTo(r*0.6,-r*0.2);ctx.lineTo(r*0.5,r*0.3);ctx.lineTo(r*0.2,r*0.8);ctx.lineTo(0,r*0.5);ctx.lineTo(-r*0.2,r*0.8);ctx.lineTo(-r*0.5,r*0.3);ctx.lineTo(-r*0.6,-r*0.2);ctx.lineTo(-r*0.4,-r*0.5);ctx.closePath(); }],
    ["warper", 15, "#8b5cf6", (ctx, r) => { ctx.beginPath();for(let i=0;i<6;i++){const a=(i/6)*Math.PI*2;const rr=i%2===0?r:r*0.4;ctx.lineTo(Math.cos(a)*rr,Math.sin(a)*rr);}ctx.closePath(); }],
    ["toxin", 13, "#16a34a", (ctx, r) => { ctx.beginPath();ctx.arc(0,0,r*0.6,0,Math.PI*2);for(let i=0;i<3;i++){const a=(i/3)*Math.PI*2;ctx.moveTo(Math.cos(a)*r*0.6,Math.sin(a)*r*0.6);ctx.quadraticCurveTo(Math.cos(a+0.5)*r,Math.sin(a+0.5)*r,Math.cos(a+0.3)*r*0.8,Math.sin(a+0.3)*r*0.8);} }],
    ["anchor", 16, "#475569", (ctx, r) => { ctx.beginPath();ctx.moveTo(0,-r);ctx.lineTo(r*0.6,r*0.2);ctx.lineTo(r*0.8,r*0.8);ctx.lineTo(0,r*0.5);ctx.lineTo(-r*0.8,r*0.8);ctx.lineTo(-r*0.6,r*0.2);ctx.closePath(); }],
    ["blink", 11, "#38bdf8", (ctx, r) => { ctx.beginPath();for(let i=0;i<16;i++){const a=(i/16)*Math.PI*2;const rr=i%3===0?r:r*0.5;ctx.lineTo(Math.cos(a)*rr,Math.sin(a)*rr);}ctx.closePath(); }],
    ["colossus", 40, "#64748b", (ctx, r) => { ctx.beginPath();ctx.moveTo(0,-r);ctx.lineTo(r*0.7,-r*0.8);ctx.lineTo(r*0.9,-r*0.2);ctx.lineTo(r*0.6,r*0.3);ctx.lineTo(r*0.8,r*0.8);ctx.lineTo(r*0.3,r);ctx.lineTo(-r*0.3,r);ctx.lineTo(-r*0.8,r*0.8);ctx.lineTo(-r*0.6,r*0.3);ctx.lineTo(-r*0.9,-r*0.2);ctx.lineTo(-r*0.7,-r*0.8);ctx.closePath(); }],
    ["rift", 22, "#7c3aed", (ctx, r) => { ctx.beginPath();ctx.moveTo(0,-r);ctx.quadraticCurveTo(r,0,0,r);ctx.quadraticCurveTo(-r,0,0,-r);ctx.closePath();ctx.moveTo(r*0.4,-r*0.3);ctx.lineTo(r*0.8,-r*0.2);ctx.lineTo(r*0.4,r*0.3);ctx.closePath();ctx.moveTo(-r*0.4,-r*0.3);ctx.lineTo(-r*0.8,-r*0.2);ctx.lineTo(-r*0.4,r*0.3);ctx.closePath(); }],
    // HYDRA — multi-headed serpent
    ["hydra", 28, "#ef4444", (ctx, r) => { ctx.beginPath();ctx.arc(0,0,r*0.6,0,Math.PI*2);for(let i=0;i<3;i++){const a=-0.4+i*0.4;ctx.moveTo(0,-r*0.4);ctx.quadraticCurveTo(Math.cos(a)*r*0.5,-r,Math.cos(a)*r*0.8,-r*1.1);ctx.lineTo(Math.cos(a)*r*0.6,-r*0.9);ctx.quadraticCurveTo(Math.cos(a)*r*0.3,-r*0.5,0,-r*0.4);} }],
    // MIMIC — looks like a gem/chest
    ["mimic", 12, "#d4a017", (ctx, r) => { ctx.beginPath();ctx.moveTo(-r,-r*0.3);ctx.lineTo(-r*0.8,-r);ctx.lineTo(r*0.8,-r);ctx.lineTo(r,-r*0.3);ctx.lineTo(r,r*0.3);ctx.lineTo(r*0.8,r);ctx.lineTo(-r*0.8,r);ctx.lineTo(-r,r*0.3);ctx.closePath();ctx.moveTo(-r*0.3,-r*0.3);ctx.lineTo(r*0.3,-r*0.3);ctx.lineTo(r*0.3,r*0.3);ctx.lineTo(-r*0.3,r*0.3);ctx.closePath(); }],
    // SIREN — alluring, curved, musical
    ["siren", 16, "#f472b6", (ctx, r) => { ctx.beginPath();ctx.arc(0,0,r*0.5,0,Math.PI*2);for(let i=0;i<6;i++){const a=(i/6)*Math.PI*2;const rr=r*(0.6+0.4*Math.sin(a*3));ctx.moveTo(0,0);ctx.quadraticCurveTo(Math.cos(a+0.2)*r*0.7,Math.sin(a+0.2)*r*0.7,Math.cos(a)*rr,Math.sin(a)*rr);} }],
    // WRAITHLORD — dark crown, imposing
    ["wraithlord", 30, "#7c3aed", (ctx, r) => { ctx.beginPath();ctx.moveTo(0,-r);ctx.lineTo(r*0.3,-r*0.6);ctx.lineTo(r*0.6,-r*0.9);ctx.lineTo(r*0.5,-r*0.4);ctx.lineTo(r,-r*0.2);ctx.lineTo(r*0.7,r*0.2);ctx.lineTo(r*0.5,r*0.8);ctx.lineTo(0,r*0.4);ctx.lineTo(-r*0.5,r*0.8);ctx.lineTo(-r*0.7,r*0.2);ctx.lineTo(-r,-r*0.2);ctx.lineTo(-r*0.5,-r*0.4);ctx.lineTo(-r*0.6,-r*0.9);ctx.lineTo(-r*0.3,-r*0.6);ctx.closePath(); }],
  ];
  for (const [name, r, color, shape] of defs) {
    enemy[name] = enemySprite(r, color, shape, false);
    enemyHit[name] = enemySprite(r, color, shape, true);
  }

  const dots: Record<string, HTMLCanvasElement> = {};
  for (const [name, color] of Object.entries({
    cyan: "#22d3ee",
    pink: "#fb7185",
    violet: "#a78bfa",
    fuchsia: "#e879f9",
    orange: "#fb923c",
    red: "#ef4444",
    emerald: "#34d399",
    lime: "#a3e635",
    white: "#e2e8f0",
    yellow: "#facc15",
    rose: "#f9a8d4",
    purple: "#c084fc",
    amber: "#fbbf24",
    indigo: "#818cf8", slate: "#94a3b8", sky: "#38bdf8",
    vortex: "#a855f7", green: "#22c55e", orangeBright: "#f97316", crimson: "#dc2626",
    phase: "#6366f1", gold: "#eab308", blood: "#e11d48", silver: "#e5e7eb",
    blue: "#60a5fa", pinkGlow: "#e879f9", teal: "#06b6d4", voidPurple: "#7c3aed",
    warp: "#8b5cf6", toxic: "#16a34a", anchor: "#475569", blink: "#38bdf8",
    colossus: "#64748b", riftPurple: "#7c3aed",
    hydra: "#ef4444", mimic: "#d4a017", siren: "#f472b6", wraithlord: "#7c3aed",
  })) {
    dots[name] = dotSprite(color);
  }

  // Form-specific player sprites
  const players: Record<string, HTMLCanvasElement> = {};
  const playerRings: Record<string, HTMLCanvasElement> = {};
  const formDefs: [string, string, string][] = [
    ["sentinel", "#22d3ee", "#a5f3fc"],
    ["phantom", "#e879f9", "#f0abfc"],
    ["bastion", "#fb923c", "#fdba74"],
    ["overseer", "#a78bfa", "#c4b5fd"],
    ["wraith", "#f43f5e", "#fda4af"],
    ["elemental", "#67e8f9", "#a5f3fc"],
    ["chronomancer", "#facc15", "#fde047"],
    ["necromancer", "#34d399", "#6ee7b7"],
    ["missile", "#ef4444", "#fca5a5"],
    ["spider", "#8b5cf6", "#c4b5fd"],
    ["tank", "#64748b", "#94a3b8"],
  ];
  for (const [id, color, accent] of formDefs) {
    if (id === "missile") {
      // Sleek arrow/rocket — pointed forward, swept wings
      players[id] = formPlayerSprite(color, accent, (ctx, r) => {
        ctx.beginPath();
        ctx.moveTo(r * 1.15, 0);
        ctx.lineTo(r * 0.1, r * 0.5);
        ctx.lineTo(r * 0.3, r * 0.9);
        ctx.lineTo(0, r * 0.3);
        ctx.lineTo(-r * 0.7, r * 0.5);
        ctx.lineTo(-r * 0.3, 0);
        ctx.lineTo(-r * 0.7, -r * 0.5);
        ctx.lineTo(0, -r * 0.3);
        ctx.lineTo(r * 0.3, -r * 0.9);
        ctx.lineTo(r * 0.1, -r * 0.5);
        ctx.closePath();
      });
    } else if (id === "spider") {
      // Spider: round body with 8 protruding legs
      players[id] = formPlayerSprite(color, accent, (ctx, r) => {
        ctx.beginPath();
        ctx.arc(0, 0, r * 0.75, 0, Math.PI * 2);
        for (let i = 0; i < 8; i++) {
          const a = (i / 8) * Math.PI * 2;
          const bend = 0.4;
          ctx.moveTo(Math.cos(a) * r * 0.75, Math.sin(a) * r * 0.75);
          ctx.lineTo(Math.cos(a + bend) * r * 0.95, Math.sin(a + bend) * r * 0.95);
          ctx.lineTo(Math.cos(a) * r * 1.15, Math.sin(a) * r * 1.15);
        }
      });
    } else if (id === "tank") {
      // Tank: heavy body with front armor plate
      players[id] = formPlayerSprite(color, accent, (ctx, r) => {
        ctx.beginPath();
        ctx.arc(0, 0, r * 0.8, 0, Math.PI * 2);
        ctx.moveTo(r * 0.7, -r * 0.4);
        ctx.lineTo(r * 1.1, -r * 0.2);
        ctx.lineTo(r * 1.1, r * 0.2);
        ctx.lineTo(r * 0.7, r * 0.4);
        ctx.closePath();
      });
    } else if (id === "bastion") {
      // Hexagonal fortress
      players[id] = formPlayerSprite(color, accent, (ctx, r) => {
        ngon(ctx, 6, r * 0.95, 0);
      });
    } else if (id === "phantom") {
      // Sleek stealth diamond
      players[id] = formPlayerSprite(color, accent, (ctx, r) => {
        ctx.beginPath();
        ctx.moveTo(0, -r);
        ctx.lineTo(r * 0.7, -r * 0.1);
        ctx.lineTo(r * 0.3, r * 0.8);
        ctx.lineTo(-r * 0.3, r * 0.8);
        ctx.lineTo(-r * 0.7, -r * 0.1);
        ctx.closePath();
      });
    } else if (id === "chronomancer") {
      // Clock/gear shape
      players[id] = formPlayerSprite(color, accent, (ctx, r) => {
        ctx.beginPath();
        ctx.arc(0, 0, r * 0.7, 0, Math.PI * 2);
        for (let i = 0; i < 10; i++) {
          const a = (i / 10) * Math.PI * 2;
          ctx.moveTo(Math.cos(a) * r * 0.7, Math.sin(a) * r * 0.7);
          ctx.lineTo(Math.cos(a) * r, Math.sin(a) * r);
        }
      });
    } else if (id === "necromancer") {
      // Ghostly skull-like shape
      players[id] = formPlayerSprite(color, accent, (ctx, r) => {
        ctx.beginPath();
        ctx.moveTo(0, -r);
        ctx.quadraticCurveTo(r, -r * 0.5, r * 0.8, r * 0.2);
        ctx.quadraticCurveTo(r * 0.5, r, 0, r * 0.7);
        ctx.quadraticCurveTo(-r * 0.5, r, -r * 0.8, r * 0.2);
        ctx.quadraticCurveTo(-r, -r * 0.5, 0, -r);
      });
    } else if (id === "wraith") {
      // Ghostly jagged shape
      players[id] = formPlayerSprite(color, accent, (ctx, r) => {
        ctx.beginPath();
        ctx.moveTo(0, -r);
        for (let i = 0; i < 6; i++) {
          const a = (i / 6) * Math.PI + Math.PI / 12;
          const rr = i % 2 === 0 ? r : r * 0.65;
          ctx.lineTo(Math.cos(a + Math.PI) * rr, Math.sin(a + Math.PI) * rr);
        }
        ctx.lineTo(0, r * 0.8);
        ctx.lineTo(-r * 0.3, r);
        ctx.lineTo(r * 0.3, r);
        ctx.closePath();
      });
    } else {
      players[id] = formPlayerSprite(color, accent);
    }
    playerRings[id] = formRingSprite(color, accent);
  }

  return {
    player: playerSprite(), playerRing: playerRingSprite(),
    players, playerRings,
    bullet: bulletSprite(), blade: bladeSprite(),
    iceBolt: iceBoltSprite(), bolt: boltSprite(),
    plasma: plasmaSprite(), nova: novaSprite(),
    tendril: tendrilSprite(), mine: mineSprite(),
    gemS: gemSprite(7, "#34d399"), gemM: gemSprite(9, "#4ade80"), gemL: gemSprite(12, "#a3e635"),
    ring: ringSprite(), enemy, enemyHit, dot: dots,
  };
}

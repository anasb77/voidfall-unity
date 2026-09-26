// Pre-rendered glow sprites. Everything expensive (radial gradients, glow) is baked
// once here so the runtime loop only does cheap drawImage calls.

export interface SpriteSet {
  player: HTMLCanvasElement;
  playerRing: HTMLCanvasElement;
  bullet: HTMLCanvasElement;
  blade: HTMLCanvasElement;
  gemS: HTMLCanvasElement;
  gemM: HTMLCanvasElement;
  gemL: HTMLCanvasElement;
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
  const r = 15;
  const pad = 22;
  const [c, ctx] = cv((r + pad) * 2);
  glow(ctx, r + pad, "#22d3ee", 0.6);
  const g = ctx.createRadialGradient(-4, -5, 2, 0, 0, r);
  g.addColorStop(0, "#ecfeff");
  g.addColorStop(0.45, "#67e8f9");
  g.addColorStop(1, "#0e7490");
  ctx.beginPath();
  ctx.arc(0, 0, r, 0, Math.PI * 2);
  ctx.fillStyle = g;
  ctx.fill();
  ctx.lineWidth = 2.5;
  ctx.strokeStyle = "#a5f3fc";
  ctx.stroke();
  // inner dark core — the "void" eye
  ctx.beginPath();
  ctx.arc(0, 0, r * 0.45, 0, Math.PI * 2);
  ctx.fillStyle = "#05060f";
  ctx.fill();
  ctx.beginPath();
  ctx.arc(0, 0, r * 0.2, 0, Math.PI * 2);
  ctx.fillStyle = "#e0faff";
  ctx.fill();
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
  c.width = 34;
  c.height = 18;
  const ctx = c.getContext("2d")!;
  ctx.translate(17, 9);
  const g = ctx.createRadialGradient(2, 0, 0.5, 2, 0, 16);
  g.addColorStop(0, "rgba(255,255,255,0.95)");
  g.addColorStop(0.25, "rgba(165,243,252,0.75)");
  g.addColorStop(0.6, "rgba(34,211,238,0.28)");
  g.addColorStop(1, "rgba(34,211,238,0)");
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.ellipse(0, 0, 16, 8, 0, 0, Math.PI * 2);
  ctx.fill();
  // hot core
  ctx.fillStyle = "#ffffff";
  ctx.beginPath();
  ctx.ellipse(3, 0, 6, 2.4, 0, 0, Math.PI * 2);
  ctx.fill();
  return c;
}

function bladeSprite(): HTMLCanvasElement {
  const c = document.createElement("canvas");
  c.width = 44;
  c.height = 22;
  const ctx = c.getContext("2d")!;
  ctx.translate(22, 11);
  const g = ctx.createRadialGradient(0, 0, 1, 0, 0, 21);
  g.addColorStop(0, "rgba(255,255,255,0.95)");
  g.addColorStop(0.3, "rgba(103,232,249,0.8)");
  g.addColorStop(0.7, "rgba(34,211,238,0.25)");
  g.addColorStop(1, "rgba(34,211,238,0)");
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.ellipse(0, 0, 21, 7.5, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = "#ffffff";
  ctx.beginPath();
  ctx.ellipse(0, 0, 12, 2.2, 0, 0, Math.PI * 2);
  ctx.fill();
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
    ["imp", 10, "#a78bfa", (ctx, r) => poly(ctx, [[0, -r], [r * 0.8, 0], [0, r], [-r * 0.8, 0]])],
    ["dasher", 12, "#e879f9", (ctx, r) => poly(ctx, [[r, 0], [-r * 0.7, r * 0.8], [-r * 0.3, 0], [-r * 0.7, -r * 0.8]])],
    ["brute", 24, "#fb923c", (ctx, r) => ngon(ctx, 6, r)],
    ["elite", 38, "#ef4444", (ctx, r) => ngon(ctx, 5, r)],
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
  })) {
    dots[name] = dotSprite(color);
  }

  return {
    player: playerSprite(),
    playerRing: playerRingSprite(),
    bullet: bulletSprite(),
    blade: bladeSprite(),
    gemS: gemSprite(7, "#34d399"),
    gemM: gemSprite(9, "#4ade80"),
    gemL: gemSprite(12, "#a3e635"),
    ring: ringSprite(),
    enemy,
    enemyHit,
    dot: dots,
  };
}

'use strict';

const fs = require('fs');
const path = require('path');
const vm = require('vm');

const ROOT = path.resolve(__dirname, '..', '..');
const OUTPUT = path.join(ROOT, 'Assets', 'VoidFall', 'Art', 'NullCity');
const AUTHORING_WIDTH = 1600;
const AUTHORING_HEIGHT = 900;
const SPRITE_SCALE = 4;

function loadCanvasModule() {
  const candidates = [
    '@napi-rs/canvas',
    process.env.NULL_CITY_CANVAS_MODULE,
    process.env.USERPROFILE && path.join(
      process.env.USERPROFILE,
      '.cache', 'codex-runtimes', 'codex-primary-runtime', 'dependencies',
      'node', 'node_modules', '@napi-rs', 'canvas'),
  ].filter(Boolean);

  for (const candidate of candidates) {
    try { return require(candidate); } catch (_) { /* Try the next deterministic runtime. */ }
  }

  throw new Error(
    'Could not load @napi-rs/canvas. Install it or set NULL_CITY_CANVAS_MODULE.');
}

const { createCanvas, Path2D } = loadCanvasModule();

function loadApprovedArt() {
  const context = vm.createContext({
    console,
    Math,
    Path2D,
    window: {},
    document: {
      createElement(tag) {
        if (tag !== 'canvas') throw new Error(`Unsupported authoring element: ${tag}`);
        return createCanvas(1, 1);
      },
    },
  });
  vm.runInContext(
    fs.readFileSync(path.join(__dirname, 'simulation.js'), 'utf8'),
    context,
    { filename: 'simulation.js' });
  vm.runInContext(
    fs.readFileSync(path.join(__dirname, 'art.js'), 'utf8'),
    context,
    { filename: 'art.js' });
  return { art: context.window.NullCityArt, sim: context.NullCitySim };
}

function ensureFolders() {
  fs.mkdirSync(path.join(OUTPUT, 'Units'), { recursive: true });
  fs.mkdirSync(path.join(OUTPUT, 'Props'), { recursive: true });
}

function writePng(canvas, relativePath) {
  const destination = path.join(OUTPUT, relativePath);
  fs.writeFileSync(destination, canvas.toBuffer('image/png'));
  return destination;
}

function fullCanvas(width, height, draw) {
  const canvas = createCanvas(width, height);
  const g = canvas.getContext('2d');
  g.scale(width / AUTHORING_WIDTH, height / AUTHORING_HEIGHT);
  draw(g);
  return canvas;
}

function cropCanvas(bounds, draw) {
  const canvas = createCanvas(bounds.width * SPRITE_SCALE, bounds.height * SPRITE_SCALE);
  const g = canvas.getContext('2d');
  g.scale(SPRITE_SCALE, SPRITE_SCALE);
  g.translate(-bounds.x, -bounds.y);
  draw(g);
  return canvas;
}

const units = [
  ['null-patrol', 0, 64, 64],
  ['null-enforcer', 1, 80, 80],
  ['null-sentinel', 2, 96, 72],
  ['null-crawler', 3, 80, 80],
  ['null-volatile', 4, 112, 112],
  ['null-gunship', 5, 136, 120],
  ['null-mech', 6, 128, 128],
  ['null-broodmother', 7, 200, 184],
  ['null-light-gunship', 8, 112, 96],
  ['null-interceptor', 9, 80, 80],
  ['null-marshal', 10, 104, 104],
  ['null-suppressor', 11, 96, 88],
  ['null-motherload', 12, 440, 320],
];

function renderUnit(art, type, width, height, time, hit, activity) {
  const canvas = createCanvas(width * SPRITE_SCALE, height * SPRITE_SCALE);
  const g = canvas.getContext('2d');
  g.scale(SPRITE_SCALE, SPRITE_SCALE);
  art.robot(g, type, width / 2, height / 2, 0, 1, time, hit, activity);
  return canvas;
}

function exportUnits(art) {
  const frameTimes = [0, 0.25, 0.5, 0.75];
  for (const [id, type, width, height] of units) {
    frameTimes.forEach((time, frame) => {
      writePng(renderUnit(art, type, width, height, time, false, 0),
        path.join('Units', `${id}-${frame}.png`));
    });
    writePng(renderUnit(art, type, width, height, 0, true, 0),
      path.join('Units', `${id}-hit.png`));
  }

  const boss = units[units.length - 1];
  for (const [state, activity] of [['exposed', -1], ['tractor', -3], ['tractor-warning', -4]]) {
    frameTimes.forEach((time, frame) => {
      writePng(renderUnit(art, boss[1], boss[2], boss[3], time, false, activity),
        path.join('Units', `null-motherload-${state}-${frame}.png`));
    });
  }

  const marshal = units.find(unit => unit[0] === 'null-marshal');
  frameTimes.forEach((time, frame) => {
    writePng(renderUnit(art, marshal[1], marshal[2], marshal[3], time, false, -2),
      path.join('Units', `null-marshal-braced-${frame}.png`));
  });
}

function exportProps(art) {
  const coreOff = { core: false, transit: false, traffic: false, hangar: false, lcd: false };
  const renderLive = (g, phase, phaseTime, dark, enabled) => {
    const layers = { ...coreOff, [enabled]: true };
    art.live(g, enabled === 'transit' ? (800 - 190) / 78 : 0,
      phase, phaseTime, dark, null, layers);
  };

  writePng(cropCanvas({ x: 650, y: 195, width: 190, height: 80 },
    g => renderLive(g, 0, 5, 0, 'transit')), 'Props/Transit.png');

  const hangarBounds = { x: 600, y: 710, width: 410, height: 180 };
  writePng(cropCanvas(hangarBounds,
    g => renderLive(g, 0, 0, 0, 'hangar')), 'Props/HangarClosed.png');
  writePng(cropCanvas(hangarBounds,
    g => renderLive(g, 1, 2, 0.82, 'hangar')), 'Props/HangarOpen.png');

  writePng(cropCanvas({ x: 170, y: 305, width: 58, height: 48 },
    g => renderLive(g, 0, 5, 0, 'traffic')), 'Props/Traffic.png');
  writePng(cropCanvas({ x: 1372, y: 305, width: 58, height: 48 },
    g => renderLive(g, 1, 5, 0.82, 'traffic')), 'Props/TrafficLockdown.png');

  const lcdBounds = { x: 1015, y: 65, width: 315, height: 85 };
  writePng(cropCanvas(lcdBounds,
    g => renderLive(g, 0, 5, 0, 'lcd')), 'Props/LcdSurveillance.png');
  writePng(cropCanvas(lcdBounds,
    g => renderLive(g, 1, 5, 0.82, 'lcd')), 'Props/LcdLockdown.png');
}

function main() {
  ensureFolders();
  const { art } = loadApprovedArt();
  // makeBackground also initializes the approved live-light source locations.
  const background = art.makeBackground();
  writePng(fullCanvas(3840, 2160, g => g.drawImage(background, 0, 0)),
    'NullCityBase.png');
  writePng(fullCanvas(2560, 1440, g => art.live(
    g, 5, 0, 5, 0, null,
    { core: true, transit: false, traffic: false, hangar: false, lcd: false })),
    'NullCityDetails.png');
  exportProps(art);
  exportUnits(art);
  console.log(`Null City authored art exported to ${OUTPUT}`);
}

main();

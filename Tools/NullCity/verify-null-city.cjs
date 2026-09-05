'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const ROOT = path.resolve(__dirname, '..', '..');
const OUTPUT = path.join(ROOT, 'Assets', 'VoidFall', 'Art', 'NullCity');

function canvasModule() {
  const candidates = [
    '@napi-rs/canvas',
    process.env.NULL_CITY_CANVAS_MODULE,
    process.env.USERPROFILE && path.join(
      process.env.USERPROFILE, '.cache', 'codex-runtimes', 'codex-primary-runtime',
      'dependencies', 'node', 'node_modules', '@napi-rs', 'canvas'),
  ].filter(Boolean);
  for (const candidate of candidates) {
    try { return require(candidate); } catch (_) { /* Try the next runtime. */ }
  }
  throw new Error('Could not load @napi-rs/canvas.');
}

const { createCanvas, loadImage } = canvasModule();

function pngFiles(folder) {
  return fs.readdirSync(folder, { withFileTypes: true }).flatMap(entry => {
    const full = path.join(folder, entry.name);
    return entry.isDirectory() ? pngFiles(full) : entry.name.endsWith('.png') ? [full] : [];
  }).sort();
}

function hashes(files) {
  return files.map(file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'));
}

function expect(condition, message) {
  if (!condition) throw new Error(message);
}

async function verifyDimensions(file, width, height) {
  const image = await loadImage(file);
  expect(image.width === width && image.height === height,
    `${path.relative(ROOT, file)} is ${image.width}x${image.height}; expected ${width}x${height}.`);
  return image;
}

async function verifyAlphaCorner(file) {
  const image = await loadImage(file);
  const canvas = createCanvas(image.width, image.height);
  const g = canvas.getContext('2d');
  g.drawImage(image, 0, 0);
  expect(g.getImageData(0, 0, 1, 1).data[3] < 255,
    `${path.relative(ROOT, file)} lost its alpha channel.`);
}

async function verifyTransparentCorners(file) {
  const image = await loadImage(file);
  const canvas = createCanvas(image.width, image.height);
  const g = canvas.getContext('2d');
  g.drawImage(image, 0, 0);
  const corners = [
    [0, 0],
    [image.width - 1, 0],
    [0, image.height - 1],
    [image.width - 1, image.height - 1],
  ];
  expect(corners.every(([x, y]) => g.getImageData(x, y, 1, 1).data[3] < 255),
    `${path.relative(ROOT, file)} contains an opaque crop background.`);
}

async function main() {
  const files = pngFiles(OUTPUT);
  expect(files.length === 90, `Expected 90 authored PNGs, found ${files.length}.`);
  const before = hashes(files);
  const exportResult = spawnSync(process.execPath, [path.join(__dirname, 'export-null-city.cjs')],
    { cwd: ROOT, encoding: 'utf8' });
  expect(exportResult.status === 0, exportResult.stderr || exportResult.stdout || 'Export failed.');
  expect(hashes(files).every((hash, index) => hash === before[index]),
    'A second export changed one or more PNG hashes.');

  await verifyDimensions(path.join(OUTPUT, 'NullCityBase.png'), 3840, 2160);
  await verifyDimensions(path.join(OUTPUT, 'NullCityDetails.png'), 2560, 1440);

  const props = {
    Transit: [760, 320],
    HangarClosed: [1640, 720],
    HangarOpen: [1640, 720],
    Traffic: [232, 192],
    TrafficLockdown: [232, 192],
    LcdSurveillance: [1260, 340],
    LcdLockdown: [1260, 340],
  };
  for (const [name, size] of Object.entries(props))
    await verifyDimensions(path.join(OUTPUT, 'Props', `${name}.png`), size[0], size[1]);
  for (const name of ['HangarClosed', 'HangarOpen', 'LcdSurveillance', 'LcdLockdown'])
    await verifyTransparentCorners(path.join(OUTPUT, 'Props', `${name}.png`));

  const units = {
    'null-patrol': [64, 64],
    'null-enforcer': [80, 80],
    'null-sentinel': [96, 72],
    'null-crawler': [80, 80],
    'null-volatile': [112, 112],
    'null-gunship': [136, 120],
    'null-mech': [128, 128],
    'null-broodmother': [200, 184],
    'null-light-gunship': [112, 96],
    'null-interceptor': [80, 80],
    'null-marshal': [104, 104],
    'null-suppressor': [96, 88],
    'null-motherload': [440, 320],
  };
  for (const [id, size] of Object.entries(units)) {
    for (let frame = 0; frame < 4; frame++)
      await verifyDimensions(path.join(OUTPUT, 'Units', `${id}-${frame}.png`),
        size[0] * 4, size[1] * 4);
    await verifyDimensions(path.join(OUTPUT, 'Units', `${id}-hit.png`),
      size[0] * 4, size[1] * 4);
  }

  for (const state of ['exposed', 'tractor', 'tractor-warning'])
    for (let frame = 0; frame < 4; frame++)
      await verifyDimensions(path.join(OUTPUT, 'Units', `null-motherload-${state}-${frame}.png`),
        1760, 1280);
  for (let frame = 0; frame < 4; frame++)
    await verifyDimensions(path.join(OUTPUT, 'Units', `null-marshal-braced-${frame}.png`), 416, 416);

  await verifyAlphaCorner(path.join(OUTPUT, 'NullCityDetails.png'));
  await verifyAlphaCorner(path.join(OUTPUT, 'Units', 'null-broodmother-0.png'));
  await verifyAlphaCorner(path.join(OUTPUT, 'Units', 'null-motherload-0.png'));
  console.log('Null City export verified: 90 deterministic PNGs, dimensions and transparency pass.');
}

main().catch(error => {
  console.error(error.stack || error.message || error);
  process.exitCode = 1;
});

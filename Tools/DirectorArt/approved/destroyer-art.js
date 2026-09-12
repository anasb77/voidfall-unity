/* Browser-only Destroyer silhouette study, revision 3. No gameplay or Unity state.
 * draw(ctx, id, x, y, radius, timeSeconds, {angle, state, reducedMotion, flash})
 * Forward = +X. States: idle, windup, attack, recover. Flash = 0..1.
 */
(function (global) {
  'use strict';

  const BLACK = '#050505';
  const WHITE = '#f5f5f5';
  const paths = new Map();
  const roster = [
    { id: 'maw', name: 'Maw', role: 'Rusher', tagline: 'All mouth. No hesitation.', color: '#fff' },
    { id: 'razorwing', name: 'Razor', role: 'Rusher', tagline: 'Hooked wings, one sudden dive.', color: '#fff' },
    { id: 'husk', name: 'Husk', role: 'Brute', tagline: 'A broken shape that will not yield.', color: '#fff' },
    { id: 'grasp', name: 'Grasp', role: 'Brute', tagline: 'Four jaws close around the void.', color: '#fff' },
    { id: 'spite', name: 'Spite', role: 'Ranged', tagline: 'Bow-shaped jaws spit from an empty throat.', color: '#fff' }
  ];

  function path(d) {
    let q = paths.get(d);
    if (!q) { q = new Path2D(d); paths.set(d, q); }
    return q;
  }

  function mass(ctx, d, p, width) {
    ctx.fillStyle = p.flash ? '#ddd' : BLACK;
    ctx.strokeStyle = WHITE;
    ctx.lineWidth = width || 0.064;
    const q = path(d);
    ctx.fill(q);
    ctx.stroke(q);
  }

  function white(ctx, d) {
    ctx.fillStyle = WHITE;
    ctx.fill(path(d));
  }

  function eye(ctx, x, y, radius) {
    ctx.fillStyle = WHITE;
    ctx.beginPath();ctx.ellipse(x,y,radius,radius*.83,0,0,Math.PI*2);ctx.fill();
    ctx.fillStyle = BLACK;
    ctx.beginPath();ctx.ellipse(x+radius*.16,y,radius*.56,radius*.30,0,0,Math.PI*2);ctx.fill();
  }

  // Eleven teeth on each jaw. Unequal lengths, roots and tip offsets keep
  // them individual at play size without hatching or texture.
  const upperTeeth = [
    [-.87, -.38, .115, .20, .022], [-.66, -.50, .12, .26, -.025],
    [-.44, -.58, .13, .29, .035], [-.22, -.61, .11, .22, -.015],
    [.00, -.62, .135, .34, .018], [.23, -.61, .115, .25, -.025],
    [.45, -.59, .135, .31, .033], [.67, -.55, .115, .23, -.014],
    [.88, -.49, .13, .27, .027], [1.08, -.41, .115, .22, -.02],
    [1.26, -.31, .105, .19, -.035]
  ];
  const lowerTeeth = [
    [-.83, .40, .12, .23, -.022], [-.62, .51, .11, .21, .025],
    [-.40, .58, .13, .32, -.035], [-.18, .61, .12, .25, .025],
    [.04, .62, .115, .27, -.016], [.26, .60, .13, .33, .03],
    [.48, .58, .115, .23, -.025], [.70, .53, .13, .31, .015],
    [.91, .47, .115, .22, -.028], [1.10, .39, .105, .25, .014],
    [1.28, .28, .10, .17, -.04]
  ];

  function teeth(ctx, row, direction) {
    ctx.fillStyle = WHITE;
    for (const t of row) {
      ctx.beginPath();
      ctx.moveTo(t[0] - t[2] / 2, t[1]);
      ctx.lineTo(t[0] + t[2] / 2, t[1] + direction * .025);
      ctx.lineTo(t[0] + t[4], t[1] + direction * t[3]);
      ctx.closePath();
      ctx.fill();
    }
  }

  function maw(ctx, p) {
    // The cavity occupies nearly the entire creature. Only the small rear
    // wedge remains outside the two jaws. In a rush the jaws clamp forward.
    const spread = p.windup ? .24 : p.attack ? -.25 : p.recover ? .06 : Math.sin(p.t * 2.8) * .018;
    const reach = p.attack ? .07 : 0;
    mass(ctx, 'M-1.3 -.16 L-1.03 -.31 L-.89 0 L-1.03 .29 L-1.28 .16 Z', p);
    ctx.save();
    ctx.translate(reach, 0);
    // Black cavity, without a line across the hinge or any glowing centre.
    ctx.fillStyle = BLACK;
    ctx.beginPath();
    ctx.moveTo(-1.11, 0);
    ctx.lineTo(-.84, -.57 - spread);
    ctx.lineTo(.15, -.75 - spread);
    ctx.lineTo(1.47, -.35 - spread);
    ctx.lineTo(1.50, .32 + spread);
    ctx.lineTo(.1, .76 + spread);
    ctx.lineTo(-.88, .54 + spread);
    ctx.closePath();
    ctx.fill();
    ctx.save();
    ctx.translate(0, -spread);
    mass(ctx, 'M-1.1 0 L-1.03 -.44 L-.59 -.69 L.04 -.77 L.67 -.70 L1.24 -.49 L1.52 -.22 L1.39 -.29 L.82 -.46 L.14 -.56 L-.46 -.53 L-.88 -.33 Z', p);
    teeth(ctx, upperTeeth, 1);
    ctx.restore();
    ctx.save();
    ctx.translate(0, spread);
    mass(ctx, 'M-1.1 0 L-.99 .43 L-.55 .68 L.12 .78 L.73 .67 L1.28 .46 L1.52 .19 L1.35 .29 L.83 .45 L.2 .56 L-.42 .53 L-.85 .34 Z', p);
    teeth(ctx, lowerTeeth, -1);
    ctx.restore();
    ctx.restore();
  }

  function razorwing(ctx, p) {
    // Restore the original two hooked wings and pointed spine.
    ctx.scale(.94, .94);
    const fold = p.windup ? .19 : p.attack ? -.12 : Math.sin(p.t * 4) * .018;
    for (const side of [-1, 1]) {
      ctx.save();ctx.scale(1, side);ctx.rotate(fold);
      mass(ctx, 'M-.42 .14 C-.98 .48 -1.12 .94 -1.02 1.35 C-.3 1.43 .63 1.00 1.16 .45 L.49 .76 L.17 .62 L-.15 .93 L-.56 .97 L-.4 .56 L.2 .28 Z', p);
      ctx.restore();
    }
    mass(ctx, 'M-1.16 0 L-.56 -.15 L-.12 -.26 L.37 -.13 L1.31 0 L.34 .14 L-.14 .23 L-.58 .12 Z', p);
    white(ctx, 'M.09 -.025 L.78 0 L.09 .025 Z');
  }

  function husk(ctx, p) {
    // A short, broad Maw enclosed in thick broken jaw plates.
    ctx.scale(.87,1.18);
    for(const side of [-1,1]){
      ctx.save();ctx.scale(1,side);
      mass(ctx,'M-1.12 .39 L-.91 .94 L-.39 1.15 L.34 1.10 L.96 .78 L1.28 .37 L.79 .51 L.24 .72 L-.49 .72 Z',p);
      ctx.restore();
    }
    maw(ctx,p);
    white(ctx,'M-1.12 -.13 L-.91 -.22 L-.97 -.12 Z M-1.12 .13 L-.91 .22 L-.97 .12 Z');
  }

  function grasp(ctx, p) {
    // Maw's teeth and empty cavity split across four clamping jaws.
    const spread=p.windup?.18:p.attack?-.20:Math.sin(p.t*2)*.015;
    ctx.fillStyle=BLACK;ctx.beginPath();ctx.arc(0,0,1.05,0,Math.PI*2);ctx.fill();
    for(let i=0;i<4;i++){
      ctx.save();ctx.rotate(i*Math.PI/2+Math.PI/4);ctx.translate(0,spread);
      mass(ctx,'M-.91 .48 L-.78 .99 L-.27 1.26 L.28 1.19 L.80 .98 L.94 .48 L.49 .66 L0 .76 L-.51 .66 Z',p);
      white(ctx,'M-.66 .66 L-.48 .25 L-.35 .73 Z M-.28 .76 L-.09 .27 L.06 .79 Z M.12 .77 L.33 .23 L.46 .71 Z M.52 .66 L.72 .30 L.79 .60 Z');
      ctx.restore();
    }
  }

  function spite(ctx, p) {
    // Bow-like mandibles frame a creature's throat; no stock, string or arrow.
    ctx.translate(p.attack?-.1:0,0);
    mass(ctx,'M-1.15 0 L-.63 -.34 L.13 -.27 L.46 0 L.13 .27 L-.63 .34 Z',p);
    for(const side of [-1,1]){
      ctx.save();ctx.scale(1,side);ctx.rotate(p.windup?.13:p.attack?-.15:0);
      mass(ctx,'M-.54 .12 L-.27 .66 L.39 1.19 L1.05 1.37 L.83 .93 L.35 .66 L.16 .21 L.61 .38 L.43 .04 Z',p);
      white(ctx,'M.25 .68 L.58 .64 L.41 .91 Z M.55 .97 L.87 .92 L.78 1.15 Z');
      ctx.restore();
    }
    white(ctx,'M-.18 -.12 L.24 -.08 L.42 0 L.24 .08 L-.18 .12 L-.03 0 Z');
  }

  const artists = { maw, razorwing, husk, grasp, spite };

  function draw(ctx, id, x, y, r, time, options = {}) {
    const artist = Object.prototype.hasOwnProperty.call(artists, id) ? artists[id] : null;
    if (!artist || !ctx || !Number.isFinite(r) || r <= 0 || !Number.isFinite(x) || !Number.isFinite(y)) return false;
    options = options || {};
    const t = !options.reducedMotion && Number.isFinite(time) ? time : 0;
    const state = options.state || 'idle';
    const p = { t, windup: state === 'windup', attack: state === 'attack', recover: state === 'recover', flash: Number(options.flash) > .35 };
    ctx.save();
    try {
      ctx.translate(x, y);
      ctx.rotate(Number.isFinite(options.angle) ? options.angle : 0);
      ctx.scale(r, r);
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      ctx.shadowBlur = 0;
      ctx.shadowOffsetX = 0;
      ctx.shadowOffsetY = 0;
      artist(ctx, p);
    } finally { ctx.restore(); }
    return true;
  }

  global.DestroyerArt = { draw, roster };
})(window);

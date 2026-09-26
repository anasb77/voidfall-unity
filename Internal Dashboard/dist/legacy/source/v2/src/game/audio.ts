// Procedural SFX via WebAudio — zero assets, tiny CPU cost.

export class Sfx {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private noiseBuf: AudioBuffer | null = null;
  private lastPlay: Record<string, number> = {};
  muted = false;

  constructor() {
    try {
      this.muted = localStorage.getItem("voidfall_muted") === "1";
    } catch {
      this.muted = false;
    }
  }

  init() {
    if (this.ctx) {
      if (this.ctx.state === "suspended") this.ctx.resume().catch(() => {});
      return;
    }
    try {
      const AC = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      this.ctx = new AC();
      this.master = this.ctx.createGain();
      this.master.gain.value = this.muted ? 0 : 0.5;
      this.master.connect(this.ctx.destination);
      // shared noise buffer
      const len = this.ctx.sampleRate * 0.5;
      this.noiseBuf = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
      const data = this.noiseBuf.getChannelData(0);
      for (let i = 0; i < len; i++) data[i] = Math.random() * 2 - 1;
    } catch {
      this.ctx = null;
    }
  }

  setMuted(m: boolean) {
    this.muted = m;
    try {
      localStorage.setItem("voidfall_muted", m ? "1" : "0");
    } catch {}
    if (this.master && this.ctx) {
      this.master.gain.setTargetAtTime(m ? 0 : 0.5, this.ctx.currentTime, 0.02);
    }
  }

  private gate(name: string, minMs: number): boolean {
    const now = performance.now();
    if (this.lastPlay[name] && now - this.lastPlay[name] < minMs) return false;
    this.lastPlay[name] = now;
    return true;
  }

  private tone(
    f0: number,
    f1: number,
    dur: number,
    type: OscillatorType,
    vol: number,
    delay = 0
  ) {
    if (!this.ctx || !this.master || this.muted) return;
    const t = this.ctx.currentTime + delay;
    const o = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    o.type = type;
    o.frequency.setValueAtTime(Math.max(20, f0), t);
    o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t + dur);
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g).connect(this.master);
    o.start(t);
    o.stop(t + dur + 0.02);
  }

  private noise(dur: number, freq: number, vol: number, q = 1, delay = 0) {
    if (!this.ctx || !this.master || !this.noiseBuf || this.muted) return;
    const t = this.ctx.currentTime + delay;
    const src = this.ctx.createBufferSource();
    src.buffer = this.noiseBuf;
    src.loop = true;
    const f = this.ctx.createBiquadFilter();
    f.type = "bandpass";
    f.frequency.value = freq;
    f.Q.value = q;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    src.connect(f).connect(g).connect(this.master);
    src.start(t);
    src.stop(t + dur + 0.02);
  }

  shoot() {
    if (!this.gate("shoot", 45)) return;
    const p = 0.92 + Math.random() * 0.16;
    this.tone(840 * p, 340 * p, 0.07, "square", 0.045);
  }

  hit() {
    if (!this.gate("hit", 40)) return;
    this.noise(0.06, 1500 + Math.random() * 600, 0.08, 1.4);
  }

  crit() {
    if (!this.gate("crit", 90)) return;
    this.tone(1400, 500, 0.1, "sawtooth", 0.07);
    this.noise(0.09, 2400, 0.09, 2);
  }

  die() {
    if (!this.gate("die", 50)) return;
    this.noise(0.16, 700 + Math.random() * 300, 0.14, 0.8);
    this.tone(240, 60, 0.18, "triangle", 0.09);
  }

  eliteDie() {
    this.noise(0.5, 320, 0.3, 0.6);
    this.tone(160, 34, 0.55, "sawtooth", 0.22);
    this.tone(520, 90, 0.4, "square", 0.1, 0.03);
  }

  gem(step: number) {
    if (!this.gate("gem", 30)) return;
    const f = 540 * Math.pow(2, Math.min(step, 24) / 12);
    this.tone(f, f * 1.35, 0.09, "sine", 0.075);
  }

  levelup() {
    const notes = [523.25, 659.25, 783.99, 1046.5];
    notes.forEach((f, i) => this.tone(f, f, 0.22, "triangle", 0.12, i * 0.07));
    this.noise(0.4, 3000, 0.05, 0.5, 0.1);
  }

  pick() {
    this.tone(660, 990, 0.1, "triangle", 0.1);
  }

  hurt() {
    if (!this.gate("hurt", 150)) return;
    this.tone(190, 55, 0.28, "sawtooth", 0.2);
    this.noise(0.22, 380, 0.2, 0.7);
  }

  ui() {
    if (!this.gate("ui", 60)) return;
    this.tone(700, 980, 0.07, "sine", 0.08);
  }

  pause() {
    this.tone(440, 330, 0.12, "sine", 0.09);
  }

  gameover() {
    const seq = [392, 311.1, 261.6, 196];
    seq.forEach((f, i) => this.tone(f, f * 0.97, 0.34, "triangle", 0.14, i * 0.16));
    this.noise(0.9, 240, 0.16, 0.5, 0.1);
  }

  warn() {
    if (!this.gate("warn", 400)) return;
    this.tone(196, 196, 0.14, "square", 0.1);
    this.tone(196, 196, 0.14, "square", 0.1, 0.18);
  }

  dash() { if (!this.gate("dash",120))return; this.noise(0.14,900,0.07,0.8); }
  evolution(){this.tone(523,1047,0.3,"sine",0.15);this.tone(659,1319,0.25,"triangle",0.1,0.08);this.tone(784,1568,0.2,"sine",0.08,0.16);this.noise(0.5,4000,0.06,0.5,0.05);}
  elite(){this.tone(160,100,0.3,"sawtooth",0.12);this.noise(0.3,200,0.1,0.5);}
  boss(){this.tone(80,50,0.5,"sawtooth",0.18);this.tone(120,60,0.4,"square",0.1,0.1);this.noise(0.6,150,0.12,0.4);}
  bossAtk(){this.tone(220,180,0.08,"square",0.06);}
  bossDie(){this.tone(300,40,0.6,"sawtooth",0.2);this.tone(600,80,0.5,"square",0.12,0.05);this.noise(0.7,400,0.18,0.3);}
  victory(){[523,659,784,1047].forEach((f,i)=>this.tone(f,f,0.3,"triangle",0.14,i*0.12));}
  coin(){this.tone(1200,1600,0.06,"sine",0.06);}
  chest(){this.tone(440,660,0.12,"triangle",0.1);this.tone(660,880,0.1,"triangle",0.08,0.08);}
  shotgun(){if(!this.gate("shoot",70))return;this.tone(220,80,0.1,"sawtooth",0.08);this.noise(0.08,800,0.1,1);}
  railgun(){if(!this.gate("shoot",300))return;this.tone(150,40,0.25,"sawtooth",0.12);this.noise(0.15,500,0.12,0.6);}
  arc(){if(!this.gate("arc",120))return;this.tone(2200,800,0.06,"sine",0.05);this.noise(0.05,5000,0.04,2);}
  flame(){if(!this.gate("flame",40))return;this.noise(0.04,400+Math.random()*200,0.025,0.5);}
  rocket(){if(!this.gate("shoot",200))return;this.tone(100,40,0.2,"sine",0.08);this.noise(0.12,300,0.1,0.6);}
  mine(){if(!this.gate("mine",300))return;this.tone(80,40,0.15,"sine",0.08);this.noise(0.1,200,0.06,0.5);}
}

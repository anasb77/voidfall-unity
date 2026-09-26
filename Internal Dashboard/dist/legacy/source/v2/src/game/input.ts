// Unified keyboard + floating touch-joystick input.

export class Input {
  keys = new Set<string>();
  // joystick state (screen space)
  joyActive = false;
  joyId = -1;
  joyBaseX = 0;
  joyBaseY = 0;
  joyX = 0;
  joyY = 0; // clamped vector, length <= 1

  private onKeyDown: (e: KeyboardEvent) => void;
  private onKeyUp: (e: KeyboardEvent) => void;
  private onDown: (e: PointerEvent) => void;
  private onMove: (e: PointerEvent) => void;
  private onUp: (e: PointerEvent) => void;
  private el: HTMLElement;

  constructor(el: HTMLElement, keyHandler: (code: string, e: KeyboardEvent) => void) {
    this.el = el;

    this.onKeyDown = (e) => {
      this.keys.add(e.code);
      keyHandler(e.code, e);
    };
    this.onKeyUp = (e) => this.keys.delete(e.code);

    this.onDown = (e) => {
      if (e.pointerType === "mouse") return;
      if (this.joyActive) return;
      this.joyActive = true;
      this.joyId = e.pointerId;
      this.joyBaseX = e.clientX;
      this.joyBaseY = e.clientY;
      this.joyX = 0;
      this.joyY = 0;
    };
    this.onMove = (e) => {
      if (!this.joyActive || e.pointerId !== this.joyId) return;
      const dx = e.clientX - this.joyBaseX;
      const dy = e.clientY - this.joyBaseY;
      const len = Math.hypot(dx, dy);
      const max = 64;
      if (len > max) {
        // drag the base along so long swipes stay responsive
        const excess = len - max;
        this.joyBaseX += (dx / len) * excess;
        this.joyBaseY += (dy / len) * excess;
        this.joyX = dx / len;
        this.joyY = dy / len;
      } else if (len > 6) {
        this.joyX = dx / max;
        this.joyY = dy / max;
      } else {
        this.joyX = 0;
        this.joyY = 0;
      }
    };
    this.onUp = (e) => {
      if (e.pointerId !== this.joyId) return;
      this.joyActive = false;
      this.joyId = -1;
      this.joyX = 0;
      this.joyY = 0;
    };

    window.addEventListener("keydown", this.onKeyDown);
    window.addEventListener("keyup", this.onKeyUp);
    el.addEventListener("pointerdown", this.onDown);
    window.addEventListener("pointermove", this.onMove, { passive: true });
    window.addEventListener("pointerup", this.onUp);
    window.addEventListener("pointercancel", this.onUp);
  }

  /** Combined movement axis, normalized. */
  axis(out: { x: number; y: number }) {
    let x = 0;
    let y = 0;
    const k = this.keys;
    if (k.has("KeyA") || k.has("ArrowLeft")) x -= 1;
    if (k.has("KeyD") || k.has("ArrowRight")) x += 1;
    if (k.has("KeyW") || k.has("ArrowUp")) y -= 1;
    if (k.has("KeyS") || k.has("ArrowDown")) y += 1;
    if (x !== 0 || y !== 0) {
      const l = Math.hypot(x, y);
      out.x = x / l;
      out.y = y / l;
    } else if (this.joyActive) {
      out.x = this.joyX;
      out.y = this.joyY;
    } else {
      out.x = 0;
      out.y = 0;
    }
  }

  destroy() {
    window.removeEventListener("keydown", this.onKeyDown);
    window.removeEventListener("keyup", this.onKeyUp);
    this.el.removeEventListener("pointerdown", this.onDown);
    window.removeEventListener("pointermove", this.onMove);
    window.removeEventListener("pointerup", this.onUp);
    window.removeEventListener("pointercancel", this.onUp);
  }
}

# Music remix events

Owner approved implementation of the brainstorming on September 6, 2026.
Mechanics combine to temporarily remix the existing soundtrack. Overclock
accelerates the song; critical health slows that accelerated song and adds
darkness; Magnet accumulates bass and inward stereo pull from actual collected
gems. Effects remain present across track shifts and expire independently.

## Accepted experience

- Overclock remains 2x when healthy. Critical applies its 0.50–0.64 multiplier
  to that rate, retaining darkening so speed cancellation still has a signature.
- Magnet: each gem from a pickup-triggered pull adds diminishing intensity,
  bounded at hundreds of gems. Collection closes with a short stereo release.
  Bass, muffle and a subtle green perimeter fade over 25 gameplay seconds.
  Ordinary proximity collection and Greed do not start the event.
- Sustained critical health (at least two gameplay seconds) followed by healing
  produces a clarity/stereo release and a five-second faint pitch wavering trace.
  Critical clears above 25% after entering at 20%, avoiding threshold chatter.
- Additional overclock pickups add a short clarity/stereo accent, rate-limited
  to avoid repeated loud hits. They never increase the healthy 2x base rate.
- Bomb keeps its duck, with a bounded short echo of the music, scaled in delay
  by the current playback rate. Repeated bombs retrigger rather than accumulate.
- Track Shift retains event envelopes and starts at measured energetic entry
  points. Track names and existing soundtrack files remain unchanged.

## Ownership and limits

Runtime reports facts without changing GameSim structs, combat RNG, pickup
iteration or saves. MusicRemixEnvelope owns gameplay-time event history;
MusicStateComposer derives mix targets. MusicDirector owns playback and filter
handoff; MusicDspFilter owns audio-thread buffers. No runtime allocation, locks,
Unity object access or per-sample transcendental functions on the audio thread.
Gameplay-time Magnet/recovery/stack envelopes freeze for modal pause and
application suspension; music retains existing pause playback. Short bomb
duck/echo gestures finish with the playing song during a modal pause, avoiding
a prolonged volume dip. Application suspension pauses playback itself.
Menu/death/new-run reset event history, duck and DSP requests.
Green is confined to the existing perimeter, respects reduced motion and never
tints Zack or HUD. Gain is bounded and user volumes remain authoritative.

## Verification

Focused EditMode tests for composition, accumulation, duration, release,
recovery, reset and DSP sample behavior. Runtime integration tests exercise
pickup collection, pause, Greed, track changes and shader state. Build into
Builds/MusicRemix, preserving the normal player. Render perimeter captures and
provide a reproducible listening scenario; distinguish objective checks from
subjective listening approval.

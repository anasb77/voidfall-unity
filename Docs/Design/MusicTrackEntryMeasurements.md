# Music Track Shift entry measurements

Measured September 6, 2026 from the 11 MP3 files in
`Assets/VoidFall/Resources/VoidFall/Music/OST`. This is signal analysis only;
the selections do not claim subjective listening approval.

## Method

The files were decoded as 48 kHz stereo float samples with libsndfile 1.2.2.
Analysis used a mono downmix on a 0.25-second grid. Candidate transitions were
ranked from three independent measurements:

- four-second post-entry RMS, favoring sustained energy rather than a single
  transient;
- the RMS change from the preceding four seconds;
- spectral change, measured across eight logarithmic bands from 0 Hz to 6 kHz,
  plus positive spectral flux at the entry frame.

Candidates within the final 12 seconds were excluded. The selected pair for
each file favors high sustained RMS and a positive energy rise, with multiband
novelty used to distinguish section changes from ordinary loud frames. Values
are quantized to the analysis grid. `Post RMS` and `rise` below cover the four
seconds after each entry; `novelty` is the normalized multiband distance from
the preceding four seconds.

## Selected entries

| Track | Duration | Entry A evidence | Entry B evidence |
|---|---:|---|---|
| Boss Battle | 129.79 s | 29.50 s; -12.15 dBFS, +4.04 dB rise, 5.05 novelty | 78.00 s; -15.17 dBFS, +1.83 dB rise, 3.12 novelty |
| Cyber Alley | 73.15 s | 29.50 s; -15.15 dBFS, +0.57 dB rise, 1.22 novelty | 56.25 s; -15.07 dBFS, +0.58 dB rise, 1.24 novelty |
| Drone Patrol | 217.25 s | 79.75 s; -15.93 dBFS, +1.49 dB rise, 2.48 novelty | 168.00 s; -15.36 dBFS, +0.40 dB rise, 2.14 novelty |
| Hidden Lab | 106.73 s | 11.00 s; -15.21 dBFS, +1.62 dB rise, 1.68 novelty | 77.50 s; -15.45 dBFS, +1.30 dB rise, 2.39 novelty |
| Holo Arcade | 121.54 s | 15.75 s; -13.54 dBFS, +5.83 dB rise, 2.41 novelty | 92.75 s; -13.59 dBFS, +0.18 dB rise, 2.10 novelty |
| Holo Bazaar | 101.76 s | 25.50 s; -16.90 dBFS, +0.43 dB rise, 1.21 novelty | 73.25 s; -16.62 dBFS, +0.84 dB rise, 1.03 novelty |
| Neon Escape | 81.29 s | 26.75 s; -12.81 dBFS, +0.95 dB rise, 2.47 novelty | 59.75 s; -12.91 dBFS, +0.08 dB rise, 1.20 novelty |
| Neon Rain | 86.45 s | 19.25 s; -16.07 dBFS, +1.11 dB rise, 3.53 novelty | 32.50 s; -15.99 dBFS, +0.18 dB rise, 1.53 novelty |
| Neon Street | 158.76 s | 49.25 s; -14.45 dBFS, +0.54 dB rise, 1.93 novelty | 102.25 s; -14.35 dBFS, +1.18 dB rise, 2.63 novelty |
| Rooftop Chase | 45.53 s | 18.00 s; -16.20 dBFS, +1.08 dB rise, 2.51 novelty | 25.00 s; -15.91 dBFS, +0.45 dB rise, 1.45 novelty |
| Synth Syndicate | 124.73 s | 67.50 s; -13.33 dBFS, +0.49 dB rise, 1.46 novelty | 80.75 s; -13.65 dBFS, +2.79 dB rise, 2.86 novelty |

The runtime's existing playback guard clamps authored starts to
`clip.length - 1`. The focused editor test additionally loads every actual MP3
as an `AudioClip` and verifies that all sampled entries are positive and before
that bound. Unknown, empty and null names retain the zero-second fallback.

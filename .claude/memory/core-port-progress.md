---
name: core-port-progress
description: Where the worm-sim C# port stands and what each Core slice decided
metadata:
  type: project
---

Porting `worm_foraging.html` (frozen oracle) to `WormForaging.Core` one slice at a time,
each committed with parity tests; Core line coverage stays ~99%. Order follows the
prototype's `step()`.

As of 2026-10-22, done: `SimConfig` (every prototype constant, one `record`), `World`
(odor `exp(-d/12)`, Lissajous lamp, temp field, food spawn/respawn), `Body` (move+clamp,
body-temp lag, eat/gut/energy), `Directions`, `SensePermutation` (swap), `Senses`
(8-channel differential sensors). Next, in order: **network → motor/softmax → learning
rule → `Simulation` step loop**, then the Avalonia UI and the parity check.

Decisions that persist:
- Core computes in `double`, prototype is `Float32Array`. Parity is **qualitative**, not
  bit-for-bit JS. Determinism = same seed+config ⇒ identical *C#* run (test-guarded).
- Sensors are **differential** (direction vs. mean of 4), so on the lamp's peak the worm
  has big thermal discomfort but ~zero heat-gradient signal — drive and gradient-sensing
  are decoupled. Hunger/thermal **drives** are not in the sensor vector; they feed the
  network directly.
- The sense-swap is a first-class `SensePermutation` (Identity / Swapped=[2,3,0,1] /
  Toggle) because Phase 2's experiment hinges on it.

Working rhythm Trey likes here: one coherent slice at a time, commit when asked, report
negative results / surprises plainly (e.g. a failing test that revealed real model
behaviour is worth calling out, not hiding). The repo `CLAUDE.md` holds the shared
version of all this.

---
name: core-port-progress
description: Where the worm-sim C# port stands and what each Core slice decided
metadata:
  type: project
---

**PHASE 1 DONE (2026-10-22): first measured baseline is in, and three-factor wins decisively.**
`WormForaging.Harness` console runner built on the same host/CLI/`appsettings.yaml`/DI/Serilog stack
as the App (`Harness:` config section; paired-by-seed design — one seed gives all three modes an
identical starting world/network/readout, so the only difference is the learning rule). Writes
`summary.csv` / `timeseries.csv` / `aggregate.csv` to repo-root `results/` (git-ignored) and logs a
headline table. 10 seeds × 20k steps in ~2.6s. **Result (measured, prototype-default *guessed*
constants):** Full ≈ **276 meals**, mean energy 0.93, survives; Hebbian ≈ 5.7 meals and Frozen ≈ 11
meals, both starve to ~0 energy. Full forages ~50× more and thermoregulates better (discomfort 0.11
vs ~0.25). **Two surprises:** (1) plain Hebbian (fixed `0.6` modulator) is *no better than frozen* —
slightly worse on the mean — so reward-blind correlation is actively unhelpful, not a weak learner
(confirms the arbitrary-`0.6` parking-lot worry). (2) steps/meal variance on the controls is huge
because most seeds eat 0–6 meals; **use meals-count, not steps/meal, as throughput when meals are
near zero.** Discomfort is recovered in the harness as `|bodyTemp − comfort|` from the snapshot, so
Core stayed untouched. Next: Phase 2 (swap-back / savings metric).


Porting `worm_foraging.html` (frozen oracle) to `WormForaging.Core` one slice at a time,
each committed with parity tests; Core line coverage stays ~99%. Order follows the
prototype's `step()`.

As of 2026-10-22, done: `SimConfig` (every prototype constant, one `record`), `World`
(odor `exp(-d/12)`, Lissajous lamp, temp field, food spawn/respawn), `Body` (move+clamp,
body-temp lag, eat/gut/energy), `Directions`, `SensePermutation` (swap), `Senses`
(8-channel differential sensors), `Network` (16 interneurons, sparse recurrence,
homeostatic gain — no learned weights), `MotorLayer`+`MotorDecision` (readout `Wo`,
softmax, inverse-CDF sample), `LearningRule`+`LearningMode` (three-factor update,
eligibility + reward prediction), `Simulation`+`SimulationSnapshot` (wires it all in
prototype `step()` order; bit-identical from a seed). **PHASE 0 IS COMPLETE — Core + Avalonia
UI ported, parity accepted (Trey, 2026-10-22; qualitative by eye — the browser oracle is unseeded
`Math.random`/`Float32`, so no numeric match exists). 143 tests, Core ~99% line cov. Nothing
measured end-to-end yet — behavioral claims (full vs hebb vs frozen) are Phase 1, by the headless
harness, which is the next thing to build.**

Decisions that persist:
- Core computes in `double`, prototype is `Float32Array`. Parity is **qualitative**, not
  bit-for-bit JS. Determinism = same seed+config ⇒ identical *C#* run (test-guarded).
- Sensors are **differential** (direction vs. mean of 4), so on the lamp's peak the worm
  has big thermal discomfort but ~zero heat-gradient signal — drive and gradient-sensing
  are decoupled. Hunger/thermal **drives** are not in the sensor vector; they feed the
  network directly.
- The sense-swap is a first-class `SensePermutation` (Identity / Swapped=[2,3,0,1] /
  Toggle) because Phase 2's experiment hinges on it.
- Component seam: `Network` = fixed reservoir (no learned weights); `MotorLayer` owns the
  learned readout `Wo`; `LearningRule` owns eligibility + reward prediction and updates `Wo`
  via `MotorLayer.Get/SetReadoutWeight`. The three-factor update is **local by construction**
  (each weight uses only its own `e[i]`, `Wo[i]`, one broadcast scalar) and a test proves it —
  this is the no-backprop hard rule, enforced not just asserted. Frozen mode = no weight change.

- UI render-trigger gotcha (cost two blank panels, fixed): an Avalonia custom `Control` redraws
  only when one of its OWN styled properties changes value. So (a) never bind a property to a
  stable mutable collection mutated in place — hand out a fresh reference each tick, or let a
  sibling property bound to the per-tick fresh `SimulationSnapshot` force the redraw; and (b) the
  control's property type must match the snapshot's exactly (binding `IReadOnlyList<long>` to an
  `IEnumerable<double>` property silently yields null). Also: `Pcg32Random.Reseed` must clear the
  Box–Muller spare or `Reset()` after an odd Gaussian count breaks determinism (found + fixed).

Working rhythm Trey likes here: one coherent slice at a time, commit when asked, report
negative results / surprises plainly (e.g. a failing test that revealed real model
behaviour is worth calling out, not hiding). The repo `CLAUDE.md` holds the shared
version of all this.

# Two needs, no backprop

A C. elegans-*inspired* foraging agent that learns online — **without backpropagation,
gradients, or a feedforward error signal.** It forages for food and regulates its body
temperature at the same time, and the two drives visibly compete for control of behavior.
All learning is local plasticity gated by a single broadcast "how's it going" scalar
(three-factor / neuromodulated Hebbian).

This repository ports the original single-file browser prototype
([`worm_foraging.html`](worm_foraging.html)) to a C# / Avalonia application so the learning
rule can be studied under reproducible, headless conditions — seeded runs, many seeds per
condition, and proper before/after metrics instead of eyeballing a canvas.

> This is an artificial-life / philosophy-of-mind study, not a product. The goal is a
> *self-maintaining learner* whose mechanism is biologically suggestive, not a benchmark
> score. See [`handoff.md`](handoff.md) for the original author's design rationale.

## The model

- **World** — a 40×40 arena. One food source emits an odor that falls off with distance; it
  respawns elsewhere when eaten. A heat lamp drifts along a slow Lissajous path, creating a
  moving warm spot. "Comfortable" body temperature sits between the ambient floor and the
  lamp's peak, so comfort requires active positioning.
- **Body** — energy drains over time and faster when uncomfortable; eating fills a *gut*
  buffer that is absorbed slowly, so reward is delayed; body temperature lags the local
  temperature.
- **Senses** — four directional odor sensors (spatial contrast, Weber-normalized, gated by
  hunger) and four directional heat sensors (gated by signed thermal need), plus two internal
  drives (hunger, temperature deviation) fed straight into the network.
- **Network** — 16 interneurons with fixed random sparse recurrent wiring, `tanh` activation,
  and per-neuron homeostatic gain control. Four motor neurons (N/E/S/W); the next move is
  sampled from a softmax over their outputs, with an adjustable exploration temperature.
- **Learning** — only the motor readout weights change. An eligibility trace records
  *which synapse pushed for the action just taken*; a global modulator (built from the change
  in odor and comfort, weighted by how urgent each need currently is) decides whether to
  reinforce or suppress that push. No backward pass exists.
- **Modes** — three-factor (the real rule), plain Hebbian (constant modulator, a control),
  and frozen (no learning, a baseline). Plus a homeostasis toggle and a "swap senses" button
  that rotates the sensor→direction mapping 180° to force relearning.

## Status

🟢 **Phase 1 complete — the first baseline is measured, and three-factor learning wins decisively.**
The whole simulation is ported with parity tests against the prototype (`SimConfig`, `World`, `Body`,
`Senses` + the sense-swap, `Network`, `MotorLayer`, `LearningRule`, and the `Simulation` step loop) —
143 tests at ~99% line coverage, a given seed reproduces a run bit-for-bit, and the Avalonia UI is
built (arena, controls, and live energy/temperature, network-weight, and steps-per-meal panels),
accepted as *qualitatively* matching the browser by eye. The headless `WormForaging.Harness` now runs
N seeds × {full, hebbian, frozen}, paired by seed, and writes CSVs plus an aggregate table.

First measured baseline *(10 seeds × 20 000 steps, prototype-default config)* — mean ± sample SD:

| mode | meals eaten | mean energy | final energy |
|---|---|---|---|
| **Full (three-factor)** | **276.5 ± 34.2** | **0.933 ± 0.035** | **0.919 ± 0.202** |
| Hebbian (fixed modulator) | 5.7 ± 10.1 | 0.021 ± 0.042 | 0.000 |
| Frozen (no learning) | 11.0 ± 11.8 | 0.027 ± 0.033 | 0.028 ± 0.087 |

Three-factor forages ~50× more and stays alive; both controls starve. A surprise worth stating
plainly: **plain Hebbian is no better than frozen** (slightly worse on the mean) — reward-blind
"fire together, wire together" is actively unhelpful here, which is exactly the case for the third
(neuromodulatory) factor. See [`TODO.md`](TODO.md) for the full table and caveats.

What is verified vs. guessed:

- *Measured (seeded C# runs):* three-factor beats both controls decisively at the default config;
  plain Hebbian ≈ frozen. The numbers above.
- *Guessed / untuned:* essentially every constant — reward weights, the absorption→reward
  scaling, learning rate, trace decay, the Hebbian mode's arbitrary `0.6` modulator. The baseline
  says the rule works *at the untuned defaults*; it is not a tuned claim. Still hypotheses.

## Project layout

```
src/WormForaging.Core/     simulation — pure, deterministic, no UI (the testable heart)
src/WormForaging.App/      Avalonia MVVM front end — renders and drives Core
tests/WormForaging.Core.Tests/   xUnit determinism + parity tests
src/WormForaging.Harness/  headless N-seed experiment runner — writes CSVs to results/
worm_foraging.html         original prototype — kept as the parity reference oracle
```

## Build and run

Requires .NET 10.

```
dotnet build
dotnet test
dotnet run --project src/WormForaging.App                       # the interactive Avalonia app
dotnet run --project src/WormForaging.Harness                   # the baseline experiment → results/*.csv
dotnet run --project src/WormForaging.Harness -- --seeds 20 --steps 50000   # a bigger run
```

## Inspiration

C. elegans: ~302 neurons, a fixed connectome, learning expressed mostly as changed sensory
gain and neuromodulator state rather than rewiring, and a peptide signalling layer that sits
apart from the wired connectome. We capture the *spirit* — local plasticity, neuromodulation,
and neuron-level homeostasis (Marder / O'Leary self-maintaining units) — not the biology.

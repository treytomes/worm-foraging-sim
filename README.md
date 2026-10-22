# Two needs, no backprop

A C. elegans-*inspired* foraging agent that learns online — **without backpropagation,
gradients, or a feedforward error signal.** It forages for food and regulates its body
temperature at the same time, and the two drives visibly compete for control of behaviour.
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

🚧 **Core complete; nothing measured yet.** The whole simulation is ported with parity tests
against the prototype — `SimConfig`, `World`, `Body`, `Senses` (+ the sense-swap), `Network`,
`MotorLayer`, `LearningRule`, and the `Simulation` step loop that runs them together — 140
tests at ~99% line coverage, and a given seed reproduces a run bit-for-bit. What does *not*
yet exist: the Avalonia UI and the headless harness, and therefore **any end-to-end behavioral
result**. The learning has still only ever been *seen* once, by hand, in a browser with an
unseeded RNG; the C# port can now reproduce runs, but *measuring* whether the rule learns —
and beats the Hebbian and frozen controls — is the next goal, not a finished result. See
[`TODO.md`](TODO.md).

What is verified vs. guessed:

- *Observed once in the browser:* three-factor learns, relearns after a sense swap, and the
  two needs compete.
- *Guessed / untuned:* essentially every constant — reward weights, the absorption→reward
  scaling, learning rate, trace decay. Treated as hypotheses until a seeded run says otherwise.

## Project layout

```
src/WormForaging.Core/     simulation — pure, deterministic, no UI (the testable heart)
src/WormForaging.App/      Avalonia MVVM front end — renders and drives Core
tests/WormForaging.Core.Tests/   xUnit determinism + parity tests
src/WormForaging.Harness/  (later) headless N-seed experiment runner
worm_foraging.html         original prototype — kept as the parity reference oracle
```

## Build and run

Requires .NET 10.

```
dotnet build
dotnet test
dotnet run --project src/WormForaging.App
```

## Inspiration

C. elegans: ~302 neurons, a fixed connectome, learning expressed mostly as changed sensory
gain and neuromodulator state rather than rewiring, and a peptide signalling layer that sits
apart from the wired connectome. We capture the *spirit* — local plasticity, neuromodulation,
and neuron-level homeostasis (Marder / O'Leary self-maintaining units) — not the biology.

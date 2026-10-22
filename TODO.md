# TODO / roadmap

Ordered. We don't start the experiment steps until the port is at verified parity and the
headless harness exists — otherwise every claim is just a browser eyeball.

Legend: `[ ]` todo · `[~]` in progress · `[x]` done · *(measured)* vs *(guessed)* for results.

## Phase 0 — Port to C# / Avalonia at feature parity  ← we are here

- [x] Scaffold solution: `Core`, `App`, `Core.Tests` projects; `.slnx`; `.gitignore`.
- [x] **`IRandom` interface** in `Core` — the single RNG abstraction every stochastic path
      depends on (network init, action sampling, food/lamp placement, Gaussian noise). Members:
      `double NextDouble()` (uniform [0,1)), `double NextGaussian()` (standard normal, Box–Muller
      to match the prototype's `randn()`), `uint NextUInt()`, `int NextInt(int maxExclusive)`.
      Injected everywhere via DI / `SimConfig` — no ambient `System.Random`.
- [x] **`Pcg32Random`** — the concrete `IRandom`: a seeded PCG32 generator (small, portable,
      high-quality). Constructed from an explicit `ulong` seed; same seed ⇒ identical stream.
      Add a `Fork()`/substream option later if components need independent streams.
- [x] Determinism test: same seed ⇒ bit-identical `IRandom` stream (15 tests; Core at 98% line cov).

### App host & infrastructure (done this session)

- [x] MVVM on **`Prism.Core` `BindableBase`** (not full Prism / not its container), matching
      HyperSpace — reconciles "use Prism" with "Generic Host owns DI" cleanly. Container question
      resolved by reviewing `C:\Users\TreyTomes\projects\HyperSpace`: it uses no Prism container.
- [x] **Generic Host** (`Microsoft.Extensions.Hosting`) + Microsoft DI + `Microsoft.Extensions.Logging`.
- [x] **Serilog**: console sink + daily rolling file sink, configured from `appsettings.yaml`.
- [x] **`appsettings.yaml` at repo root** (via `NetEscapades.Configuration.Yaml`), located by
      walking up from the executable so it's editable in place; `--config <path>` override.
- [x] **`System.CommandLine`** CLI: `--seed`, `--config`, `--log-level` (parse-only).
- [x] Move `Assets` → repo-root `./assets`; linked into Avalonia via `<Link>`.
- [x] Repo standards: warnings-as-errors + required XML docs (`Directory.Build.props`) + 80%
      line-coverage gate (coverlet).
- [ ] **Smoke-run the app** to confirm the host boots, reads YAML, logs to console + file, and the
      seeded draws render (not yet launched this session).
- [ ] `SimConfig` — every prototype constant becomes a named, documented field.
- [ ] Port the world: arena, odor field, moving heat lamp, temperature field, food respawn.
- [ ] Port the body: energy, gut/absorption, body-temperature lag.
- [ ] Port the senses: 4 odor + 4 heat (gated, Weber-normalized) + 2 internal drives.
- [ ] Port the network: 16 interneurons, sparse recurrent wiring, `tanh`, homeostatic gain.
- [ ] Port motor layer + softmax action sampling — rewrite as a clean cumulative sample
      (the prototype's loop is correct but fragile; see handoff "known rough edges").
- [ ] Port the learning rule: eligibility trace, modulator, three modes (full/hebb/frozen).
- [ ] Avalonia UI (custom `Control.Render` + `DispatcherTimer`): arena first, plus the
      essential controls (pause, reset, swap, mode, homeostasis, explore, speed) and a basic
      stats readout. **Scope decision: Core-faithful, UI-lean** — exact simulation parity now,
      richer panels (energy/temp history, network+weights, steps-per-meal) added incrementally.
- [ ] **Parity check:** with a fixed seed, confirm C# reproduces the prototype's qualitative
      behaviour (learns, relearns after swap, needs compete). Document any intentional diffs.
      Core parity is the gate; the extra visual panels are not.

## Phase 1 — Headless harness + baselines (handoff step 1)

- [ ] `WormForaging.Harness` console runner: run N seeds × {full, hebb, frozen}.
- [ ] Metrics: steps-per-meal, energy over time, discomfort over time; aggregate across seeds.
- [ ] First measured baseline: does three-factor beat Hebbian and frozen, and by how much?
      *Mark measured vs. guessed in the write-up.*

## Phase 2 — Swap-back / savings metric (handoff step 2)

- [ ] Repeatedly swap senses; measure whether relearning speeds up (savings).
- [ ] Test the prototype author's prediction: **no savings**, because the readout just
      overwrites. Confirming or refuting this gates whether slow/fast weights solve a real
      problem. Report plainly either way.

## Phase 3+ — the open-ended experiments (handoff steps 3–8)

- [ ] Slow/fast weights: fast decaying component + slow component that consolidates only on
      consistent same-direction pressure.
- [ ] Replay: rehearse stored eligibility traces during low-urgency periods to protect old
      mappings.
- [ ] Scaling test: interneurons 16 → 200; watch learning speed and noise.
- [ ] Delayed reward: push reward later (slower absorption / reward only on eating) to find
      where eligibility traces break.
- [ ] Hidden-layer plasticity (sensor→interneuron weights) via local rules — node
      perturbation, e-prop style. **Still no backprop.**
- [ ] Evolve the fixed connectome across generations (Baldwin-effect style) with
      within-lifetime readout learning.

## Parking lot

- Seeded RNG lets us pin the "results vary a lot by seed" problem the handoff flags.
- Hebbian mode's fixed `0.6` modulator is arbitrary — revisit once we have the harness.
- Validate the asymmetric advantage-normalizer epsilons (odor `1e-4` vs thermal `2e-3`).

# CLAUDE.md — worm-inspired foraging sim

Project-specific guidance for Claude Code in this repo. Machine-level guidance lives in
`C:\Users\TreyTomes\CLAUDE.md` and still applies.

## What this is

A small artificial-life experiment: a C. elegans-*inspired* agent that forages for food
while regulating body temperature, and **learns online without backpropagation**. We are
porting the original single-file browser prototype (`worm_foraging.html`) to a C# /
Avalonia project so we can run reproducible, headless experiments on the learning rule.

See `README.md` for the model, `TODO.md` for the roadmap, and `handoff.md` for the
original design notes and rationale from the prototype author.

## Hard rules (these are the point of the project — do not violate)

- **No backpropagation. No gradient computation. No automatic differentiation.** Anywhere.
- Learning stays **local plus a single broadcast scalar** (three-factor / neuromodulated
  Hebbian). A synapse may use only signals available at that synapse plus the global
  modulator. If a change would need a backward pass, it does not belong here.
- This is a philosophical/artificial-life study, not a product. Biological plausibility of
  the *mechanism* matters more than task performance.

## Architecture

- `src/WormForaging.Core/` — the simulation. **Pure, deterministic, zero UI dependencies.**
  World, body/metabolism, senses, recurrent network, the learning rule, and the `Simulation`
  step loop. All tunables live in one `SimConfig`. State is exposed as plain snapshots so UI
  and harness can both read it.
  - **Port status (slice by slice, each with parity tests, Core line cov ~99%):**
    `SimConfig` ✓ · `World` (odor/temp fields, Lissajous lamp, food spawn) ✓ · `Body`
    (movement, thermoregulation, metabolism) ✓ · `Directions` + `SensePermutation` + `Senses`
    (8-channel differential sensors, swap) ✓ · `Network` (16 interneurons, sparse recurrence,
    homeostatic gain; owns NO learned weights) ✓ · `MotorLayer` + `MotorDecision` (readout `Wo`,
    softmax `Policy`, inverse-CDF `Select`) ✓ · `LearningRule` + `LearningMode`
    (three-factor update; owns eligibility + reward prediction) ✓ · `Simulation` step loop +
    `SimulationSnapshot` (wires it all in prototype order; bit-identical from a seed) ✓.
    **Phases 0–2 are complete: Core + Avalonia UI ported (parity accepted qualitatively by eye — the
    browser oracle is unseeded `Math.random`/`Float32`, so there is no numeric match), and the headless
    harness has produced two measured results. 143 tests, Core ~99% line. _Measured_ (10 seeds × 20k
    steps, default config): three-factor forages ~50× more than either control (~276 meals vs ~6
    Hebbian / ~11 frozen) and survives while both starve; plain Hebbian is no better than frozen.
    _Phase 2 (`--experiment swap`)_: repeated sense-swaps show **no savings** — recovery after a swap
    (collapse to ≈0, re-climb over ~6–7k steps) is no faster for a familiar mapping than a novel one,
    confirming the author's prediction (the readout just overwrites). Every constant is still a
    prototype guess. Next is Phase 3 (slow/fast weights, replay, scaling, delayed reward, hidden-layer
    plasticity, evolved connectome).** Order follows the prototype's `step()`; see `TODO.md` for the
    live checklist and the full result tables.
  - **Component seam:** `Network` holds the fixed reservoir only; `MotorLayer` holds the learned
    readout `Wo`; `LearningRule` holds the eligibility trace + reward-prediction state and updates
    `Wo` through `MotorLayer`'s flat `Get/SetReadoutWeight`. The three-factor update is **local by
    construction** — each weight uses only its own `e[i]`, `Wo[i]`, and one broadcast scalar — and
    a test asserts it (no backward pass, ever). Frozen mode touches no weights (not even decay).
  - **Precision:** Core computes in `double`; the prototype uses `Float32Array`. Parity is
    *qualitative*, not bit-for-bit JS. "Deterministic" means same seed + config ⇒ identical
    *C#* run (guarded by tests), not identical to the browser.
  - **Model character worth remembering:** the odor and heat sensors are *differential*
    (direction vs. the mean of all four), so a uniform field reads as zero and — notably —
    sitting on the lamp's peak gives large thermal *discomfort* but near-zero heat *gradient*
    signal. Discomfort (a drive, fed to the net directly) and gradient sensing (a direction)
    are deliberately decoupled. A body's `Hunger`/`ThermalDeviation` drives are separate from
    the sensor vector and enter the network as their own weighted inputs.
- `src/WormForaging.App/` — Avalonia front end. Renders the sim and mirrors the prototype's
  controls. Contains **no simulation logic** — it only drives `Core` and draws. Rendering is a
  custom `Control` overriding `Render(DrawingContext)`, driven by a `DispatcherTimer`; odor/heat
  glows use `RadialGradientBrush`. No SkiaSharp dependency. Panels: arena, energy/temp history,
  interneuron activations + readout-weight heatmap, steps-per-meal.
  - **Render-trigger gotcha (cost two blank panels):** a custom `Control` redraws only when one of
    *its own* styled properties changes *value*. Two consequences, both learned the hard way. (1) A
    property bound to a stable mutable collection (same `List` reference, mutated in place) never
    looks changed, so `RaisePropertyChanged` fires but the control never re-renders — hand out a
    **fresh reference** each tick (e.g. `History = buffer.ToArray()`), or bind it to the per-tick
    fresh `SimulationSnapshot` so a sibling property forces the redraw. (2) The control's property
    type must match the snapshot's **exactly**: binding `IReadOnlyList<long>` (snapshot
    `MealIntervals`) to an `IEnumerable<double>` property silently yields null. The working panels
    get their redraw from `CurrentSnapshot` being a new record every tick.
  - **Host & DI:** a Generic Host (`Microsoft.Extensions.Hosting`) is the single composition root
    (Microsoft DI). Built in `Hosting/AppHostBuilder.cs`, started in `App.axaml.cs`. This mirrors
    the **HyperSpace** app (`C:\Users\TreyTomes\projects\HyperSpace`), which is the compatibility
    reference for this stack.
  - **MVVM:** Prism's `BindableBase` (`Prism.Core`, container-agnostic) — *not* the full Prism
    framework and *not* its DryIoc container. That is the whole trick: Prism.Core for view-model
    plumbing, Microsoft DI for the container, no conflict.
  - **Config:** `appsettings.yaml` at the **repo root** (`NetEscapades.Configuration.Yaml`), found
    by walking up from the executable; `--config <path>` overrides.
  - **Logging:** Serilog — console sink + daily rolling file sink (`./logs/` by default).
  - **CLI:** `System.CommandLine` parses `--seed`, `--config`, `--log-level` (parse-only; Avalonia
    runs the UI loop).
- `tests/WormForaging.Core.Tests/` — xUnit. Determinism and parity tests.
- `src/WormForaging.Harness/` — headless console runner for N-seed experiments. Same host/CLI/
  `appsettings.yaml`/DI/Serilog stack as the App (its own thin `HarnessHostBuilder`, no shared hosting
  assembly — Core stays pure). Two experiments, chosen with `--experiment`:
  - `baseline` (default, Phase 1): N seeds × {full, hebbian, frozen} **paired by seed** (one seed ⇒
    identical starting world/network/readout for all three modes, so only the learning rule differs);
    writes `summary.csv` / `timeseries.csv` / `aggregate.csv`. Knobs `--seeds/--base-seed/--steps/--sample`.
  - `swap` (Phase 2): warm the Full learner on Identity, then toggle the mapping every `segment` steps
    for `swaps` cycles, measuring meals-per-epoch recovery; writes `swap_cycles.csv` /
    `swap_recovery.csv` / `swap_savings.csv`. Knobs under `Harness:Swap:`, `--warmup/--segment/--swaps`.

  All CSVs go to the git-ignored repo-root `results/`. Discomfort/meals are read from the snapshot
  (`|bodyTemp − comfort|` for discomfort), so the harness reads Core without changing it. 10 seeds ×
  20k steps ≈ 2.6s (baseline); the default swap run (10 × 140k steps) ≈ 6s.

Target: **.NET 10**, **Avalonia 11.3**, **Prism.Core** (BindableBase) + **Microsoft.Extensions.Hosting/DI** + **Serilog** — matching the HyperSpace framework.

## Non-negotiable conventions

- **Determinism first.** No `System.Random`-by-default and no `DateTime`/wall-clock seeding
  buried in Core. All randomness flows through the `IRandom` abstraction
  (`Core/Rng/IRandom.cs`; concrete `Pcg32Random`), seeded explicitly. Same seed + same config ⇒
  bit-identical run. This is what makes every later experiment possible; it is guarded by a test.
- **All magic numbers live in `SimConfig`**, named. The prototype is full of inline constants
  the author flagged as untuned guesses — porting them is fine, but they must become named,
  documented config fields, not literals scattered through the step loop.
- `Core` never references Avalonia, a `Dispatcher`, or any UI type. If you reach for one, the
  logic is in the wrong project.
- Keep `worm_foraging.html` **unchanged** until C# reaches verified feature parity — it is the
  reference oracle we check the port against.

## Standards enforced by the build (`Directory.Build.props`)

- **Warnings are errors**, repo-wide. A warning is a bug we haven't read yet.
- **XML docs are required** on public API (`GenerateDocumentationFile` → CS1591 → error). Tests
  opt out; the App's XAML-generated code is exempt, but hand-written public members are not.
- **80% line coverage** gate on `Core`, enforced on every `dotnet test` via coverlet (the test
  assembly is excluded from the measurement).

## Build / run / test

```
dotnet build                                   # warnings-as-errors; XML docs required
dotnet test                                    # runs tests AND fails under 80% line coverage
dotnet run --project src/WormForaging.App      # launches the Avalonia app
dotnet run --project src/WormForaging.App -- --seed 42       # reproducible run
dotnet run --project src/WormForaging.App -- --help          # CLI options
```

Config: `appsettings.yaml` at the repo root. Logs: `./logs/wormforaging-<date>.log` plus console.

## Working preferences (from Trey)

- **Report negative results plainly.** A learning rule that doesn't learn is a finding.
- **Mark what is measured vs. guessed.** Parameter values from the prototype are guesses until
  a run says otherwise; say so.
- Verify literature claims before citing them (the handoff's neuroscience notes are from
  memory).
- This is meant to be an enjoyable, exploratory collaboration — think out loud, propose, and
  don't bury a real trade-off under a default.

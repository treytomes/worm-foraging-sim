# TODO / roadmap

Ordered. We don't start the experiment steps until the port is at verified parity and the
headless harness exists — otherwise every claim is just a browser eyeball.

Legend: `[ ]` todo · `[~]` in progress · `[x]` done · *(measured)* vs *(guessed)* for results.

## Phase 0 — Port to C# / Avalonia at feature parity  ← ✓ complete

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
- [x] **Smoke-run the app** to confirm the host boots, reads YAML, logs to console + file, and the
      seeded draws render (not yet launched this session).
- [x] `SimConfig` — every prototype constant becomes a named, documented field (one `record`;
      all defaults = prototype values; `double` not `float32`, parity is qualitative not bit-exact).
- [x] Port the world: arena, odor field `exp(-d/12)`, Lissajous heat lamp, temperature field
      `0.25+0.9·exp(…)`, food spawn/respawn with rejection radius. `World` owns the environment
      only (no body/net); 18 parity tests, Core at 99% line cov.
- [x] Port the body: position + arena clamp, body-temperature lag `+=0.08·(temp−body)`, eating,
      gut absorption `min(gut,0.01)`, energy balance `+ab−0.002−0.006·d1`, meal-interval log.
      `Body` reads `World` but holds no sensing/net/learning; 16 tests, Core at 99.5% line cov.
- [x] Port the senses: `Senses` builds the 8-channel vector — 4 odor (hunger-gated, Weber-normalized
      `gate·10·(c−mo)/(mo+1e-6)`) + 4 heat (`(−dev·6)·6·(t−mt)`), differential so a flat field reads 0.
      Swap is a first-class `SensePermutation` (Identity / Swapped / Toggle); shared N/E/S/W `Directions`
      added. The "2 internal drives" (hunger, dev) are NOT in this vector — they enter the network
      directly, and are already exposed on `Body`. 23 tests (Senses + permutation), Core at 99.6%.
- [x] Port the network: `Network` = 16 interneurons, fixed random wiring (Win, sparse ~20% Wrec,
      drive weights, bias), `u = wh·hunger + wt·dev·3 + b + Win·s + Wrec·xₚ`, `x = tanh(g·u)`, plus
      homeostatic gain adaptation (`g += 0.002·(0.4−ax)`, clamped). Readout `Wo` deliberately NOT
      here — it's the motor/learning layer's. Step contract: `Activate` → (motor/learning read it)
      → `AdaptAndCommit`. First component with random init, so it anchors determinism. 17 tests
      incl. exact forward-formula parity; Core at 99.7% line / 100% method.
- [x] Port motor layer + softmax action sampling: `MotorLayer` owns the readout `Wo` (the only
      weights learning changes), `Policy` = softmax(`Wo·x / T`) with max-shift, `Select` samples it.
      Rewrote the prototype's fragile guarded sampler as a plain inverse-CDF walk — verified
      equivalent (same buckets, same `K-1` fallback). Exposes flat `Get/SetReadoutWeight` for the
      three-factor rule; `MotorDecision` carries choice + probabilities (the `one-hot − p` term).
      18 tests incl. scripted-draw bucket parity + 200k-trial frequency check; Core at 99.8%.
- [x] Port the learning rule: `LearningRule` + `LearningMode` enum. Eligibility `e = 0.7·e +
      (one-hot−p)·x`; reward-prediction baselines/scales (`bo,ro,bt,rt`) → clipped surprise
      `rOd,rTh`; modulator `wH·(rOd+ab·50) + wT·rTh` with modes full=`r` / hebb=`0.6` / frozen=`0`;
      readout update `Wo += 0.04·mod·e − 1e-4·Wo`, clamped ±4, **skipped entirely when frozen** (no
      decay either). **Hard rule enforced + tested:** update is local — each `Wo[i]` depends only on
      its own `e[i]`, `Wo[i]`, and the one broadcast scalar; a test proves zeroing all but one
      eligibility entry changes exactly one weight. No backward pass anywhere. 19 tests; Core at 98.9%.
- [x] **`Simulation` step loop** — wires World→Senses→Network→Motor→Body→Learning in the prototype's
      exact order; drives read once at step top and reused in the modulator; odor delta measured
      across the move but before respawn. Runtime controls (Mode, ExploreTemperature, Homeostasis,
      Permutation/SwapSenses). `SimulationSnapshot` = immutable copied state for UI + harness.
      16 end-to-end tests: **bit-identical runs from a seed**, Full changes `Wo` / Frozen never does,
      bounded state over 5k steps, eat path fires. Core at 98.6%. **Core is complete.**
      *(No behavioral claim yet — full-vs-frozen meal rates etc. are Phase 1, by the harness.)*
- [x] Avalonia UI (custom `Control.Render` + `DispatcherTimer`): arena (worm, food, lamp, odor/heat
      `RadialGradientBrush` glows, trail, thermal ring) + all controls (pause, reset, swap, mode,
      homeostasis, explore, speed) + stats readout, plus the richer panels — energy/temp history,
      interneuron activations + readout-weight heatmap, and steps-per-meal. Each custom `Control`
      redraws only when one of *its own* styled properties changes value: bind at least one property
      to the per-tick fresh snapshot (or hand out a fresh reference), and keep the control's property
      type identical to the snapshot's (a mismatched element type makes the binding silently yield
      null — that was the steps-per-meal panel's blank-until-fixed bug).
- [x] **Parity check:** accepted as qualitatively equivalent by eye (Trey, 2026-10-22). The check is
      *qualitative* by construction — the frozen prototype uses an unseeded `Math.random` + `Float32`,
      so there is no numeric match to the browser; "parity" = the same phenomena (learns, relearns
      after swap, needs compete). No intentional behavioral diffs noted. The *measured* full-vs-hebb-vs-
      frozen comparison is Phase 1, by the harness.

## Phase 1 — Headless harness + baselines (handoff step 1)  ← ✓ complete

- [x] `WormForaging.Harness` console runner: runs N seeds × {full, hebb, frozen}, **paired by seed**
      (one seed ⇒ identical starting world/network/readout for all three modes, so the only
      difference is the learning rule). Same host/CLI/`appsettings.yaml`/DI/Serilog stack as the App
      (`Harness:` config section; `--seeds/--base-seed/--steps/--sample/--out` overrides). Writes
      `summary.csv` (per mode×seed), `timeseries.csv` (sampled energy/discomfort/meals), and
      `aggregate.csv` (mean±SD per mode), and logs the headline table. Output → repo-root `results/`
      (git-ignored). 10 seeds × 20k steps runs in ~2.6s.
- [x] Metrics: steps-per-meal (mean of meal intervals), energy over time, discomfort over time
      (`|bodyTemp − comfort|`, recovered from the snapshot — Core untouched), meals eaten, final
      energy; aggregated across seeds with sample SD (non-finite steps-per-meal from meal-less runs
      dropped, with an `_n` column recording how many seeds contributed).
- [x] **First measured baseline *(measured, 10 seeds × 20 000 steps, prototype-default config,
      2026-10-22)*:** three-factor **decisively** beats both controls.
      | mode | meals | steps/meal | mean energy | mean discomfort | final energy |
      |---|---|---|---|---|---|
      | **Full** | **276.5 ± 34.2** | **73.1 ± 9.3** | **0.933 ± 0.035** | **0.113 ± 0.009** | **0.919 ± 0.202** |
      | Hebbian | 5.7 ± 10.1 | 2863 ± 2931 | 0.021 ± 0.042 | 0.255 ± 0.011 | 0.000 ± 0.000 |
      | Frozen | 11.0 ± 11.8 | 2219 ± 1583 | 0.027 ± 0.033 | 0.245 ± 0.016 | 0.028 ± 0.087 |
      Full forages ~50× more, keeps energy near full and survives; both controls starve to ~0.
      **Surprises worth flagging:** (1) plain Hebbian (fixed `0.6` modulator) is *no better than
      frozen* — on the mean it's slightly worse (5.7 vs 11.0 meals), i.e. reward-blind correlation is
      actively unhelpful here, not a weak learner. Confirms the parking-lot suspicion about the
      arbitrary `0.6`. (2) steps/meal variance on the controls is enormous because most seeds eat
      0–6 meals, so the interval rides on one lucky meal — **meals-count is the robust throughput
      metric when meals are near zero; steps/meal is not.** All numbers are from the *guessed*
      prototype constants — the result is "the rule works at the untuned defaults," not a tuned claim.

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

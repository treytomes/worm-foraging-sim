# Handoff: worm-inspired foraging sim (learning without backprop)

Attach `worm_foraging.html` (single-file browser sim) alongside this note. Read it first; it is about 200 lines of vanilla JS plus canvas.

## Goal and philosophy
Trey (independent developer, 22 years of experience, longtime artificial-life enthusiast) is exploring **lifelong learning without backpropagation or feedforward-only nets**. He treats his AI systems as philosophical subjects, not tools. The sim should capture the *spirit* of C. elegans, not model it exactly. Hard rule: **no backprop or gradient computation anywhere**. Learning must stay local plus a broadcast scalar.

## What exists (all in the HTML file)
- **World:** 40x40 arena. One food source (odor falls off as exp(-d/12)), respawns after being eaten. A heat lamp moves on a slow Lissajous path. Temperature is 0.25 + 0.9*gaussian (sigma 8). Comfortable body temperature is 0.55.
- **Body:** energy (drains 0.002/step plus 0.006*discomfort), a gut buffer (eating adds 0.6, absorbed at 0.01/step, so satiety is delayed), and body temperature (lags local temperature at rate 0.08).
- **Senses (8 inputs):** four directional odor sensors (spatial contrast, normalized by mean, Weber-style) scaled by a hunger gate; four directional heat sensors scaled by signed thermal need (positive when cold). Two internal inputs: hunger and body-temperature deviation.
- **Network:** 16 interneurons, fixed random sparse recurrent wiring, tanh with per-neuron homeostatic gain (target mean |activity| 0.4). Four motor neurons (N/E/S/W). Action is sampled from a softmax over readout logits (temperature slider is the exploration knob).
- **Learning (only the 4x16 readout weights Wo change):**
  - eligibility trace e = 0.7*e + (chosen_k - p_k) * x_j
  - modulator r = hunger_weight*(odor-change advantage + 50*absorbed_food) + thermal_weight*(comfort-change advantage), weights are 0.2+hunger and 0.2+4*discomfort. Advantages are normalized by running baseline and rms.
  - update: Wo += 0.04 * r * e, with slight decay and clamp at 4.
- **Modes:** three-factor, plain Hebbian (constant modulator), frozen. Plus a homeostasis toggle and a "swap senses" button that rotates the sensor-to-direction mapping by 180 degrees.

## What is verified
Trey ran it in the browser and reports that three-factor learns well, including relearning after the senses are swapped, and that the two needs visibly compete. **The code has never been run in a test harness**, and the author (a previous Claude instance) never executed it. Treat parameter values as untuned guesses.

## Known rough edges
- The action-sampling loop is awkward (works, but rewrite it as a plain cumulative sample).
- Hebbian mode uses a fixed modulator of 0.6, which is arbitrary.
- Interneuron wiring is random, so results vary a lot by seed. There is no seeded RNG yet.
- Reward weights and the 50x absorption scaling have not been validated.

## Why the design looks like this
- Real C. elegans has 302 neurons, fixed topology, synaptic strength changes (e.g. glutamate receptor trafficking), and heavy neuromodulation (serotonin, dopamine, insulin-like and other peptides). Learning is mostly changed sensory gain and neuromodulator state, not rewiring. Peptide signaling forms a separate layer from the wired connectome.
- Credit assignment is made tractable by structure: dense immediate reward (odor change), local credit at the motor layer, and fixed upstream features. This works because the task is easy. Delayed rewards or hidden-layer learning bring the problem back.
- Neuron-level homeostasis (Marder, O'Leary) is the "self-maintaining unit" idea Trey likes. Literature details above are from memory, so verify before citing.

## Proposed next steps (in suggested order)
1. **Headless harness in Node:** seeded RNG, run N seeds per condition, report steps per meal, energy and discomfort over time. Needed before any claims.
2. **Swap-back test and savings metric:** swap senses repeatedly and measure whether relearning gets faster. Expect no savings in the current design, since the readout just overwrites.
3. **Slow/fast weights:** each readout synapse gets a fast, decaying component and a slow one that consolidates only when the fast component is pushed consistently in the same direction.
4. **Replay:** rehearse stored eligibility traces during low-urgency periods to protect old mappings.
5. **Scaling test:** interneurons from 16 to 200 and watch learning speed and noise.
6. **Delayed reward:** slow gut absorption or reward only on eating, to find where eligibility traces break.
7. **Hidden-layer plasticity** (sensor-to-interneuron weights), using local rules (node perturbation, e-prop style) rather than backprop.
8. **Evolve the fixed connectome** across generations (Baldwin-effect style) with within-lifetime readout learning.

## Working preferences
- Report negative results plainly. Mark what is measured versus guessed.

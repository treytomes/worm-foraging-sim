# How the virtual neurotransmitter works (and why it learns)

This document explains the learning mechanism at the heart of the project: the single
broadcast "virtual neurotransmitter" and the three-factor rule it drives. It is the
conceptual companion to the code in
[`LearningRule.cs`](../src/WormForaging.Core/LearningRule.cs); where a formula appears here,
the corresponding method is linked.

The one hard rule the whole project exists to respect: **no backpropagation, no gradients,
no automatic differentiation.** A synapse may change using only signals available right at
that synapse plus one global scalar. Everything below is a consequence of taking that
constraint seriously — and the surprising part is that, in this architecture, the
constraint costs nothing.

---

## 1. The big picture: a frozen reservoir with a learned readout

The network has three tiers — sensor/drive inputs, a pool of 16 recurrent interneurons, and
4 motor neurons (N/E/S/W) — but only one set of weights ever changes:

- The **interneuron reservoir** (input weights `Win`, sparse recurrent weights `Wrec`, drive
  weights, biases) is random at construction and **never trained**. It is a fixed, recurrent,
  nonlinear dynamical system that churns the input stream into a rich 16-dimensional "soup."
- The **readout** `Wo` (16 → 4) is the *only* thing learning touches. It is a **linear**
  map from the soup to the motor logits, which are then softmaxed into a policy and sampled.

This is **reservoir computing** (an echo-state network): let a fixed random recurrent pool do
the hard representational work for free, and train only a simple linear readout on top. Keep
that linear-readout fact in mind — it is the reason, in Section 6, that a local rule turns out
to be an *exact* learning signal rather than an approximation.

It also mirrors the biology the project is chasing: *C. elegans* has a fixed connectome and
expresses most learning through neuromodulation and changes near the output, not by rewiring
the reservoir.

---

## 2. What the "virtual neurotransmitter" is

It is a **single scalar**, `modulator`, recomputed every step and broadcast *identically* to
all 64 readout synapses. Four properties make it neurotransmitter-*like* rather than just a
number:

1. **Diffuse, not point-to-point.** One value bathes every readout weight alike — volume
   transmission, like a neuromodulator released into a region, not a wire to one synapse.
2. **Evaluative, not informational.** It encodes *"how is it going"* (a signed magnitude),
   not *"do this." * The "what to do" lives entirely in the activations.
3. **It gates plasticity, never activity.** The modulator touches *only* `Wo`, in
   [`ApplyReadoutUpdate`](../src/WormForaging.Core/LearningRule.cs). It never enters the
   network's forward pass. So the neurotransmitter affects **learning, not thinking**: the
   worm's moment-to-moment behavior is driven by senses + drives + recurrence; the
   transmitter only sculpts the readout *between* decisions.
4. **It is a prediction *error*, not raw reward** (Section 4) — dopamine-style.

---

## 3. The three factors at one synapse

A readout weight `Wo[k,i]` (motor `k` ← interneuron `i`) changes using only three
locally-available quantities:

```
ΔWo[i] = lr · modulator · e[i]  −  decay · Wo[i]
          ↑        ↑         ↑
         rate   the global   this synapse's own
              transmitter    eligibility tag
```

with `lr = 0.04` and `decay = 1e-4`. There is **no cross-weight term and no backward pass** —
the update would give the identical answer if run on each weight in isolation. That locality
*is* the hypothesis under test, not an implementation detail. The three-factor structure —
`Δw = rate × (local eligibility) × (global neuromodulator)` — is the standard form of
neuromodulated Hebbian plasticity.

The next three sections are the four stages that produce this update, in the order the
simulation runs them.

---

## 4. Stage 1 — the eligibility trace (a local, fading tag)

At action time, [`AccumulateEligibility`](../src/WormForaging.Core/LearningRule.cs):

```
e[k,j] = 0.7 · e[k,j] + (onehot_k − p_k) · x_j
```

- `(onehot_k − p_k)` is the **policy-gradient term**. For the action actually chosen,
  `1 − p_k > 0` (and *larger* when that action was unlikely, so surprising choices earn more
  credit); for the actions not chosen, `−p_k < 0`.
- `x_j` is the presynaptic interneuron activation.
- The product tags each synapse with *"this neuron was active while this action was taken more
  than expected"* — a Hebbian coincidence between presynaptic activity and the decision.

The trace **decays at 0.7 per step** — a fading memory (~3–10 steps) of which synapses were
recently responsible. That decay exists to bridge **delayed reward**: eating fills a gut that
is absorbed slowly, so the payoff of a good move arrives later. The tag keeps a decaying
record of responsibility so there is still something for the transmitter to multiply against
when it arrives. This is the biological **synaptic tag** idea — a synapse that just
participated is marked *eligible* to change, but does not change yet.

> **Why a short trace suffices.** The big delayed lump from digestion is spread out as a
> per-step *drip* of absorbed food, and the odor-gradient signal fires immediately on each
> move. So the reward is effectively de-delayed into small, near-immediate pieces, which a
> short trace can catch. Pushing reward later (slower absorption, reward only on eating) until
> this breaks is exactly the Phase 3 "delayed reward" experiment.

---

## 5. Stage 2 — reward-prediction error (the "surprise")

The transmitter is not raw reward; it is *surprise relative to what has been happening
lately*. [`UpdateRewardPrediction`](../src/WormForaging.Core/LearningRule.cs) keeps, for each
of two channels (odor, thermal), a running **baseline** and **scale** (both exponential moving
averages at rate `0.01`), then reports:

```
surprise = clip( (change − baseline) / (scale + ε),  ±3 )
```

This is reward-prediction-*error* coding — the Schultz dopamine story. If you reliably get the
same odor gain every step, the baseline catches up and the surprise decays toward 0: you stop
reinforcing what is already expected (habituation). Dividing by the running scale *whitens* the
signal, so a channel that is usually noisy does not drown out a quiet one. (The two epsilons —
odor `1e-4`, thermal `2e-3` — are asymmetric and flagged for review; they only guard the
divide-by-zero.)

---

## 6. Stage 3 — assembling the transmitter, with competing needs

[`Modulator`](../src/WormForaging.Core/LearningRule.cs):

```
modulator = (0.2 + hunger)      · (odorSurprise + absorbed·50)    ← food channel
          + (0.2 + 4·discomfort) · thermalSurprise                ← thermal channel
```

Two reward channels, each **gated by how urgent that need currently is**: the hunger weight
grows as energy drops; the thermal weight grows with discomfort. So when the worm is starving,
the food channel dominates the transmitter and learning bends toward foraging; when it is
overheating, the thermal channel takes over. **That is the "two needs compete" dynamic** —
expressed entirely in one scalar, by reweighting. (`absorbed·50` is a direct appetitive kick
for actually eating, added on top of the predicted-vs-surprise gradient signal; it is not
baseline-subtracted.)

---

## 7. Stage 4 — the plasticity update

[`ApplyReadoutUpdate`](../src/WormForaging.Core/LearningRule.cs), per weight:

```
Wo[i] ← clip( Wo[i] + 0.04·modulator·e[i] − 1e-4·Wo[i],  ±4 )
```

- `modulator > 0` ("going well") → synapses that pushed for the recent action are
  strengthened *in the direction they pushed*.
- `modulator < 0` ("going badly") → those same tendencies are pushed back down.
- The `−1e-4·Wo` term is reward-independent decay: a constant forgetting that bounds the
  weights and lets stale associations fade.

---

## 8. Why it works without backpropagation

This is the crux. The update `ΔWo ∝ modulator · (onehot − p)·x` is exactly **online policy
gradient** (REINFORCE) with an eligibility trace and a reward baseline. Normally the policy
gradient needs a backward pass. It does **not** here, for one reason:

The readout is **linear** — the motor logit is `z_k = Σ_i Wo[k,i]·x_i`. For a softmax policy on
a linear logit, the gradient of the log-policy is

```
∂ log π(a | x) / ∂Wo[k,i] = (onehot_k − p_k) · x_i
```

— a purely **local** quantity (presynaptic activation × a readout-layer term). The fixed
nonlinear reservoir already did the hard representational work, so the only thing left to learn
is linear, and the gradient of a linear-softmax policy is local by construction. Multiply that
local eligibility by a scalar reward and you have an unbiased (if noisy) policy-gradient step.
The reward-prediction baseline of Section 5 is the variance-reducing baseline/advantage term
that makes such estimators usable online.

So "biologically plausible local rule" and "actually a correct learning signal" are not in
tension here — the reservoir architecture is precisely what collapses them into the same thing.

---

## 9. The three modes

Same eligibility, same update equation — they differ *only* in what the transmitter carries:

| Mode | `modulator` | Meaning |
| --- | --- | --- |
| **Full** | the competing-needs reward (Section 6) | the real three-factor rule |
| **Hebbian** | a fixed `0.6` | reward-*blind* correlation ("fire/select together, wire together") |
| **Frozen** | `0` (update skipped entirely, no decay) | the no-learning control |

**Measured result (10 seeds × 20k steps, default config):** Full forages ~50× more than either
control (~276 meals vs ~6 Hebbian / ~11 frozen) and survives while both starve. Notably, plain
**Hebbian is no better than frozen** — slightly worse on the mean. A tag with no evaluation
just amplifies whatever it last did; it is the *third factor* (the neurotransmitter) that turns
correlation into learning. That contrast is the clearest demonstration of why this channel has
to exist. See [`TODO.md`](../TODO.md) for the full tables.

---

## 10. How honest to be about the biology

The ingredients are genuinely biologically **motivated**: eligibility traces / synaptic
tagging, dopamine-like reward-prediction error, diffuse neuromodulation that gates plasticity
rather than activity, and homeostatic self-regulation of the reservoir. But the exact
functional form — a softmax policy gradient — is an engineering choice that *happens* to be
local; a real synapse is not claimed to compute precisely `(onehot − p)·x`. Per the project's
stated aim, the mechanism is **biologically suggestive, not a literal model of a worm.**

And every constant above (`0.7`, `0.01`, `50`, `0.04`, the weights) is still a **guess** carried
over from the browser prototype, untuned. The measured results say the rule works *at the
untuned defaults* — not that these are the right numbers.

---

## Where this lives in the code

| Piece | File |
| --- | --- |
| The learning rule (all four stages) | [`LearningRule.cs`](../src/WormForaging.Core/LearningRule.cs) |
| The readout weights `Wo`, softmax policy, action sampling | [`MotorLayer.cs`](../src/WormForaging.Core/MotorLayer.cs) |
| The fixed reservoir | [`Network.cs`](../src/WormForaging.Core/Network.cs) |
| Every constant, named and documented | [`SimConfig.cs`](../src/WormForaging.Core/SimConfig.cs) |
| The step order that wires the stages together | [`Simulation.cs`](../src/WormForaging.Core/Simulation.cs) |

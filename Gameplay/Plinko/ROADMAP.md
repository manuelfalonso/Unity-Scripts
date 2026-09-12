# Deterministic Plinko — Feature Roadmap

**Status:** Core simulation working and verified; modular elements not started
**Module:** `Gameplay/Plinko`
**Assembly:** `SombraStudios.Shared.Gameplay.Plinko` (own asmdef, zero optional-package references)
**Reference spec:** *Project Wild: UNO Peggo (Plinko)* — Kelly Weeren / Arreanna Rostosky

---

## 1. Goal

A **deterministic Plinko board system**: given a hand placed layout, pick an entry and a catch point
and get back a ballistic trajectory guaranteed to end in that catch point — a different one each time
the same pair is asked for.

Who chooses the catch point is out of scope. It may be a server, a loot table, or a debug button. The
contract is local:

> Name an entry and a catch point. Get back several different, physically plausible trajectories that
> all land there.

Two requirements pull against each other:

1. **Determinism** — the token lands where it was asked to. Always, on every platform, at any framerate.
2. **Realism** — the fall must read as a bouncing token, not a marble on rails, and repeated drops must
   not look like replays of each other.

---

## 2. Approach: simulate off-line, record, replay

The spec settles the architecture:

> The drop will not be live-physics based. Once levels are developed they will be put through a
> backend simulator. This will create [a few thousand] paths the Drop Token can take through each
> level. When the player launches the Drop Token, one of those simulated pre-baked paths will be
> picked and replayed for the player.

So: simulate thousands of drops off-line with real bounce physics, discard any that never reach a
catch point, file the rest by where they landed, and replay a recording at runtime.

- **Determinism** comes from the *recording*. The catch point was decided at bake time; playback only
  reads back stored positions, so float drift can move the token by a pixel but never change the
  outcome.
- **Realism** comes from each recording being one continuous simulation. Every arc starts exactly
  where the last ended, and arrival speed and angle carry through the whole board.

### 2.1 The bounce model — why the first attempt failed

The first implementation modelled each peg as offering exactly **two exits**, at a fixed speed and
angle chosen per peg. It was deterministic, cheap, and allowed exact path counting with a
reverse-topological DP. It also looked wrong, for two reasons:

1. **The token left every peg identically** regardless of how it arrived. Arrival speed and angle were
   discarded, so the fall had no momentum and read as scripted.
2. **The contact point and the departure point were different places** on the peg. The token visibly
   jumped a fraction of a peg diameter at every bounce. A surface-sweep patch hid it but did not fix
   the cause.

The current model reflects the incoming velocity about the real contact normal, damps it by
restitution, and then rotates it by a bounded random scatter standing in for spin and surface
irregularity. A peg therefore has a continuum of outcomes rather than two, and the token's momentum
is preserved through the board.

**What this cost:** exact path counting. A graph of two-exit pegs can be enumerated; a continuous
reflection cannot. Coverage is now a *measured* outcome reported after simulating, not a number known
in advance. That is the right trade — and it is what the spec assumes anyway, since it also specifies
discarding simulated drops that fail to land.

### 2.2 Rejected alternatives

| | Rejected because |
|---|---|
| Two fixed deflections per peg | Motion reads as scripted; contact and departure points disagree. Tried, then replaced. |
| Quantised bounce state in a graph | Keeps counting, but merging states reintroduces the position jump at every bounce, bounded by bucket size instead of eliminated. |
| Unity physics with a mid-flight nudge | Not reproducible across platforms or framerates, and the correction is visible. |
| Solve launch parameters per drop | Chaotic; a 1-ULP difference flips the outcome. |

---

## 3. Architecture

```
PlinkoLayout (hand placed pegs, entries, catch points, walls)
   └─> PlinkoDropSimulator ── N drops per entry ──> discard: escaped, stuck, timed out
            └─> PlinkoSolver: file by catch point, pick a distinct spread, duration band
                     └─> PlinkoPathSet.Draw ──> PlinkoBallPlayer ──> Transform
```

Everything above `PlinkoBallPlayer` is a plain object, constructible with `new` and covered by Edit
Mode tests. The two MonoBehaviours only bootstrap, tick and expose Inspector fields.

### 3.1 Two rules keep the tracer honest

Both were written in response to real bugs, and the penetration suite exists to hold them.

1. **A contact only counts while the token is approaching that surface.** A bounce has to start
   exactly on the surface it just left, or the recording would have a gap. The first attempt achieved
   that by ignoring the just-hit peg for the whole of the next arc — which meant that when gravity
   arced the token back down onto the same peg, it passed clean through the centre. Rejecting a
   contact when the token is already moving *away* from that surface gives the same freedom at t = 0
   while keeping the peg solid for the rest of the arc.
2. **Search steps are sized from speed, not from the clock.** A fixed step count across a fixed time
   budget cannot work on a tall board: sized for a slow arc, a fast token near the bottom steps
   straight over a peg; sized for the fast case, every slow arc pays the same cost. Deriving each step
   from the token's current speed bounds its travel to 40 % of a contact radius throughout — and,
   because it terminates as soon as a contact is found, it turned out *faster* than the fixed count it
   replaced (0.5 s versus 1.0 s on the staircase).

### 3.2 Distinctness needs geometry, not contact identity

Filing drops by *which* pegs they hit is not enough. On a sparse board two drops can strike the same
single peg and still fly completely differently, so their contact lists are identical while the falls
look nothing alike. Requiring a contact difference of 2 was therefore unsatisfiable there, and the
staircase board kept 20 paths where it should have kept 57.

`MinTrajectoryDistance` is now the primary test: sample both drops at the same fractions of their own
duration and require them to get some minimum distance apart. Contact difference remains as a
secondary test, applied only when both drops have enough contacts to satisfy it.

---

## 4. Not looking rigged

Every observable that correlates with the catch point is a leak.

- **Duration normalization.** A near catch point that consistently finishes sooner than a far one is
  readable with a stopwatch before the token lands. Kept drops are constrained to a band around the
  median. Real bounces spread durations more than fixed deflections did, so the band is wider (±35 %).
- **Geometric distinctness**, per section 3.1 — otherwise the "variety" is drops that differ only in
  their last bounce.
- **Non-repeating draw.** `PlinkoPathSet.Draw` shuffles and cycles, so a trajectory does not come back
  until the pool is exhausted.
- **Reachability must be checked before offering a choice.** With real physics an outer entry cannot
  reach the far opposite catch point. Offering a button that cannot work is itself a tell.

---

## 5. Milestones

| # | Milestone | State |
|---|---|---|
| M0 | Hand placed layout, generators, catch points, reflective walls | done |
| M1 | Ballistic tracer against pegs, walls and catch point mouths | done |
| M2 | Real reflection bounce model with bounded scatter | done |
| M3 | Drop simulator with escape, stuck and timeout discards | done |
| M4 | Bake: file by catch point, geometric distinctness, duration band | done |
| M5 | Bake report: landing rate, coverage, discard reasons, per-pair shortfalls | done |
| M6 | Runtime playback, contact events with impact speed, token spin | done |
| M7 | Edit Mode suite (18 tests) | done |
| M8 | Two test scenes: symmetric grid, sparse staircase | done |
| M9 | Documentation with setup, testing and measured config ranges | done |
| M10 | Serialise the baked path set to an asset | done |
| M11 | Modular elements: bumpers, gates, funnels, destructible blocks | not started |
| M12 | Luck-weighted path selection | not started |

### M10 — persistence, done

`PlinkoPathSetSO` stores the drops, the layout they came from, and a signature over every input.
`PlinkoPathSetBaker` (Editor) writes it from a config asset's inspector or from CI; `PlinkoBoard`
loads it on Awake instead of simulating, and refuses — loudly — if the signature does not match the
board it is attached to.

The recording exploits two properties to stay small: each arc begins exactly where the last ended, so
only the first position is stored, and gravity is constant per bake. That leaves a velocity and a
duration per arc, plus two small numbers per contact; contact times and positions are recomputed on
load. The 105-path staircase asset is 25 KB.

Bake cost itself came down from ~4.2 s to ~1.8 s for 6000 drops on the grid, from three changes:

- **Per-drop RNG streams** instead of one stream per entry. Beyond the speed, this decouples a drop
  from every drop before it, so raising the simulation count adds drops rather than shifting existing
  ones — and it is the precondition for ever running the bake in parallel.
- **A candidate cap per pair.** A busy catch point attracted thousands of landed drops when eight were
  wanted, and every one of them was recorded into arrays before being discarded. Splitting
  `SimulateDrop` from `BuildLastPath` means a surplus drop now allocates nothing at all.
- **No delegates or square roots in the tracer's inner loop.** Bracketing a contact used to capture a
  closure per candidate peg per step; it now compares squared distances inline, and the layout is read
  from a flat `PlinkoLayoutSnapshot` rather than through `List<T>` indexers.

Jobs were considered and rejected for now: every drop is independent so `IJobParallelFor` would be
near-linear, but a struct job cannot call the managed tracer, and maintaining a second copy of the
collision code — after the tunnelling bug — is a worse risk than a one-off editor bake is a cost.

### M11 — modular elements

The spec's remaining level elements, all of which the simulator can host as new collider kinds:

- **Bumpers** — a peg that also awards a reward and tracks a radial fill across drops. Needs a
  reward hook on contact and per-instance hit state carried into the recording.
- **Gates** — circles the token falls *through*, awarding a bonus. A non-reflecting trigger collider;
  the recording needs to list which gates it passed.
- **Funnels** — the token enters and exits at the bottom in one of three directions, "random from a
  player facing perspective". Naturally a bake-time choice, so it fits the existing scatter draw.
- **Destructible blocks** — 5 to 10 hits, damage scaling with the multiplier. These **mutate the board
  mid-drop**, so a recording is only valid for a given block state. Either re-simulate the remainder
  when a block breaks, or bake per block-state, which explodes combinatorially. Re-simulation is the
  viable route and is why the simulator must stay fast.
- **Additional shapes and ramps** — already expressible as `PlinkoWall` segments.

Every one of these also needs its contact recorded in `PlinkoPath` so playback can fire the reward at
the right moment, which the spec calls out as a later iteration.

### M12 — Luck

> Which simulated path is chosen can be affected by Luck, in that Luck affects if the player is more
> or less likely to get a drop that results in a higher or lower amount of rewards.

Once gates and bumpers exist, each recording has a total bonus value. `Draw` then becomes a weighted
pick over the pool by that value, biased by Luck, instead of the current uniform cycle.

---

## 6. Determinism contract

1. A `PlinkoPath` is a recording. Playback reads stored arcs and never re-simulates.
2. Bake randomness comes from `DeterministicRandom` (seeded xorshift128). Never `UnityEngine.Random`,
   whose state is global and whose algorithm is not contractually stable across Unity versions.
3. `TraceSubsteps` and `RefineIterations` are fixed config data, never framerate or platform derived.
4. Playback advances on an accumulated elapsed value, not chained `deltaTime`.
5. No float computed at playback time can change the catch point.

---

## 7. Measured behaviour

Default settings, 12 × 9 staggered grid, 5 entries, 9 catch points, 1200 simulations per entry:

- **Landing rate:** 99.9 % of 6000 simulated drops reached a catch point (4 discarded on the bounce
  budget).
- **Coverage:** 45 of 45 entry and catch point pairs, none underfilled — report reads `complete`.
- **Contacts:** mean 21.5 per drop, max 65.
- **Duration:** median 3.75 s, kept range 2.45–5.06 s.
- **Bake cost:** 1.8 s, or none at all when loading a baked asset.
- **Penetration:** zero. Closest approach to any peg across a full drop equals the contact radius
  exactly.

Sparse staircase, 5 entries, 5 pegs, 5 catch points, 1200 per entry:

- **Landing rate:** 100 %.
- **Coverage:** 22 of 25 pairs. The rest are physically impossible and reported.
- **Contacts:** mean 2.4 per drop.
- **Bake cost:** 0.46 s, asset 25 KB for 105 paths.

---

## 8. Open questions

- **Multiball** — must simultaneous tokens avoid visually overlapping? Recordings are unaware of each
  other, so deconfliction is a separate problem.
- **Multiple levels** — the spec supports several levels per event, each needing its own bake. Where
  should baked sets be stored and keyed?
- **Catch point payout weighting** — should path counts per catch point follow the DP value spread, or
  stay uniform and let Luck do the biasing?

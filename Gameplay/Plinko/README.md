# Deterministic Plinko

Pick an **entry** and a **catch point**. Get back a recorded ballistic drop that lands there — a
different trajectory every time, and always the catch point you asked for.

- **Assembly:** `SombraStudios.Shared.Gameplay.Plinko`
- **Namespace:** `SombraStudios.Shared.Gameplay.Plinko`
- **Dependencies:** none. Unity Engine and the .NET BCL only.
- **Design notes:** [ROADMAP.md](ROADMAP.md)

---

## 1. What it does

```csharp
PlinkoBakeResult result = PlinkoSolver.Bake(PlinkoLayoutGenerator.CreateStaggeredGrid());

var random = new DeterministicRandom(seed: 1);
PlinkoPath drop = result.PathSet.Draw(entryIndex: 0, catchPointIndex: 6, random);

Vector2 where = drop.Evaluate(1.2f);   // token position 1.2 s in
int landsIn = drop.CatchPointIndex;    // always 6
```

`Draw` cycles a shuffled pool, so calling it again for the same pair returns a **different** recorded
drop until the pool is exhausted. That is the headline requirement: same entry, same catch point,
visibly different fall.

## 2. How it works

Off-line, the system simulates thousands of drops through the layout. Every bounce is a **true
reflection** of the incoming velocity about the contact normal, damped by restitution and then
scattered within bounds. Drops that reach a catch point are recorded whole and filed by where they
landed; drops that get stuck or escape are discarded. At runtime one recording is picked and replayed.

That is exactly the pipeline the UNO Peggo spec describes:

> The drop will not be live-physics based. Once levels are developed they will be put through a
> backend simulator. This will create [a few thousand] paths the Drop Token can take through each
> level. When the player launches the Drop Token, one of those simulated pre-baked paths will be
> picked and replayed for the player.

**Determinism** comes from the recording, not from re-simulating: the catch point was decided when
the drop was baked, and playback only reads back stored positions. **Realism** comes from every
recording being one continuous physical simulation — each arc starts exactly where the previous ended,
so the token never jumps, and arrival speed and angle carry through the whole board.

```
PlinkoLayout (hand placed pegs, entries, catch points, walls)
   └─> PlinkoDropSimulator  ── thousands of drops ──> discard what never lands
            └─> PlinkoSolver: file by catch point, keep a distinct spread
                     └─> PlinkoPathSet.Draw ──> PlinkoBallPlayer ──> Transform
```

### Why not a peg graph with fixed deflections

An earlier version modelled each peg as offering exactly two exits at a fixed speed and angle. It was
deterministic and cheap, but the motion was wrong: the token left every peg identically no matter how
it arrived, so it read as a marble on rails, and the contact point never matched the departure point
so the token visibly jumped around each peg. Real reflection fixes both, at the cost of giving up
exact path *counting* in favour of measured coverage.

## 3. Scene setup, step by step

1. **Create the board.** Empty GameObject, add **`PlinkoBoard`**. Pick a **Preset** —
   `Staggered Grid` for a classic symmetric board, `Staircase` for the sparse test board, or
   `From Asset` to use a hand placed layout. Gizmos immediately draw pegs, entries, catch points,
   walls and the play area.
2. **Position it.** The board is authored in a 2D plane (X across, Y up). Move and rotate the
   GameObject to place that plane; a 3D game just rotates the transform. Everything is projected via
   `PlinkoBoard.BoardToWorld`.
3. **Create the token.** Add a sphere, scale it to `2 × PlinkoSimConfig.BallRadius` (0.36 by
   default), add **`PlinkoBallPlayer`**, and assign your board to its **Board** field.
4. **Frame it.** For the default grid, an orthographic camera at `(0, -4.8, -10)` with size `7`. For
   the staircase, `(0, -2.4, -10)` with size `5.6`.
5. **Drop.**
   ```csharp
   [SerializeField] private PlinkoBallPlayer _player;

   _player.Drop(entryIndex: 0, catchPointIndex: 6);
   ```
6. **Read the report.** With **Log Report** on, the Console prints the bake summary on Awake — where
   unreachable pairs and underfilled pairs are reported.

### Checking reachability first

With real physics, an outer entry genuinely cannot throw a token into the far opposite catch point.
Ask before offering the option:

```csharp
if (board.IsReachable(entryIndex, catchPointIndex)) { /* enable the button */ }
```

### Hooking up effects

```csharp
_player.PegContacted  += (worldPos, speed) => _hitEffect.Play(worldPos, speed);
_player.WallContacted += (worldPos, speed) => _wallEffect.Play(worldPos, speed);
_player.TokenLanded   += catchPointIndex   => _payout.Award(catchPointIndex);
```

Impact speed comes with every contact, so effects and sounds can scale with how hard the token hit —
which the fixed-deflection model could not express.

### Shipping the paths as an asset

Simulating on Awake costs a second or two. A build should not pay that: bake once, ship the data.

1. Select a `PlinkoBoardConfigSO` and press **Bake Path Set Asset** in its inspector. A
   `<name> Paths.asset` appears beside it, holding the drops *and* the layout they were baked from.
2. Assign that asset to the board's **Path Set Asset** field. On Awake the board loads it instead of
   simulating, and logs how many paths it loaded.

`Assets/Sombra Studios/ScriptableObjects/Gameplay/Plinko/` holds a worked example, and
`PlinkoStaircaseTestScene` is wired to it.

For CI or a build script, call the same code directly:

```csharp
PlinkoBakeReport report = PlinkoPathSetBaker.Bake(layout, sim, bounce, generation, assetPath);
if (!report.IsComplete) { /* coverage gap — decide whether to ship it */ }
```

**The asset carries a signature over the layout and every setting.** If the board it is assigned to
does not match, the board logs an error and simulates instead of replaying. That check is not
optional bookkeeping: a recording baked from a slightly different board still plays back perfectly
smoothly while the token glides through pegs that have moved and lands where it no longer can.

The data stays small because each arc starts exactly where the last ended — so only the first
position is stored — and gravity is constant, so it is stored once. That leaves a velocity and a
duration per arc. The 105-path staircase asset is **25 KB**; a few thousand paths lands around a
megabyte.

### Hand placing a layout

`Sombra Studios ▸ Gameplay ▸ Plinko Board Config` creates a `PlinkoBoardConfigSO`. Its context menu
has **Fill With Staggered Grid** / **Fill With Staircase** to seed the lists, then edit `Pegs`,
`Entries`, `CatchPoints` and `Walls` directly. Set the board's preset to `From Asset` to use it.

Catch points are not tied to the bottom: give one a higher `Y` and it becomes the spec's "additional
catch point". Walls are line segments, so `AddTriangle` builds the spec's triangular side shapes.

## 4. The test scenes

Both live in `Assets/Sombra Studios/Scenes/Plinko/`.

| Scene | Board |
|---|---|
| `PlinkoGridTestScene` | Symmetric 12 × 9 staggered grid, 5 entries, 9 catch points |
| `PlinkoStaircaseTestScene` | Sparse: 5 entries, 5 pegs — one below each entry, each at a different height, 5 catch points |

Press Play. `PlinkoTestDriver` spawns visuals for pegs, catch points and walls, draws the whole chosen
trajectory as a line, and gives you:

- **Entry** row and **Catch** row to pick the pair. Catch points this entry cannot reach are greyed out.
- **Drop** to fire one token, **Auto drop** to repeat, **Cycle catch points** to walk the reachable ones.
- A header counting **Drops / Landed as requested / Mismatches**.

**Mismatches must stay at zero.** Any mismatch is also logged as a Console error.

Verified on both scenes: 99.9 % of simulated drops land in a catch point, no mismatches, and no drop
passes through a peg.

The staircase board is deliberately unfair — with one peg per column, many entry and catch point pairs
are physically impossible and the report says so. That is the point: it proves the system reports what
a layout cannot do instead of faking it.

## 5. Running the tests

`Assets/Sombra Studios/Shared/Tests/EditMode/Plinko/PlinkoSolverTests.cs` — 18 tests in
`SombraStudios.Shared.Tests.EditMode`.

**Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**, or filter to
`SombraStudios.Shared.Tests.Plinko`.

| Test | Guards |
|---|---|
| `Layout_StaggeredGrid...` / `Layout_Staircase...` | Geometry, gapless catch points, one peg per entry at distinct heights |
| `Bounce_ReflectsAboutTheNormalAndLosesEnergy` | The reflection really is a mirror, damped |
| `Bounce_AlwaysLeavesTheSurface` | 72 incoming angles all depart — nothing can stick to a peg |
| `Bounce_ScatterProducesManyOutcomesNotTwo` | The specific regression: a peg must not collapse to two exits |
| `Simulator_IsReproducibleForTheSameSeed` | Same seed, bit-identical drop |
| `Simulator_ArcsJoinUpExactly` | Every arc starts where the last ended, so the token never jumps |
| `Simulator_EveryPathLandsWhereItSaysGeometrically` | Recorded catch point matches the rendered position |
| `Simulator_NeverReportsSuccessWithoutReachingACatchPoint` | No stuck tokens smuggled through |
| `PlinkoPenetrationTests` (5 tests) | **The token never passes through a peg** — see below |
| `Solver_GridLandsEveryDropAndCoversMostPairs` | 100 % landing rate, coverage, and honest reporting |
| `Solver_RepeatedDrawsForOnePairGiveDifferentPaths` | The headline requirement |
| `Solver_StaircaseReportsWhatItCannotReach...` | Sparse layouts report gaps rather than hanging |
| `Path_EvaluateIsContinuousAcrossArcBoundaries` | Playback continuity |

### The penetration suite

`PlinkoPenetrationTests.cs` samples the whole recorded trajectory every 0.5 ms and measures the
closest the token centre ever gets to any peg. Nothing else catches a tunnelling bug, because passing
through a peg does not change the landing catch point — every other test still passes while it happens.

It runs on the grid, on the sparse staircase, at high restitution (0.9, where bounces are fastest and
steepest) and with a deliberately coarse trace, plus a check that every recorded bounce sits exactly
on the surface it bounced off. **If you change the tracer or the bounce model, run these first.**

The bug they were written for: after bouncing off a peg, the tracer used to ignore that peg for the
whole of the next arc so the bounce could start on its surface without instantly re-colliding. When
gravity arced the token back down onto the same peg, it passed clean through the centre. The fix was
to stop excluding the peg and instead reject any contact where the token is already *moving away* from
that surface — physically correct, and it keeps the peg solid for the rest of the arc. Sizing each
search step from the token's current speed closed the second, rarer route to the same symptom: a fast
token near the bottom of a tall board stepping straight over a peg.

## 6. Configuration reference

Ranges below were measured by sweeping each value on the default grid and reading the bake report.
The failure column is what actually happened, not a guess.

### `PlinkoBounceConfig` — the realism dials

| Field | Default | Recommended | Notes |
|---|---|---|---|
| `Restitution` | 0.7 | **0.6–0.8** | The single most important value. Measured on the default grid — landing rate and coverage of the 45 entry/catch pairs: 0.5 → 97.9 %, 42/45; 0.6 → 99.7 %, 43/45; **0.7 → 99.9 %, 45/45**; 0.8 → 100 %, 45/45 but 24.1 contacts per drop and drops up to 5.3 s. Below 0.5 the token dies on the pegs; above 0.8 it ricochets and drops drag. |
| `ScatterDegrees` | 18 | 10–25 | Variety dial. Coverage is 45/45 at 6, 18 and 30 alike, because the peg grid supplies most of the spread; this mostly changes how bouncy it *looks*. 0 makes every drop from an entry identical. |
| `SpeedScatter` | 0.12 | 0.05–0.2 | Timing variety as well as directional. |
| `MinBounceSpeed` | 0.6 | 0.4–1.0 | Floor so a spent token still clears the peg. |
| `MinDepartureAngle` | 8 | 5–15 | Stops a grazing bounce running along the surface and re-colliding. Do not set to 0. |
| `WallRestitution` | 0.45 | 0.3–0.6 | Lower than the pegs, so walls return the token to play instead of pinging it across. |
| `WallScatterDegrees` | 6 | 0–10 | Keep small — walls should be predictable. |

### `PlinkoSimConfig` — ball and budget

| Field | Default | Recommended | Notes |
|---|---|---|---|
| `BallRadius` | 0.18 | 0.10–0.30 | With `PegRadius` 0.12 and peg spacing 1.0, keep `PegRadius + BallRadius` under **0.40 × spacing**; past ~0.45 × spacing the token cannot fit between pegs at all. |
| `LaunchSpeed` | 1.0 | 0.5–2.0 | Downward speed at the entry. |
| `Gravity` | 39.24 | see below | 9.81 ÷ 0.25, i.e. one board unit read as 0.25 m. |
| `MaxBounces` | 80 | 40–120 | A drop exceeding this is discarded. Raise it if `BounceBudget` discards appear with high restitution. |
| `MaxDropDuration` | 20 | 10–30 | The no-stuck-tokens guard. |
| `TraceSubsteps` | 160 | 128–256 | Fixed data, never framerate derived. |
| `TraceMaxTime` | 2.5 | 1.5–4 | Longest single arc between two bounces. |
| `RefineIterations` | 24 | 16–32 | Bisection depth for contact times. |

**Changing drop speed without changing the board:** scale `Gravity` by *k* and every speed by √*k*.
The trajectories keep their shape while durations scale by 1/√*k*.

### `PlinkoLayout` — hand placed geometry

| Field | Notes |
|---|---|
| `Pegs` | Explicit centres. `PlinkoLayoutGenerator` fills these; edit freely afterwards. |
| `PegRadius` | 0.08–0.20 at spacing 1.0. One size for all pegs, per the spec. |
| `Entries` | One per Drop Button. Place them above the first peg row. |
| `CatchPoints` | Tile them edge to edge along the bottom so nothing can slip between. Raise a `Y` to make a mid-board catch point. |
| `Walls` | Segments. `AddPlayAreaWalls` adds the sides; `AddTriangle` adds a side shape. |
| `PlayArea` | A token leaving this voids the drop. Keep the sides walled or the landing rate falls. |

### `PlinkoGenerationConfig` — how many and how different

| Field | Default | Recommended | Notes |
|---|---|---|---|
| `SimulationsPerEntry` | 1200 | **1200 on Awake, 3000+ offline** | 1200 × 5 entries costs about 1.8 s on the grid. The spec calls for a few thousand per level; raise it freely for an asset bake, which a build never pays for. |
| `MaxCandidatesPerPair` | 96 | 64–256, or 0 for no cap | How many landed drops a pair holds while choosing which to keep. A busy catch point can attract thousands when eight are wanted, and recording them all was most of the old bake cost. Drops arrive in random order, so the first hundred are already a fair sample. Raise it if strict distinctness leaves pairs underfilled. |
| `PathsPerPair` | 8 | 4–16 | Baked size is `entries × catch points × this`. |
| `Seed` | 20260910 | any | Same seed, layout and settings give the same set. |
| `MinTrajectoryDistance` | 0.35 | 0.2–0.8 | How far apart two kept drops must get, in board units. **Primary distinctness test** — it is the only one that works on a sparse board, where two drops can hit the same single peg and still fly very differently. |
| `MinContactDifference` | 2 | 1–3 | Secondary, and only applied when both drops have enough contacts to satisfy it. Without that guard the staircase board kept 20 paths instead of 57. |
| `MaxDivergenceFraction` | 0.7 | 0.4–0.9 | Two kept drops must split within this fraction of the fall. |
| `NormalizeDuration` | true | true | Leave on — see below. |
| `DurationTolerance` | 0.35 | 0.2–0.5 | Band half-width around the median. Real bounces spread durations more than the old fixed model, so this is wider than you might expect. |

**Leave `NormalizeDuration` on.** If a near catch point consistently finishes sooner than a far one,
the outcome is readable with a stopwatch before the token lands. On the default grid, kept durations
run 2.29–4.72 s around a 3.50 s median across every catch point.

## 7. Reading the bake report

```
Plinko bake: 358 paths kept from 5996/6000 landed drops (99.9 %), 1755 ms.
Signature: D428AAE51C3C4951
Duration: median 3.76s, kept range 2.45s to 5.07s.
Contacts per drop: mean 21.5, max 65.
Discarded [BounceBudget]: 4.
Result: complete.
```

The signature line is what a baked asset stores. Two bakes of the same board and settings share it;
anything else does not.

| Line | Meaning | What to do |
|---|---|---|
| Landing rate below ~95 % | Tokens are getting lost or stuck | Check the discard reasons below |
| `Discarded [LeftPlayArea]` | Tokens escaped sideways | Call `AddPlayAreaWalls`, or widen `PlayArea` |
| `Discarded [BounceBudget]` | Too many bounces to finish | Raise `MaxBounces`, or lower `Restitution` |
| `Discarded [TooLong]` | Drop ran past its time budget | Raise `MaxDropDuration`, or lower `Restitution` |
| `Discarded [Timeout]` | A single arc reached nothing | Raise `TraceMaxTime`, or check for a gap in the walls |
| `UNREACHABLE pairs` | No simulated drop ever connected them | Often physically correct on a sparse or wide board. Grey the option out, raise `Restitution`, or add pegs |
| `Underfilled pairs` | Fewer kept than requested, with how many landed | Raise `SimulationsPerEntry`, or lower `MinTrajectoryDistance` |
| `Duration band relaxed` | A pair could only be filled outside the band | Widen `DurationTolerance`; until then that catch point is a timing leak |

## 8. Determinism contract

1. A `PlinkoPath` is a **recording**. Playback reads stored arcs and never re-simulates.
2. Bake randomness comes from `DeterministicRandom` (seeded xorshift128). Never `UnityEngine.Random`,
   whose state is global and whose algorithm is not guaranteed stable across Unity versions.
3. `TraceSubsteps` and `RefineIterations` are fixed config data, never framerate or platform derived.
4. Playback advances on an accumulated elapsed value, not chained `deltaTime`.
5. No float computed at playback time can change the catch point.

## 9. Limitations

- **Coverage is measured, not guaranteed.** With real reflections some entry and catch point pairs are
  physically impossible. Call `IsReachable` before offering a choice.
- **Baking is not free.** About 0.3 ms per simulated drop on the default grid, so 6000 drops takes
  ~1.8 s. Bake to an asset rather than paying it at startup.
- **The bake is single threaded.** Every drop is independent and each now has its own RNG stream, so
  fanning out across `IJobParallelFor` would be near-linear — but it means maintaining a second copy
  of the collision code as a struct job, which after the tunnelling bug is a liability. Not worth it
  while an asset bake is a one-off editor cost.
- **One token at a time is assumed.** Recordings are unaware of each other, so simultaneous tokens can
  visually overlap.
- **Bumpers, gates, funnels and destructible blocks are not implemented.** The spec's modular elements
  need per-element reactions and reward hooks. Walls and side shapes are in; the rest is M10 in the
  roadmap.
- **Luck-weighted path selection is not implemented.** The spec wants path choice biased by reward
  value; `Draw` currently treats every path in a pool equally.

## 10. Public API

| Type | Role |
|---|---|
| `PlinkoSolver` | `Bake(layout, sim, bounce, generation)` — the one call that does everything. |
| `PlinkoLayout` / `PlinkoCatchPoint` / `PlinkoWall` | Hand placed geometry. |
| `PlinkoLayoutGenerator` | `CreateStaggeredGrid`, `CreateStaircase`, and the helpers behind them. |
| `PlinkoSimConfig` / `PlinkoBounceConfig` / `PlinkoGenerationConfig` | Plain configs with defaults in code. |
| `PlinkoBoardConfigSO` | Optional asset wrapper for a hand placed level. |
| `PlinkoDropSimulator` | Simulates one drop. `LastFailure` says why one was discarded. |
| `PlinkoTracer` / `ArcSegment` | Ballistic tracing against pegs, walls and catch points. |
| `PlinkoPath` / `PlinkoContact` | A recorded drop and what it struck. |
| `PlinkoPathSetSO` | Baked drops plus their layout and signature, as an asset. |
| `PlinkoPathSetBaker` | Editor only. Bakes a layout to an asset; callable from CI. |
| `PlinkoHash` | Signature over a layout and its settings. |
| `PlinkoLayoutSnapshot` | Flat array copy of a layout, read by the tracer's inner loop. |
| `PlinkoDropOutcome` | Where a drop ended, without building a recording. |
| `PlinkoPathSet` | The baked pool, with non-repeating `Draw` and `IsReachable`. |
| `PlinkoBakeReport` / `PlinkoBakeResult` | What the bake managed and what it could not. |
| `DeterministicRandom` | Seeded, stable xorshift128. |
| `PlinkoBoard` / `PlinkoBallPlayer` | Thin MonoBehaviour adapters. |

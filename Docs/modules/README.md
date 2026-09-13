# Module map

One page per module, written for whoever — human or assistant — has to find
something fast. Read the page for the module you're working in; don't read them
all.

For "where is type X", grep [`INDEX.tsv`](../../INDEX.tsv) at the repo root
instead. See the **Finding things** section of `CLAUDE.md`.

## Modules

Each is one assembly, `SombraStudios.Shared.<Module>`, most with an optional
`Editor/` sub-assembly.

| Module | Scripts | Page | What's in it |
|---|---:|---|---|
| `Utility/` | 108 | [Utility.md](Utility.md) | Timers, lerp, coroutine hosts, gizmos, loggers, and a long tail of single-purpose helpers |
| `Patterns/` | 92 | [Patterns.md](Patterns.md) | Design patterns: observer (3 flavours), state machines, singletons, pooling, service locator, DI |
| `Gameplay/` | 66 | [Gameplay.md](Gameplay.md) | Drop-on components: movement, drag/throw, spawners, `Behaviours/` controller+SO pairs |
| `Systems/` | 62 | [Systems.md](Systems.md) | Self-contained systems: stats, resources, objectives, teleport, close captions, loot |
| `VFX/` | 47 | [VFX.md](VFX.md) | `PropertySO/` shader-property system (Material vs MaterialPropertyBlock), parallax, camera shake |
| `Examples/` | 36 | — | Runnable demos of patterns and delegates. **Free to delete** |
| `ScriptableObjects/` | 28 | — | SO architecture: `Conditions/`, `Patterns/`, `RuntimeSets/`, `Values/`, plus editor tooling |
| `UI/` | 23 | — | Inventory, panel drag/resize, image drag, mobile UI |
| `XR/` | 16 | — | Interactables, socket interactors, key/lock. Almost entirely `UNITY_XR_INTERACTION_TOOLKIT` |
| `Audio/` | 16 | — | SFX system with dictionaries and level manager; some of it gated on serialized-collections |
| `Animations/` | 15 | — | Animator helpers and `AnimatorParameterReference/` codegen |
| `Scenes/` | 14 | — | Scene loading `Strategies/`, `SceneAsset/` references, editor scene switcher |
| `Physics/` | 12 | — | Physics-engine helpers |
| `Extensions/` | 10 | — | C# / Unity extension methods |
| `Enums/` | 9 | — | Shared enumerations, including the Unity-message groups |
| `Tools/` | 8 | — | General tools, notably `Search/` for assets and references |
| `Services/` | 8 | — | Third-party integrations: Ads, Firebase, notifications, web API. Mostly gated |
| `Attributes/` | 8 | — | Custom property attributes and their drawers |
| `Networking/` | 20 | [Networking.md](Networking.md) | Steam lobby + NGO session plumbing; `Sessions/` is dependency-free, `Steam/` and `Netcode/` are gated |
| `Editor/` | 4 | — | Repo-wide editor utilities |
| `Optimization/` | 3 | — | `ReturnToPool` and friends |
| `Structs/` | 3 | — | Lightweight serializable value containers |
| `Tests/` | 3 | — | Edit-mode tests (`Structs`, `AI`). See the Testing section of `CLAUDE.md` |
| `AI/` | 2 | — | `LineOfSight` + `IsInSightData` |
| `Interfaces/` | 2 | — | Cross-module contracts |
| `Inputs/` | 1 | — | Input helpers |
| `Splines/` | 1 | — | Spline-package helpers. Gated on `UNITY_SPLINES` |
| `Tilemaps/` | 1 | — | Tilemap helpers |
| `Video/` | 1 | — | Video playback helpers |

Modules without a page are small enough that `INDEX.tsv` plus the file itself is
faster than prose. Add a page when a module grows a real choice to explain —
that is what the five existing pages all have in common.

## What a module page should contain

Keep them short and decision-oriented:

1. **What the module is for**, in two sentences.
2. **Layout** — one line per subfolder.
3. **The choice**, if there is one. Modules with several implementations of the
   same idea (three observers, two state machines, Material vs
   MaterialPropertyBlock) need a table that says *when* to pick each. This is
   the part that saves the most time.
4. **One drop-in snippet** that compiles.
5. **Gotchas** — deprecated folders, separate Editor assemblies, static state
   that needs unregistering.
6. **Gates** — the define symbols in that module, plus the `awk` one-liner to
   re-check.

## Known drift

`README.md` lists a few subfolders that no longer exist (`Utility/FrameRate/`,
`Utility/Logger/` — it is `Loggers/` — and `Systems/Tutorial/`). `INDEX.tsv` is
generated and does not have this problem; prefer it when the two disagree.

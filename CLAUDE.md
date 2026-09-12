# CLAUDE.md

Context for Claude Code working in this repository.

## Project

- **Name:** SombraStudios.Shared
- **What it is:** a shared Unity library of reusable systems, utilities, and
  patterns (AI, Animations, Audio, Gameplay, Networking, Optimization,
  Patterns, Physics, Services, Systems, UI, Utility, VFX, XR, etc.), used
  across multiple game projects.
- **How it's consumed:** this repo can be added as a **git submodule** 
  inside each consuming project. It is not a
  standalone playable project — there is no single game, scene flow, or
  target platform to design around here.
- **Docs:** published with DocFX from `Docs/` to
  https://manuelfalonso.github.io/manuelfalonso/ via the
  `.github/workflows/deploy-docfx.yml` action on every push to `main`.

## Finding things (do this before any search)

`INDEX.tsv` at the repo root is a generated, greppable index of every public
type in the library — one tab-separated row each, columns
`Type · Kind · Base · Namespace · Path · Gate · Summary`. It is the entry point
for every "where is X" or "is there something for Y" question.

1. **Grep the index; never read it.** `grep -i pool INDEX.tsv` costs a few
   hundred tokens and returns exact paths. Reading the whole file costs upwards
   of 25k tokens — don't `cat` it, don't open it in an editor view.
2. **Read the owning module's page** in `Docs/modules/<Module>.md` — what the
   module is for, its entry points, and a drop-in snippet.
3. **Only then open the `.cs` file.**

Do not Glob or Grep the whole tree to answer a discovery question. 600+ source
files across 30 modules makes that slow and expensive, and the `Base` column
already answers "MonoBehaviour, ScriptableObject, or plain class?" without
opening anything. Fall back to a tree-wide search only when the index has no
hit at all.

The `Gate` column is the define symbol guarding the type, `-` when
unconditional. A gated type will not compile in a project lacking that symbol,
so check it before recommending anything.

Regenerate the index after adding, renaming, or deleting a public type, and
commit it alongside the change:

```bash
python .github/scripts/generate_index.py
```

CI runs the same script with `--check` on every push and fails if the index is
out of date. A stale index is worse than no index, because it stops the reader
from searching further.

**Do not answer "what exists" from `Docs/obj/`.** Those yml files are committed
DocFX output — a snapshot from the last manual regeneration, not the current
sources, and types gated behind a symbol `docfx.json` does not set are missing
from them entirely. `.ignore` keeps them out of search results; `INDEX.tsv` is
generated from the sources and verified in CI, so it is the current answer.
The regeneration procedure is in `README.md` under "Regenerating the
documentation".

## Read these first

These files are authoritative and take precedence over anything summarized
below. Read the relevant one before writing code:

- **`INDEX.tsv`** — generated index of every public type (grep it, per above).
- **`Docs/manual/CodeStyle.md`** — the full C# style sheet (naming, formatting,
  comments, events, ScriptableObjects). This is the source of truth for style;
  the section below only highlights what's easiest to get wrong. It is prose with
  fenced examples rather than a compiled file, so it ships as documentation
  instead of as a `MonoBehaviour` in every consuming project.
- **`README.md`** — canonical folder structure with per-subfolder
  descriptions, plus the preprocessor-directive table (see below).
- **`Docs/modules/`** — one page per module: purpose, entry points, drop-in
  snippet. Read the page for the module you're working in.
- **`Docs/`** — DocFX config and manual pages.

## Environment

- **Unity 6.3** (6000.3.x) minimum — do not use APIs deprecated or removed in
  Unity 6. Consuming projects may be on a newer Unity version; don't rely on
  anything introduced after 6.3 without checking.
- **Render pipeline / Input system / 2D vs 3D:** none assumed. Consuming
  projects vary (URP/HDRP/Built-in, new Input System vs legacy, 2D vs 3D).
  Code here must stay agnostic to these unless a module is explicitly scoped
  to one (e.g. a folder clearly named for it).
- **IDE:** Rider / Visual Studio (per-developer, not fixed here).
- **Host projects are disposable.** Whatever project this submodule happens to
  be checked out inside is temporary — a scratch project, a fresh prototype,
  or a real game. Never infer a guarantee from the current host's
  `Packages/manifest.json`, installed packages, Scripting Define Symbols, or
  Project Settings. The only things guaranteed everywhere are the Unity Editor
  itself and the .NET BCL. Everything else is optional and must be guarded.

### Optional packages and preprocessor directives

Consuming projects install different packages, so anything depending on a
non-guaranteed package **must** be wrapped in the project's define symbol and
referenced from the module's asmdef. `README.md` holds the authoritative
table; current symbols include `UNITY_SPLINES`, `UNITY_XR_INTERACTION_TOOLKIT`,
`CINEMACHINE`, `UNITY_ADVERTISEMENTS`, `NAUGHTY_ATTRIBUTES`,
`A_YELLOWPAPER_SERIALIZED_COLLECTIONS`, `DOTWEEN`, `Eflatun_SceneReference`,
`FIREBASE_APP`, `FIREBASE_AUTH`.

```csharp
#if CINEMACHINE
    // Cinemachine-dependent code
#endif
```

Reuse an existing symbol rather than inventing a variant. If a task needs a
package with no symbol yet, tell me — it's my call, not a silent addition.
Never assume a package is available just because some consuming project
happens to have it installed.

**Prefer asmdef `versionDefines` over manually-set Scripting Define Symbols.**
A `versionDefines` entry makes Unity define the symbol automatically when the
package is present, so a fresh project needs zero Project Settings setup — the
module simply compiles itself out when the package is missing:

```jsonc
// in the module's .asmdef
"versionDefines": [
  { "name": "com.unity.cinemachine", "expression": "", "define": "CINEMACHINE" }
]
```

An empty `expression` means "any version present". Where an entire assembly is
meaningless without the package, add `"defineConstraints": ["CINEMACHINE"]` as
well: Unity then skips compiling that assembly altogether, so its unresolved
package references cannot produce errors in a project that lacks the package.
`#if` guards alone do **not** save an asmdef whose *references* are missing —
only a define constraint does.

## Hard rules

1. **Never edit or create files in** `Library/`, `Temp/`, `Obj/`, `Logs/`,
   `UserSettings/`, or `Build/` (of whichever host project has this submodule
   checked out). These are machine-generated and enormous.
2. **Never hand-edit** `.unity` scenes, `.prefab`, or `.asset` files. They are
   YAML with internal file IDs; editing them by hand corrupts references.
   Scene and prefab changes are made by me in the Editor.
3. **Never create, delete, or rename `.meta` files.** Unity owns them. If you
   move or delete a `.cs` file, tell me so I do it through the Editor instead —
   moving a file without its `.meta` breaks every reference to it, in *every*
   consuming project.
4. **Never edit `ProjectSettings/`** of a host project from in here — this
   submodule has no project settings of its own to own.
5. **This folder is its own git repository** (a submodule), independent of
   whatever project it's checked out inside. Don't run git commands here
   assuming you're in the host project's repo, and don't assume the host
   project's `.gitignore` applies. Submodules are commonly checked out in
   detached HEAD — before committing, confirm we're on a proper branch rather
   than committing to detached HEAD.
6. **Never claim something "works" unless you actually verified it.** By
   default you cannot see the Console or enter Play mode — say what you
   expect and what I should look for. The Unity CLI (below) can sometimes
   verify for real; if you didn't run it, don't imply you did.

## Architecture rules (ranked)

These are *architecture* rules, distinct from the style rules in
`Docs/manual/CodeStyle.md`. They are ranked: when two conflict, the lower
number wins.

**POCO** = Plain Old CLR Object — an ordinary C# class with no Unity base
type. Not a `MonoBehaviour`, not a `ScriptableObject`: constructible with
`new`, testable without the Editor, requiring no scene, prefab, or asset.

### 0. Fast prototyping is the tie-breaker

This library exists to stand a prototype up quickly. When a rule below is
ambiguous, choose whatever makes a system faster to read, drop in, and extend.
*Time-to-first-use* — the number of steps needed to get a system working in a
fresh project — is the metric that arbitrates.

### 1. Logic in POCOs, MonoBehaviours as thin adapters

Put behaviour in plain C# classes. A `MonoBehaviour` should only bootstrap,
tick, expose fields to the Inspector, and forward Unity messages. This keeps
logic testable without the Editor and off the managed↔native message path.

Every POCO needs a defined owner: something constructs it, ticks it if it needs
ticking, and disposes it. Trading Unity's lifecycle for leaked event
subscriptions or static state is a net loss — whatever subscribes must
unsubscribe.

### 2. Composition over inheritance

Compose behaviour from small parts. A single-level abstract base is fine where
it removes real duplication; three levels of inheritance is a defect. Never
inherit merely to share a field.

### 3. Interfaces must be earned

Add an interface when there are **two or more real implementations**, or when
it is the specific seam a consumer is meant to replace. Otherwise start
concrete — a one-implementation interface costs a file, a namespace hop, and an
indirection while delivering no substitutability. Extracting an interface later
is a small refactor; deleting a speculative one is a breaking public API
change.

### 4. Config: POCO with defaults first, ScriptableObject only as a wrapper

Default to a plain C# config class whose defaults are set in code, so
`new MovementConfig()` works with zero assets and zero Inspector setup. Pass it
in by constructor or property — **injected, not static**. Mutable statics
survive across Play sessions when domain reload is disabled, leak between
tests, and cannot hold two variants at once.

A `ScriptableObject` is an *optional* thin wrapper holding such a POCO, for
when a designer genuinely needs to author variants as assets. It must never be
the only way to configure a system.

### 5. Serialization is a cost, not a default

Serialize only what a human actually needs to edit or what genuinely must
persist. Every serialized field is a schema that breaks prefab and asset data
across every consuming project when renamed or retyped (see Serialization
below).

### 6. Assume no packages; guard everything optional

**The baseline is an empty Unity project.** Dropping this library into a fresh
project with a default manifest must produce **zero compile errors** — that is
the acceptance test for the whole repo, and it applies to test assemblies and
Editor assemblies exactly as much as to runtime code.

Core systems therefore depend on the Unity Editor and the .NET BCL only.
Nothing here may *require* a third-party serializer, tween library, attribute
pack, or even a first-party optional package (Input System, Cinemachine,
Splines, TextMeshPro, Test Framework, URP/HDRP) to function.

Anything touching an optional package must be guarded on both levels:

1. `versionDefines` in the asmdef, so the symbol appears automatically when the
   package is present (see Environment above) — no manual project setup.
2. `defineConstraints` on any assembly that cannot compile without it, so Unity
   skips the assembly entirely rather than erroring on missing references.

Gated code **must be useful with the symbol off**, or live in a folder
consumers opt into wholesale — a `#if` branch is a code path that is never
compiled and therefore never verified.

### 7. Editor code is optional, never required

A system must work at runtime with its `Editor/` folder deleted. Editor tooling
is acceptable only when the tool *is* the feature. If a system only functions
after someone runs a menu item, it fails rule 0.

### 8. Atomize, and delete what does not earn its place

One system, one job, small files. A script whose feature is extremely rare or
adds no real value should go. Because this library is consumed as a submodule
by projects that update on their own schedule, deletion is triaged — **Keep /
Merge / Deprecate / Delete**.

**Reference count is evidence, not a verdict.** "No callers in this repo" and
"no callers in the project I can see" do not mean unused: other consuming
projects are not visible from here, and a general-purpose utility can be
valuable before anything happens to call it. Before proposing a deletion, judge
the script on its own terms — is the capability useful, does it scale, does it
compose with the systems around it, is it the obvious sibling of something in
active use? Only recommend removing what fails *those* tests. Say plainly which
projects were searched and which were not.

`Examples/` and demo scripts are free to delete; anything else public follows
Public API stability above, and per hard rule 3 you flag the file for me to
remove in the Editor rather than touching `.meta` files.

## Unity CLI (experimental — opt-in)

A `unity` CLI may be installed and on PATH (`~/AppData/Local/Unity/bin/unity`,
`1.0.0-beta.5` at time of writing). It can talk to a running Editor over a
local HTTP API **only if that host project has `com.unity.pipeline` in its
manifest** — most won't, and a fresh prototype certainly won't. Both the CLI
and the package are **experimental/beta**. Check, don't assume: if
`unity status` returns nothing, there is no connected Editor and you fall back
to the normal "tell me what to look for" loop. Never treat the CLI as
available, and never add `com.unity.pipeline` to a project to make it work.

Useful, read-only, safe to run unprompted:

```bash
unity status --no-banner
```

- `unity status` — which Editors are connected (port, project, version, state).
  Empty output means no Editor is running with the Pipeline package; in that
  case fall back to the normal "tell me what to look for" loop.
- `unity list` / `unity cmd` — commands the connected Editor actually exposes.
  Check this rather than assuming a capability exists.
- `unity doctor`, `unity logs` — environment diagnostics and the **Hub** log
  (not the Editor console).

**Ask before running** `unity test`, `unity build`, `unity run`, or `unity
open`. These spawn or drive an Editor and can lock `Library/`, conflicting
with the Editor I already have open, and can take many minutes.

Submodule-specific caveats:

- **This folder is not a Unity project.** It has no `ProjectSettings/` or
  `Packages/`, so every CLI command must target the host project root (the
  folder containing `Assets/`), not this directory. Run them from there or
  pass the project path explicitly.
- Test runs execute the *host project's* whole suite, not just this library's
  tests — filter to the relevant assemblies when checking a change here.
- A green run in one host project doesn't prove the change is good in the
  others; consumers differ in Unity version, render pipeline, and define
  symbols.

## Public API stability

Anything public here can be referenced by multiple consuming projects that
may not update in lockstep. Treat breaking changes as expensive:

- Prefer additive changes over renaming/removing public members.
- If a public member must go, mark it `[Obsolete]` with guidance first rather
  than deleting it outright, and tell me which consuming projects to check.
- Renaming or retyping a serialized field breaks existing prefab/ScriptableObject
  data in *every* project holding a reference — see Serialization below.
- Ask before large refactors that touch a module's public surface.

## Working loop

The normal cycle is:

1. You write or modify C# under the relevant module folder here.
2. I recompile and enter Play mode in whichever host project has this
   submodule checked out.
3. I paste back Console errors, warnings, or a description of the wrong
   behavior.
4. You fix.
5. Once verified, changes are committed and pushed in *this* repo's own
   history, separate from the host project's commit. Host projects then bump
   their submodule pointer on their own schedule — don't assume a change here
   is "live" everywhere just because it's committed.

## Folder structure

Top-level modules, one asmdef each (`SombraStudios.Shared.<Module>`), most
with an optional `Editor/` sub-asmdef:

```
AI/  Animations/  Attributes/  Audio/  Editor/  Enums/  Examples/
Extensions/  Gameplay/  Inputs/  Interfaces/  Networking/  Optimization/
Patterns/  Physics/  Scenes/  ScriptableObjects/  Services/  Splines/
Structs/  Systems/  Tools/  UI/  Utility/  VFX/  Video/  XR/
```

`README.md` has the full breakdown with per-subfolder descriptions — consult
it before deciding where something belongs; it's more current than this list.

New functionality goes in the matching existing module where possible. Only
add a new top-level module (and asmdef) when nothing existing fits — ask
first if it's not obvious, and update `README.md` when you do.

## C# conventions

**`Docs/manual/CodeStyle.md` is the authoritative style sheet** (adapted from
Unity's Code Style Guide e-book). Read it rather than inferring style.
Highlights that are easy to get wrong:

- **Allman braces** (opening brace on its own line), ~120 char lines.
- Namespace under `SombraStudios.Shared`, sub-namespaced to match the module
  folder (e.g. `SombraStudios.Shared.Gameplay.Spawners`).
- One MonoBehaviour per file; filename matches the type name exactly.
- `private` fields with `[SerializeField]`, prefixed `_camelCase`. Properties,
  methods, public fields, and constants `PascalCase`.
- ScriptableObjects: suffix the type `SO`, and `CreateAssetMenu` `menuName`
  always starts with `Sombra Studios/`.
- Events: verb phrases, present participle for before / past participle for
  after (`OpeningDoor` / `DoorOpened`), raiser named `OnDoorOpened`. Prefer
  `System.Action`.
- `[Tooltip]` instead of a comment on a serialized field; `[Header]` to group.
  Comments explain *why*, not *what*. No `#region`. No commented-out code.
- **`<summary>` XML docs on public types and members** — these are published
  to the DocFX site, so on a shared library they're part of the deliverable,
  not optional polish.

Unity-specific rules not covered by `CodeStyle.md`:

- New modules needing Editor-only code get a `<Module>/Editor/` folder with
  its own `SombraStudios.Shared.<Module>.Editor.asmdef`, mirroring existing
  modules.
- Prefer composition over deep MonoBehaviour inheritance.
- `TryGetComponent` over `GetComponent` + null check.
- Cache component lookups in `Awake`; never call `GetComponent`, `Find`,
  `FindObjectOfType`, or `Camera.main` inside `Update`.
- No LINQ or per-frame allocation in hot paths (`Update`, `FixedUpdate`,
  collision callbacks). It's fine in setup and editor code.
- Physics work goes in `FixedUpdate`, input polling in `Update`.
- Tunable data goes in a POCO config class with defaults in code, injected
  into the system; wrap it in a `ScriptableObject` only when a designer needs
  to author variants as assets (architecture rule 4). Not hardcoded constants,
  not prefab-baked values, and not a mutable static.
- Pool anything spawned repeatedly (projectiles, VFX, damage numbers).
- Coroutines are fine; match whatever the surrounding module already uses if
  it's already standardized on UniTask/Awaitable.

## Serialization

Changing a serialized field's name or type silently wipes its value on every
prefab/ScriptableObject asset across every consuming project that references
it — not just this repo. If a rename is necessary, use `[FormerlySerializedAs]`
and tell me explicitly which consuming projects to re-check.

## Testing

Tests live in `Tests/EditMode/`, behind
`SombraStudios.Shared.Tests.EditMode.asmdef`.

The Test Framework is an optional package like any other (architecture rule 6):
a fresh project may not have it, and a consumer may remove it. A test assembly
that assumes it is present produces compile errors in every project that lacks
it — which is exactly the failure mode the whole library must avoid.

### Guarding the test assembly

Every test asmdef carries a define constraint, so Unity skips compiling it
entirely when the Test Framework is absent:

```jsonc
{
  "name": "SombraStudios.Shared.Tests.EditMode",
  "references": [
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner",
    "SombraStudios.Shared.Structs",       // only modules actually under test
    "SombraStudios.Shared.AI"
  ],
  "includePlatforms": ["Editor"],
  "precompiledReferences": ["nunit.framework.dll"],
  "overrideReferences": true,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

`UNITY_INCLUDE_TESTS` is defined by Unity itself when the Test Framework is
installed — no Project Settings entry, no manual setup. Without the constraint,
the unresolved `UnityEngine.TestRunner` and `nunit.framework.dll` references
error out even though no test code is being used.

The whole `Tests/` tree must be deletable by a consumer with zero effect on
runtime code. Nothing outside `Tests/` may reference anything inside it.

### What gets tested

- **Location:** `Tests/EditMode/<Module>/`, behind one asmdef that references
  **only the modules actually under test**. Grow that reference list as modules
  are reviewed — never reference all modules up front, since one package-gated
  module would break the whole test assembly. A module gated on an optional
  package gets its own test asmdef, whose `defineConstraints` list both
  `UNITY_INCLUDE_TESTS` and that module's symbol.
- **Edit Mode only** by default. Play Mode tests need a scene and cost seconds
  each; add one only when the thing under test is genuinely frame- or
  physics-dependent.
- **Test POCOs, not MonoBehaviours.** Adapters get no tests. If a
  `MonoBehaviour` looks like it needs one, that is the signal its logic belongs
  in a POCO (architecture rule 1) — the suite is a design pressure, not a
  coverage exercise.
- **No coverage target.** Every kept system with a branch, a formula, or a
  state transition gets at least one test. Pure glue gets none.
- **Review before test.** Tests are written folder by folder during the review
  pass, and only for systems with a Keep verdict — never for code awaiting
  deletion.
- **Refactoring existing code:** write a characterization test pinning the
  current observable behaviour *first*, then extract the POCO, and keep it
  green. That is what makes rule 1 safe to apply to a public API that multiple
  projects consume.
- Tests run from whichever host project has the Test Framework installed; that
  is the consumer's choice, not a dependency of this repo. `unity test`, where
  available at all, executes the host project's entire suite — filter to
  `SombraStudios.Shared.*` assemblies.

## Git

- This is a standalone git repo consumed as a submodule; it has its own
  remote and history, separate from any host project.
- Do not commit or push unless I ask.
- Commit messages: short imperative subject, no trailing period.
- Since a submodule checkout is often a detached HEAD, check `git status`/
  current branch before committing here.
- Pushing to `main` triggers the DocFX deploy workflow, republishing the
  public docs site. Malformed XML doc comments surface there, not just in the
  Editor.

## Preferences

- Ask before large refactors that touch many files, and especially before
  anything that changes a module's public API (see above).
- If a request is ambiguous, ask one question rather than guessing broadly.
- Prefer the smallest change that solves the problem. Don't add abstraction
  layers, interfaces, or event buses unless I ask for them.

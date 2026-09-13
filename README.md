## Manuel F. Alonso — Game Developer

Game developer focused on mobile and VR/XR work.
I build reusable, well-documented systems rather than one-off scripts.

- 🔭 Currently: [Game Developer / Trick Studios] [Teaching Assistant (TA) / ORT Argentina]
- 💼 **Open to contract and freelance work** — Unity, mobile, VR/XR
- 🛠 Unity (C#), editor tools and utilities · XR Interaction Toolkit · native iOS (Obj-C) & Android (Kotlin) plugins · Firebase · GitHub Actions CI
- 📚 This repo doubles as my open Unity library — [API docs](https://manuelfalonso.github.io/manuelfalonso/)
- 📫 [LinkedIn](https://www.linkedin.com/in/manuel-f-alonso/) · [manuelalonso89@gmail.com](mailto:manuelalonso89@gmail.com) · [itch.io](https://manuelfalonso.itch.io/)

---

# Unity Scripts Repository
![Background 1280-720](https://github.com/user-attachments/assets/10a650d4-e674-4dea-9b3e-635640fcf29f)

Welcome to my collection of Unity scripts, a growing library designed to help Unity developers streamline their development process. This repository contains reusable, optimized, and production-ready scripts that focus on modularity and clean code practices.

## Table of Contents

0. [Bio](#manuel-f-alonso--game-developer)
1. [Finding a script](#finding-a-script)
2. [About Me](#about-me)
3. [Technologies Used](#technologies-used)
4. [Packages and Preprocessor Directives](#packages-and-preprocessor-directives)
5. [Repository Structure](#repository-structure)
6. [Getting Started](#getting-started)
7. [Regenerating the documentation](#regenerating-the-documentation)
8. [Key Features](#key-features)
   - [1. Prefab Instantiation Management with Scriptable Objects](#1-prefab-instantiation-management-with-scriptable-objects)
9. [Contributing](#contributing)

## Finding a script

There are 600+ scripts across 30 modules here, so start from the generated
index rather than browsing folders:

```bash
grep -i pool INDEX.tsv
```

[`INDEX.tsv`](INDEX.tsv) has one row per public type — `Type · Kind · Base ·
Namespace · Path · Gate · Summary` — regenerated from the sources and verified
in CI. The `Base` column tells you whether something is a `MonoBehaviour`, a
`ScriptableObject`, or a plain class before you open it, and `Gate` names the
define symbol it needs, if any.

Per-module guides are in [`Docs/modules/`](Docs/modules/README.md).
[`CONSUMERS.md`](CONSUMERS.md) covers adding the submodule to a project and
pointing an AI assistant at the index so it stops grepping the whole tree.

> The [published API site](https://manuelfalonso.github.io/manuelfalonso/) is
> built from committed DocFX metadata and can lag behind the sources. When the
> site and `INDEX.tsv` disagree, the index is right.

## About Me

I am a Unity Developer with a passion for creating scalable and reusable code. My experience spans across VR game development, mobile optimization, and implementing complex systems in Unity. This repository reflects my journey in game development and my desire to share useful tools with the Unity community.

## Technologies Used

- **Unity**: All scripts are developed using [Unity](https://unity.com/), leveraging both C# scripting and the Unity Engine.
- **Docfx**: Documentation for the repository is generated using [Docfx](https://dotnet.github.io/docfx/), providing detailed explanations and guides for each script. You can visit the [documentation site here](https://manuelfalonso.github.io/manuelfalonso/).
  
## Packages and Preprocessor Directives

This repository includes various Unity packages, each associated with preprocessor directives to ensure compatibility and efficient usage. Below is a list of the packages used and their related directives:

### Unity
| Package                       | Preprocessor Directive Symbol      | Documentation |
|-------------------------------|------------------------------------|-------------------------------|
| **Splines**                   | `#if UNITY_SPLINES`                | [Splines](https://docs.unity3d.com/Packages/com.unity.splines@2.4/manual/index.html) |
| **XR Interaction Toolkit**    | `#if UNITY_XR_INTERACTION_TOOLKIT` | [XR Interaction Toolkit](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.0/manual/index.html) |
| **Cinemachine**               | `#if CINEMACHINE`                  | [Cinemachine](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/index.html) |
| **Ads**                       | `#if UNITY_ADVERTISEMENTS`         | [Ads](https://docs.unity.com/ads/en-us/manual/UnityAdsHome) |
| **Netcode for GameObjects**   | `#if NETCODE_GAMEOBJECTS`          | [Netcode for GameObjects](https://docs-multiplayer.unity3d.com/netcode/current/about/) |

### External
| Package                       | Preprocessor Directive Symbol              | Documentation |
|-------------------------------|--------------------------------------------|-------------------------------|
| **Naughty Attributes**        | `#if NAUGHTY_ATTRIBUTES`                   | [Naughty Attributes](https://assetstore.unity.com/packages/p/naughtyattributes-129996) |
| **Serialized Dictionary**     | `#if A_YELLOWPAPER_SERIALIZED_COLLECTIONS` | [Serialized Dictionary](https://assetstore.unity.com/packages/tools/utilities/serialized-dictionary-243052) |
| **DOTween (HOTween v2)**      | `#if DOTWEEN`                              | [DOTween (HOTween v2)](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/index.html) |
| **Eflatun.SceneReference**    | `#if Eflatun_SceneReference`               | [Eflatun.SceneReference](https://github.com/starikcetin/Eflatun.SceneReference.git#4.0.0) |
| **Firebase**                  | `#if FIREBASE_APP`                         | [Firebase](https://firebase.google.com/docs/unity/setup?hl=en) |
| **Firebase Authentication**   | `#if FIREBASE_AUTH`                        | [Firebase Authentication](https://firebase.google.com/docs/auth/unity/start?hl=en) |
| **Facepunch Steam Transport** | `#if FACEPUNCH_TRANSPORT`                  | [Facepunch Transport for NGO](https://github.com/Unity-Technologies/multiplayer-community-contributions/tree/main/Transports/com.community.netcode.transport.facepunch) |

Make sure the appropriate symbols are defined in the **Scripting Define Symbols** in Unity's **Player Settings** when working with these packages:
Go to Edit > Project Settings > Player. Under **Scripting Define Symbols**, add a new symbol, for example, CINEMACHINE.

`NETCODE_GAMEOBJECTS` and `FACEPUNCH_TRANSPORT` are the exception: they are
declared as asmdef `versionDefines`, so Unity sets them automatically when the
package is present and clears them when it is not. Do not add them by hand.
See [Docs/manual/SteamworksSetup.md](Docs/manual/SteamworksSetup.md).

## Repository Structure

The repository is organized into the following main folders:

- **Docs/**: Includes all files necessary to generate the documentation site using Docfx.
- **/**: Contains all the Unity scripts organized by functionality.
   - **AI/**: Scripts for artificial intelligence systems and logic.
   - **Animations/**: Scripts related to animators and animation handling.
   - **Attributes/**: Scripts for custom editor attributes and property drawers.
   - **Audio/**: Scripts for audio systems, including sound effects, footsteps, and ambient sounds.
   - **Editor/**: Editor utility scripts, such as scriptable objects, search tools, and custom gizmos.
   - **Enums/**: General-purpose enumerations used across the project.
   - **Examples/**: Demonstration scripts for common patterns and delegates usage.
   - **Extensions/**: C# extension methods to enhance core functionalities.
   - **Gameplay/**: Scripts designed to improve or extend gameplay mechanics.
      - **Behaviours/**: Reusable behavior scripts for common gameplay actions.
      - **Drag/**: Mouse dragging mechanics scripts.
      - **Movement2D/**: Scripts for 2D movement and rotation.
      - **PlayerController/**: Scripts for handling character control and interactions.
      - **PlayerMovement2D/**: Scripts focused on 2D player movement mechanics.
      - **PlayerMovement3D/**: Scripts for handling 3D player movement.
      - **Spawners/**: Systems for spawning objects or entities in the game world.
   - **Inputs/**: 
   - **Interfaces/**: General-purpose interfaces for consistent API designs.
   - **Networking/**: Basic NetCode boilerplate scripts for networked functionality.
   - **Optimization/**: Scripts aimed at improving performance and resource management.
   - **Patters/**: Implementations of programming patterns, like Singleton or Factory.
   - **Physics/**: Scripts interacting with Unity's physics engine.
   - **Scenes/**: Scripts related to managing and transitioning between Unity scenes.
      - **Editor/**: Editor scene change tool
	  - **SceneAsset/**: Utilities for referencing and managing Unity scene assets at runtime.
	  - **Strategies/**: Scenes strategies encapsulated with a flexible and extendable architecture.
   - **ScriptableObjects/**: Scriptable object architecture scripts
      - **Conditions/**: Scriptable objects representing conditional evaluations.
	  - **Editor/**: Editor tools and custom inspectors for managing ScriptableObjects efficiently.
	  - **Patterns/**: ScriptableObject-based implementations of common design patterns.
	  - **RuntimeSets/**: Runtime collections of objects that update dynamically during gameplay.
	  - **Values/**: Generic value containers using ScriptableObjects for shared, observable data.
   - **Services/**: Scripts for third-party integrations or service management.
      - **Advertisement/**: Boilerplate scripts for handling Unity Ads integration.
      - **Firebase/**: Utility scripts for integrating Firebase services.
      - **Notifications/**: Scripts for managing Unity’s notification system.
      - **WebAPI/**: Scripts demonstrating web API consumption.
   - **Splines/**: Scripts related to Unity's Spline package functionalities.
   - **Structs/**: Generic serializable data containers designed for lightweight and efficient value types.
   - **Systems/**: Various game systems and utilities.
      - **CloseCaptions/**: Scripts for implementing closed captions in games.
	  - **Damage/**: Handles the application, calculation, and effects of damage in gameplay.
      - **GameState/**: Scripts to manage game state transitions and logic.
	  - **Heal/**: Encapsulates healing mechanics, including health restoration logic.
      - **LootBox/**: Systems for loot box generation and rewards.
	  - **Objectives/**: Defines goals, milestones, and mission progress tracking.
      - **Resource/**: Systems to manage resources like health, mana, etc.
      - **Stats/**: Systems for managing character stats or game variables.
      - **Teleport/**: Scripts handling teleportation mechanics.
      - **Tutorial/**: Scripts for managing in-game tutorials and guidance.
	  - **Unlock/**: 
   - **Tools/**: General-purpose tools for various tasks.
      - **Search/**: Provides runtime and editor search tools for filtering and locating assets and references.
   - **UI/**: Scripts for managing user interface elements.
      - **ImageDrag/**: Scripts to allow dragging of UI images.
      - **InventorySystem/**: Scripts for handling inventory systems.
      - **Mobile/**: UI scripts optimized for mobile devices.
      - **PanelDragAndResize/**: Scripts for dragging and resizing UI panels.
   - **Utility/**: General utility scripts that provide common functionality.
      - **Cooldown/**: Scripts to manage cooldown timers.
      - **Coroutines/**: Scripts to simplify coroutine management.
      - **Destroyer/**: Conditional object destruction logic.
      - **DontDestroy/**: Ensures persistence of GameObjects across scene transitions.
      - **Lerp/**: Linear interpolation utility scripts.
	  - **Logger/**: Simple and customizable logging system for debugging and tracing.
      - **FrameRate/**: Scripts to manage or display frame rates.
	  - **Mirror/**: Scripts to mirror the Transform on position, rotation or scale along a given axis.
      - **Mobile/**: Scripts for mobile-specific functionality, like touch inputs or keyboards.
	  - **NullReferenceChecker/**: Helps identify and report missing object references.
      - **PrefabInstantiateOnLoad/**: Automatically instantiate prefabs on scene load.
      - **Resources/**: Scripts to manage resource loading and references.
	  - **Sprites/**: Utility scripts and containers for managing sprite assignments and metadata.
      - **Timers/**: Timer-related scripts for time-based actions.
      - **TimeScale/**: Scripts for controlling time scaling in the game.
      - **UnityGizmos/**: Custom Gizmo drawing utilities.
      - **UnityMessages/**: Scripts to handle Unity event functions, like Awake() and Start().
   - **VFX/**: Scripts for managing visual effects.
      - **CameraShake/**: Scripts for implementing camera shake effects using DoTween.
      - **Parallax/**: Parallax scrolling effects for 2D or 3D.
      - **PropertySO/**: ScriptableObject-driven system for applying and reverting visual effects modularly.
   - **Video/**: Scripts related to video playback and controls.
   - **XR/**: Scripts related to XR (extended reality) interactions.
      - **Interactables/**: Scripts for XR interactable objects.
      - **KeyLockSystem/**: Systems for key-based locking mechanisms in XR environments.
      - **SocketInteractor/**: Scripts for handling socket interactions in XR setups.

## Getting Started

To get started with this repository:

1. Clone the repository to your local machine.
2. Make sure all necessary packages are installed and enabled in your Unity project.
3. Add the required scripting define symbols (mentioned above) in your Unity Player settings.
4. Review the [documentation](https://manuelfalonso.github.io/manuelfalonso/) for detailed usage instructions for each script.

## Regenerating the documentation

The published site is built from **committed** DocFX metadata: `docfx metadata`
extracts one `.yml` file per namespace and type into `Docs/obj/temp/api/`, those
files are committed, and the GitHub Actions workflow builds the HTML from them
on every push to `main`.

That folder cannot be gitignored. The metadata step reads the Unity-generated
`SombraStudios.Shared.*.csproj` files at the host project root, which do not
exist on the CI runner — so CI can only build from what is committed.

Run this after any change to a public type or its `<summary>` docs.

### Prerequisites

- **DocFX as a .NET global tool.** The standalone `docfx.exe` download fails
  with `No instances of MSBuild could be detected`: it is a self-contained
  binary, so `MSBuildLocator` has no `dotnet` SDK context to query and cannot
  fall back to a Visual Studio install.

  ```bash
  dotnet tool install -g docfx
  ```

- **The `.csproj` files must exist** at the host project root (the folder
  containing `Assets/`). Unity generates them and they are gitignored, so open
  the project in Unity once if they are missing.

### Steps

Run all of these from the repo root (the folder holding this `README.md`).

**1. Purge the old metadata.** DocFX overwrites and adds, but never deletes.
Because the folder is committed, the `.yml` of any type you deleted or renamed
survives forever and keeps rendering on the site. Emptying the folder first is
the only way those entries get removed:

```powershell
Remove-Item -Recurse -Force Docs\obj\temp\api
```

**2. Regenerate.** Use the `metadata` subcommand, not bare `docfx` — the latter
also runs the site build and writes a local `Docs/_site/` you do not need:

```bash
docfx metadata Docs/docfx.json
```

**3. Check the output for errors.** This step is not optional. On a compile
error DocFX prints `warning: No .NET API detected for .` and writes **zero**
`.yml` files. Before step 1 existed in this procedure that failure was
invisible — the committed folder stayed populated and the site rebuilt happily
from stale data. A successful run reports no errors and leaves ~790 `.yml`
files:

```bash
ls -1 Docs/obj/temp/api/*.yml | wc -l
```

**4. Review the diff.** `git status -- Docs/obj` should show deletions for
types that no longer exist, modifications for changed docs, and additions for
new types. Deletions are the point of step 1; a run with none of them means the
purge did not happen.

**5. Regenerate the type index** if you added, renamed, or removed a public
type, so `INDEX.tsv` matches. CI fails the push otherwise:

```bash
python .github/scripts/generate_index.py
```

**6. Commit and push to `main`.** This triggers `deploy-docfx.yml`, which
rebuilds and republishes the site from the metadata you just committed.

### Gotcha: `DefineConstants`

`Docs/docfx.json` sets `properties.DefineConstants`, and MSBuild treats it as a
global property that **replaces** each project's own define list rather than
adding to it. So a symbol listed there un-gates the matching `#if` blocks
during extraction — but the assembly references still come from the `.csproj`.

List a symbol whose assembly is not referenced there and every guarded file
fails with `CS0246: The type or namespace name '…' could not be found`, which
by step 3 means no documentation at all. This is what `NAUGHTY_ATTRIBUTES` did:
it was listed here, but the symbol is not in the project's Scripting Define
Symbols and NaughtyAttributes is an Asset Store import, so nothing referenced
it.

Only add a symbol here once its package is actually installed and referenced by
the relevant asmdef. Types gated behind a symbol left out of this list are
simply absent from the site — see the `Gate` column in
[`INDEX.tsv`](INDEX.tsv) for which those are.

## Key Features

This repository includes a variety of features designed to enhance Unity development. Below are some of the key features:

### 1. Prefab Instantiation Management with Scriptable Objects

- **Scriptable Object-Based Instantiation**: This system allows you to manage the instantiation of prefabs using ScriptableObjects, promoting clean, decoupled code.
- **`RuntimeInitializeOnLoadMethod` Integration**: Prefabs are instantiated at runtime using the `RuntimeInitializeOnLoadMethod` attribute, providing flexibility for initialization before or after scene loading.
- **Support for `RuntimeInitializeLoadType`**: Each instantiation can be configured to initialize at different points in the application lifecycle by managing the `RuntimeInitializeLoadType`, such as:
  - **BeforeSceneLoad**: Initialize prefabs before any scene is loaded.
  - **AfterSceneLoad**: Initialize prefabs after the first scene is loaded.
  - **AfterAssembliesLoaded**: Run instantiation after the assemblies are loaded.

#### When to Use It

- When you need to automatically instantiate essential objects (e.g., managers, global controllers) during game loading or when entering Play Mode, ensuring consistency across all scenes.
- For large projects where you want to centralize and simplify scene setup by ensuring base objects are always present without manual intervention.

#### When Not to Use It

- If the prefab needs to be instantiated on-demand based on user interactions (e.g., spawning enemies or items during gameplay).
- When prefabs need to be instantiated with specific runtime data that cannot be pre-configured in a ScriptableObject.

### 2. Strategy Pattern Implementation with ScriptableObjects

- **Generic Strategy Interface (`IStrategy<T>`)**: A reusable, type-safe interface that defines an `Execute(T context)` method, enabling flexible behavior definition across various systems.
- **ScriptableObject-Based Strategies**: Each strategy is implemented as a `ScriptableObject`, allowing you to create, reuse, and edit behaviors directly in the Unity Editor without modifying code.
- **`MultiStrategy` Composition**: Compose multiple strategies into a single `MultiStrategySO`, which executes each sub-strategy sequentially, enabling modular and scalable behavior trees or action stacks.
- **Plug-and-Play Architecture**: Integrate new behaviors at runtime or in the editor simply by creating new `StrategySO` assets and assigning them to MonoBehaviours or systems that consume `IStrategy<T>`.

#### When to Use It

- When designing AI, abilities, event responses, or any modular behavior that benefits from separation of concerns and reuse.
- When you want to empower designers or non-programmers to configure behaviors in the Unity Editor without writing code.
- In projects that require easily testable and swappable logic components following the open/closed principle.

#### When Not to Use It

- For performance-critical operations where `ScriptableObject` indirection might introduce unnecessary overhead.
- When behavior logic is tightly coupled to runtime data or state that cannot be serialized or injected into a stateless strategy context.

### 3. Enum Flags Utility Extensions

- **Bitwise Operation Helpers**: Provides clean, readable extension methods to work with `[Flags]` enums in C#, such as `HasFlagFast`, `AddFlag`, and `RemoveFlag`.
- **Improved Readability**: Eliminates verbose bitwise logic in your code, making conditionals involving enum flags much easier to read and maintain.
- **Safe and Generic**: Fully generic implementation that ensures type safety across all enum flag operations using constraints.

#### Example Usage

```csharp
[Flags]
public enum UnitState
{
    None = 0,
    Idle = 1 << 0,
    Moving = 1 << 1,
    Attacking = 1 << 2
}

// Combine flags
UnitState state = UnitState.Idle | UnitState.Moving;

// Check flag
bool isMoving = state.HasFlagFast(UnitState.Moving);

// Remove a flag
state = state.RemoveFlag(UnitState.Idle);

// Add a flag
state = state.AddFlag(UnitState.Attacking);
```

#### When to Use It

- When using [Flags] enums to represent combinations of states, permissions, toggles, or modifiers.
- In systems where flag manipulation is frequent and needs to remain concise and highly readable (e.g., state machines, ability systems, UI settings, etc).

#### When Not to Use It

- If you're not using [Flags] enums, or if your enum represents mutually exclusive values.
- In highly performance-critical sections where even the minimal overhead of an extension method might be a concern (though in most cases it's negligible).

### 4. Extensible ScriptableObject Observer Pattern (Generic Event System)

- **ScriptableObject-Based Event Dispatching**: A highly reusable observer pattern using `ScriptableObject` assets as event channels. Events are defined as generic base classes, supporting payloads or parameterless types.
- **Generic and Extensible**: The system is fully generic, enabling you to create type-safe event assets with payloads (e.g., `GameEvent<T>`) or without (e.g., `VoidEvent`). This makes it easy to scale across multiple event types without duplicating logic.
- **Runtime Listener Support**: Includes MonoBehaviour-based `EventListener<T>` and `VoidEventListener` scripts that register/unregister with the corresponding `ScriptableObject` events automatically during the object’s lifecycle.
- **Editor Tooling Included**: Custom editor drawers and inspectors show live subscribers at runtime, allowing quick debugging and better insight into event flow across your project.

#### When to Use It

- For decoupling game systems like UI, audio, input, and gameplay logic using event-driven architecture.
- In projects where designers or non-programmers need to connect logic via the Inspector without tight coupling.
- When you want a centralized, editor-visible, and testable alternative to UnityEvents or C# delegates.

#### When Not to Use It

- If your project doesn't benefit from decoupling or is too small to justify the abstraction.
- In performance-critical inner loops where event overhead could introduce latency (e.g., physics step logic).

### 5. Scene Management via ScriptableObject Strategies

- **Decoupled Scene Loading Logic**: This system replaces tightly coupled scene-loading code with flexible ScriptableObject strategies, enabling reusable and testable scene operations.
- **Strategy Pattern Architecture**: Each scene-related behavior (load, unload, reload, set active, etc.) is encapsulated within a dedicated `SceneStrategySO`, following the Strategy pattern. These behaviors are pluggable and composable at runtime.
- **Support for Multi-Strategy Composition**: Complex loading sequences can be created by composing multiple strategies via `MultiSceneStrategySO`, allowing for chainable and layered scene logic (e.g., load multiple scenes additively, then set one active).
- **Asset-Driven Design for Reusability**: Scene strategies are defined as assets, allowing designers or developers to reuse and reference them directly in other systems like gameplay logic, UI buttons, or startup sequences without code changes.
- **Lifecycle Awareness and Delayed Execution**: Strategies support integration with Unity’s coroutine system for asynchronous loading, and can be configured to delay, wait for conditions, or execute in order.

#### When to Use It

- In large or modular projects where scene loading behaviors need to be reused across multiple contexts (e.g., level management, transitions, UI-driven flows).
- When you want to enable designers to configure and tweak scene logic without code intervention.
- For tools, prototypes, or frameworks that require flexible, data-driven scene control.

#### When Not to Use It

- For simple or one-off scene transitions where the overhead of asset creation and modular logic isn’t justified.
- If you need tight control over scene loading lifecycles in performance-critical code, where monolithic logic might be more efficient.

### 6. Asset and Reference Search Tool (Editor-Only)

- **Advanced Search Utility for Assets and Components**: This custom Editor tool enables deep and flexible search operations across your Unity project, including assets, components in scenes, and serialized references.
- **Supports Asset and Scene Queries**: It can locate assets by type, name, and path, and also search scenes for components and fields that reference specific objects or types.
- **Reference Search Functionality**: Built-in tools allow you to trace serialized references to specific ScriptableObjects, prefabs, or MonoBehaviours—ideal for refactoring and debugging.
- **Modular Search Architecture**: The tool is organized into modular search strategies, allowing extension and customization to fit your studio or project’s asset workflow.
- **Context Menu Integration**: Many of the search utilities can be triggered via the Unity context menu, enabling fast one-click investigation from the Project or Hierarchy views.

#### When to Use It

- When cleaning up unused assets or determining the impact of removing a ScriptableObject or prefab.
- During refactors to track all references to a given type or instance.
- For debugging unexpected behaviors linked to misplaced or redundant references in scenes or prefabs.

#### When Not to Use It

- If you're looking for runtime search or tracking (this tool is strictly Editor-only).
- In very small projects where reference tracing can be managed manually.

### 7. Coroutine Management Utilities

- **Centralized Coroutine Handling via Static Class**: Provides a global, static entry point to start and stop coroutines without requiring a MonoBehaviour reference.
- **Persistent Coroutine Runner**: A hidden, automatically created `MonoBehaviour` instance is used internally to run coroutines safely from any context.
- **Safe Coroutine Lifecycle**: Ensures coroutines persist correctly through scene loads (if desired) and avoids common pitfalls with manually-managed runners or singletons.
- **Simplified API for Non-MonoBehaviour Scripts**: Greatly improves ergonomics when triggering coroutines from ScriptableObjects, static methods, or services without tight coupling to GameObjects.

#### When to Use It

- When you want to decouple coroutine execution from scene objects or lifetime-bound MonoBehaviours.
- In utility libraries, service locators, or ScriptableObject systems where coroutine support is required.

#### When Not to Use It

- In cases where you need more complex coroutine scheduling or time-slicing, such as a job system or task queue.

### 8. Lerp Utilities for Smooth Transitions

- **Generic Lerp Utility Functions**: Provides reusable linear interpolation (`Lerp`) and unclamped interpolation methods for various data types like `float`, `Vector2`, `Vector3`, and `Color`.
- **Ease of Use in Gameplay and UI Code**: Enables smooth transitions without the boilerplate, helping with animations, UI effects, camera movement, fading, or value blending.
- **Custom Extension Methods**: Includes intuitive and readable extension methods for applying interpolation directly on supported types, increasing code clarity.
- **Support for Fixed and Delta Time**: Offers overloads for both unscaled and delta time, ensuring smooth performance regardless of framerate or time scaling.

#### When to Use It

- When you need to implement smooth transitions over time for values, visuals, or game logic.
- For improving the readability and maintainability of any codebase with frequent interpolation needs.

#### When Not to Use It

- For highly complex animations or easing that require tweening libraries.
- If transitions depend on physics-based motion or require precise physical accuracy.

### 9. Unity Message Forwarding for Decoupled Callbacks

- **Unity Lifecycle Event Forwarders**: Contains MonoBehaviour scripts like `UnityInitializationMessagesHandler`, `UnityDecommissioningMessagesHandler`, `UnityOnMouseMessagesHandler`, `UnityPhysicsCollisionMessagesHandler`, and `UnityPhysicsTriggerMessagesHandler` that expose Unity’s internal lifecycle messages through public C# events.
- **UnityEvent-Based Architecture**: Each script defines one or more `UnityEvent` delegates that external components can subscribe to, enabling decoupled logic execution tied to specific Unity lifecycle events.
- **Minimal and Reusable**: Designed to be lightweight components that can be attached to any GameObject to broadcast Unity messages without subclassing MonoBehaviour.

#### When to Use It

- When you want to connect Unity lifecycle events (like `Awake` or `Update`) to external systems without inheriting or modifying MonoBehaviour scripts.
- For rapid prototyping where you need quick lifecycle event hooks in modular components.
- To trigger effects, behaviors, or custom logic in a decoupled and testable way.

#### When Not to Use It

- If your project architecture already favors centralized lifecycle handling (e.g., via managers or script execution order).
- When directly writing behavior inside MonoBehaviour methods is simpler and clearer.

### 10. VFX PropertySO Management with Scriptable Objects

- **Scriptable Object-Based VFX Control**: Utilize ScriptableObjects to manage and control various VFX elements like `BlendShapes`, `MaterialPropertyBlock`, and `Material` in Unity.
- **BlendShapes**: Control mesh deformations through blend shapes, allowing for dynamic and procedural visual effects.
- **MaterialPropertyBlock**: Use `MaterialPropertyBlock` to modify material properties at runtime, such as colors, textures, or shaders, without altering the original material.
- **Material Scripting API**: Interface with Unity’s Material API to manipulate and manage materials efficiently during gameplay.

#### When to Use It

- When you want to centralize and manage your VFX logic in reusable ScriptableObject assets.
- For large projects where VFX behaviors are complex and need to be managed across multiple objects or scenes without cluttering individual GameObjects.

#### When Not to Use It

- If VFX behaviors need to be very specific to a single object or require frequent, unique modifications based on gameplay.

## Contributing

If you would like to contribute to this repository, feel free to submit a pull request or open an issue. Any suggestions or improvements are welcome!

---

Thank you for visiting this repository. Happy coding!

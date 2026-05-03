# Axion Engine

A C# 3D game engine in the spirit of Unity — sleek, intuitive, scriptable in **three** interchangeable languages: **Gel**, **Silica**, and a **Scratch-style block** language. Open `Axion.sln` in Visual Studio 2022 and press **F5**.

> Status: **v0.1** — engine **foundation** with rendering, scene graph, ECS, scripting (3 languages + cross-conversion), physics, audio, animation, prefabs, and a runnable editor host. Rivalling Unity's full feature breadth is a long road; what's here is a real, professionally-architected base you can grow. See _Roadmap_ at the bottom for what's next.

---

## What's in the box

| Layer | What it does | Tech |
|---|---|---|
| **Axion.Core** | Scene graph, GameObject + Component, Transform hierarchy, Time, Input, Logging, Coroutines, Events, Asset DB, Prefabs, JSON scene save/load | pure C# / `System.Numerics` |
| **Axion.Scripting** | Three scripting languages (**Gel**, **Silica**, **Blocks**) with one shared AST so any language converts to any other; a Roslyn-based host that compiles user scripts to `.NET` assemblies at runtime | Roslyn |
| **Axion.Rendering** | OpenGL 3.3 renderer: window, shaders, meshes (cube/quad/sphere primitives), camera, textures, materials, directional lighting | OpenTK 4 + StbImageSharp |
| **Axion.Physics** | Rigid-body dynamics with box & sphere colliders, gravity, impulses | BepuPhysics 2 |
| **Axion.Engine** | Built-in components: `MeshRenderer`, `Camera`, `Light`, `Rigidbody`, `AudioSource`, `Animator`, `Script`. Audio via OpenAL, immediate-mode UI canvas, the `AxionApp` main loop | OpenTK.Audio |
| **Axion.Editor** | Cross-platform runner: loads any scene file, falls back to a default scene if none, dumps the scene tree on launch, free-fly camera fallback. CLI also includes a language converter (`--convert gel silica file.gel`). | OpenTK |

---

## Build & Run

**Requirements:** Visual Studio 2022 (17.8+) **or** the .NET 8 SDK.

```bash
# From a terminal:
cd axion-engine
dotnet build -c Release
dotnet run --project src/Axion.Editor                                   # default scene
dotnet run --project src/Axion.Editor -- samples/cube-scene.json        # load a scene
dotnet run --project src/Axion.Editor -- --convert gel silica samples/cube.gel
```

In Visual Studio: open `Axion.sln`, set **Axion.Editor** as startup project, press **F5**.

The first build downloads packages from NuGet (~30 MB).

---

## Three scripting languages, one engine

Every Axion script — regardless of source language — is parsed into a single shared AST and emitted as a `Behavior` subclass that the engine attaches to a `GameObject`. This means **the same logic in Gel, Silica, or Blocks compiles to identical IL** and runs at native .NET speed.

```
                      ┌──────────┐
   .gel  ──parse──┐   │  shared  │   ┌── Gel    ─emit─→ .gel
   .sil  ──parse──┼──▶│   AST    │──▶├── Silica ─emit─→ .sil
   .blocks ──────┘    │ProgramNode│   └── C#     ─emit─→ .cs (Roslyn)
                      └──────────┘
```

**Convert any source between languages:**

```bash
AxionEditor --convert gel    silica samples/cube.gel
AxionEditor --convert silica gel    samples/cube.sil
AxionEditor --convert gel    cs     samples/cube.gel
AxionEditor --convert blocks gel    samples/cube.blocks.json
```

### Lifecycle hooks (Unity-style)

In any of the three languages, define functions named:

| Function | When it's called |
|---|---|
| `start()` | Once per attached script, before the first frame. |
| `loop()`  | Every rendered frame. |
| _top-level statements_ | Treated as `start()` body when there's no explicit `start`. |

The engine automatically maps these to `Behavior.Start()` / `Behavior.Update()`.

### Built-in functions exposed to scripts

`print(x)`, `Time.DeltaTime`, `Time.ElapsedTime`, `Input.GetKey`, `Input.GetAxis`, `Vec.V(x,y,z)`, `Mathf.*`, `Log.Info/Warn/Error`, plus **everything in Axion.Core** is callable directly because scripts compile against the engine assembly.

---

## Sample scene

```bash
dotnet run --project src/Axion.Editor -- samples/cube-scene.json
```

This opens a window showing a sky-blue cube spinning over a dark plane, lit by a single directional light. The cube's `ScriptComponent` references `samples/cube.gel`. Edit `cube.gel`, restart, and the cube spins differently. Try replacing it with `cube.sil` or `cube.blocks.json` — same behavior, different language.

---

## Architecture quick reference

```
GameObject ─owns── Transform ─forms── scene hierarchy
            ├──── MeshRendererComponent  (Mesh + Material)
            ├──── CameraComponent        (Axion.Rendering.Camera)
            ├──── LightComponent
            ├──── RigidbodyComponent     ↔ BepuPhysics body
            ├──── AudioSourceComponent   ↔ OpenAL source
            ├──── AnimatorComponent      (keyframe Transform tween)
            └──── ScriptComponent        ↔ ScriptHost (Roslyn) ↔ user .gel/.sil/.blocks

         ┌──────────────────────────────┐
         │           AxionApp           │  ← main loop
         │ ┌──────┐ ┌──────┐ ┌────────┐ │
         │ │Scene │ │Renderer│ │Physics│ │
         │ └──────┘ └──────┘ └────────┘ │
         │ ┌──────┐ ┌──────┐ ┌────────┐ │
         │ │Audio │ │Input │ │Scripts │ │
         │ └──────┘ └──────┘ └────────┘ │
         └──────────────────────────────┘
```

---

## Roadmap

| Subsystem | v0.1 (today)              | v0.2 (next)                                 | Long-haul                       |
|---|---|---|---|
| Renderer  | Forward + dir light       | PBR, shadow maps, post-processing           | Deferred + clustered, Vulkan    |
| Editor    | Scene runner + tree dump  | ImGui-based hierarchy / inspector / viewport| Drag-drop assets, Play/Pause    |
| Physics   | Box + sphere, dynamics    | Capsule, mesh colliders, joints, raycasts   | Cloth, ragdoll                  |
| Animation | Transform keyframe tween  | Skeletal animation + blend trees            | State machines + IK             |
| Audio     | WAV via OpenAL            | OGG / MP3 streaming                         | Spatial mixer + DSP             |
| Scripting | Gel / Silica / Blocks     | Hot-reload, debugger attach                 | JIT-tracing optimisations       |
| UI        | Immediate-mode Canvas     | Retained UI elements + layout               | Built-in animations             |
| Assets    | Texture loader            | OBJ/glTF mesh loader, prefab variants       | Async streaming                 |

PRs welcome — the architecture is intentionally Unity-shaped to make migration straightforward.

---

## Companion: Silica Gel IDE

`/silicagel` is a separate Visual Studio solution containing **Silica Gel**, an Avalonia-based IDE built specifically for editing `.gel` and `.sil` files with syntax highlighting, completion, theming, and one-click language conversion. See `silicagel/README.md`.

---

© 2026 — engine architecture by you & Claude. Engine and editor source are MIT-licensed; you own the games you build with it.

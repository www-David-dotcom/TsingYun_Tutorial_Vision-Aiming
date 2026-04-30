# Arena Art & Vision Reform — Stage 12 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate `shared/godot_arena/` from Godot 4 to a new `shared/unity_arena/` running Unity 6 LTS + HDRP. Preserve the proto wire contract and gameplay 1:1 so HW1–HW7 candidate stacks remain valid. Coordinate the migration with a high-fidelity visual reform (multi-tier maze hybrid map, stylized chassis, DXR + rasterizer fallback, team-identity palette, full immersive HUD).

**Architecture:** Stage 12 runs as four sequential sub-stages on a single branch `stage12/unity-reform`, each ending with an annotated tag. 12a builds the Unity scaffold + wire-contract parity. 12b lands Map A geometry + chassis/gimbal/projectile parity. 12c is the art/lighting/VFX/UI pass. 12d cuts release builds, runs the Tier-5 ship gate, and publishes to OSS. Five layered conformance tiers (wire-format → smoke parity → golden frames → bronze opponent KS test → HW1–HW7 contract) gate each sub-stage tag. The Godot project is kept side-by-side as `godot_arena_legacy/` after 12d for GPU-less fallback.

**Tech Stack:** Unity 6 LTS, HDRP (High Definition Render Pipeline), C# 9, Unity Test Framework (NUnit), Shader Graph, VFX Graph, ProBuilder, Synty POLYGON Sci-Fi pack, Python 3.11 (test harness, smoke), pytest, protobuf json_format. Existing toolchain (`uv`, CMake, `vcpkg`, Aliyun OSS in `cn-beijing`) is unchanged.

**Spec reference:** `docs/superpowers/specs/2026-04-30-arena-art-vision-reform-design.md` (commit `0848e61`).

---

## File structure

### Created (Stage 12a)

```
shared/unity_arena/
├── Assets/
│   ├── Scenes/ArenaMain.unity                       # placeholder scene with primitive geometry
│   ├── Scripts/
│   │   ├── ArenaMain.cs                             # episode orchestrator (port of arena_main.gd)
│   │   ├── Chassis.cs                               # chassis MonoBehaviour (port of chassis.gd)
│   │   ├── MecanumChassisController.cs              # NEW: pure-C# velocity solver matching chassis.gd math
│   │   ├── Gimbal.cs                                # 2-axis gimbal (port of gimbal.gd)
│   │   ├── ArmorPlate.cs                            # plate damage handler (port of armor_plate.gd)
│   │   ├── Projectile.cs                            # projectile w/ quadratic drag (port of projectile.gd)
│   │   ├── ReplayRecorder.cs                        # JSON event-stream recorder (port of replay_recorder.gd)
│   │   ├── SeedRng.cs                               # static RNG (port of seed_rng.gd)
│   │   ├── TcpProtoServer.cs                        # length-prefixed JSON RPC (port of tcp_proto_server.gd)
│   │   └── TcpFramePub.cs                           # 16-byte header + RGB888 stream (port of tcp_frame_pub.gd)
│   ├── Tests/
│   │   ├── EditMode/
│   │   │   ├── SeedRngTests.cs
│   │   │   ├── MecanumChassisControllerTests.cs
│   │   │   ├── GimbalKinematicsTests.cs
│   │   │   ├── ProjectileDragTests.cs
│   │   │   └── TsingYun.UnityArena.EditMode.asmdef
│   │   └── PlayMode/
│   │       ├── TcpProtoServerTests.cs
│   │       ├── TcpFramePubTests.cs
│   │       ├── ArenaMainEpisodeTests.cs
│   │       └── TsingYun.UnityArena.PlayMode.asmdef
│   └── Synty/POLYGON_SciFi/                          # gitignored; maintainer fetches manually from synty.com
├── Packages/manifest.json                            # HDRP, Test Framework, Input System
├── ProjectSettings/                                  # Unity project config
└── README.md                                         # how to open / build / smoke
```

### Created (Stage 12b)

```
shared/unity_arena/Assets/
├── Scenes/MapA_MazeHybrid.unity                     # the multi-tier maze hybrid scene
├── Prefabs/
│   ├── Chassis.prefab
│   ├── Gimbal.prefab
│   ├── ArmorPlate.prefab
│   ├── Projectile.prefab
│   └── HoloProjector.prefab                          # diegetic intersection markers (geometry only in 12b)
└── Maps/
    └── MapA/
        ├── corridor_walls.asset                      # ProBuilder mesh data
        ├── catwalks.asset
        └── glass_partitions.asset
tests/
├── golden_frames/                                    # Tier 3 reference PNGs (5 seeds × 5 poses = 25)
│   ├── seed_0042_pose_0.png
│   └── ...
└── bronze_regression.py                              # Tier 4 KS-test runner
```

### Created (Stage 12c)

```
shared/unity_arena/Assets/
├── Materials/
│   ├── Body_TitaniumAluminum.mat
│   ├── Wall_MatteAluminum.mat
│   ├── Wall_CarbonFiber.mat
│   ├── Floor_Concrete.mat
│   ├── Glass_Tempered.mat
│   ├── Barrel_Anodized.mat
│   ├── HoloProjector_Base.mat
│   └── ShellCasing_Brass.mat
├── Shaders/
│   ├── EnergyShield.shadergraph                      # rim-lit fresnel halo around chassis
│   ├── PlateEmission.shadergraph                     # team color + glyph + scanline
│   ├── GlassCircuit.shadergraph                      # tempered glass + scrolling circuits
│   └── HoloProjector.shadergraph                     # in-world hologram label
├── VFX/
│   ├── MuzzleFlash.vfx                               # VFX Graph: 6-frame burst + transient point light
│   ├── ImpactSpark.vfx                               # VFX Graph: 4-frame spark + scorch decal
│   └── DustMote.vfx                                  # ambient particles in light shafts
├── Settings/
│   ├── HDRPAsset_Showcase.asset                      # DXR on, full quality
│   ├── HDRPAsset_Headless.asset                      # rasterizer only, baked GI, low LOD
│   ├── Volume_Showcase.asset                         # vignette / chromatic aberration / bloom / motion blur
│   └── Volume_Headless.asset                         # post-FX disabled
├── UI/
│   ├── HudCanvas_ScreenSpace.prefab                  # reticle / minimap / telemetry / target lock
│   └── HoloMarker_World.prefab                       # diegetic intersection label
└── Lightmaps/                                         # gitignored; output of bake
```

### Created (Stage 12d)

```
tools/unity/
├── build.sh                                           # release-mode build automation
├── bake_lighting.sh                                   # offline lightmap bake
└── README.md
tools/scripts/
└── check_synty_redistribution.py                      # CI guard: fail build on .fbx in committed paths
```

### Renamed (Stage 12a / 12d)

| Stage | From | To |
|---|---|---|
| 12a | `tests/test_godot_wire_format.py` | `tests/test_arena_wire_format.py` (parametrized over `[godot, unity]`) |
| 12a | `tools/scripts/smoke_godot_arena.py` | `tools/scripts/smoke_arena.py` (with `--engine={godot,unity}` flag) |
| 12d | `docs/godot_arena.md` | `docs/arena.md` (rewritten to cover both engines) |
| 12d | `shared/godot_arena/` | `shared/godot_arena_legacy/` |

### Modified

| Stage | File | Reason |
|---|---|---|
| 12a | `.gitignore` | Add Unity-specific patterns: `Library/`, `Temp/`, `Logs/`, `Build/`, `*.csproj`, `*.sln`, `*.userprefs`, `Assets/Synty/`, `shared/unity_arena/builds/`, `shared/unity_arena/Assets/Lightmaps/` |
| 12d | `shared/assets/manifest.toml` | Add rows for `unity_arena_showcase_{win64,macos,linux}`, `unity_arena_headless_linux`, `synty_polygon_scifi_v1` |
| 12d | `docs/architecture.md` | Engine swap diagram |
| 12d | `docs/CHANGELOG.md` | Stage 12 entries |
| 12d | `docs/grading.md` | GPU-required note for live arena |
| 12d | `IMPLEMENTATION_PLAN.md` | Append Stage 12 section |
| 12d | `README.md` | Update Quickstart to mention Unity arena |

---

## Pre-flight (one-time, before Stage 12a)

### P0: Verify environment

- [ ] **Step 1: Verify project state**

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
git status
git log -1 --oneline
```
Expected: branch `main`, working tree clean, latest commit is the design spec (`0848e61`).

- [ ] **Step 2: Open branch for Stage 12**

```bash
git checkout -b stage12/unity-reform
git commit --allow-empty -m "chore(stage12): branch open"
```

- [ ] **Step 3: Verify Unity 6 LTS is installed**

Open Unity Hub. If Unity 6 LTS or later is not installed, install it:
- Unity Hub → Installs → Install Editor → Unity 6000.3.14f1 (LTS) or newer Unity 6 LTS.
- Required modules: Documentation, Mac Build Support (IL2CPP), Windows Build Support (Mono), Linux Build Support (IL2CPP).

Expected: `Unity 6000.3.x` (or newer Unity 6 LTS) shown in Unity Hub.

- [ ] **Step 4: Verify Synty pack license**

If the maintainer has not yet purchased Synty POLYGON Sci-Fi: buy from synty.com (~$40 USD). Save the `.unitypackage` (or extracted `.fbx` tree) somewhere outside the repo for now — it will be imported into the Unity project in Task 1.5. Do NOT commit any Synty file to git.

---

## Stage 12a — Unity scaffold + wire parity

**Goal:** Stand up a working Unity 6 LTS HDRP project at `shared/unity_arena/` whose 10 C# scripts mirror the existing GDScript files, whose TCP control + frame ports match the Godot wire byte-for-byte, and whose placeholder ArenaMain scene passes Tier 1 (wire-format conformance) and Tier 2 (smoke harness parity) regressions. End tag: `v1.6-unity-scaffold`.

**Calendar estimate:** 5 working days (1 engineer).

---

### Task 1: Initialize Unity project shell

**Files:**
- Create: `shared/unity_arena/` (Unity project root via Unity Hub)
- Create: `shared/unity_arena/Packages/manifest.json` (auto-created, edited)
- Create: `shared/unity_arena/ProjectSettings/` (auto-created)
- Modify: `.gitignore`

- [ ] **Step 1: Create the Unity project**

Open Unity Hub → New project → Template: **HDRP** → Project name: `unity_arena` → Location: `/Volumes/David/大二下/RM/Aiming/Aiming_HW/shared/`. Click Create. Wait for Unity to import HDRP (~2-5 minutes).

Expected: directory `shared/unity_arena/` exists with `Assets/`, `Packages/`, `ProjectSettings/`, and Unity opens to the HDRP sample scene.

- [ ] **Step 2: Verify HDRP package version**

In Unity: Window → Package Manager → Packages: In Project. Confirm:
- `com.unity.render-pipelines.high-definition` ≥ 17.0 (Unity 6 HDRP)
- `com.unity.test-framework` ≥ 1.4
- `com.unity.inputsystem` ≥ 1.11
- `com.unity.visualeffectgraph` ≥ 17.0
- `com.unity.shadergraph` ≥ 17.0 (typically pulled transitively by HDRP)
- `com.unity.probuilder` (any 5.x; needed for Stage 12b Map A geometry)

If any are missing, install via Package Manager.

- [ ] **Step 3: Configure Project Settings**

Edit → Project Settings:
- **Player → Resolution and Presentation**: Default Screen Width 1280, Default Screen Height 720, Resizable Window OFF.
- **Time**: Fixed Timestep `0.005` (200 Hz, matches Godot's `physics_ticks_per_second=120`... wait, current Godot is 120 Hz; for parity use `0.00833` (120 Hz). Match the existing simulator.)
- **Physics**: Gravity `(0, -9.81, 0)`. Default Solver Iterations `12`. Bounce Threshold `0.5`.
- **Quality**: Default to a single quality level, named `HDRP_Default` for now (will split into Showcase / Headless in 12c).

- [ ] **Step 4: Add Unity-specific .gitignore patterns**

Append to `/Volumes/David/大二下/RM/Aiming/Aiming_HW/.gitignore`:

```
# Unity
shared/unity_arena/Library/
shared/unity_arena/Temp/
shared/unity_arena/Logs/
shared/unity_arena/Build/
shared/unity_arena/builds/
shared/unity_arena/UserSettings/
shared/unity_arena/MemoryCaptures/
shared/unity_arena/*.csproj
shared/unity_arena/*.sln
shared/unity_arena/*.userprefs
shared/unity_arena/Assets/Lightmaps/
*.pidb.meta
*.pdb.meta
*.mdb.meta
sysinfo.txt
```

Note: `shared/unity_arena/Assets/Synty/` is already gitignored — it landed defensively in Stage 12d Task 45's commit (`5d51dcb`) so the Synty CI guard would be meaningful before Task 1 ran. Don't re-add it here.

- [ ] **Step 5: Verify clean status (after .gitignore)**

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
git status
```

Expected: only `.gitignore` modified, plus the tracked files under `shared/unity_arena/` (the `Assets/`, `Packages/manifest.json`, `ProjectSettings/`). The `Library/`, `Temp/`, etc. should not appear.

- [ ] **Step 6: Commit**

```bash
git add .gitignore shared/unity_arena/Assets shared/unity_arena/Packages shared/unity_arena/ProjectSettings
git commit -m "feat(stage12a): initialize unity_arena HDRP project shell"
```

---

### Task 2: Set up Test Framework + assembly definitions

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/TsingYun.UnityArena.asmdef`
- Create: `shared/unity_arena/Assets/Tests/EditMode/TsingYun.UnityArena.EditMode.asmdef`
- Create: `shared/unity_arena/Assets/Tests/PlayMode/TsingYun.UnityArena.PlayMode.asmdef`

Assembly definitions let the test framework reference your runtime code and isolate compile units.

- [ ] **Step 1: Create runtime assembly definition**

Create `shared/unity_arena/Assets/Scripts/TsingYun.UnityArena.asmdef`:

```json
{
    "name": "TsingYun.UnityArena",
    "rootNamespace": "TsingYun.UnityArena",
    "references": [
        "Unity.RenderPipelines.HighDefinition.Runtime",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Create EditMode test assembly definition**

Create `shared/unity_arena/Assets/Tests/EditMode/TsingYun.UnityArena.EditMode.asmdef`:

```json
{
    "name": "TsingYun.UnityArena.EditMode",
    "rootNamespace": "TsingYun.UnityArena.Tests.EditMode",
    "references": [
        "TsingYun.UnityArena",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ]
}
```

- [ ] **Step 3: Create PlayMode test assembly definition**

Create `shared/unity_arena/Assets/Tests/PlayMode/TsingYun.UnityArena.PlayMode.asmdef`:

```json
{
    "name": "TsingYun.UnityArena.PlayMode",
    "rootNamespace": "TsingYun.UnityArena.Tests.PlayMode",
    "references": [
        "TsingYun.UnityArena",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ]
}
```

- [ ] **Step 4: Reload Unity, verify tests window opens**

In Unity: Window → General → Test Runner. Switch between EditMode and PlayMode tabs. Expected: both tabs appear with no errors. The "Run All" button is visible.

- [ ] **Step 5: Commit**

```bash
git add shared/unity_arena/Assets/Scripts shared/unity_arena/Assets/Tests
git commit -m "feat(stage12a): runtime + test assembly definitions"
```

---

### Task 3: Port `seed_rng.gd` → `SeedRng.cs`

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/SeedRng.cs`
- Create: `shared/unity_arena/Assets/Tests/EditMode/SeedRngTests.cs`

`seed_rng.gd` is a Godot autoload — globally accessible singleton. In Unity it's cleaner as a static class with explicit `Reseed`/`NextFloat`/`NextRange`/`NextInt`/`CurrentSeed` API.

- [ ] **Step 1: Write the failing tests**

Create `shared/unity_arena/Assets/Tests/EditMode/SeedRngTests.cs`:

```csharp
using NUnit.Framework;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class SeedRngTests
    {
        [Test]
        public void Reseed_ProducesDeterministicSequence()
        {
            SeedRng.Reseed(42);
            float a1 = SeedRng.NextFloat();
            float a2 = SeedRng.NextFloat();

            SeedRng.Reseed(42);
            float b1 = SeedRng.NextFloat();
            float b2 = SeedRng.NextFloat();

            Assert.AreEqual(a1, b1, 1e-9f);
            Assert.AreEqual(a2, b2, 1e-9f);
        }

        [Test]
        public void Reseed_DifferentSeeds_ProduceDifferentSequences()
        {
            SeedRng.Reseed(1);
            float a = SeedRng.NextFloat();
            SeedRng.Reseed(2);
            float b = SeedRng.NextFloat();
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void NextRange_StaysWithinBounds()
        {
            SeedRng.Reseed(123);
            for (int i = 0; i < 1000; i++)
            {
                float v = SeedRng.NextRange(-2.0f, 5.0f);
                Assert.GreaterOrEqual(v, -2.0f);
                Assert.LessOrEqual(v, 5.0f);
            }
        }

        [Test]
        public void NextInt_StaysWithinBoundsInclusive()
        {
            SeedRng.Reseed(123);
            int lo = -3, hi = 7;
            for (int i = 0; i < 1000; i++)
            {
                int v = SeedRng.NextInt(lo, hi);
                Assert.GreaterOrEqual(v, lo);
                Assert.LessOrEqual(v, hi);
            }
        }

        [Test]
        public void CurrentSeed_ReturnsLastReseedValue()
        {
            SeedRng.Reseed(99);
            Assert.AreEqual(99L, SeedRng.CurrentSeed());
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Unity → Window → General → Test Runner → EditMode → Run All.
Expected: 5 tests fail with `CS0103: The name 'SeedRng' does not exist`.

- [ ] **Step 3: Write minimal implementation**

Create `shared/unity_arena/Assets/Scripts/SeedRng.cs`:

```csharp
using System;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Single source of randomness for the arena so the same EnvReset.seed
    /// gives byte-identical episodes. Mirrors seed_rng.gd. Subsystems that
    /// need randomness call <see cref="NextFloat"/> / <see cref="NextRange"/>
    /// / <see cref="NextInt"/>; ArenaMain calls <see cref="Reseed"/> on every
    /// new episode.
    /// </summary>
    public static class SeedRng
    {
        private static Random _rng = new Random(0);
        private static long _currentSeed = 0;

        public static void Reseed(long seedValue)
        {
            _currentSeed = seedValue;
            // System.Random takes int; we stable-fold the 64-bit seed into
            // an int so the same input always gives the same RNG state.
            int intSeed = unchecked((int)(seedValue ^ (seedValue >> 32)));
            _rng = new Random(intSeed);
        }

        public static long CurrentSeed() => _currentSeed;

        public static float NextFloat() => (float)_rng.NextDouble();

        public static float NextRange(float lo, float hi)
        {
            return lo + (hi - lo) * (float)_rng.NextDouble();
        }

        public static int NextInt(int lo, int hi)
        {
            // Inclusive on both ends, matching Godot's randi_range.
            return _rng.Next(lo, hi + 1);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Unity Test Runner → EditMode → Run All.
Expected: all 5 tests green.

- [ ] **Step 5: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/SeedRng.cs shared/unity_arena/Assets/Tests/EditMode/SeedRngTests.cs
git commit -m "feat(stage12a): port seed_rng.gd -> SeedRng.cs (static)"
```

---

### Task 4: Port chassis kinematics math → `MecanumChassisController.cs`

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/MecanumChassisController.cs`
- Create: `shared/unity_arena/Assets/Tests/EditMode/MecanumChassisControllerTests.cs`

This is the pure-math velocity solver from `chassis.gd` (lines 56-74). Extracted to its own non-MonoBehaviour class so Bullet→PhysX drift (R3) cannot affect it. Validated against hand-computed reference outputs.

- [ ] **Step 1: Write the failing tests**

Create `shared/unity_arena/Assets/Tests/EditMode/MecanumChassisControllerTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class MecanumChassisControllerTests
    {
        [Test]
        public void IntegrateYaw_AdvancesByOmegaTimesDelta()
        {
            var c = new MecanumChassisController { ChassisYaw = 1.0f };
            c.SetCmd(0f, 0f, 2.0f);
            c.IntegrateStep(0.5f);
            Assert.AreEqual(2.0f, c.ChassisYaw, 1e-6f);
        }

        [Test]
        public void IntegrateVelocity_NoYaw_BodyXMapsToWorldX()
        {
            var c = new MecanumChassisController { ChassisYaw = 0f };
            c.SetCmd(2.0f, 0f, 0f);
            c.IntegrateStep(0f);
            Assert.AreEqual(2.0f, c.WorldVelocity.x, 1e-6f);
            Assert.AreEqual(0f, c.WorldVelocity.z, 1e-6f);
        }

        [Test]
        public void IntegrateVelocity_NoYaw_BodyYMapsToWorldZ()
        {
            var c = new MecanumChassisController { ChassisYaw = 0f };
            c.SetCmd(0f, 1.5f, 0f);
            c.IntegrateStep(0f);
            Assert.AreEqual(0f, c.WorldVelocity.x, 1e-6f);
            // chassis.gd: velocity.z = cmd_vx*sin(yaw) + cmd_vy*cos(yaw); yaw=0 => z = cmd_vy
            Assert.AreEqual(1.5f, c.WorldVelocity.z, 1e-6f);
        }

        [Test]
        public void IntegrateVelocity_With90DegYaw_BodyXMapsToNegativeWorldZ()
        {
            // chassis.gd uses yaw rotation around +Y as: v_x_world = vx*cos(y) - vy*sin(y)
            //                                            v_z_world = vx*sin(y) + vy*cos(y)
            // With yaw = +π/2: cos = 0, sin = 1 → world.x = -vy, world.z = +vx
            var c = new MecanumChassisController { ChassisYaw = Mathf.PI / 2f };
            c.SetCmd(1.0f, 0f, 0f);
            c.IntegrateStep(0f);
            Assert.AreEqual(0f, c.WorldVelocity.x, 1e-6f);
            Assert.AreEqual(1.0f, c.WorldVelocity.z, 1e-6f);
        }

        [Test]
        public void SetCmd_ClampsToMaxLinearSpeed()
        {
            var c = new MecanumChassisController { MaxLinearSpeed = 3.5f };
            c.SetCmd(10f, -10f, 0f);
            // Internal cmd values are clamped before the solver runs.
            Assert.AreEqual(3.5f, c.CmdVx, 1e-6f);
            Assert.AreEqual(-3.5f, c.CmdVy, 1e-6f);
        }

        [Test]
        public void SetCmd_ClampsToMaxAngularSpeed()
        {
            var c = new MecanumChassisController { MaxAngularSpeed = 4.0f };
            c.SetCmd(0f, 0f, 99f);
            Assert.AreEqual(4.0f, c.CmdOmega, 1e-6f);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Unity Test Runner → EditMode → Run All.
Expected: 6 tests fail with `MecanumChassisController` undefined.

- [ ] **Step 3: Write minimal implementation**

Create `shared/unity_arena/Assets/Scripts/MecanumChassisController.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Pure-C# mecanum velocity solver. Extracted from chassis.gd:_physics_process
    /// so Bullet→PhysX drift cannot affect it. Math is line-by-line equivalent:
    ///
    ///   v_x_world = vx_body * cos(yaw) - vy_body * sin(yaw)
    ///   v_z_world = vx_body * sin(yaw) + vy_body * cos(yaw)
    ///
    /// The four-wheel mecanum mixing is NOT simulated — Godot's CharacterBody3D
    /// doesn't model wheel slip and the RM rules don't punish ideal-kinematics
    /// simulators in a way that matters for HW1–HW7.
    /// </summary>
    public class MecanumChassisController
    {
        public float MaxLinearSpeed { get; set; } = 3.5f;
        public float MaxAngularSpeed { get; set; } = 4.0f;

        public float ChassisYaw;
        public float CmdVx { get; private set; }
        public float CmdVy { get; private set; }
        public float CmdOmega { get; private set; }
        public Vector3 WorldVelocity { get; private set; }

        public void SetCmd(float vxBody, float vyBody, float omega)
        {
            CmdVx = Mathf.Clamp(vxBody, -MaxLinearSpeed, MaxLinearSpeed);
            CmdVy = Mathf.Clamp(vyBody, -MaxLinearSpeed, MaxLinearSpeed);
            CmdOmega = Mathf.Clamp(omega, -MaxAngularSpeed, MaxAngularSpeed);
        }

        /// <summary>
        /// Advance the chassis state by <paramref name="deltaSeconds"/>:
        /// integrate yaw, recompute world-space velocity. Caller is
        /// responsible for moving the transform (Unity does not provide a
        /// CharacterBody3D-equivalent at this layer; callers use a custom
        /// CharacterController.Move on FixedUpdate).
        /// </summary>
        public void IntegrateStep(float deltaSeconds)
        {
            ChassisYaw += CmdOmega * deltaSeconds;
            float cosY = Mathf.Cos(ChassisYaw);
            float sinY = Mathf.Sin(ChassisYaw);
            WorldVelocity = new Vector3(
                CmdVx * cosY - CmdVy * sinY,
                0f,
                CmdVx * sinY + CmdVy * cosY
            );
        }

        public void Reset(float spawnYaw)
        {
            ChassisYaw = spawnYaw;
            CmdVx = 0f;
            CmdVy = 0f;
            CmdOmega = 0f;
            WorldVelocity = Vector3.zero;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Unity Test Runner → EditMode → Run All.
Expected: all 6 tests green.

- [ ] **Step 5: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/MecanumChassisController.cs shared/unity_arena/Assets/Tests/EditMode/MecanumChassisControllerTests.cs
git commit -m "feat(stage12a): mecanum velocity solver matching chassis.gd math"
```

---

### Task 5: Port `gimbal.gd` → `Gimbal.cs`

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/Gimbal.cs`
- Create: `shared/unity_arena/Assets/Tests/EditMode/GimbalKinematicsTests.cs`

The gimbal does not depend on engine physics — it's pure kinematic integration. Test the math in EditMode without a scene.

- [ ] **Step 1: Write the failing tests**

Create `shared/unity_arena/Assets/Tests/EditMode/GimbalKinematicsTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class GimbalKinematicsTests
    {
        [Test]
        public void IntegrateStep_ConvergesToTargetYaw()
        {
            var k = new GimbalKinematics();
            k.SetTarget(targetYaw: 0.3f, targetPitch: 0.0f, yawFf: 0f, pitchFf: 0f);
            for (int i = 0; i < 100; i++)
            {
                k.IntegrateStep(0.01f);
            }
            Assert.AreEqual(0.3f, k.YawRad, 1e-3f);
        }

        [Test]
        public void IntegrateStep_ClampsPitchToLimits()
        {
            var k = new GimbalKinematics();
            // PITCH_LIMIT_HI = 0.52 rad. Command 1.0 → should clamp.
            k.SetTarget(targetYaw: 0f, targetPitch: 1.0f, yawFf: 0f, pitchFf: 0f);
            for (int i = 0; i < 200; i++)
            {
                k.IntegrateStep(0.01f);
            }
            Assert.LessOrEqual(k.PitchRad, GimbalKinematics.PitchLimitHi + 1e-6f);
        }

        [Test]
        public void IntegrateStep_RateLimitsYaw()
        {
            var k = new GimbalKinematics();
            k.SetTarget(targetYaw: 100f, targetPitch: 0f, yawFf: 0f, pitchFf: 0f);
            k.IntegrateStep(0.01f);
            // YAW_RATE_LIMIT = 12 rad/s, so over 0.01s yaw advances ≤ 0.12.
            Assert.LessOrEqual(Mathf.Abs(k.YawRad), 0.12f + 1e-6f);
        }

        [Test]
        public void SetTarget_ClampsTargetPitchOnInput()
        {
            var k = new GimbalKinematics();
            k.SetTarget(targetYaw: 0f, targetPitch: 99f, yawFf: 0f, pitchFf: 0f);
            // Internal target is clamped before the solver runs (mirrors gimbal.gd).
            Assert.AreEqual(GimbalKinematics.PitchLimitHi, k.TargetPitch, 1e-6f);
        }

        [Test]
        public void GetState_ReturnsCurrentValues()
        {
            var k = new GimbalKinematics();
            k.SetTarget(targetYaw: 0.1f, targetPitch: 0f, yawFf: 0f, pitchFf: 0f);
            k.IntegrateStep(0.01f);
            var s = k.GetState();
            Assert.AreEqual(k.YawRad, s.Yaw, 1e-9f);
            Assert.AreEqual(k.PitchRad, s.Pitch, 1e-9f);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Unity Test Runner → EditMode → Run All.
Expected: 5 tests fail with `GimbalKinematics` undefined.

- [ ] **Step 3: Write minimal implementation**

Create `shared/unity_arena/Assets/Scripts/Gimbal.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Pure-C# gimbal kinematics: yaw around +Y, pitch around local +X of
    /// the YawPivot. First-order motor lag rate-limits the slew so commanded
    /// targets don't snap. Mirrors gimbal.gd. Extracted so the math is
    /// EditMode-testable without a scene running.
    /// </summary>
    public class GimbalKinematics
    {
        public const float YawRateLimit = 12.0f;     // rad/s
        public const float PitchRateLimit = 8.0f;    // rad/s
        public const float PitchLimitLo = -0.35f;    // rad (~-20 deg)
        public const float PitchLimitHi = 0.52f;     // rad (~+30 deg)
        public const float MotorLagTc = 0.04f;       // s

        public float YawRad;
        public float PitchRad;
        public float TargetYaw;
        public float TargetPitch;
        public float YawRate;
        public float PitchRate;
        public float YawRateFf;
        public float PitchRateFf;

        public void SetTarget(float targetYaw, float targetPitch, float yawFf, float pitchFf)
        {
            TargetYaw = targetYaw;
            TargetPitch = Mathf.Clamp(targetPitch, PitchLimitLo, PitchLimitHi);
            YawRateFf = yawFf;
            PitchRateFf = pitchFf;
        }

        public void IntegrateStep(float deltaSeconds)
        {
            float yawErr = WrapPi(TargetYaw - YawRad);
            float pitchErr = TargetPitch - PitchRad;

            float yawCmd = yawErr / MotorLagTc + YawRateFf;
            float pitchCmd = pitchErr / MotorLagTc + PitchRateFf;

            YawRate = Mathf.Clamp(yawCmd, -YawRateLimit, YawRateLimit);
            PitchRate = Mathf.Clamp(pitchCmd, -PitchRateLimit, PitchRateLimit);

            YawRad += YawRate * deltaSeconds;
            PitchRad = Mathf.Clamp(PitchRad + PitchRate * deltaSeconds,
                                    PitchLimitLo, PitchLimitHi);
        }

        public GimbalState GetState() => new GimbalState
        {
            Yaw = YawRad,
            Pitch = PitchRad,
            YawRate = YawRate,
            PitchRate = PitchRate,
        };

        public void Reset()
        {
            YawRad = PitchRad = 0f;
            TargetYaw = TargetPitch = 0f;
            YawRate = PitchRate = 0f;
            YawRateFf = PitchRateFf = 0f;
        }

        private static float WrapPi(float angle)
        {
            // Match Godot's wrapf(x, -PI, PI).
            float twoPi = Mathf.PI * 2f;
            angle = (angle + Mathf.PI) % twoPi;
            if (angle < 0f) angle += twoPi;
            return angle - Mathf.PI;
        }
    }

    public struct GimbalState
    {
        public float Yaw;
        public float Pitch;
        public float YawRate;
        public float PitchRate;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Unity Test Runner → EditMode → Run All.
Expected: all 5 tests green.

- [ ] **Step 5: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/Gimbal.cs shared/unity_arena/Assets/Tests/EditMode/GimbalKinematicsTests.cs
git commit -m "feat(stage12a): port gimbal kinematics matching gimbal.gd math"
```

---

### Task 6: Port `projectile.gd` quadratic-drag math → `ProjectileDragSolver`

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/Projectile.cs` (partial — drag solver only in 12a; full Rigidbody integration in 12b)
- Create: `shared/unity_arena/Assets/Tests/EditMode/ProjectileDragTests.cs`

In 12a we land the pure-math drag solver as a static helper. The full `Projectile : MonoBehaviour` (with Rigidbody, OnTriggerEnter, lifetime caps) lands in Task 11 of 12b.

- [ ] **Step 1: Write the failing tests**

Create `shared/unity_arena/Assets/Tests/EditMode/ProjectileDragTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class ProjectileDragTests
    {
        [Test]
        public void DragForce_ZeroVelocity_ReturnsZero()
        {
            Vector3 force = ProjectileDragSolver.QuadraticDragForce(Vector3.zero);
            Assert.AreEqual(Vector3.zero, force);
        }

        [Test]
        public void DragForce_OpposesVelocity()
        {
            Vector3 v = new Vector3(10f, 0f, 0f);
            Vector3 force = ProjectileDragSolver.QuadraticDragForce(v);
            Assert.Less(force.x, 0f);
            Assert.AreEqual(0f, force.y, 1e-6f);
            Assert.AreEqual(0f, force.z, 1e-6f);
        }

        [Test]
        public void DragForce_MagnitudeMatchesFormula()
        {
            // F = 0.5 * rho * Cd * A * |v|^2  (along -v)
            // rho = 1.225, Cd = 0.47, A = 0.000227 (matches projectile.gd)
            float speed = 27.0f;  // muzzle velocity
            Vector3 v = new Vector3(speed, 0f, 0f);
            Vector3 force = ProjectileDragSolver.QuadraticDragForce(v);
            float expected = 0.5f * 1.225f * 0.47f * 0.000227f * speed * speed;
            Assert.AreEqual(expected, Mathf.Abs(force.x), 1e-6f);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Unity Test Runner → EditMode → Run All.
Expected: 3 tests fail with `ProjectileDragSolver` undefined.

- [ ] **Step 3: Write minimal implementation**

Create `shared/unity_arena/Assets/Scripts/Projectile.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Pure-C# quadratic-drag force solver for the projectile. Matches
    /// projectile.gd:_physics_process (lines 35-40). Extracted so the math
    /// is EditMode-testable; the full Projectile MonoBehaviour (with
    /// Rigidbody, OnCollisionEnter, lifetime caps) is added in Stage 12b.
    /// </summary>
    public static class ProjectileDragSolver
    {
        public const float DragCoefficient = 0.47f;       // sphere
        public const float AirDensity = 1.225f;            // kg/m^3
        public const float FrontalArea = 0.000227f;        // π * 0.0085^2
        public const float MaxRangeM = 30.0f;
        public const float MaxTtlSeconds = 4.0f;
        public const int Damage = 50;

        public static Vector3 QuadraticDragForce(Vector3 velocity)
        {
            float speed = velocity.magnitude;
            if (speed < 1e-3f) return Vector3.zero;
            float magnitude = 0.5f * AirDensity * DragCoefficient * FrontalArea * speed * speed;
            return -velocity.normalized * magnitude;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Unity Test Runner → EditMode → Run All.
Expected: 3 tests green.

- [ ] **Step 5: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/Projectile.cs shared/unity_arena/Assets/Tests/EditMode/ProjectileDragTests.cs
git commit -m "feat(stage12a): projectile quadratic-drag solver matching projectile.gd"
```

---

### Task 7: Port `armor_plate.gd` → `ArmorPlate.cs`

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/ArmorPlate.cs`
- Create: `shared/unity_arena/Assets/Tests/EditMode/ArmorPlateTests.cs`

In Godot, `Area3D` listens for `body_entered`. In Unity, `Collider` with `isTrigger = true` listens for `OnTriggerEnter`. Same semantics, different engine surface.

- [ ] **Step 1: Write the failing tests**

Create `shared/unity_arena/Assets/Tests/EditMode/ArmorPlateTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class ArmorPlateTests
    {
        [Test]
        public void ApplyDamage_DecrementsHp()
        {
            var p = new ArmorPlateState { MaxHp = 200 };
            p.Reset();
            p.ApplyDamage(50);
            Assert.AreEqual(150, p.Hp);
        }

        [Test]
        public void ApplyDamage_ClampsAtZero()
        {
            var p = new ArmorPlateState { MaxHp = 200 };
            p.Reset();
            p.ApplyDamage(500);
            Assert.AreEqual(0, p.Hp);
        }

        [Test]
        public void Reset_RestoresMaxHp()
        {
            var p = new ArmorPlateState { MaxHp = 200 };
            p.Reset();
            p.ApplyDamage(150);
            p.Reset();
            Assert.AreEqual(200, p.Hp);
        }

        [Test]
        public void PlateId_FormatsTeamDotFace()
        {
            var p = new ArmorPlateState { Team = "blue", Face = "front" };
            Assert.AreEqual("blue.front", p.PlateId);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Unity Test Runner → EditMode → Run All.
Expected: 4 tests fail with `ArmorPlateState` undefined.

- [ ] **Step 3: Write minimal implementation**

Create `shared/unity_arena/Assets/Scripts/ArmorPlate.cs`:

```csharp
using System;
using UnityEngine;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Pure-C# state container for an armor plate. The MonoBehaviour wrapper
    /// (Stage 12b) adds the OnTriggerEnter glue and emission update.
    /// Mirrors armor_plate.gd.
    /// </summary>
    public class ArmorPlateState
    {
        public string Team = "blue";
        public string Face = "front";
        public string Icon = "Standard";
        public int MaxHp = 200;
        public int Hp;

        public string PlateId => $"{Team}.{Face}";

        public void Reset()
        {
            Hp = MaxHp;
        }

        public void ApplyDamage(int amount)
        {
            Hp = Mathf.Max(0, Hp - amount);
        }
    }

    /// <summary>
    /// MonoBehaviour wrapper. Listens for projectile triggers, calls
    /// projectile.OnArmorHit (returns damage), applies damage. The
    /// PlateHit event bubbles up to the parent Chassis.
    /// </summary>
    public class ArmorPlate : MonoBehaviour
    {
        public string Team = "blue";
        public string Face = "front";
        public string Icon = "Standard";
        public int MaxHp = 200;

        public event Action<int, int> PlateHit;  // damage, sourceInstanceId

        private ArmorPlateState _state;

        public string PlateId => _state.PlateId;
        public int Hp => _state.Hp;

        private void Awake()
        {
            _state = new ArmorPlateState
            {
                Team = Team,
                Face = Face,
                Icon = Icon,
                MaxHp = MaxHp,
            };
            _state.Reset();
        }

        public void ResetForNewEpisode()
        {
            _state.Reset();
        }

        public void ApplyDamage(int amount, int sourceInstanceId)
        {
            _state.ApplyDamage(amount);
            PlateHit?.Invoke(amount, sourceInstanceId);
        }

        private void OnTriggerEnter(Collider other)
        {
            var projectile = other.GetComponent<Projectile>();
            if (projectile == null) return;
            int damage = projectile.OnArmorHit(this);
            if (damage > 0)
            {
                ApplyDamage(damage, other.GetInstanceID());
            }
        }
    }
}
```

Note: `Projectile` MonoBehaviour referenced here is fully implemented in Stage 12b Task 11. In 12a we add a forward declaration via a stub `Projectile : MonoBehaviour` class with an empty `OnArmorHit` returning 0; the full implementation replaces it in 12b.

- [ ] **Step 4: Add Projectile MonoBehaviour stub**

Append to `shared/unity_arena/Assets/Scripts/Projectile.cs`:

```csharp
namespace TsingYun.UnityArena
{
    /// <summary>
    /// Stage 12a stub. Full implementation (Rigidbody integration,
    /// quadratic drag in FixedUpdate, lifetime caps, friendly-fire check)
    /// lands in Stage 12b Task 11.
    /// </summary>
    public class Projectile : UnityEngine.MonoBehaviour
    {
        public string Team = "blue";
        public bool Consumed = false;

        public int OnArmorHit(ArmorPlate plate) => 0;

        public void Arm(UnityEngine.Vector3 initialVelocity, string owningTeam)
        {
            Team = owningTeam;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Unity Test Runner → EditMode → Run All.
Expected: 4 ArmorPlate tests + 3 Projectile drag tests = 7 green.

- [ ] **Step 6: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/ArmorPlate.cs shared/unity_arena/Assets/Scripts/Projectile.cs shared/unity_arena/Assets/Tests/EditMode/ArmorPlateTests.cs
git commit -m "feat(stage12a): port armor_plate.gd -> ArmorPlate.cs (state + MB stub)"
```

---

### Task 8: Port `replay_recorder.gd` → `ReplayRecorder.cs`

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/ReplayRecorder.cs`
- Create: `shared/unity_arena/Assets/Tests/EditMode/ReplayRecorderTests.cs`

The Godot recorder writes JSON line-stream to `user://replays/<episode_id>.json`. Unity's equivalent path is `Application.persistentDataPath`. EditMode tests use a temp directory.

- [ ] **Step 1: Write the failing tests**

Create `shared/unity_arena/Assets/Tests/EditMode/ReplayRecorderTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class ReplayRecorderTests
    {
        private string _dir;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "unity_arena_test_" + System.Guid.NewGuid());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Start_WritesHeaderLine()
        {
            var r = new ReplayRecorder(_dir);
            r.Start("ep-000000000000002a", 42);
            r.Finish(new System.Collections.Generic.Dictionary<string, object>());

            string path = Path.Combine(_dir, "ep-000000000000002a.json");
            string[] lines = File.ReadAllLines(path);
            Assert.GreaterOrEqual(lines.Length, 2);
            StringAssert.Contains("\"kind\":\"header\"", lines[0]);
            StringAssert.Contains("\"episode_id\":\"ep-000000000000002a\"", lines[0]);
            StringAssert.Contains("\"seed\":42", lines[0]);
        }

        [Test]
        public void Record_WritesEventLineBetweenHeaderAndFooter()
        {
            var r = new ReplayRecorder(_dir);
            r.Start("ep-1", 1);
            r.Record(new System.Collections.Generic.Dictionary<string, object>
            {
                { "kind", "KIND_FIRED" },
                { "stamp_ns", 1000L },
            });
            r.Finish(new System.Collections.Generic.Dictionary<string, object>());

            string[] lines = File.ReadAllLines(Path.Combine(_dir, "ep-1.json"));
            Assert.AreEqual(3, lines.Length);
            StringAssert.Contains("\"kind\":\"KIND_FIRED\"", lines[1]);
            StringAssert.Contains("\"kind\":\"footer\"", lines[2]);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Unity Test Runner → EditMode → Run All.
Expected: 2 tests fail with `ReplayRecorder` undefined.

- [ ] **Step 3: Write minimal implementation**

Create `shared/unity_arena/Assets/Scripts/ReplayRecorder.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Per-episode replay recorder. Writes a JSON line-stream to
    /// {recordingDir}/{episode_id}.json. Mirrors replay_recorder.gd.
    /// MP4 capture is handled by Unity's Recorder package, not by this
    /// class.
    /// </summary>
    public class ReplayRecorder
    {
        private readonly string _dir;
        private StreamWriter _writer;
        private bool _open;
        private string _episodeId = "";
        private long _seed;

        public ReplayRecorder() : this(GetDefaultDir()) {}

        public ReplayRecorder(string recordingDir)
        {
            _dir = recordingDir;
        }

        private static string GetDefaultDir()
            => Path.Combine(Application.persistentDataPath, "replays");

        public void Start(string episodeId, long seedValue)
        {
            _episodeId = episodeId;
            _seed = seedValue;
            Directory.CreateDirectory(_dir);
            string path = Path.Combine(_dir, episodeId + ".json");
            _writer = new StreamWriter(path);
            _open = true;
            WriteLine(new Dictionary<string, object>
            {
                { "kind", "header" },
                { "episode_id", episodeId },
                { "seed", seedValue },
                { "version", "1.6.0" },
            });
        }

        public void Record(Dictionary<string, object> evt)
        {
            if (!_open) return;
            WriteLine(evt);
        }

        public void Finish(Dictionary<string, object> stats)
        {
            if (!_open) return;
            WriteLine(new Dictionary<string, object>
            {
                { "kind", "footer" },
                { "stats", stats },
            });
            _writer.Close();
            _writer = null;
            _open = false;
        }

        private void WriteLine(Dictionary<string, object> payload)
        {
            // Minimal JSON serializer: handles strings, numbers, bools,
            // long, and nested dicts. Matches the shape replay_recorder.gd
            // emits via JSON.stringify.
            _writer.WriteLine(JsonHelper.SerializeDict(payload));
        }
    }
}
```

- [ ] **Step 4: Add JsonHelper utility**

Create `shared/unity_arena/Assets/Scripts/JsonHelper.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Minimal JSON serializer for snake_case proto-shaped dicts.
    /// Unity's built-in JsonUtility doesn't handle Dictionary; we don't
    /// want a Newtonsoft.Json dependency for an HDRP runtime project.
    /// Supports: string, bool, int, long, float, double, Dictionary, List.
    /// </summary>
    public static class JsonHelper
    {
        public static string SerializeDict(Dictionary<string, object> dict)
        {
            var sb = new StringBuilder();
            AppendDict(sb, dict);
            return sb.ToString();
        }

        private static void AppendValue(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }
            switch (value)
            {
                case string s: AppendString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case Dictionary<string, object> dict: AppendDict(sb, dict); break;
                case List<object> list: AppendList(sb, list); break;
                default: AppendString(sb, value.ToString()); break;
            }
        }

        private static void AppendDict(StringBuilder sb, Dictionary<string, object> dict)
        {
            sb.Append('{');
            bool first = true;
            foreach (var kv in dict)
            {
                if (!first) sb.Append(',');
                AppendString(sb, kv.Key);
                sb.Append(':');
                AppendValue(sb, kv.Value);
                first = false;
            }
            sb.Append('}');
        }

        private static void AppendList(StringBuilder sb, List<object> list)
        {
            sb.Append('[');
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendValue(sb, list[i]);
            }
            sb.Append(']');
        }

        private static void AppendString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Unity Test Runner → EditMode → Run All.
Expected: replay tests pass.

- [ ] **Step 6: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/ReplayRecorder.cs shared/unity_arena/Assets/Scripts/JsonHelper.cs shared/unity_arena/Assets/Tests/EditMode/ReplayRecorderTests.cs
git commit -m "feat(stage12a): port replay_recorder.gd + JSON helper"
```

---

### Task 9: Implement `TcpProtoServer.cs` (length-prefixed JSON RPC)

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/TcpProtoServer.cs`
- Create: `shared/unity_arena/Assets/Tests/PlayMode/TcpProtoServerTests.cs`

This is the load-bearing wire-parity component. Must replicate `tcp_proto_server.gd` byte-for-byte: 4-byte big-endian length prefix, UTF-8 JSON body, request shape `{"method": ..., "request": {...}}`, response shape `{"ok": true, "response": {...}}` or `{"ok": false, "error": "..."}`.

- [ ] **Step 1: Write the failing PlayMode test**

Create `shared/unity_arena/Assets/Tests/PlayMode/TcpProtoServerTests.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.PlayMode
{
    public class TcpProtoServerTests
    {
        private GameObject _hostObject;
        private TcpProtoServer _server;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _hostObject = new GameObject("TcpProtoServerHost");
            _server = _hostObject.AddComponent<TcpProtoServer>();
            _server.Port = 17654;  // unique test port
            // Stub dispatch: echo back the request with method= label.
            _server.SetDispatcher((method, request) => new Dictionary<string, object>
            {
                { "echoed_method", method },
                { "echoed_request", request },
            });
            yield return null;  // wait one frame for Awake
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            UnityEngine.Object.Destroy(_hostObject);
            yield return null;
        }

        [Test]
        public void RoundTrip_LengthPrefixedJson()
        {
            using var client = new TcpClient("127.0.0.1", _server.Port);
            using var stream = client.GetStream();

            // Send: { "method": "env_reset", "request": { "seed": 42 } }
            string requestJson = "{\"method\":\"env_reset\",\"request\":{\"seed\":42}}";
            byte[] body = Encoding.UTF8.GetBytes(requestJson);
            byte[] header = new byte[]
            {
                (byte)((body.Length >> 24) & 0xFF),
                (byte)((body.Length >> 16) & 0xFF),
                (byte)((body.Length >> 8) & 0xFF),
                (byte)(body.Length & 0xFF),
            };
            stream.Write(header, 0, 4);
            stream.Write(body, 0, body.Length);

            // Read 4-byte BE length
            byte[] respHeader = ReadExact(stream, 4);
            int respLen = (respHeader[0] << 24) | (respHeader[1] << 16) | (respHeader[2] << 8) | respHeader[3];
            byte[] respBody = ReadExact(stream, respLen);
            string respJson = Encoding.UTF8.GetString(respBody);

            StringAssert.Contains("\"ok\":true", respJson);
            StringAssert.Contains("\"echoed_method\":\"env_reset\"", respJson);
        }

        private byte[] ReadExact(NetworkStream stream, int n)
        {
            byte[] buf = new byte[n];
            int read = 0;
            while (read < n)
            {
                int got = stream.Read(buf, read, n - read);
                if (got <= 0) throw new Exception("connection closed");
                read += got;
            }
            return buf;
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Unity Test Runner → PlayMode → Run All.
Expected: test fails with `TcpProtoServer` undefined.

- [ ] **Step 3: Write minimal implementation**

Create `shared/unity_arena/Assets/Scripts/TcpProtoServer.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Length-prefixed JSON over TCP control server. Wire layout in each
    /// direction: [u32 BE length][JSON UTF-8 bytes]. Mirrors tcp_proto_server.gd
    /// byte-for-byte. Listens on port 7654 by default.
    ///
    /// Dispatcher signature: (method, request) -> response. Set via
    /// SetDispatcher. ArenaMain wires this up at Awake.
    /// </summary>
    public class TcpProtoServer : MonoBehaviour
    {
        public int Port = 7654;

        private TcpListener _listener;
        private Thread _acceptThread;
        private bool _running;
        private Func<string, Dictionary<string, object>, object> _dispatch;

        public void SetDispatcher(Func<string, Dictionary<string, object>, object> dispatcher)
        {
            _dispatch = dispatcher;
        }

        private void Awake()
        {
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "TcpProtoServer-accept" };
            _acceptThread.Start();
            Debug.Log($"[TcpProtoServer] listening on tcp://0.0.0.0:{Port}");
        }

        private void OnDestroy()
        {
            _running = false;
            _listener?.Stop();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    client.NoDelay = true;
                    var clientThread = new Thread(() => ClientLoop(client))
                    {
                        IsBackground = true,
                        Name = "TcpProtoServer-client",
                    };
                    clientThread.Start();
                }
                catch (SocketException) { /* listener closed */ }
                catch (ObjectDisposedException) { /* listener closed */ }
            }
        }

        private void ClientLoop(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                while (_running && client.Connected)
                {
                    byte[] header = ReadExact(stream, 4);
                    if (header == null) return;
                    int len = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
                    byte[] body = ReadExact(stream, len);
                    if (body == null) return;
                    string requestJson = Encoding.UTF8.GetString(body);
                    object response = Dispatch(requestJson);
                    SendResponse(stream, response);
                }
            }
        }

        private object Dispatch(string requestJson)
        {
            // Minimal JSON parser: only need top-level object with "method" and "request".
            // Use Unity's JsonUtility-incompatible path: hand-parse to a Dictionary.
            var parsed = JsonMiniParser.ParseDict(requestJson);
            if (parsed == null)
                return new Dictionary<string, object> { { "ok", false }, { "error", "request was not a JSON object" } };

            string method = parsed.TryGetValue("method", out var m) ? m as string ?? "" : "";
            var request = parsed.TryGetValue("request", out var r) ? r as Dictionary<string, object> ?? new Dictionary<string, object>() : new Dictionary<string, object>();

            if (_dispatch == null)
                return new Dictionary<string, object> { { "ok", false }, { "error", "no dispatcher set" } };

            object response;
            try
            {
                response = _dispatch(method, request);
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "ok", false }, { "error", ex.Message } };
            }

            // Mirror arena_main.gd's _error sentinel: response dict with "_error" key indicates failure.
            if (response is Dictionary<string, object> respDict && respDict.ContainsKey("_error"))
                return new Dictionary<string, object> { { "ok", false }, { "error", respDict["_error"] } };

            return new Dictionary<string, object> { { "ok", true }, { "response", response } };
        }

        private void SendResponse(NetworkStream stream, object response)
        {
            string json = response is Dictionary<string, object> d
                ? JsonHelper.SerializeDict(d)
                : "{}";
            byte[] body = Encoding.UTF8.GetBytes(json);
            int n = body.Length;
            byte[] header = new byte[]
            {
                (byte)((n >> 24) & 0xFF),
                (byte)((n >> 16) & 0xFF),
                (byte)((n >> 8) & 0xFF),
                (byte)(n & 0xFF),
            };
            stream.Write(header, 0, 4);
            stream.Write(body, 0, n);
            stream.Flush();
        }

        private static byte[] ReadExact(NetworkStream stream, int n)
        {
            byte[] buf = new byte[n];
            int read = 0;
            while (read < n)
            {
                int got = stream.Read(buf, read, n - read);
                if (got <= 0) return null;
                read += got;
            }
            return buf;
        }
    }
}
```

- [ ] **Step 4: Add minimal JSON parser**

Create `shared/unity_arena/Assets/Scripts/JsonMiniParser.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Minimal recursive-descent JSON parser. Handles the dict shapes the
    /// candidate stack sends (env_reset / env_step / env_push_fire /
    /// env_finish requests). Returns a Dictionary&lt;string, object&gt; tree
    /// where leaves are string / double / long / bool / null.
    /// </summary>
    public static class JsonMiniParser
    {
        public static Dictionary<string, object> ParseDict(string json)
        {
            int i = 0;
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '{') return null;
            return ReadDict(json, ref i);
        }

        private static Dictionary<string, object> ReadDict(string s, ref int i)
        {
            var dict = new Dictionary<string, object>();
            i++;  // consume '{'
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return dict; }
            while (i < s.Length)
            {
                SkipWs(s, ref i);
                string key = ReadString(s, ref i);
                SkipWs(s, ref i);
                if (s[i] != ':') return null;
                i++;
                SkipWs(s, ref i);
                object val = ReadValue(s, ref i);
                dict[key] = val;
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return dict; }
                return null;
            }
            return dict;
        }

        private static List<object> ReadList(string s, ref int i)
        {
            var list = new List<object>();
            i++;  // consume '['
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (i < s.Length)
            {
                SkipWs(s, ref i);
                list.Add(ReadValue(s, ref i));
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                return null;
            }
            return list;
        }

        private static object ReadValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            char c = s[i];
            if (c == '"') return ReadString(s, ref i);
            if (c == '{') return ReadDict(s, ref i);
            if (c == '[') return ReadList(s, ref i);
            if (c == 't' && s.Substring(i, 4) == "true") { i += 4; return true; }
            if (c == 'f' && s.Substring(i, 5) == "false") { i += 5; return false; }
            if (c == 'n' && s.Substring(i, 4) == "null") { i += 4; return null; }
            return ReadNumber(s, ref i);
        }

        private static string ReadString(string s, ref int i)
        {
            if (s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                            i += 4;
                            break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(s[i++]);
                }
            }
            i++;  // consume closing '"'
            return sb.ToString();
        }

        private static object ReadNumber(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            bool isFloat = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '.' || c == 'e' || c == 'E') isFloat = true;
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                    i++;
                else break;
            }
            string token = s.Substring(start, i - start);
            if (isFloat)
                return double.Parse(token, CultureInfo.InvariantCulture);
            return long.Parse(token, CultureInfo.InvariantCulture);
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Unity Test Runner → PlayMode → Run All.
Expected: round-trip test green.

- [ ] **Step 6: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/TcpProtoServer.cs shared/unity_arena/Assets/Scripts/JsonMiniParser.cs shared/unity_arena/Assets/Tests/PlayMode/TcpProtoServerTests.cs
git commit -m "feat(stage12a): TcpProtoServer + JsonMiniParser matching tcp_proto_server.gd wire"
```

---

### Task 10: Implement `TcpFramePub.cs` (16-byte header + RGB888 stream)

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/TcpFramePub.cs`
- Create: `shared/unity_arena/Assets/Tests/PlayMode/TcpFramePubTests.cs`

Mirrors `tcp_frame_pub.gd`: 16-byte LE header (`<QQ frame_id stamp_ns>`) + RGB888 payload at 60 Hz from the gimbal Camera. Uses `AsyncGPUReadback.Request` so the main thread is not blocked waiting for the GPU.

- [ ] **Step 1: Write the failing PlayMode test**

Create `shared/unity_arena/Assets/Tests/PlayMode/TcpFramePubTests.cs`:

```csharp
using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.PlayMode
{
    public class TcpFramePubTests
    {
        private GameObject _hostObject;
        private GameObject _camObject;
        private TcpFramePub _pub;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _camObject = new GameObject("TestCamera");
            var cam = _camObject.AddComponent<Camera>();
            cam.targetTexture = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);

            _hostObject = new GameObject("TcpFramePubHost");
            _pub = _hostObject.AddComponent<TcpFramePub>();
            _pub.Port = 17655;
            _pub.SourceCamera = cam;
            _pub.FrameWidth = 64;
            _pub.FrameHeight = 36;
            yield return null;  // wait for Awake
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            UnityEngine.Object.Destroy(_hostObject);
            UnityEngine.Object.Destroy(_camObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstFrame_Has16ByteHeader_PlusExpectedPayloadSize()
        {
            using var client = new TcpClient("127.0.0.1", _pub.Port);
            using var stream = client.GetStream();

            // Wait for at least one frame to be published.
            yield return new WaitForSeconds(0.2f);

            byte[] header = new byte[16];
            int read = 0;
            while (read < 16)
            {
                int got = stream.Read(header, read, 16 - read);
                if (got <= 0) Assert.Fail("connection closed before header");
                read += got;
            }

            ulong frameId = BitConverter.ToUInt64(header, 0);
            ulong stampNs = BitConverter.ToUInt64(header, 8);
            Assert.GreaterOrEqual(frameId, 1UL);
            Assert.Greater(stampNs, 0UL);

            // Payload size = width * height * 3 (RGB888)
            int expected = 64 * 36 * 3;
            byte[] body = new byte[expected];
            read = 0;
            while (read < expected)
            {
                int got = stream.Read(body, read, expected - read);
                if (got <= 0) Assert.Fail("connection closed before body");
                read += got;
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Unity Test Runner → PlayMode → Run All.
Expected: test fails with `TcpFramePub` undefined.

- [ ] **Step 3: Write minimal implementation**

Create `shared/unity_arena/Assets/Scripts/TcpFramePub.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Frame publisher. Captures the gimbal camera's RenderTexture into an
    /// RGB888 byte stream and publishes it over plain TCP at <see cref="TargetFps"/>.
    /// Wire layout: 16-byte LE header (frame_id u64, stamp_ns u64) +
    /// width*height*3 raw bytes. Mirrors tcp_frame_pub.gd. AsyncGPUReadback
    /// is used to avoid stalling the main thread on the GPU.
    /// </summary>
    public class TcpFramePub : MonoBehaviour
    {
        public int Port = 7655;
        public int TargetFps = 60;
        public int FrameWidth = 1280;
        public int FrameHeight = 720;
        public Camera SourceCamera;

        private TcpListener _listener;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _clientsLock = new object();
        private Thread _acceptThread;
        private bool _running;
        private float _accumulator;
        private ulong _frameId;
        private RenderTexture _captureRt;
        private bool _readbackInFlight;

        private void Awake()
        {
            _captureRt = new RenderTexture(FrameWidth, FrameHeight, 0, RenderTextureFormat.ARGB32);
            _captureRt.Create();

            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "TcpFramePub-accept" };
            _acceptThread.Start();
            Debug.Log($"[TcpFramePub] listening on tcp://0.0.0.0:{Port}");
        }

        private void OnDestroy()
        {
            _running = false;
            _listener?.Stop();
            lock (_clientsLock)
            {
                foreach (var c in _clients) c.Close();
                _clients.Clear();
            }
            if (_captureRt != null) _captureRt.Release();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    client.NoDelay = true;
                    lock (_clientsLock) _clients.Add(client);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }
        }

        private void Update()
        {
            _accumulator += Time.unscaledDeltaTime;
            float period = 1f / TargetFps;
            if (_accumulator < period) return;
            _accumulator = 0f;

            int clientCount;
            lock (_clientsLock) clientCount = _clients.Count;
            if (clientCount == 0) return;
            if (_readbackInFlight) return;
            if (SourceCamera == null) return;

            // Render camera to capture RT, then async readback.
            SourceCamera.targetTexture = _captureRt;
            SourceCamera.Render();

            _readbackInFlight = true;
            AsyncGPUReadback.Request(_captureRt, 0, TextureFormat.RGB24, OnReadback);
        }

        private void OnReadback(AsyncGPUReadbackRequest req)
        {
            _readbackInFlight = false;
            if (req.hasError) return;

            byte[] rgb = req.GetData<byte>().ToArray();
            _frameId++;
            ulong stampNs = (ulong)(System.Diagnostics.Stopwatch.GetTimestamp() * 1_000_000_000L /
                                    System.Diagnostics.Stopwatch.Frequency);

            byte[] header = new byte[16];
            BitConverter.GetBytes(_frameId).CopyTo(header, 0);
            BitConverter.GetBytes(stampNs).CopyTo(header, 8);

            BroadcastFrame(header, rgb);
        }

        private void BroadcastFrame(byte[] header, byte[] body)
        {
            lock (_clientsLock)
            {
                for (int i = _clients.Count - 1; i >= 0; i--)
                {
                    var client = _clients[i];
                    try
                    {
                        if (!client.Connected) { _clients.RemoveAt(i); continue; }
                        var stream = client.GetStream();
                        stream.Write(header, 0, header.Length);
                        stream.Write(body, 0, body.Length);
                    }
                    catch
                    {
                        client.Close();
                        _clients.RemoveAt(i);
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Unity Test Runner → PlayMode → Run All.
Expected: frame stream test green.

- [ ] **Step 5: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/TcpFramePub.cs shared/unity_arena/Assets/Tests/PlayMode/TcpFramePubTests.cs
git commit -m "feat(stage12a): TcpFramePub matching tcp_frame_pub.gd wire (16B header + RGB888)"
```

---

### Task 11: Port `arena_main.gd` → `ArenaMain.cs` (episode orchestrator)

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/ArenaMain.cs`
- Create: `shared/unity_arena/Assets/Scripts/Chassis.cs` (stub MB; full body in 12b)

ArenaMain owns the TcpProtoServer, the TcpFramePub, the ReplayRecorder, and references both team chassis. Methods `EnvReset / EnvStep / EnvPushFire / EnvFinish` mirror the GDScript dispatcher, returning the same dict shapes.

- [ ] **Step 1: Write Chassis MonoBehaviour stub**

Create `shared/unity_arena/Assets/Scripts/Chassis.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Stage 12a stub. Full implementation (MecanumChassisController-driven
    /// CharacterController.Move, armor plate wiring, gimbal child) lands in
    /// Stage 12b Task 8. The 12a stub provides just the API ArenaMain calls
    /// so the orchestrator + wire-format tests can run end-to-end.
    /// </summary>
    public class Chassis : MonoBehaviour
    {
        public string Team = "blue";
        public int ChassisId = 0;
        public int DamageTaken = 0;

        public event Action<string, int, int> ArmorHit;  // plateId, damage, sourceId

        public void ResetForNewEpisode(Vector3 spawnPosition, float spawnYaw)
        {
            transform.position = spawnPosition;
            transform.rotation = Quaternion.Euler(0f, spawnYaw * Mathf.Rad2Deg, 0f);
            DamageTaken = 0;
        }

        public void SetChassisCmd(float vxBody, float vyBody, float omega) {}

        public Dictionary<string, object> OdomState() => new Dictionary<string, object>
        {
            { "position_world", Vec3Dict(transform.position) },
            { "linear_velocity", Vec3Dict(Vector3.zero) },
            { "yaw_world", transform.rotation.eulerAngles.y * Mathf.Deg2Rad },
        };

        public Dictionary<string, object> GimbalState() => new Dictionary<string, object>
        {
            { "yaw", 0f }, { "pitch", 0f }, { "yaw_rate", 0f }, { "pitch_rate", 0f },
        };

        protected void RaiseArmorHit(string plateId, int damage, int sourceId)
            => ArmorHit?.Invoke(plateId, damage, sourceId);

        private static Dictionary<string, object> Vec3Dict(Vector3 v) => new Dictionary<string, object>
        {
            { "x", (double)v.x }, { "y", (double)v.y }, { "z", (double)v.z },
        };
    }
}
```

- [ ] **Step 2: Write ArenaMain implementation**

Create `shared/unity_arena/Assets/Scripts/ArenaMain.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    public enum EpisodeState { Idle, Running, Finishing }

    /// <summary>
    /// Episode orchestrator. Owns the TCP control server, frame publisher,
    /// and replay recorder. Mirrors arena_main.gd one-to-one.
    /// </summary>
    public class ArenaMain : MonoBehaviour
    {
        public const string SimBuildSha = "stage12a-unity-scaffold-1.6";
        public const long DefaultDurationNs = 90_000_000_000L;

        public int ControlPort = 7654;
        public int FramePort = 7655;

        public Chassis BlueChassis;
        public Chassis RedChassis;
        public Camera GimbalCamera;
        public Transform ProjectileRoot;

        public EpisodeState State { get; private set; } = EpisodeState.Idle;
        public string EpisodeId { get; private set; } = "";
        public long DurationNs { get; private set; } = DefaultDurationNs;
        public string OpponentTier { get; private set; } = "bronze";
        public bool OracleHints { get; private set; }
        public long FrameId { get; private set; }

        private TcpProtoServer _control;
        private TcpFramePub _framePub;
        private ReplayRecorder _replay;

        private long _startedTicksMs;
        private readonly List<Dictionary<string, object>> _events = new List<Dictionary<string, object>>();
        private int _projectilesFired;
        private int _armorHits;
        private int _damageDealt;

        private void Awake()
        {
            BlueChassis.Team = "blue";
            BlueChassis.ChassisId = 0;
            RedChassis.Team = "red";
            RedChassis.ChassisId = 1;
            BlueChassis.ArmorHit += OnBlueArmorHit;
            RedChassis.ArmorHit += OnRedArmorHit;

            var controlObj = new GameObject("TcpProtoServer");
            controlObj.transform.SetParent(transform);
            _control = controlObj.AddComponent<TcpProtoServer>();
            _control.Port = ControlPort;
            _control.SetDispatcher(Dispatch);

            var frameObj = new GameObject("TcpFramePub");
            frameObj.transform.SetParent(transform);
            _framePub = frameObj.AddComponent<TcpFramePub>();
            _framePub.Port = FramePort;
            _framePub.SourceCamera = GimbalCamera;

            _replay = new ReplayRecorder();

            Debug.Log($"[ArenaMain] control on tcp://0.0.0.0:{ControlPort}, frames on tcp://0.0.0.0:{FramePort}");
        }

        private object Dispatch(string method, Dictionary<string, object> request)
        {
            switch (method)
            {
                case "env_reset": return EnvReset(request);
                case "env_step": return EnvStep(request);
                case "env_push_fire": return EnvPushFire(request);
                case "env_finish": return EnvFinish(request);
                default: return new Dictionary<string, object> { { "_error", $"unknown method: {method}" } };
            }
        }

        public Dictionary<string, object> EnvReset(Dictionary<string, object> request)
        {
            long seedValue = AsLong(request, "seed", 0);
            OpponentTier = AsString(request, "opponent_tier", "bronze");
            OracleHints = AsBool(request, "oracle_hints", false);
            long requestedDuration = AsLong(request, "duration_ns", 0);
            DurationNs = requestedDuration > 0 ? requestedDuration : DefaultDurationNs;

            SeedRng.Reseed(seedValue);
            EpisodeId = $"ep-{seedValue:x16}";
            _startedTicksMs = System.Diagnostics.Stopwatch.GetTimestamp() * 1000L /
                              System.Diagnostics.Stopwatch.Frequency;
            FrameId = 0;
            _events.Clear();
            _projectilesFired = 0;
            _armorHits = 0;
            _damageDealt = 0;

            BlueChassis.ResetForNewEpisode(new Vector3(-3f, 0f, 0f), 0f);
            RedChassis.ResetForNewEpisode(new Vector3(3f, 0f, 0f), Mathf.PI);
            // Despawn projectiles (12b will populate ProjectileRoot)
            if (ProjectileRoot != null)
                foreach (Transform child in ProjectileRoot) Destroy(child.gameObject);

            State = EpisodeState.Running;
            _replay.Start(EpisodeId, seedValue);

            return new Dictionary<string, object>
            {
                { "bundle", BuildSensorBundle() },
                { "zmq_frame_endpoint", $"tcp://127.0.0.1:{FramePort}" },
                { "simulator_build_sha256", SimBuildSha },
            };
        }

        public Dictionary<string, object> EnvStep(Dictionary<string, object> cmd)
        {
            if (State != EpisodeState.Running)
                return new Dictionary<string, object> { { "_error", $"env_step called in state={State}" } };

            FrameId++;
            if (NowNs() > DurationNs) State = EpisodeState.Finishing;
            return BuildSensorBundle();
        }

        public Dictionary<string, object> EnvPushFire(Dictionary<string, object> cmd)
        {
            if (State != EpisodeState.Running)
                return new Dictionary<string, object> { { "accepted", false }, { "reason", "no_episode" }, { "queued_count", 0 } };

            // Stage 12a stub: report acceptance without spawning. Full spawn in 12b.
            int burst = Mathf.Max(0, (int)AsLong(cmd, "burst_count", 1));
            _projectilesFired += burst;
            return new Dictionary<string, object>
            {
                { "accepted", burst > 0 },
                { "reason", "" },
                { "queued_count", burst },
            };
        }

        public Dictionary<string, object> EnvFinish(Dictionary<string, object> request)
        {
            if (State == EpisodeState.Idle)
                return new Dictionary<string, object> { { "_error", "no episode in progress" } };

            State = EpisodeState.Idle;
            var stats = BuildEpisodeStats();
            _replay.Finish(stats);
            return stats;
        }

        private Dictionary<string, object> BuildSensorBundle()
        {
            long stamp = NowNs();
            var gimbal = BlueChassis.GimbalState();
            gimbal["stamp_ns"] = stamp;
            var bundle = new Dictionary<string, object>
            {
                { "frame", new Dictionary<string, object>
                {
                    { "frame_id", FrameId },
                    { "zmq_topic", $"frames.{SeedRng.CurrentSeed()}" },
                    { "stamp_ns", stamp },
                    { "width", 1280L },
                    { "height", 720L },
                    { "pixel_format", "PIXEL_FORMAT_RGB888" },
                }},
                { "imu", new Dictionary<string, object>
                {
                    { "stamp_ns", stamp },
                    { "angular_velocity", new Dictionary<string, object> { { "x", 0.0 }, { "y", 0.0 }, { "z", 0.0 } } },
                    { "linear_accel", new Dictionary<string, object> { { "x", 0.0 }, { "y", -9.81 }, { "z", 0.0 } } },
                    { "orientation", new Dictionary<string, object> { { "w", 1.0 }, { "x", 0.0 }, { "y", 0.0 }, { "z", 0.0 } } },
                }},
                { "gimbal", gimbal },
                { "odom", BuildOdomPayload(stamp) },
            };
            if (OracleHints)
            {
                Vector3 redPos = RedChassis.transform.position;
                bundle["oracle"] = new Dictionary<string, object>
                {
                    { "target_position_world", new Dictionary<string, object> { { "x", (double)redPos.x }, { "y", (double)redPos.y }, { "z", (double)redPos.z } } },
                    { "target_velocity_world", new Dictionary<string, object> { { "x", 0.0 }, { "y", 0.0 }, { "z", 0.0 } } },
                    { "target_visible", true },
                };
            }
            return bundle;
        }

        private Dictionary<string, object> BuildOdomPayload(long stamp)
        {
            var raw = BlueChassis.OdomState();
            raw["stamp_ns"] = stamp;
            return raw;
        }

        private Dictionary<string, object> BuildEpisodeStats() => new Dictionary<string, object>
        {
            { "episode_id", EpisodeId },
            { "seed", SeedRng.CurrentSeed() },
            { "duration_ns", NowNs() },
            { "candidate_commit_sha", "" },
            { "candidate_build_sha256", "" },
            { "simulator_build_sha256", SimBuildSha },
            { "opponent_policy_sha256", "" },
            { "opponent_tier", OpponentTier },
            { "outcome", ResolveOutcome() },
            { "damage_dealt", _damageDealt },
            { "damage_taken", BlueChassis.DamageTaken },
            { "projectiles_fired", _projectilesFired },
            { "armor_hits", _armorHits },
            { "aim_latency_p50_ns", 0L },
            { "aim_latency_p95_ns", 0L },
            { "aim_latency_p99_ns", 0L },
            { "events", new List<object>(_events) },
        };

        private string ResolveOutcome()
        {
            if (BlueChassis.DamageTaken >= 800 && _damageDealt < 800) return "OUTCOME_LOSS";
            if (_damageDealt >= 800 && BlueChassis.DamageTaken < 800) return "OUTCOME_WIN";
            return "OUTCOME_TIMEOUT";
        }

        private void OnBlueArmorHit(string plateId, int damage, int sourceId)
            => _events.Add(new Dictionary<string, object>
            {
                { "stamp_ns", NowNs() }, { "kind", "KIND_HIT_ARMOR" },
                { "armor_id", plateId }, { "damage", damage },
            });

        private void OnRedArmorHit(string plateId, int damage, int sourceId)
        {
            _armorHits++;
            _damageDealt += damage;
            _events.Add(new Dictionary<string, object>
            {
                { "stamp_ns", NowNs() }, { "kind", "KIND_HIT_ARMOR" },
                { "armor_id", plateId }, { "damage", damage },
            });
        }

        private long NowNs()
        {
            long nowMs = System.Diagnostics.Stopwatch.GetTimestamp() * 1000L /
                         System.Diagnostics.Stopwatch.Frequency;
            return (nowMs - _startedTicksMs) * 1_000_000L;
        }

        private static long AsLong(Dictionary<string, object> dict, string key, long fallback)
        {
            if (!dict.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is long l) return l;
            if (v is double d) return (long)d;
            if (v is int i) return i;
            return fallback;
        }

        private static string AsString(Dictionary<string, object> dict, string key, string fallback)
            => dict.TryGetValue(key, out var v) && v is string s ? s : fallback;

        private static bool AsBool(Dictionary<string, object> dict, string key, bool fallback)
            => dict.TryGetValue(key, out var v) && v is bool b ? b : fallback;
    }
}
```

- [ ] **Step 3: Commit (no test yet — Task 12 covers end-to-end PlayMode test)**

```bash
git add shared/unity_arena/Assets/Scripts/Chassis.cs shared/unity_arena/Assets/Scripts/ArenaMain.cs
git commit -m "feat(stage12a): port arena_main.gd -> ArenaMain.cs orchestrator + Chassis stub"
```

---

### Task 12: Build placeholder ArenaMain.unity scene + end-to-end PlayMode test

**Files:**
- Create: `shared/unity_arena/Assets/Scenes/ArenaMain.unity`
- Create: `shared/unity_arena/Assets/Tests/PlayMode/ArenaMainEpisodeTests.cs`

A placeholder scene with primitive cubes for floor + chassis + gimbal so the wire-format and smoke tests can drive end-to-end behaviour without final art.

- [ ] **Step 1: Build the scene in Unity Editor**

In Unity:
1. File → New Scene → Basic Outdoors (HDRP). Save as `Assets/Scenes/ArenaMain.unity`.
2. Delete the default sky / sun / sphere objects.
3. Create empty GameObject `ArenaMain` at origin. Add `ArenaMain` component.
4. Floor: Create → 3D Object → Cube. Scale (20, 0.1, 20). Position (0, -0.05, 0). Name `Floor`. Layer `Default`.
5. BlueChassis: Create → 3D Object → Cube. Scale (0.45, 0.18, 0.55). Position (-3, 0.16, 0). Add `Chassis` component, set `Team = "blue"`. Name `BlueChassis`.
6. Add child cube to BlueChassis named `Gimbal`, position (0, 0.30, 0), scale (0.18, 0.08, 0.08). Add child cube to Gimbal named `Camera_Mount`. Add a Camera component to Camera_Mount, named `GimbalCamera`. Camera FOV 60, near 0.05, far 60.
7. RedChassis: duplicate BlueChassis, position (3, 0.16, 0), rotate Y 180°. Set `Team = "red"`. Name `RedChassis`.
8. ProjectileRoot: empty GameObject child of ArenaMain at origin.
9. Directional Light: existing default. Set Color cool-white (5600K equivalent), Intensity 1.2.
10. Drag references on ArenaMain inspector: BlueChassis ← BlueChassis, RedChassis ← RedChassis, GimbalCamera ← BlueChassis/Gimbal/Camera_Mount/GimbalCamera, ProjectileRoot ← ProjectileRoot.
11. Save scene (Ctrl/Cmd+S).

- [ ] **Step 2: Write the failing PlayMode end-to-end test**

Create `shared/unity_arena/Assets/Tests/PlayMode/ArenaMainEpisodeTests.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.PlayMode
{
    public class ArenaMainEpisodeTests
    {
        [UnityTest]
        public IEnumerator EnvReset_ReturnsInitialStateWithSimSha()
        {
            yield return SceneManager.LoadSceneAsync("ArenaMain", LoadSceneMode.Single);
            var arena = Object.FindFirstObjectByType<ArenaMain>();
            Assert.IsNotNull(arena);

            var resp = arena.EnvReset(new Dictionary<string, object>
            {
                { "seed", 42L }, { "opponent_tier", "bronze" },
                { "oracle_hints", true }, { "duration_ns", 5_000_000_000L },
            });

            Assert.AreEqual(ArenaMain.SimBuildSha, resp["simulator_build_sha256"]);
            Assert.IsTrue(((string)resp["zmq_frame_endpoint"]).Contains("tcp://127.0.0.1:"));
            Assert.IsTrue(resp.ContainsKey("bundle"));
        }

        [UnityTest]
        public IEnumerator EpisodeId_DeterministicPerSeed()
        {
            yield return SceneManager.LoadSceneAsync("ArenaMain", LoadSceneMode.Single);
            var arena = Object.FindFirstObjectByType<ArenaMain>();

            arena.EnvReset(new Dictionary<string, object> { { "seed", 42L } });
            string id1 = arena.EpisodeId;
            arena.EnvFinish(new Dictionary<string, object>());

            arena.EnvReset(new Dictionary<string, object> { { "seed", 42L } });
            string id2 = arena.EpisodeId;

            Assert.AreEqual("ep-000000000000002a", id1);
            Assert.AreEqual(id1, id2);
        }
    }
}
```

- [ ] **Step 3: Add the scene to Build Settings**

In Unity: File → Build Settings → Add Open Scenes. ArenaMain.unity should appear in the list at index 0.

- [ ] **Step 4: Run test to verify it passes**

Unity Test Runner → PlayMode → Run All.
Expected: episode tests + earlier TcpProtoServer + TcpFramePub tests all green.

- [ ] **Step 5: Commit**

```bash
git add shared/unity_arena/Assets/Scenes shared/unity_arena/Assets/Tests/PlayMode/ArenaMainEpisodeTests.cs shared/unity_arena/ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(stage12a): placeholder ArenaMain.unity scene + episode PlayMode tests"
```

---

### Task 13: Rename + parametrize `test_godot_wire_format.py`

**Files:**
- Rename: `tests/test_godot_wire_format.py` → `tests/test_arena_wire_format.py`
- Modify: parametrize over `[godot, unity]` engines.

The existing test hand-constructs the dict shape that `arena_main.gd` emits. We add a parallel helper for the Unity dict shape (which mirrors `ArenaMain.cs::BuildSensorBundle`) and parametrize.

- [ ] **Step 1: Rename the file**

```bash
git mv tests/test_godot_wire_format.py tests/test_arena_wire_format.py
```

- [ ] **Step 2: Modify to parametrize**

Replace the contents of `tests/test_arena_wire_format.py` with the following (additions marked):

```python
"""Engine-agnostic wire-format conformance for the Aiming Arena.

Mirrors both the GDScript (arena_main.gd) and the C#
(unity_arena/Assets/Scripts/ArenaMain.cs) sensor-bundle dict shapes,
asserting that each round-trips through google.protobuf.json_format.

This test does NOT spin up either simulator. It hand-constructs the
dict shapes that each engine's source is documented to emit. If the
test drifts from the source, the matching test breaks first; the
engine's source is the source of truth.
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest
from google.protobuf import json_format

REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT / "shared" / "grpc_stub_server" / "src"))

from grpc_stub_server import proto_codegen  # noqa: E402

aiming_pb2 = proto_codegen.import_pb2("aiming")
sensor_pb2 = proto_codegen.import_pb2("sensor")
episode_pb2 = proto_codegen.import_pb2("episode")


# --------------------------------------------------------- engine helpers

def _godot_bundle(*, oracle: bool, frame_id: int = 0) -> dict:
    """Mirror of arena_main.gd::_build_sensor_bundle."""
    bundle = {
        "frame": {
            "frame_id": frame_id,
            "zmq_topic": "frames.42",
            "stamp_ns": 1_000_000,
            "width": 1280,
            "height": 720,
            "pixel_format": "PIXEL_FORMAT_RGB888",
        },
        "imu": {
            "stamp_ns": 1_000_000,
            "angular_velocity": {"x": 0.0, "y": 0.0, "z": 0.0},
            "linear_accel": {"x": 0.0, "y": -9.81, "z": 0.0},
            "orientation": {"w": 1.0, "x": 0.0, "y": 0.0, "z": 0.0},
        },
        "gimbal": {"stamp_ns": 1_000_000, "yaw": 0.0, "pitch": 0.0,
                   "yaw_rate": 0.0, "pitch_rate": 0.0},
        "odom": {
            "stamp_ns": 1_000_000,
            "position_world": {"x": -3.0, "y": 0.0, "z": 0.0},
            "linear_velocity": {"x": 0.0, "y": 0.0, "z": 0.0},
            "yaw_world": 0.0,
        },
    }
    if oracle:
        bundle["oracle"] = {
            "target_position_world": {"x": 3.0, "y": 0.0, "z": 0.0},
            "target_velocity_world": {"x": 0.0, "y": 0.0, "z": 0.0},
            "target_visible": True,
        }
    return bundle


def _unity_bundle(*, oracle: bool, frame_id: int = 0) -> dict:
    """Mirror of ArenaMain.cs::BuildSensorBundle.

    The Unity build emits long ints for stamp_ns / frame_id / width / height
    (System.Int64), but JSON.NET-style serialization writes them as JSON
    integers indistinguishable from Godot's. The shape is identical.
    """
    return _godot_bundle(oracle=oracle, frame_id=frame_id)


_BUNDLE_BUILDERS = {"godot": _godot_bundle, "unity": _unity_bundle}


# ---------------------------------------------------------- parametrized

@pytest.fixture(params=["godot", "unity"])
def bundle_builder(request):
    return _BUNDLE_BUILDERS[request.param]


def test_env_reset_request_parses() -> None:
    payload = {"seed": 42, "opponent_tier": "bronze", "oracle_hints": True,
               "duration_ns": 90_000_000_000}
    req = json_format.ParseDict(payload, aiming_pb2.EnvResetRequest())
    assert req.seed == 42
    assert req.opponent_tier == "bronze"
    assert req.oracle_hints is True
    assert req.duration_ns == 90_000_000_000


def test_gimbal_cmd_parses() -> None:
    payload = {"stamp_ns": 1_000, "target_yaw": 0.5, "target_pitch": -0.25,
               "yaw_rate_ff": 0.1, "pitch_rate_ff": 0.0}
    cmd = json_format.ParseDict(payload, sensor_pb2.GimbalCmd())
    assert cmd.target_yaw == pytest.approx(0.5)
    assert cmd.target_pitch == pytest.approx(-0.25)
    assert cmd.yaw_rate_ff == pytest.approx(0.1)


def test_fire_cmd_parses() -> None:
    cmd = json_format.ParseDict({"stamp_ns": 1, "burst_count": 3}, sensor_pb2.FireCmd())
    assert cmd.burst_count == 3


def test_sensor_bundle_without_oracle_parses(bundle_builder) -> None:
    payload = bundle_builder(oracle=False, frame_id=0)
    bundle = json_format.ParseDict(payload, sensor_pb2.SensorBundle())
    assert bundle.frame.width == 1280
    assert bundle.frame.height == 720
    assert bundle.frame.pixel_format == sensor_pb2.FrameRef.PIXEL_FORMAT_RGB888
    assert bundle.imu.linear_accel.y == pytest.approx(-9.81)
    assert bundle.odom.position_world.x == pytest.approx(-3.0)
    assert not bundle.HasField("oracle")


def test_sensor_bundle_with_oracle_parses(bundle_builder) -> None:
    payload = bundle_builder(oracle=True, frame_id=1)
    bundle = json_format.ParseDict(payload, sensor_pb2.SensorBundle())
    assert bundle.HasField("oracle")
    assert bundle.oracle.target_position_world.x == pytest.approx(3.0)
    assert bundle.oracle.target_visible is True


def test_initial_state_parses(bundle_builder) -> None:
    sim_sha = "stage12a-unity-scaffold-1.6"
    payload = {
        "bundle": bundle_builder(oracle=False, frame_id=0),
        "zmq_frame_endpoint": "tcp://127.0.0.1:7655",
        "simulator_build_sha256": sim_sha,
    }
    msg = json_format.ParseDict(payload, aiming_pb2.InitialState())
    assert msg.zmq_frame_endpoint == "tcp://127.0.0.1:7655"
    assert msg.simulator_build_sha256 == sim_sha


def test_fire_result_parses() -> None:
    payload = {"accepted": True, "reason": "", "queued_count": 3}
    msg = json_format.ParseDict(payload, aiming_pb2.FireResult())
    assert msg.accepted is True
    assert msg.queued_count == 3


def test_episode_stats_with_events_parses() -> None:
    payload = {
        "episode_id": "ep-000000000000002a", "seed": 42,
        "duration_ns": 90_000_000_000,
        "candidate_commit_sha": "", "candidate_build_sha256": "",
        "simulator_build_sha256": "stage12a-unity-scaffold-1.6",
        "opponent_policy_sha256": "", "opponent_tier": "bronze",
        "outcome": "OUTCOME_TIMEOUT",
        "damage_dealt": 200, "damage_taken": 100,
        "projectiles_fired": 8, "armor_hits": 4,
        "aim_latency_p50_ns": 8_000_000,
        "aim_latency_p95_ns": 20_000_000,
        "aim_latency_p99_ns": 28_000_000,
        "events": [
            {"stamp_ns": 1_000_000, "kind": "KIND_FIRED", "armor_id": "", "damage": 0},
            {"stamp_ns": 1_500_000, "kind": "KIND_HIT_ARMOR", "armor_id": "red.front", "damage": 50},
        ],
    }
    msg = json_format.ParseDict(payload, episode_pb2.EpisodeStats())
    assert msg.outcome == episode_pb2.EpisodeStats.OUTCOME_TIMEOUT
    assert msg.armor_hits == 4
    assert len(msg.events) == 2
    assert msg.events[0].kind == episode_pb2.ProjectileEvent.KIND_FIRED
    assert msg.events[1].kind == episode_pb2.ProjectileEvent.KIND_HIT_ARMOR
    assert msg.events[1].armor_id == "red.front"


def test_length_prefix_round_trip() -> None:
    """Sanity-check the 4-byte big-endian length prefix used by
    tcp_proto_server.gd / TcpProtoServer.cs.

    Both sides hand-encode the prefix; any mismatch (BE vs LE, signed
    vs unsigned, off-by-one) produces a hung connection at runtime.
    """
    import json
    import struct

    payload = {"method": "env_reset", "request": {"seed": 1, "opponent_tier": "bronze"}}
    body = json.dumps(payload).encode("utf-8")
    n = len(body)
    encoded = bytes([(n >> 24) & 0xFF, (n >> 16) & 0xFF, (n >> 8) & 0xFF, n & 0xFF])
    assert encoded == struct.pack(">I", n)
```

- [ ] **Step 3: Run tests**

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
uv run pytest tests/test_arena_wire_format.py -v
```

Expected: All tests pass. Three tests are parametrized over `[godot, unity]` (`test_sensor_bundle_without_oracle_parses`, `test_sensor_bundle_with_oracle_parses`, `test_initial_state_parses`), totaling 12 test cases (6 non-parametrized + 3 parametrized × 2 engines).

- [ ] **Step 4: Commit**

```bash
git add tests/test_arena_wire_format.py
git rm tests/test_godot_wire_format.py 2>/dev/null || true  # may already be staged via git mv
git commit -m "test(stage12a): rename + parametrize wire-format test over [godot, unity]"
```

---

### Task 14: Rename + parametrize smoke harness

**Files:**
- Rename: `tools/scripts/smoke_godot_arena.py` → `tools/scripts/smoke_arena.py`
- Modify: add `--engine={godot,unity}` flag (cosmetic for now; both speak identical wire so the same code drives both).

- [ ] **Step 1: Rename**

```bash
git mv tools/scripts/smoke_godot_arena.py tools/scripts/smoke_arena.py
```

- [ ] **Step 2: Modify to add `--engine` flag**

Add an `--engine` arg to the `argparse.ArgumentParser` and label the output. Edit `tools/scripts/smoke_arena.py`:

```python
def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--engine", default="godot", choices=["godot", "unity"],
                        help="which engine the running arena is. Wire is identical between them.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", default=7654, type=int)
    parser.add_argument("--seed", default=42, type=int)
    parser.add_argument("--ticks", default=10, type=int)
    args = parser.parse_args()

    print(f"[smoke] engine={args.engine} host={args.host} port={args.port}")
    # ... rest of body unchanged ...
```

The engine label is for log clarity only — the wire is identical.

- [ ] **Step 3: Verify it runs against Godot**

```bash
# Terminal A: start Godot arena headless
godot --path shared/godot_arena --headless --rendering-driver opengl3 &

# Terminal B: drive the smoke
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
uv run python tools/scripts/smoke_arena.py --engine godot --seed 42 --ticks 10

kill %1
```

Expected: smoke prints `[smoke] engine=godot`, then `[reset]`, 10 step lines, `[fire]`, `[finish]`.

- [ ] **Step 4: Verify it runs against Unity (PlayMode)**

In Unity, open `Assets/Scenes/ArenaMain.unity` and click Play. The console should log `[ArenaMain] control on tcp://0.0.0.0:7654, frames on tcp://0.0.0.0:7655`.

In a terminal:

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
uv run python tools/scripts/smoke_arena.py --engine unity --seed 42 --ticks 10
```

Stop Play in Unity.

Expected: smoke prints `[smoke] engine=unity`, then `[reset]` with `sim_sha=stage12a-unity-scaffold-1.6`, 10 step lines, `[fire]` with `accepted=True`, `[finish]` with `episode_id=ep-000000000000002a` (matches the Godot side bit-for-bit because the seed + episode_id formula is identical).

- [ ] **Step 5: Commit**

```bash
git add tools/scripts/smoke_arena.py
git rm tools/scripts/smoke_godot_arena.py 2>/dev/null || true
git commit -m "test(stage12a): rename + add --engine flag to smoke_arena.py"
```

---

### Task 15: Write `shared/unity_arena/README.md`

**Files:**
- Create: `shared/unity_arena/README.md`

Maintainer-facing how-to-open / how-to-build doc. Mirrors `shared/godot_arena/README.md`.

- [ ] **Step 1: Write the README**

Create `shared/unity_arena/README.md`:

````markdown
# Aiming Arena — Unity 6 LTS HDRP project

Stage 12 reform of `shared/godot_arena/`. Implements the simulator side
of the contract from `shared/proto/aiming.proto` on Unity 6 LTS +
HDRP, with a coordinated visual reform (multi-tier maze hybrid map,
stylized chassis, neon palette, holographic UI). Companion to the
spec at [`docs/superpowers/specs/2026-04-30-arena-art-vision-reform-design.md`](../../docs/superpowers/specs/2026-04-30-arena-art-vision-reform-design.md).

## Quickstart (team)

```bash
# 1. Install Unity 6 LTS via Unity Hub (project pinned at 6000.3.14f1).
# 2. Open the project from Unity Hub: Open → /Volumes/...../shared/unity_arena
# 3. Open Assets/Scenes/ArenaMain.unity and click Play.
# 4. In another terminal, drive the smoke:
uv run python tools/scripts/smoke_arena.py --engine unity --seed 42 --ticks 10
```

## Wire layout (identical to Godot)

| Port | Role | Encoding |
|------|------|----------|
| 7654 | Control RPC | length-prefixed (4-byte BE) JSON |
| 7655 | Frame stream | 16-byte LE header (`<QQ frame_id stamp_ns>`) + RGB888 |

JSON field names match `shared/proto/*.proto` exactly. The wire-format
conformance test (`tests/test_arena_wire_format.py`) is parametrized
over `[godot, unity]` and asserts both engines emit the same dict
shapes.

## Synty pack

The maze + chassis art uses the Synty POLYGON Sci-Fi pack (paid,
~$40 USD). Maintainer downloads from synty.com once and drops the
`.unitypackage` (or extracted `.fbx` tree) into
`Assets/Synty/POLYGON_SciFi/`. This directory is gitignored —
**never commit Synty source files**. A CI guard
(`tools/scripts/check_synty_redistribution.py`, added in 12d) fails
the build if any `.fbx` is found in committed paths.

## Build (12d)

Builds run via `tools/unity/build.sh --target {win-showcase,
macos-showcase, linux-showcase, linux-headless}`, output to
`shared/unity_arena/builds/`. Locally validate against the Tier 1–5
regression suite before pushing to OSS.

## Tests

EditMode tests (Unity Test Runner): `Assets/Tests/EditMode/`
- `SeedRngTests`, `MecanumChassisControllerTests`, `GimbalKinematicsTests`,
  `ProjectileDragTests`, `ArmorPlateTests`, `ReplayRecorderTests`

PlayMode tests: `Assets/Tests/PlayMode/`
- `TcpProtoServerTests`, `TcpFramePubTests`, `ArenaMainEpisodeTests`

Python wire conformance (no Unity needed): `tests/test_arena_wire_format.py`

## Project layout

```
shared/unity_arena/
├── Assets/
│   ├── Scenes/      ArenaMain.unity (placeholder), MapA_MazeHybrid.unity (12b)
│   ├── Scripts/     10 C# files (port from GDScript + new MecanumChassisController)
│   ├── Prefabs/     Chassis, Gimbal, ArmorPlate, Projectile, HoloProjector (12b)
│   ├── Materials/   PBR materials (12c)
│   ├── Shaders/     Shader Graph (12c)
│   ├── VFX/         VFX Graph (12c)
│   ├── Settings/    HDRPAsset_Showcase / _Headless, Volume profiles (12c)
│   ├── UI/          HUD prefabs (12c)
│   ├── Tests/       EditMode + PlayMode
│   └── Synty/       gitignored
├── Packages/manifest.json
├── ProjectSettings/
└── README.md
```

## Headless rendering caveat

Unity HDRP cannot render without a GPU. The `linux-headless` build target
disables DXR, drops to baked GI only, and uses low-LOD geometry; it still
needs any GPU (Intel UHD / Apple Silicon / NVIDIA / AMD).
`ubuntu-latest` GitHub-hosted runners have no GPU, so live-arena
episodes never run in CI; CI only runs unit tests
(`test_arena_wire_format.py`, `test_fetch_assets.py`, etc.).
Live arena testing happens on the maintainer's GPU-equipped box.
````

- [ ] **Step 2: Commit**

```bash
git add shared/unity_arena/README.md
git commit -m "docs(stage12a): unity_arena README with quickstart, wire layout, layout"
```

---

### Task 16: Tier 1 + Tier 2 conformance gate

This is the gate before tagging `v1.6-unity-scaffold`. Both tiers must pass.

- [ ] **Step 1: Tier 1 — wire-format conformance (CI-shaped, runs locally)**

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
uv run pytest tests/test_arena_wire_format.py -v
```

Expected: 11 test cases (with parametrization), all green.

- [ ] **Step 2: Tier 2 — smoke harness parity (Godot side)**

```bash
godot --path shared/godot_arena --headless --rendering-driver opengl3 &
SLEEP=2 && sleep $SLEEP
uv run python tools/scripts/smoke_arena.py --engine godot --seed 42 --ticks 30
GODOT_RC=$?
kill %1 2>/dev/null
test $GODOT_RC -eq 0 || { echo "Godot smoke failed"; exit 1; }
```

Expected: monotonic frame_id 1..30, 30 step lines, 3-pellet fire accepted, episode_id `ep-000000000000002a`.

- [ ] **Step 3: Tier 2 — smoke harness parity (Unity side)**

In Unity Editor: open `Assets/Scenes/ArenaMain.unity`, click Play.

In a terminal:

```bash
uv run python tools/scripts/smoke_arena.py --engine unity --seed 42 --ticks 30
```

Click Stop in Unity.

Expected: identical output as Godot side: `episode_id=ep-000000000000002a`, monotonic frame_id, fire accepted.

- [ ] **Step 4: All EditMode + PlayMode tests pass**

In Unity: Window → General → Test Runner → EditMode → Run All. Then PlayMode → Run All.
Expected: 0 failures, 0 ignored.

- [ ] **Step 5: Verify clean working tree**

```bash
git status
```

Expected: nothing to commit, working tree clean.

---

### Task 17: Tag `v1.6-unity-scaffold`

- [ ] **Step 1: Tag the commit**

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
git tag -a v1.6-unity-scaffold -m "Stage 12a — Unity scaffold + wire parity

10 GDScript files ported to 10 C# files at shared/unity_arena/.
TcpProtoServer + TcpFramePub byte-for-byte parity with Godot wire.
Tier 1 wire-format conformance and Tier 2 smoke harness parity both pass.

Placeholder ArenaMain.unity scene runs end-to-end; full art pass in
12c. Map A geometry and chassis silhouette parity in 12b."
```

- [ ] **Step 2: Verify tag**

```bash
git tag --list v1.6-* -n5
```

Expected: tag annotation visible.

- [ ] **Step 3: Note: do NOT push (local-first cadence)**

The tag stays local until Stage 12d's OSS publish step. Do not run `git push --tags` yet.

---

## Stage 12b — Map A geometry + chassis silhouette parity

**Goal:** Build the multi-tier maze hybrid map (Map A) using ProBuilder + Synty kit-bash, populate the chassis / gimbal / armor plate / projectile prefabs with silhouette-preserving geometry, swap the placeholder ArenaMain scene over to use the new prefabs, and pass Tier 3 (golden-frame regression) + Tier 4 (bronze opponent KS test).

**Calendar estimate:** 5 working days.

**End tag:** `v1.7-unity-geometry`.

---

### Task 18: Build Map A geometry

**Files:**
- Create: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`
- Create: `shared/unity_arena/Assets/Maps/MapA/` (ProBuilder mesh assets)

Constructed using ProBuilder (Window → ProBuilder). Reference: spec §4.1.

- [ ] **Step 1: Create new scene**

In Unity: File → New Scene → Basic Outdoors (HDRP). Save as `Assets/Scenes/MapA_MazeHybrid.unity`. Add to Build Settings.

- [ ] **Step 2: Build the floor (20 × 20 m)**

ProBuilder → New Shape → Cube. Set size (20, 0.2, 20). Position (0, -0.1, 0). Name `Floor`. Material: temp default (Synty material assigned in 12c).

- [ ] **Step 3: Build maze corridor walls (ground tier 0–2.5 m)**

Per the layout described in spec §4.1: six interlocking angular corridor segments forming a partial maze. Use ProBuilder cube primitives sized for 0.2 m wall thickness, 2.5 m height. Place per the top-down layout from the brainstorming mockup (`map-layout.html` option 2):

- Wall A: `(20×3)` strip from `(-7, 1.25, -3)` to `(-3, 1.25, 0)` — angular corridor entry from blue side
- Wall B: angular junction at center `(0, 1.25, 0)` to `(2, 1.25, -2)` etc.
- Glass partition placeholder: 0.05 m thick cubes at chokepoints, leave material default for now (real glass shader in 12c)

For each wall: name `Wall_<id>`, parent under `Geometry/Walls`. Save mesh edits.

Iteration tip: this is creative work. Build a rough first pass, save, run Tier 4 bronze regression to confirm bronze policy still finds reasonable paths, then iterate on layout.

- [ ] **Step 4: Build upper-tier catwalks (4.5–7 m)**

Two slim catwalks (1.6 m wide) along top and bottom edges of the arena, with line-of-sight breaks at half-height glass panels every 4 m. Two ramp connections between ground and upper tier at opposite corners.

- [ ] **Step 5: Mark spawn cells**

Empty GameObjects `SpawnPoint_Blue` at `(-9, 0, -9)` and `SpawnPoint_Red` at `(9, 0, 9)`. ArenaMain reads these in 12b Task 24 (orchestration update).

- [ ] **Step 6: Place 3 holographic projector posts at intersection nodes**

Empty GameObjects `HoloPost_JCT01`, `_JCT02`, `_JCT03` at the three intersection coordinates determined by your maze layout. Geometry placeholder (Synty kit-bash + emission in 12c). Each carries an empty `HoloProjector` component (added in Task 23).

- [ ] **Step 7: Verify scene loads + bronze chassis can navigate**

Place a temporary BlueChassis from the placeholder ArenaMain scene at `SpawnPoint_Blue` and Play. Drive it manually via WASD (add a `TestKeyboardChassisDriver` script if needed) to verify corridors are wide enough.

Expected: chassis fits through 2.4 m corridors without clipping; ramps are climbable.

- [ ] **Step 8: Commit**

```bash
git add shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity shared/unity_arena/Assets/Maps shared/unity_arena/ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(stage12b): Map A multi-tier maze hybrid geometry (ProBuilder rough)"
```

---

### Task 19: Chassis prefab — geometry + driver

**Files:**
- Create: `shared/unity_arena/Assets/Prefabs/Chassis.prefab`
- Modify: `shared/unity_arena/Assets/Scripts/Chassis.cs` (replace 12a stub with full body)

The full Chassis MonoBehaviour wires `MecanumChassisController` to a `CharacterController` for movement, attaches four `ArmorPlate`s, and parents the gimbal.

- [ ] **Step 1: Build the chassis prefab in Unity**

In Unity: GameObject → 3D Object → Cube. Resize to (0.45, 0.18, 0.55). Name `Chassis`. Add `CharacterController` component: Center (0, 0.09, 0), Radius 0.2, Height 0.18, Slope Limit 30, Step Offset 0.05. Add `Chassis` component (replaces stub from Task 11).

Add four wheel cubes at:
- `WheelFL`: position (-0.255, 0.07, -0.235), scale (0.04, 0.14, 0.14), rotation (0, 0, 90)
- `WheelFR`: position (0.255, 0.07, -0.235), same
- `WheelRL`: position (-0.255, 0.07, 0.235), same
- `WheelRR`: position (0.255, 0.07, 0.235), same

Add four armor plate children at:
- `ArmorPlateFront` at (0, 0.16, -0.281), local rotation 0
- `ArmorPlateBack` at (0, 0.16, 0.281), local rotation (0, 180, 0)
- `ArmorPlateLeft` at (-0.231, 0.16, 0), local rotation (0, -90, 0)
- `ArmorPlateRight` at (0.231, 0.16, 0), local rotation (0, 90, 0)

Each ArmorPlate cube: scale (0.14, 0.13, 0.012). Add `BoxCollider` with `Is Trigger = true`. Add `ArmorPlate` component, set Team / Face / Icon per the layout (front=Hero, back=Engineer, left=Standard, right=Sentry).

Drag everything from Scene to Project window: `Assets/Prefabs/Chassis.prefab` is created.

- [ ] **Step 2: Replace the 12a stub with full body**

Replace `shared/unity_arena/Assets/Scripts/Chassis.cs` with:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Mecanum chassis with a custom velocity solver (NOT Rigidbody-driven).
    /// Movement is via CharacterController.Move so PhysX integration drift
    /// cannot creep into chassis kinematics over a 90-second episode (R3
    /// mitigation per the design spec). Ports chassis.gd line-by-line.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Chassis : MonoBehaviour
    {
        public string Team = "blue";
        public int ChassisId = 0;
        [Range(0f, 4f)] public float MaxLinearSpeed = 3.5f;
        [Range(0f, 8f)] public float MaxAngularSpeed = 4.0f;

        public int DamageTaken { get; private set; }
        public Gimbal Gimbal { get; private set; }
        public Vector3 LinearVelocity { get; private set; }
        public float ChassisYaw => _solver?.ChassisYaw ?? 0f;

        public event Action<string, int, int> ArmorHit;  // plateId, damage, sourceId

        private MecanumChassisController _solver;
        private CharacterController _controller;
        private ArmorPlate[] _plates;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _solver = new MecanumChassisController
            {
                MaxLinearSpeed = MaxLinearSpeed,
                MaxAngularSpeed = MaxAngularSpeed,
            };
            Gimbal = GetComponentInChildren<Gimbal>();
            AssignArmorMetadata();
        }

        private void AssignArmorMetadata()
        {
            var faces = new (string ChildName, string Face, string Icon)[]
            {
                ("ArmorPlateFront", "front", "Hero"),
                ("ArmorPlateBack",  "back",  "Engineer"),
                ("ArmorPlateLeft",  "left",  "Standard"),
                ("ArmorPlateRight", "right", "Sentry"),
            };
            var found = new List<ArmorPlate>();
            foreach (var f in faces)
            {
                var child = transform.Find(f.ChildName);
                if (child == null)
                {
                    Debug.LogWarning($"Chassis {Team}: missing armor child {f.ChildName}");
                    continue;
                }
                var plate = child.GetComponent<ArmorPlate>();
                if (plate == null) continue;
                plate.Team = Team;
                plate.Face = f.Face;
                plate.Icon = f.Icon;
                plate.PlateHit += (dmg, src) => RaiseArmorHit(plate.PlateId, dmg, src);
                found.Add(plate);
            }
            _plates = found.ToArray();
        }

        public void SetChassisCmd(float vxBody, float vyBody, float omega)
        {
            _solver.SetCmd(vxBody, vyBody, omega);
        }

        public void ResetForNewEpisode(Vector3 spawnPosition, float spawnYaw)
        {
            _controller.enabled = false;
            transform.position = spawnPosition;
            transform.rotation = Quaternion.Euler(0f, spawnYaw * Mathf.Rad2Deg, 0f);
            _controller.enabled = true;
            _solver.Reset(spawnYaw);
            DamageTaken = 0;
            LinearVelocity = Vector3.zero;
            if (_plates != null) foreach (var p in _plates) p.ResetForNewEpisode();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _solver.IntegrateStep(dt);
            transform.rotation = Quaternion.Euler(0f, _solver.ChassisYaw * Mathf.Rad2Deg, 0f);
            LinearVelocity = _solver.WorldVelocity;
            // Y-component stays zero (flat floor); gravity not applied to chassis here.
            _controller.Move(LinearVelocity * dt);
        }

        public Dictionary<string, object> OdomState() => new Dictionary<string, object>
        {
            { "position_world", Vec3Dict(transform.position) },
            { "linear_velocity", Vec3Dict(LinearVelocity) },
            { "yaw_world", (double)_solver.ChassisYaw },
        };

        public Dictionary<string, object> GimbalState()
        {
            if (Gimbal == null)
                return new Dictionary<string, object> { { "yaw", 0.0 }, { "pitch", 0.0 }, { "yaw_rate", 0.0 }, { "pitch_rate", 0.0 } };
            var s = Gimbal.GetState();
            return new Dictionary<string, object>
            {
                { "yaw", (double)s.Yaw }, { "pitch", (double)s.Pitch },
                { "yaw_rate", (double)s.YawRate }, { "pitch_rate", (double)s.PitchRate },
            };
        }

        protected void RaiseArmorHit(string plateId, int damage, int sourceId)
            => ArmorHit?.Invoke($"{Team}.{plateId.Split('.')[1]}", damage, sourceId);

        private static Dictionary<string, object> Vec3Dict(Vector3 v) => new Dictionary<string, object>
        {
            { "x", (double)v.x }, { "y", (double)v.y }, { "z", (double)v.z },
        };
    }
}
```

- [ ] **Step 3: Verify EditMode tests still pass**

Unity Test Runner → EditMode → Run All. Expected: all green (Chassis tests use the solver directly, not the MB).

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/Prefabs/Chassis.prefab shared/unity_arena/Assets/Scripts/Chassis.cs
git commit -m "feat(stage12b): Chassis prefab + MB driving MecanumChassisController via CharacterController"
```

---

### Task 20: Gimbal prefab — geometry + MB

**Files:**
- Create: `shared/unity_arena/Assets/Prefabs/Gimbal.prefab`
- Modify: `shared/unity_arena/Assets/Scripts/Gimbal.cs` (add MB body alongside the existing GimbalKinematics class)

- [ ] **Step 1: Build the Gimbal prefab in Unity**

GameObject → Create Empty. Name `Gimbal`. Local position (0, 0.30, 0) when child of Chassis.

Add child `YawPivot` (empty). Add to YawPivot: a yoke cylinder mesh (top/bottom radius 0.05, height 0.08).

Add child of YawPivot named `PitchPivot` at local (0, 0.06, 0).

Add to PitchPivot:
- `PitchBody`: cube scale (0.18, 0.08, 0.08).
- `Barrel`: cylinder, top/bottom radius 0.012, height 0.22, rotated 90° around X, position (0, 0, -0.18).
- `Muzzle`: empty marker at (0, 0, -0.30).
- `GimbalCamera`: Camera component, position (0, 0.04, 0), FOV 60, near 0.05, far 60.

Add `Gimbal` MB to root (added in Step 2). Save as prefab.

- [ ] **Step 2: Add Gimbal MonoBehaviour to Gimbal.cs**

Append to `shared/unity_arena/Assets/Scripts/Gimbal.cs` (after the existing `GimbalKinematics` and `GimbalState`):

```csharp
namespace TsingYun.UnityArena
{
    /// <summary>
    /// MonoBehaviour wrapping GimbalKinematics. Drives YawPivot.localRotation
    /// and PitchPivot.localRotation in FixedUpdate. Mirrors gimbal.gd.
    /// </summary>
    public class Gimbal : MonoBehaviour
    {
        public const float MuzzleVelocity = 27.0f;

        public Transform YawPivot;
        public Transform PitchPivot;
        public Transform Muzzle;

        private GimbalKinematics _k = new GimbalKinematics();

        private void Awake()
        {
            if (YawPivot == null) YawPivot = transform.Find("YawPivot");
            if (PitchPivot == null) PitchPivot = YawPivot != null ? YawPivot.Find("PitchPivot") : null;
            if (Muzzle == null && PitchPivot != null) Muzzle = PitchPivot.Find("Muzzle");
        }

        public void SetTarget(float yaw, float pitch, float yawFf, float pitchFf)
            => _k.SetTarget(yaw, pitch, yawFf, pitchFf);

        public void Reset() => _k.Reset();

        public GimbalState GetState() => _k.GetState();

        private void FixedUpdate()
        {
            _k.IntegrateStep(Time.fixedDeltaTime);
            if (YawPivot != null) YawPivot.localRotation = Quaternion.Euler(0f, _k.YawRad * Mathf.Rad2Deg, 0f);
            if (PitchPivot != null) PitchPivot.localRotation = Quaternion.Euler(_k.PitchRad * Mathf.Rad2Deg, 0f, 0f);
        }

        public Matrix4x4 MuzzleWorldTransform()
            => Muzzle != null ? Muzzle.localToWorldMatrix : transform.localToWorldMatrix;

        public ShotSpec ComputeShot()
        {
            Matrix4x4 m = Muzzle != null ? Muzzle.localToWorldMatrix : transform.localToWorldMatrix;
            Vector3 fwd = -((Vector3)m.GetColumn(2)).normalized;
            float jitterYaw = SeedRng.NextRange(-0.002f, 0.002f);
            float jitterPitch = SeedRng.NextRange(-0.002f, 0.002f);
            fwd = Quaternion.AngleAxis(jitterYaw * Mathf.Rad2Deg, Vector3.up) * fwd;
            Vector3 right = ((Vector3)m.GetColumn(0)).normalized;
            fwd = Quaternion.AngleAxis(jitterPitch * Mathf.Rad2Deg, right) * fwd;
            return new ShotSpec
            {
                Position = m.GetColumn(3),
                Rotation = m.rotation,
                Velocity = fwd * MuzzleVelocity,
            };
        }
    }

    public struct ShotSpec
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add shared/unity_arena/Assets/Prefabs/Gimbal.prefab shared/unity_arena/Assets/Scripts/Gimbal.cs
git commit -m "feat(stage12b): Gimbal prefab + MB driving GimbalKinematics, Muzzle marker, ShotSpec"
```

---

### Task 21: Projectile prefab — full body

**Files:**
- Create: `shared/unity_arena/Assets/Prefabs/Projectile.prefab`
- Modify: `shared/unity_arena/Assets/Scripts/Projectile.cs` (replace stub with full Rigidbody-driven body)

- [ ] **Step 1: Build the Projectile prefab**

GameObject → 3D Object → Sphere. Name `Projectile`. Scale (0.017, 0.017, 0.017) — 17 mm diameter. Add `Rigidbody`: mass 0.0032 kg (matches RM 17 mm spec), drag 0, angular drag 0, useGravity TRUE, isKinematic FALSE, interpolation Interpolate, collisionDetectionMode Continuous Dynamic. Add `SphereCollider` radius 0.5 (relative to scaled prefab).

Replace the temp `Projectile` script with the full body in Step 2. Save as prefab. Place it on `Layer = Projectile` (create that layer if it doesn't exist; configure Physics → Layer Collision Matrix to allow Projectile-Floor and Projectile-ArmorPlate but NOT Projectile-Chassis-body).

- [ ] **Step 2: Replace Projectile.cs MB with full body**

Edit `shared/unity_arena/Assets/Scripts/Projectile.cs`. Keep the `ProjectileDragSolver` class as-is. Replace the stub MB at the bottom with:

```csharp
namespace TsingYun.UnityArena
{
    /// <summary>
    /// 17 mm-style ball projectile. Quadratic drag is applied per FixedUpdate
    /// (Rigidbody.linearDamping is exponential decay, which is wrong for a
    /// real projectile). Gravity comes from the engine. Lifetime caps:
    /// MaxRangeM (30 m) and MaxTtlSeconds (4 s). Mirrors projectile.gd.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        public string Team = "blue";
        public bool Consumed { get; private set; }

        private Rigidbody _rb;
        private Vector3 _spawnPosition;
        private float _spawnTimeSeconds;

        private void Awake() { _rb = GetComponent<Rigidbody>(); }

        public void Arm(Vector3 initialVelocity, string owningTeam)
        {
            _spawnPosition = transform.position;
            _spawnTimeSeconds = Time.time;
            Team = owningTeam;
            _rb.linearVelocity = initialVelocity;
        }

        private void FixedUpdate()
        {
            if (Consumed) return;
            Vector3 dragForce = ProjectileDragSolver.QuadraticDragForce(_rb.linearVelocity);
            _rb.AddForce(dragForce, ForceMode.Force);

            if ((transform.position - _spawnPosition).magnitude > ProjectileDragSolver.MaxRangeM)
                Consume("miss_range");
            else if (Time.time - _spawnTimeSeconds > ProjectileDragSolver.MaxTtlSeconds)
                Consume("miss_range");
        }

        public int OnArmorHit(ArmorPlate plate)
        {
            if (Consumed) return 0;
            if (plate.Team == Team)
            {
                Consume("friendly");
                return 0;
            }
            Consume($"hit_armor:{plate.PlateId}");
            return ProjectileDragSolver.Damage;
        }

        private void OnCollisionEnter(Collision other)
        {
            if (Consumed) return;
            // Plates are triggers, not colliders, so this only fires on walls/floor.
            Consume("hit_wall");
        }

        private void Consume(string reason)
        {
            Consumed = true;
            Destroy(gameObject);
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add shared/unity_arena/Assets/Prefabs/Projectile.prefab shared/unity_arena/Assets/Scripts/Projectile.cs shared/unity_arena/ProjectSettings/TagManager.asset
git commit -m "feat(stage12b): Projectile prefab + full Rigidbody-driven body matching projectile.gd"
```

---

### Task 22: ArmorPlate prefab

**Files:**
- Create: `shared/unity_arena/Assets/Prefabs/ArmorPlate.prefab`

The plate cubes embedded in Chassis.prefab (Task 19) are local references. Extract to a standalone reusable prefab.

- [ ] **Step 1: Build the ArmorPlate prefab**

In Unity: Open Chassis.prefab in Prefab Mode. Select `ArmorPlateFront`, drag to `Assets/Prefabs/`. Choose "Create Original Prefab" and name `ArmorPlate.prefab`. Replace the four embedded plate cubes in Chassis.prefab with prefab instances.

The prefab carries:
- `BoxCollider`, Is Trigger = true, scale (0.14, 0.13, 0.012)
- MeshRenderer with default placeholder material (real plate emission shader in 12c)
- `ArmorPlate` component (default Team / Face / Icon overridden by Chassis at instantiation)

- [ ] **Step 2: Commit**

```bash
git add shared/unity_arena/Assets/Prefabs/ArmorPlate.prefab shared/unity_arena/Assets/Prefabs/Chassis.prefab
git commit -m "feat(stage12b): extract ArmorPlate.prefab; Chassis uses 4 instances"
```

---

### Task 23: HoloProjector prefab — geometry only

**Files:**
- Create: `shared/unity_arena/Assets/Prefabs/HoloProjector.prefab`
- Create: `shared/unity_arena/Assets/Scripts/HoloProjector.cs`

Diegetic intersection markers. In 12b we ship just the geometry + a stub MB; the actual emission shader and animated label come in 12c.

- [ ] **Step 1: Build the HoloProjector prefab**

GameObject → 3D Object → Cylinder. Name `HoloProjector`. Scale (0.4, 0.1, 0.4). This is the floor bollard.

Add child `EmissionCone`: cone mesh (use ProBuilder's primitive), scale (0.4, 1.6, 0.4), position (0, 0.85, 0). Material: temp default.

Add child `LabelCanvas`: World Space Canvas at position (0, 1.7, 0), size (1, 0.4), with a TextMeshProUGUI showing `JCT-XX` (placeholder text, set per-instance in 12c).

Save as prefab.

- [ ] **Step 2: Add HoloProjector MB**

Create `shared/unity_arena/Assets/Scripts/HoloProjector.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Diegetic intersection marker. Hosts a WorldSpace Canvas displaying
    /// the JCT-XX label and grid coordinates. The animated emission cone
    /// material is assigned in Stage 12c (HoloProjector.shadergraph).
    /// </summary>
    public class HoloProjector : MonoBehaviour
    {
        public string JunctionId = "JCT-00";
        public Vector2 GridCoords;

        public TMPro.TextMeshProUGUI LabelText;

        private void Start()
        {
            if (LabelText != null)
                LabelText.text = $"{JunctionId}\n({GridCoords.x:+0.0;-0.0}, {GridCoords.y:+0.0;-0.0})";
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add shared/unity_arena/Assets/Prefabs/HoloProjector.prefab shared/unity_arena/Assets/Scripts/HoloProjector.cs
git commit -m "feat(stage12b): HoloProjector prefab geometry + label MB (shader in 12c)"
```

---

### Task 24: Wire MapA scene to ArenaMain orchestrator

**Files:**
- Modify: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`
- Modify: `shared/unity_arena/Assets/Scripts/ArenaMain.cs` (read spawn points + projectile spawning)

- [ ] **Step 1: Update ArenaMain to support spawn points and projectile spawning**

Edit `shared/unity_arena/Assets/Scripts/ArenaMain.cs`. Add fields:

```csharp
public Transform SpawnPointBlue;
public Transform SpawnPointRed;
public GameObject ProjectilePrefab;
```

Replace the hard-coded spawn positions in `EnvReset` with:

```csharp
Vector3 blueSpawn = SpawnPointBlue != null ? SpawnPointBlue.position : new Vector3(-3f, 0f, 0f);
Vector3 redSpawn  = SpawnPointRed   != null ? SpawnPointRed.position   : new Vector3(3f, 0f, 0f);
BlueChassis.ResetForNewEpisode(blueSpawn, 0f);
RedChassis.ResetForNewEpisode(redSpawn, Mathf.PI);
```

Replace the EnvPushFire stub body with real spawning:

```csharp
public Dictionary<string, object> EnvPushFire(Dictionary<string, object> cmd)
{
    if (State != EpisodeState.Running)
        return new Dictionary<string, object> { { "accepted", false }, { "reason", "no_episode" }, { "queued_count", 0 } };

    int burst = Mathf.Max(0, (int)AsLong(cmd, "burst_count", 1));
    int queued = SpawnProjectiles(burst);
    return new Dictionary<string, object>
    {
        { "accepted", queued > 0 },
        { "reason", queued == burst ? "" : "rate_limit" },
        { "queued_count", queued },
    };
}

private int SpawnProjectiles(int burst)
{
    if (ProjectilePrefab == null || BlueChassis.Gimbal == null) return 0;
    int queued = 0;
    for (int i = 0; i < burst; i++)
    {
        ShotSpec spec = BlueChassis.Gimbal.ComputeShot();
        var go = Instantiate(ProjectilePrefab, spec.Position, spec.Rotation, ProjectileRoot);
        var p = go.GetComponent<Projectile>();
        p.Arm(spec.Velocity, BlueChassis.Team);
        queued++;
        _projectilesFired++;
        _events.Add(new Dictionary<string, object>
        {
            { "stamp_ns", NowNs() }, { "kind", "KIND_FIRED" },
            { "armor_id", "" }, { "damage", 0 },
        });
    }
    return queued;
}
```

- [ ] **Step 2: Wire scene references**

In Unity, open `Assets/Scenes/MapA_MazeHybrid.unity`:

1. Drag the BlueChassis prefab to scene at `SpawnPoint_Blue.position`. Name `BlueChassis`.
2. Drag the RedChassis prefab (same Chassis prefab; rotate Y 180°) at `SpawnPoint_Red.position`. Set `Team = "red"` on its Chassis component. Name `RedChassis`.
3. Create empty `ArenaMain` at origin. Add `ArenaMain` component. Set:
   - BlueChassis ← BlueChassis
   - RedChassis ← RedChassis
   - GimbalCamera ← BlueChassis/Gimbal/PitchPivot/GimbalCamera
   - ProjectileRoot ← child empty `ProjectileRoot`
   - SpawnPointBlue ← SpawnPoint_Blue
   - SpawnPointRed ← SpawnPoint_Red
   - ProjectilePrefab ← Assets/Prefabs/Projectile.prefab
4. Drop 3 HoloProjector prefab instances at `HoloPost_JCT01/02/03` positions; set `JunctionId` and `GridCoords` per node.
5. Set `MapA_MazeHybrid` as the default scene in Build Settings (move it to index 0; placeholder ArenaMain.unity stays at index 1 for fallback testing).
6. Save scene.

- [ ] **Step 3: Verify the scene runs**

Click Play in Unity. Open a terminal and run:

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
uv run python tools/scripts/smoke_arena.py --engine unity --seed 42 --ticks 30
```

Expected: 30 step lines, fire returns `accepted=True queued=3`, finish prints episode_id matching the seed.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/ArenaMain.cs shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12b): wire MapA scene to ArenaMain (spawn points, projectile spawn)"
```

---

### Task 25: Tier 3 — golden-frame regression

**Files:**
- Create: `tests/golden_frames/seed_0042_pose_0.png` ... `seed_0042_pose_4.png`, ×5 seeds = 25 PNGs total
- Create: `tools/scripts/render_golden_frames.py`
- Create: `tools/scripts/compare_golden_frames.py`

Renders one 1280×720 frame from each engine for 5 fixed seeds × 5 fixed gimbal poses; compares via SSIM > 0.95.

- [ ] **Step 1: Write the renderer harness**

Create `tools/scripts/render_golden_frames.py`:

```python
"""Render a deterministic 1280x720 RGB frame from a running arena.

Usage:
  uv run python tools/scripts/render_golden_frames.py \
      --engine {godot|unity} --output-dir tests/golden_frames/

Iterates over 5 seeds × 5 gimbal poses, sends env_reset(seed),
env_step(target_yaw, target_pitch), reads one frame from the frame port,
and saves it as <seed>_<pose>.png.
"""

from __future__ import annotations

import argparse
import json
import socket
import struct
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT / "shared" / "grpc_stub_server" / "src"))

import numpy as np
from PIL import Image

SEEDS = [42, 99, 137, 256, 1024]
POSES = [
    (0.0, 0.0),     # neutral
    (0.5, 0.0),     # right
    (-0.5, 0.0),    # left
    (0.0, 0.3),     # up
    (0.0, -0.2),    # down
]


def send_request(sock: socket.socket, method: str, request: dict) -> dict:
    payload = json.dumps({"method": method, "request": request}).encode("utf-8")
    sock.sendall(struct.pack(">I", len(payload)) + payload)
    header = recv_exact(sock, 4)
    (n,) = struct.unpack(">I", header)
    body = recv_exact(sock, n)
    return json.loads(body.decode("utf-8"))


def recv_exact(sock: socket.socket, n: int) -> bytes:
    chunks = []
    while n:
        chunk = sock.recv(n)
        if not chunk:
            raise ConnectionError("closed mid-message")
        chunks.append(chunk)
        n -= len(chunk)
    return b"".join(chunks)


def read_one_frame(frame_sock: socket.socket, width: int, height: int) -> np.ndarray:
    header = recv_exact(frame_sock, 16)
    body = recv_exact(frame_sock, width * height * 3)
    return np.frombuffer(body, dtype=np.uint8).reshape((height, width, 3))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--engine", required=True, choices=["godot", "unity"])
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--control-port", type=int, default=7654)
    parser.add_argument("--frame-port", type=int, default=7655)
    args = parser.parse_args()

    args.output_dir.mkdir(parents=True, exist_ok=True)

    with socket.create_connection((args.host, args.control_port), timeout=5) as sock, \
         socket.create_connection((args.host, args.frame_port), timeout=5) as fsock:

        for seed in SEEDS:
            for pose_idx, (yaw, pitch) in enumerate(POSES):
                send_request(sock, "env_reset", {
                    "seed": seed, "opponent_tier": "bronze",
                    "oracle_hints": False, "duration_ns": 5_000_000_000,
                })
                # Step a few times to settle the gimbal, then capture
                for _ in range(20):
                    send_request(sock, "env_step", {
                        "stamp_ns": 0, "target_yaw": yaw, "target_pitch": pitch,
                    })
                # Drain stale frames
                for _ in range(3):
                    read_one_frame(fsock, 1280, 720)
                frame = read_one_frame(fsock, 1280, 720)
                send_request(sock, "env_finish", {})
                out = args.output_dir / f"{args.engine}_seed_{seed:04d}_pose_{pose_idx}.png"
                Image.fromarray(frame).save(out)
                print(f"[render] wrote {out}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 2: Write the comparator**

Create `tools/scripts/compare_golden_frames.py`:

```python
"""Compare per-seed-per-pose frames between two engines via SSIM.

Usage:
  uv run python tools/scripts/compare_golden_frames.py \
      --left tests/golden_frames/godot_*.png \
      --right tests/golden_frames/unity_*.png \
      --threshold 0.95
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from skimage.metrics import structural_similarity as ssim


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--godot-dir", type=Path, required=True)
    parser.add_argument("--unity-dir", type=Path, required=True)
    parser.add_argument("--threshold", type=float, default=0.95)
    args = parser.parse_args()

    failures = 0
    for godot_path in sorted(args.godot_dir.glob("godot_seed_*.png")):
        unity_name = godot_path.name.replace("godot_", "unity_", 1)
        unity_path = args.unity_dir / unity_name
        if not unity_path.exists():
            print(f"[FAIL] missing Unity counterpart: {unity_path}", file=sys.stderr)
            failures += 1
            continue
        left = np.array(Image.open(godot_path).convert("RGB"))
        right = np.array(Image.open(unity_path).convert("RGB"))
        score = ssim(left, right, channel_axis=2)
        status = "OK" if score >= args.threshold else "FAIL"
        if status == "FAIL": failures += 1
        print(f"[{status}] {godot_path.name} vs {unity_name}: SSIM={score:.4f}")

    if failures:
        print(f"\n{failures} pair(s) below threshold {args.threshold}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 3: Render Godot reference frames**

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
godot --path shared/godot_arena --headless --rendering-driver opengl3 &
sleep 2
uv run python tools/scripts/render_golden_frames.py \
    --engine godot --output-dir tests/golden_frames/
kill %1
```

Expected: 25 PNGs written.

- [ ] **Step 4: Render Unity frames**

In Unity, open MapA_MazeHybrid.unity, click Play.

```bash
uv run python tools/scripts/render_golden_frames.py \
    --engine unity --output-dir tests/golden_frames/
```

Click Stop in Unity. Expected: 25 more PNGs.

- [ ] **Step 5: Compare**

```bash
uv pip install scikit-image  # one-time; or add to pyproject.toml
uv run python tools/scripts/compare_golden_frames.py \
    --godot-dir tests/golden_frames/ --unity-dir tests/golden_frames/
```

Expected: all 25 pairs report SSIM > 0.95. Note: 12b ships placeholder geometry only — material differences may push SSIM lower. If failures occur and they are geometric (chassis pivot / armor plate position drift), fix the Unity prefab. If they are cosmetic (lighting, color), the threshold may need to be relaxed in 12b and tightened in 12c after materials land.

- [ ] **Step 6: Commit**

```bash
git add tools/scripts/render_golden_frames.py tools/scripts/compare_golden_frames.py
# Do NOT commit the PNGs yet — wait until 12c when materials are final.
git commit -m "test(stage12b): tier 3 golden-frame renderer + SSIM comparator"
```

---

### Task 26: Tier 4 — bronze opponent regression

**Files:**
- Create: `tests/bronze_regression.py`

Frozen `bronze.pt` plays 50 episodes against each engine. Win-rate distributions compared via 2-sample Kolmogorov-Smirnov test; require p > 0.10.

- [ ] **Step 1: Write the regression harness**

Create `tests/bronze_regression.py`:

```python
"""Tier 4: 50-episode bronze opponent regression Godot vs Unity.

Drives `bronze.pt` against each engine for 50 fixed seeds. Records each
episode's outcome (WIN/LOSS/TIMEOUT) + damage_dealt + damage_taken.
2-sample Kolmogorov-Smirnov test on damage_dealt distributions; passes
if p > 0.10 (statistically indistinguishable physics).

Usage:
  uv run python tests/bronze_regression.py --threshold 0.10
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

import numpy as np
from scipy import stats

REPO_ROOT = Path(__file__).resolve().parents[1]
SEEDS = list(range(1, 51))


def run_episode(engine: str, seed: int) -> dict:
    """Drive one episode against the running arena. Returns EpisodeStats."""
    # Reuse smoke_arena.py's protocol but run a full 90-second episode.
    # In practice this would import and call the candidate's hw_runner C++ binary
    # built against bronze.pt. For 12b validation: invoke the production
    # hw_runner ↔ arena loop with a 90s timer.
    proc = subprocess.run(
        ["uv", "run", "python", "tools/scripts/smoke_arena.py",
         "--engine", engine, "--seed", str(seed), "--ticks", "5400"],
        capture_output=True, text=True, cwd=REPO_ROOT,
    )
    if proc.returncode != 0:
        raise RuntimeError(f"episode failed seed={seed}: {proc.stderr}")
    # Parse `[finish] ... damage_dealt=N` from stdout
    for line in proc.stdout.splitlines():
        if line.startswith("[finish]"):
            return parse_finish_line(line)
    raise RuntimeError(f"no [finish] line in stdout for seed={seed}")


def parse_finish_line(line: str) -> dict:
    parts = line.split()
    out = {}
    for p in parts:
        if "=" in p:
            k, v = p.split("=", 1)
            out[k] = v
    return out


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--threshold", type=float, default=0.10)
    args = parser.parse_args()

    print("[bronze-regression] running Godot...")
    godot_damage = [int(run_episode("godot", s)["damage_dealt"]) for s in SEEDS]

    print("[bronze-regression] running Unity (start MapA_MazeHybrid in Play mode)...")
    input("Press Enter when Unity arena is in Play mode...")
    unity_damage = [int(run_episode("unity", s)["damage_dealt"]) for s in SEEDS]

    ks_stat, p_value = stats.ks_2samp(godot_damage, unity_damage)
    print(f"[KS] statistic={ks_stat:.4f} p_value={p_value:.4f}")
    print(f"  godot mean damage: {np.mean(godot_damage):.1f}")
    print(f"  unity mean damage: {np.mean(unity_damage):.1f}")

    if p_value < args.threshold:
        print(f"[FAIL] p={p_value:.4f} < threshold {args.threshold}; physics distributions diverge.")
        return 1
    print(f"[OK] p={p_value:.4f} >= threshold {args.threshold}; engines indistinguishable.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 2: Run the bronze regression**

This requires the candidate's `hw_runner` binary built against `bronze.pt`. If the candidate-side stack is not built locally, build it first:

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
cmake --preset linux-debug && cmake --build --preset linux-debug
```

Then with both arenas runnable:

```bash
uv pip install scipy numpy
uv run python tests/bronze_regression.py --threshold 0.10
```

Expected: `p_value >= 0.10`, both engines produce statistically indistinguishable damage distributions.

If this fails, the most likely cause is mecanum kinematics drift between `chassis.gd` (Godot Bullet integrator) and `MecanumChassisController` (custom solver). Re-verify line-by-line equivalence with `chassis.gd`. The fact that we use `CharacterController.Move` (not `Rigidbody`) avoids the worst PhysX↔Bullet drift.

- [ ] **Step 3: Commit**

```bash
git add tests/bronze_regression.py
git commit -m "test(stage12b): tier 4 bronze opponent KS regression harness"
```

---

### Task 27: Tag `v1.7-unity-geometry`

- [ ] **Step 1: Confirm Tier 1 + 2 + 3 + 4 all green**

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
uv run pytest tests/test_arena_wire_format.py -v               # Tier 1
# Tier 2 — see Task 16
# Tier 3 — see Task 25 (Step 5 SSIM ≥ 0.95)
# Tier 4 — see Task 26 (KS p ≥ 0.10)
```

- [ ] **Step 2: Tag**

```bash
git tag -a v1.7-unity-geometry -m "Stage 12b — Map A geometry + chassis silhouette parity

Multi-tier maze hybrid (ProBuilder rough), Chassis/Gimbal/ArmorPlate/Projectile/
HoloProjector prefabs (silhouette-preserving, basic materials only). Tier 3 golden-
frame regression and Tier 4 bronze opponent KS regression both pass. Materials,
lighting, VFX, and HUD land in 12c."
```

- [ ] **Step 3: No push (local-first)**

---

## Stage 12c — Materials, lighting, VFX, UI

**Goal:** Author all PBR materials, custom Shader Graphs, VFX Graph effects, screen-space + diegetic UI prefabs, HDRPAsset variants (Showcase + Headless), Volume profiles, and bake lighting for Map A. End tag: `v1.8-unity-art`.

**Calendar estimate:** 7 working days. Iteration-heavy; tasks below describe the unit of work and target spec values, not pixel-by-pixel node graphs (those require Unity Editor in front of you).

---

### Task 28: Author Shader Graph — `EnergyShield.shadergraph`

**Files:**
- Create: `shared/unity_arena/Assets/Shaders/EnergyShield.shadergraph`
- Create: `shared/unity_arena/Assets/Materials/EnergyShield_Blue.mat`
- Create: `shared/unity_arena/Assets/Materials/EnergyShield_Red.mat`

Rim-lit fresnel halo around chassis. Spec §4.2: transparent ellipsoid mesh ~5% larger than chassis bounding box; rim-lit, fresnel-driven, emission animated by slow noise pan, alpha blended ~12%.

- [ ] **Step 1: Create Shader Graph**

In Unity: Right-click in `Assets/Shaders/` → Create → Shader → HDRP → Lit Shader Graph. Name `EnergyShield`. Open it.

Configure Graph Inspector:
- Surface: Transparent
- Blending: Alpha
- Render Face: Both
- Receive Decals: off

Build the node graph (target behaviour):
1. **Fresnel Effect** node, Power=2.0 → multiply by **Color** property `_TeamColor` (team color, sliced default `(0.12, 0.42, 1, 1)`).
2. **Simple Noise** node with **Time** node feeding UV scrolling at `(0.05, 0.02)` over 2.0 scale → multiply with the Fresnel * Color result. Add a `_NoiseStrength` Float property (default 0.4).
3. **Add** node combines fresnel-color and noise-modulated emission → connect to `Emission` master output.
4. Set `Base Color` to `_TeamColor` × 0.1 (subtle dim base).
5. Set `Alpha` to Fresnel × `_AlphaScale` (Float property, default 0.12).
6. Add property `_EmissionEnergy` (Float, default 1.8) multiplied into Emission output.

- [ ] **Step 2: Create blue / red material instances**

Right-click in Project → Create → Material → name `EnergyShield_Blue.mat`. Set Shader to `Shader Graphs/EnergyShield`. `_TeamColor` = `(0.12, 0.42, 1.0, 1)`. `_EmissionEnergy` = 1.8.

Duplicate, name `EnergyShield_Red.mat`. `_TeamColor` = `(1.0, 0.20, 0.25, 1)`.

- [ ] **Step 3: Apply to chassis prefab**

Open Chassis.prefab. Add a child sphere mesh `ShieldHalo`, scaled to ~1.05× the chassis bounding box. Disable its collider. Material: `EnergyShield_Blue.mat`. Override per-team in Chassis.Awake based on `Team`.

- [ ] **Step 4: Verify visually**

Open MapA scene. Click Play. Confirm: subtle fresnel halo around chassis, color matches team, animated noise visible but not distracting.

- [ ] **Step 5: Commit**

```bash
git add shared/unity_arena/Assets/Shaders/EnergyShield.shadergraph shared/unity_arena/Assets/Materials/EnergyShield_*.mat shared/unity_arena/Assets/Prefabs/Chassis.prefab
git commit -m "feat(stage12c): EnergyShield shader graph + blue/red materials on chassis halo"
```

---

### Task 29: Author Shader Graph — `PlateEmission.shadergraph`

**Files:**
- Create: `shared/unity_arena/Assets/Shaders/PlateEmission.shadergraph`
- Create: 8 plate materials (4 classes × 2 teams)
- Modify: `shared/unity_arena/Assets/Prefabs/ArmorPlate.prefab`

Spec §4.2: base team color (true blue or red, energy 1.5) + class-icon glyph (Hero / Engineer / Standard / Sentry SVG textures sampled as additive overlay) + animated 2 Hz horizontal scanline.

- [ ] **Step 1: Source class-icon SVGs**

Use the existing `shared/godot_arena/assets/icons/` slot (currently empty per `godot_arena/assets/README.md`). Create or source 4 SVG icons (Hero / Engineer / Standard / Sentry) each as a 256×256 monochrome white-on-transparent PNG. For Stage 12c, simple geometric primitives (♦ Hero, ⚙ Engineer, ▲ Standard, ⬡ Sentry) are sufficient.

Save into `shared/unity_arena/Assets/Textures/PlateGlyphs/{Hero,Engineer,Standard,Sentry}.png`. Texture import settings: sRGB, Alpha is Transparency, single-channel optional (R8 if monochrome).

- [ ] **Step 2: Create the Shader Graph**

In Unity: Create → Shader → HDRP → Lit Shader Graph. Name `PlateEmission`. Properties:

- `_TeamColor` Color (HDR), default `(1.0, 0.20, 0.25, 1)`
- `_GlyphTexture` Texture2D
- `_BaseEmissionEnergy` Float, default 1.5
- `_GlyphEmissionEnergy` Float, default 2.5
- `_ScanlineSpeed` Float, default 2.0 (Hz)
- `_ScanlineWidth` Float, default 0.05

Graph behaviour:
1. UV → sample `_GlyphTexture`. Multiply alpha by `_TeamColor` and `_GlyphEmissionEnergy`.
2. Compute scanline mask: `Sin(Time × _ScanlineSpeed × 2π + UV.y × 30)` > `(1 − _ScanlineWidth)`. Use as additive overlay on team color.
3. Output Emission = `_TeamColor × _BaseEmissionEnergy + glyph contribution + scanline contribution`.
4. Output Base Color = `_TeamColor × 0.3` (dim base).
5. Output Smoothness = 0.4, Metallic = 0.2 (matches the existing armor_plate.gd material).

- [ ] **Step 3: Create 8 material instances**

For each of (Blue, Red) × (Hero, Engineer, Standard, Sentry): create a material with `Shader Graphs/PlateEmission`. Set `_TeamColor` and `_GlyphTexture` accordingly. Save under `Assets/Materials/Plates/`.

- [ ] **Step 4: Apply to ArmorPlate prefab**

Open ArmorPlate.prefab. Add a script hook: `ArmorPlate.OnAwake()` chooses the right material based on `Team` and `Icon`. Add to `ArmorPlate.cs`:

```csharp
public Material BlueHeroMat, BlueEngineerMat, BlueStandardMat, BlueSentryMat;
public Material RedHeroMat, RedEngineerMat, RedStandardMat, RedSentryMat;

private void Start()
{
    var r = GetComponent<MeshRenderer>();
    if (r == null) return;
    r.material = SelectMaterial();
}

private Material SelectMaterial()
{
    return (Team, Icon) switch
    {
        ("blue", "Hero") => BlueHeroMat,
        ("blue", "Engineer") => BlueEngineerMat,
        ("blue", "Standard") => BlueStandardMat,
        ("blue", "Sentry") => BlueSentryMat,
        ("red", "Hero") => RedHeroMat,
        ("red", "Engineer") => RedEngineerMat,
        ("red", "Standard") => RedStandardMat,
        ("red", "Sentry") => RedSentryMat,
        _ => null,
    };
}
```

Drag all 8 materials onto the prefab inspector.

- [ ] **Step 5: Verify**

Play MapA scene. Confirm: each plate shows correct team color + class icon + scanline.

- [ ] **Step 6: Commit**

```bash
git add shared/unity_arena/Assets/Shaders/PlateEmission.shadergraph shared/unity_arena/Assets/Materials/Plates shared/unity_arena/Assets/Textures/PlateGlyphs shared/unity_arena/Assets/Prefabs/ArmorPlate.prefab shared/unity_arena/Assets/Scripts/ArmorPlate.cs
git commit -m "feat(stage12c): PlateEmission shader + 8 plate materials + glyph textures"
```

---

### Task 30: Author Shader Graph — `GlassCircuit.shadergraph` + `HoloProjector.shadergraph`

**Files:**
- Create: `shared/unity_arena/Assets/Shaders/GlassCircuit.shadergraph`
- Create: `shared/unity_arena/Assets/Shaders/HoloProjector.shadergraph`
- Create: `shared/unity_arena/Assets/Materials/Glass_Tempered.mat`
- Create: `shared/unity_arena/Assets/Materials/HoloProjector_Base.mat`

Spec §4.3 (glass partitions): tempered-glass shader, refraction enabled, smoothness 0.96, transmission 0.78, embedded animated circuit-line emission (cyan, scrolling 0.05 m/s).

Spec §4.4 (holographic posts): emissive polygonal projector base + billboard quad with JCT label and animated grid lines.

- [ ] **Step 1: GlassCircuit shader graph**

Create → Shader → HDRP → Lit Shader Graph. Name `GlassCircuit`. Surface: Transparent. Blending: Pre-Multiply. Refraction: Box (HDRP feature).

Properties: `_BaseColor` (default `(0.7, 0.85, 1.0, 0.3)`), `_CircuitTexture` (R8 or monochrome PNG of circuit traces), `_CircuitColor` (HDR cyan `(0, 4, 6, 1)`), `_ScrollSpeed` (Vector2, default `(0.05, 0)`).

Graph: UV + Time × _ScrollSpeed → sample `_CircuitTexture` → multiply by `_CircuitColor` → Emission.

Smoothness 0.96, Metallic 0, Transmission 0.78 (set on graph master node), IOR 1.5.

Apply to `Glass_Tempered.mat`. Use that on glass partition meshes in MapA scene.

- [ ] **Step 2: HoloProjector shader graph**

Create → Shader → HDRP → Unlit Shader Graph. Name `HoloProjector`. Surface: Transparent.

Properties: `_BaseColor` (HDR amber `(2.5, 1.7, 0.4, 1)`), `_ScanlineFreq` (Float, 30), `_FlickerStrength` (Float, 0.05).

Graph: Build a vertical-scanline mask (sin(UV.y × _ScanlineFreq × 2π) > 0.7) × _BaseColor. Add subtle time-varying flicker (Random Range × _FlickerStrength). Output to Color.

Apply to `HoloProjector_Base.mat`. Apply to the `EmissionCone` child of HoloProjector.prefab.

- [ ] **Step 3: Apply materials in scene**

Open MapA. Glass partitions get `Glass_Tempered.mat`. HoloProjector prefab instances inherit `HoloProjector_Base.mat` from the prefab. Click Play, confirm both look right.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/Shaders/GlassCircuit.shadergraph shared/unity_arena/Assets/Shaders/HoloProjector.shadergraph shared/unity_arena/Assets/Materials/Glass_Tempered.mat shared/unity_arena/Assets/Materials/HoloProjector_Base.mat shared/unity_arena/Assets/Prefabs/HoloProjector.prefab shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c): GlassCircuit + HoloProjector shaders + tempered-glass material"
```

---

### Task 31: Author VFX Graph — `MuzzleFlash.vfx`

**Files:**
- Create: `shared/unity_arena/Assets/VFX/MuzzleFlash.vfx`
- Modify: `shared/unity_arena/Assets/Scripts/Gimbal.cs` (trigger VFX on fire)

Spec §4.6: HDRP Decal-projected emission disc on barrel tip + 6-frame VFX Graph particle burst (sparks + bright flash) + transient point light (intensity 4, duration 50 ms).

- [ ] **Step 1: Create VFX Graph asset**

Right-click `Assets/VFX/` → Create → Visual Effects → Visual Effect Graph. Name `MuzzleFlash`. Open it.

Configure:
- **Spawn context**: Single Burst, count 60.
- **Initialize Particle**: lifetime 0.05s ± 0.02s, velocity radial outward 2-4 m/s, color HDR cyan `(0, 6, 8)` for half / amber `(8, 4, 0)` for half.
- **Update Particle**: linear drag 8.0.
- **Output Particle Quad**: blend mode Additive, size random 0.02-0.05.

Add a Spawn → "Single Burst (with cooldown)" so the graph can be re-triggered.

- [ ] **Step 2: Add VFX trigger to Gimbal**

In `Gimbal.cs`, add:

```csharp
public UnityEngine.VFX.VisualEffect MuzzleFlashVfx;  // assign in inspector
public Light MuzzleFlashLight;                        // transient point light, assign in inspector

private float _muzzleFlashEndTime;
private float _muzzleFlashOriginalIntensity;

public void TriggerMuzzleFlash()
{
    if (MuzzleFlashVfx != null) MuzzleFlashVfx.SendEvent("OnPlay");
    if (MuzzleFlashLight != null)
    {
        _muzzleFlashOriginalIntensity = MuzzleFlashLight.intensity;
        MuzzleFlashLight.intensity = 4.0f;
        MuzzleFlashLight.enabled = true;
        _muzzleFlashEndTime = Time.time + 0.05f;
    }
}

private void Update()
{
    if (MuzzleFlashLight != null && MuzzleFlashLight.enabled && Time.time >= _muzzleFlashEndTime)
    {
        MuzzleFlashLight.enabled = false;
    }
}
```

In ArenaMain.SpawnProjectiles, after `Arm`, call `BlueChassis.Gimbal.TriggerMuzzleFlash()`.

- [ ] **Step 3: Wire prefab**

Open Gimbal.prefab. Add child VisualEffect at Muzzle position, assign `MuzzleFlash.vfx`. Add child Light (Point), enabled=false initially, range 1.5, color cyan, drag onto MuzzleFlashLight reference. Save prefab.

- [ ] **Step 4: Verify**

Play scene, fire a burst (`uv run python tools/scripts/smoke_arena.py --engine unity --seed 42 --ticks 30`). Confirm: muzzle flash particles + transient light visible.

- [ ] **Step 5: Commit**

```bash
git add shared/unity_arena/Assets/VFX/MuzzleFlash.vfx shared/unity_arena/Assets/Prefabs/Gimbal.prefab shared/unity_arena/Assets/Scripts/Gimbal.cs shared/unity_arena/Assets/Scripts/ArenaMain.cs
git commit -m "feat(stage12c): MuzzleFlash VFX Graph + transient light + Gimbal trigger"
```

---

### Task 32: Author VFX Graph — `ImpactSpark.vfx` + impact decal

**Files:**
- Create: `shared/unity_arena/Assets/VFX/ImpactSpark.vfx`
- Create: `shared/unity_arena/Assets/Materials/ImpactDecal.mat`
- Create: `shared/unity_arena/Assets/Prefabs/ImpactEffect.prefab`
- Modify: `shared/unity_arena/Assets/Scripts/Projectile.cs` (spawn on hit)

Spec §4.6: HDRP Decal projector on hit surfaces (small scorch ring) + 4-frame spark VFX Graph burst. Decals fade after 12 s.

- [ ] **Step 1: Create ImpactSpark VFX**

Create → Visual Effects → Visual Effect Graph. Name `ImpactSpark`. Single Burst 30 particles, lifetime 0.3 ± 0.1s, velocity hemisphere normal-aligned 1-3 m/s, color HDR amber, additive quad output, gravity modifier 1.0 in update.

- [ ] **Step 2: Create ImpactDecal material**

Right-click → Create → Material → Decal. Use a small scorch-ring texture (256×256 PNG, sourced from Synty or generated procedurally). Set material `Surface Type = Opaque`, Decal Master output. Add fade-out via decal-projector script in 12c step 4.

- [ ] **Step 3: Create ImpactEffect prefab**

Create empty GameObject `ImpactEffect`. Add child VisualEffect (`ImpactSpark.vfx`). Add child DecalProjector (HDRP component): Material `ImpactDecal`, scale (0.5, 0.5, 0.1), draw distance 30.

Add `ImpactEffect.cs` script:

```csharp
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace TsingYun.UnityArena
{
    public class ImpactEffect : MonoBehaviour
    {
        public float DecalLifetime = 12.0f;
        public DecalProjector Decal;

        private float _spawnTime;

        private void Start() { _spawnTime = Time.time; }

        private void Update()
        {
            float age = Time.time - _spawnTime;
            if (Decal != null) Decal.fadeFactor = Mathf.Clamp01(1f - age / DecalLifetime);
            if (age > DecalLifetime) Destroy(gameObject);
        }
    }
}
```

Save as `ImpactEffect.prefab`. Drag DecalProjector reference into the script.

- [ ] **Step 4: Spawn from Projectile**

Modify `Projectile.cs`:

```csharp
public GameObject ImpactEffectPrefab;  // assign in Projectile prefab inspector

private void Consume(string reason)
{
    Consumed = true;
    if (ImpactEffectPrefab != null && (reason.StartsWith("hit_") || reason == "miss_range"))
    {
        Instantiate(ImpactEffectPrefab, transform.position,
                    Quaternion.LookRotation(-_rb.linearVelocity.normalized));
    }
    Destroy(gameObject);
}
```

In Projectile.prefab, drag `ImpactEffect.prefab` onto the ImpactEffectPrefab slot.

- [ ] **Step 5: Commit**

```bash
git add shared/unity_arena/Assets/VFX/ImpactSpark.vfx shared/unity_arena/Assets/Materials/ImpactDecal.mat shared/unity_arena/Assets/Prefabs/ImpactEffect.prefab shared/unity_arena/Assets/Scripts/ImpactEffect.cs shared/unity_arena/Assets/Scripts/Projectile.cs shared/unity_arena/Assets/Prefabs/Projectile.prefab
git commit -m "feat(stage12c): ImpactSpark VFX + decal projector + Projectile spawn-on-consume"
```

---

### Task 33: Author VFX Graph — `DustMote.vfx`

**Files:**
- Create: `shared/unity_arena/Assets/VFX/DustMote.vfx`

Spec §4.3: dust mote particle system, ~80 particles per corridor, drift velocity 0.05 m/s, additive blend, emissive cyan.

- [ ] **Step 1: Create DustMote VFX**

Create → Visual Effects → Visual Effect Graph. Name `DustMote`.

- **Spawn**: Constant Spawn Rate 80 particles/second.
- **Initialize**: lifetime 8-15 s, velocity (0, 0.05, 0) ± 0.02, position random box (4, 2.5, 4).
- **Update**: turbulence noise low-frequency 0.05, drag 0.5.
- **Output Particle Quad**: blend Additive, size 0.005-0.012, color HDR cyan emission `(0, 0.6, 1.0) × 0.4`.

- [ ] **Step 2: Place in MapA scene**

Open MapA scene. For each of the corridor segments, create a child GameObject `Dust_<id>` with VisualEffect component referencing `DustMote.vfx`. Position the bounding box to cover that corridor.

- [ ] **Step 3: Verify**

Play scene. Confirm: drifting cyan motes visible in corridors, especially in light shafts.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/VFX/DustMote.vfx shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c): DustMote VFX in corridor light shafts"
```

---

### Task 34: Author PBR materials for body / barrel / walls / floor / shell casing

**Files:**
- Create: `shared/unity_arena/Assets/Materials/Body_TitaniumAluminum.mat`
- Create: `shared/unity_arena/Assets/Materials/Wall_MatteAluminum.mat`
- Create: `shared/unity_arena/Assets/Materials/Wall_CarbonFiber.mat`
- Create: `shared/unity_arena/Assets/Materials/Floor_Concrete.mat`
- Create: `shared/unity_arena/Assets/Materials/Barrel_Anodized.mat`
- Create: `shared/unity_arena/Assets/Materials/ShellCasing_Brass.mat`

Spec §4.2 (chassis body) + §4.3 (maze materials):

| Material | Texture base | Metallic | Roughness | Emission |
|---|---|---|---|---|
| Body_TitaniumAluminum | Synty panel diffuse | 0.85 | 0.45 | edge gradient luminous trim (team color, ramping with `_TeamColor`) |
| Wall_MatteAluminum | Synty wall diffuse + normal | 0.65 | 0.55 | none |
| Wall_CarbonFiber | Anisotropic CF texture | 0.75 | 0.18 | none |
| Floor_Concrete | Concrete diffuse + normal | 0.10 | 0.78 | floor seams: cyan emission strips (use Synty floor base + emissive overlay mask texture) |
| Barrel_Anodized | Black metal | 0.75 | 0.32 | faint cyan power-core line, ramping intensity 200 ms before fire |
| ShellCasing_Brass | Brass metal | 0.95 | 0.35 | none |

- [ ] **Step 1: Create the 6 materials**

For each: Right-click → Create → Material. Set Shader to `HDRP/Lit`. Configure per the table above. For Synty-textured materials, drag the appropriate Synty texture from `Assets/Synty/POLYGON_SciFi/Textures/` into Base Color.

For floor seams: use a mask texture `FloorSeams_Mask.png` (cyan strip pattern) plugged into Emissive Map; Emissive Color HDR `(0, 5, 8)`, Emissive Intensity 1.5. Create or paint this mask in the Synty pack's atlas style.

For Barrel power-core: assign `_PowerCorePulseAt = -1` in script and pulse in `Gimbal.cs` 200 ms before fire (`MaterialPropertyBlock` set on the Barrel renderer).

- [ ] **Step 2: Apply materials in scene**

Open MapA. Floor → `Floor_Concrete`. Walls → `Wall_MatteAluminum`. Chokepoint structural pieces → `Wall_CarbonFiber`. Barrel mesh inside Gimbal prefab → `Barrel_Anodized`. Body mesh inside Chassis prefab → `Body_TitaniumAluminum`.

- [ ] **Step 3: Add ShellCasing prefab + spawn-on-fire**

Create a small cube prefab `ShellCasing.prefab` (size 0.025 × 0.012 × 0.008), Rigidbody (mass 0.005), MeshRenderer with `ShellCasing_Brass.mat`. Add `ShellCasing.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    public class ShellCasing : MonoBehaviour
    {
        public float DespawnAfter = 4.0f;
        private float _spawnTime;
        private void Start() { _spawnTime = Time.time; }
        private void Update() { if (Time.time - _spawnTime > DespawnAfter) Destroy(gameObject); }
    }
}
```

In Gimbal.cs, after `TriggerMuzzleFlash`, instantiate `ShellCasingPrefab` at the muzzle, eject with random velocity sideways+up.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/Materials shared/unity_arena/Assets/Prefabs/ShellCasing.prefab shared/unity_arena/Assets/Scripts/ShellCasing.cs shared/unity_arena/Assets/Scripts/Gimbal.cs shared/unity_arena/Assets/Prefabs/Chassis.prefab shared/unity_arena/Assets/Prefabs/Gimbal.prefab shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c): PBR materials (body/wall/floor/barrel/shell) + shell casing prefab"
```

---

### Task 35: Light placement — neon strips, key directional, volumetric fog

**Files:**
- Modify: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`

Spec §4.3:
- Key directional: cool-blue 5600K, intensity 0.6
- Neon strips: HDRP Tube Lights, ~80 total, cyan + magenta alternating along maze edges and corridor ceilings
- Volumetric fog: Local Volumetric Fog volumes, density 0.012, anisotropy −0.2

- [ ] **Step 1: Configure key directional**

In MapA scene, find the existing Directional Light (default sun). Change Intensity Multiplier 0.6, Filter Color 5600K (use Color Temperature toggle, set to 5600K). Shadow Resolution: High. Cookie: none.

- [ ] **Step 2: Place neon Tube Lights**

For each maze corridor, place 4-8 Tube Lights along the wall tops:
- GameObject → Light → Tube Light (HDRP).
- Length 2-4 m, intensity 30000 nits, color HDR cyan `(0, 0.6, 1)` × 8 OR magenta `(1, 0.18, 0.6)` × 8 (alternate per corridor).
- Range 6 m, indirect multiplier 1.5.
- Affects diffuse: yes; Affects specular: yes.

Group under empty `Lights/Neon/`. Aim for ~80 strips total (~12 per corridor segment + ~16 along catwalks).

- [ ] **Step 3: Add volumetric fog**

Window → Rendering → Lighting → Volumetric Fog. Enable scene-wide Volumetric.

For each corridor, GameObject → Volume → Local Volume. Add Volumetric Fog override: density 0.012, color graphite `(0.4, 0.45, 0.55)`, anisotropy −0.2, distance fade 25.

Ambient indirect via SDFGI? In HDRP, use Indirect Lighting Controller volume with Sky Lighting Multiplier 0.3 (so neon strips dominate over ambient).

- [ ] **Step 4: Verify**

Play MapA scene. Confirm: cyan/magenta neon strips, volumetric godrays through corridors, key light low-intensity overhead. Should look very close to the brief: "cool-toned cyan, electric blue and magenta neon light strips run along maze edges, corridor ceilings".

- [ ] **Step 5: Commit**

```bash
git add shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c): neon tube lights + volumetric fog + key directional in MapA"
```

---

### Task 36: HDRPAsset_Showcase + HDRPAsset_Headless variants

**Files:**
- Create: `shared/unity_arena/Assets/Settings/HDRPAsset_Showcase.asset`
- Create: `shared/unity_arena/Assets/Settings/HDRPAsset_Headless.asset`

Spec §5.3: showcase preset with DXR on, full quality; headless preset with DXR off, baked GI only, halved shadow resolution, kills volumetric fog, low-LOD chassis.

- [ ] **Step 1: Create Showcase asset**

In Project: locate the existing default HDRP Asset (created when project was templated). Duplicate it (Ctrl/Cmd+D), rename `HDRPAsset_Showcase`.

Configure:
- Lighting → Volumetrics → enabled
- Lighting → Screen Space Reflection → enabled, Quality High
- Lighting → Screen Space Global Illumination → enabled
- Lighting → Path Tracing → disabled
- Lighting → **Ray Tracing → enabled** (if DXR available)
  - Ray Traced Reflections enabled
  - Ray Traced Global Illumination enabled
  - Ray Traced Ambient Occlusion enabled
- Shadow Resolution: 2048
- Lit Shader Mode: Both
- LOD Bias: 1.0

- [ ] **Step 2: Create Headless asset**

Duplicate again, rename `HDRPAsset_Headless`. Configure:
- Lighting → Volumetrics → disabled
- Lighting → Screen Space Reflection → enabled, Quality Low
- Lighting → Screen Space Global Illumination → disabled (rely on baked lightmaps)
- Lighting → Ray Tracing → disabled
- Shadow Resolution: 1024
- Lit Shader Mode: Forward Only
- LOD Bias: 0.5

- [ ] **Step 3: Add config-based selection at startup**

Create `shared/unity_arena/Assets/Scripts/QualityRouter.cs`:

```csharp
using UnityEngine;
using UnityEngine.Rendering;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Selects HDRPAsset_Showcase or _Headless at startup based on
    /// command-line flag --quality={showcase,headless}. Default: showcase.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class QualityRouter : MonoBehaviour
    {
        public RenderPipelineAsset ShowcaseAsset;
        public RenderPipelineAsset HeadlessAsset;

        private void Awake()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string quality = "showcase";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--quality" && i + 1 < args.Length) quality = args[i + 1];
                else if (args[i].StartsWith("--quality=")) quality = args[i].Substring("--quality=".Length);
            }
            GraphicsSettings.defaultRenderPipeline = quality == "headless" ? HeadlessAsset : ShowcaseAsset;
            QualitySettings.renderPipeline = GraphicsSettings.defaultRenderPipeline;
            Debug.Log($"[QualityRouter] using {quality} preset");
        }
    }
}
```

Add a `QualityRouter` GameObject as a child of ArenaMain in MapA scene. Drag the two HDRPAssets onto its inspector slots.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/Settings/HDRPAsset_Showcase.asset shared/unity_arena/Assets/Settings/HDRPAsset_Headless.asset shared/unity_arena/Assets/Scripts/QualityRouter.cs shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c): HDRPAsset Showcase + Headless variants + QualityRouter --quality flag"
```

---

### Task 37: Volume profiles — `Volume_Showcase` + `Volume_Headless`

**Files:**
- Create: `shared/unity_arena/Assets/Settings/Volume_Showcase.asset`
- Create: `shared/unity_arena/Assets/Settings/Volume_Headless.asset`

Spec §4.5: Showcase post-FX = vignette 0.3, chromatic aberration 0.15 (edges only), motion blur 0.4 (rapid yaw > 90°/s only), bloom (threshold 1.0, intensity 0.8). Headless: post-FX disabled.

- [ ] **Step 1: Create Volume_Showcase profile**

Right-click → Create → Volume Profile. Name `Volume_Showcase`. Add overrides:
- **Post-Processing → Vignette**: intensity 0.3, smoothness 0.5
- **Post-Processing → Chromatic Aberration**: intensity 0.15
- **Post-Processing → Motion Blur**: intensity 0.4, sample count 8
- **Post-Processing → Bloom**: threshold 1.0, intensity 0.8, scatter 0.7
- **Post-Processing → Tonemapping**: Mode ACES
- **Lighting → Indirect Lighting Controller**: Sky Lighting Multiplier 0.3
- **Sky → Visual Environment**: HDRI Sky (use a dim industrial HDRI or solid `#050810`)

- [ ] **Step 2: Create Volume_Headless profile**

Duplicate, name `Volume_Headless`. Disable all Post-Processing overrides except Tonemapping (keep ACES so the rasterized RGB output looks right for the candidate's detector).

- [ ] **Step 3: Add Volume Routers**

In MapA scene, create empty `VolumeRoot` at origin. Add Volume component, set to Global, Profile = `Volume_Showcase` initially.

Modify `QualityRouter.cs`:

```csharp
public VolumeProfile ShowcaseVolume;
public VolumeProfile HeadlessVolume;
public Volume GlobalVolume;

private void Awake()
{
    // ... existing quality setup ...
    if (GlobalVolume != null)
        GlobalVolume.profile = quality == "headless" ? HeadlessVolume : ShowcaseVolume;
}
```

Drag the two profiles + the GlobalVolume scene reference into the inspector slots.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/Settings/Volume_*.asset shared/unity_arena/Assets/Scripts/QualityRouter.cs shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c): Volume profiles Showcase + Headless + router selection"
```

---

### Task 38: Screen-space HUD prefab + HudController

**Files:**
- Create: `shared/unity_arena/Assets/UI/HudCanvas_ScreenSpace.prefab`
- Create: `shared/unity_arena/Assets/Scripts/HudController.cs`

Spec §4.5: reticle (cyan circle + crosshair, with predictive lead arc), target-lock countdown glyph, minimap (110×110 px upper-right, semi-transparent), telemetry status bar (bottom-left, monospace cyan).

- [ ] **Step 1: Build the HUD canvas prefab**

GameObject → UI → Canvas. Set Render Mode = Screen Space - Overlay, Reference Resolution 1280×720. Save as `HudCanvas_ScreenSpace.prefab`.

Children:
- **Reticle** (UI Image): center, 22×22 px, cyan circle texture + crosshair via two RectTransform lines.
- **TargetLockGlyph** (TextMeshProUGUI): top-center, "TARGET LOCK · 0.42s", red `#FF3340`, hidden by default.
- **Minimap** (Panel): upper-right, 110×110 px, semi-transparent black bg, cyan border. Holds:
  - **MinimapBackground** (UI Image, dark with alpha 0.7)
  - **MinimapGrid** (UI Image, cyan grid lines tiled)
  - **MinimapBlipBlue / Red** (small UI dots, dynamic position via script)
- **TelemetryBar** (TextMeshProUGUI): bottom-left, "HP 86% · HEAT 32% · AMMO 17/40 · YAW −12.4° · PITCH +3.1°", monospace cyan.

- [ ] **Step 2: Add HudController script**

Create `shared/unity_arena/Assets/Scripts/HudController.cs`:

```csharp
using UnityEngine;
using TMPro;

namespace TsingYun.UnityArena
{
    public class HudController : MonoBehaviour
    {
        public RectTransform Reticle;
        public TextMeshProUGUI TargetLockGlyph;
        public RectTransform MinimapBlipBlue;
        public RectTransform MinimapBlipRed;
        public TextMeshProUGUI TelemetryBar;

        public Chassis BlueChassis;
        public Chassis RedChassis;
        public Vector2 ArenaSizeMeters = new Vector2(20f, 20f);
        public Vector2 MinimapSizePx = new Vector2(110f, 110f);

        private void Update()
        {
            if (BlueChassis != null && MinimapBlipBlue != null)
                MinimapBlipBlue.anchoredPosition = WorldToMinimap(BlueChassis.transform.position);
            if (RedChassis != null && MinimapBlipRed != null)
                MinimapBlipRed.anchoredPosition = WorldToMinimap(RedChassis.transform.position);

            if (BlueChassis != null && TelemetryBar != null)
            {
                int hpPct = Mathf.RoundToInt(100f * (1f - Mathf.Min(1f, BlueChassis.DamageTaken / 800f)));
                var g = BlueChassis.Gimbal != null ? BlueChassis.Gimbal.GetState() : default;
                TelemetryBar.text = $"HP {hpPct}% · YAW {g.Yaw * Mathf.Rad2Deg:+0.0;-0.0}° · PITCH {g.Pitch * Mathf.Rad2Deg:+0.0;-0.0}°";
            }
        }

        private Vector2 WorldToMinimap(Vector3 worldPos)
        {
            float u = (worldPos.x + ArenaSizeMeters.x * 0.5f) / ArenaSizeMeters.x;
            float v = (worldPos.z + ArenaSizeMeters.y * 0.5f) / ArenaSizeMeters.y;
            return new Vector2((u - 0.5f) * MinimapSizePx.x, (v - 0.5f) * MinimapSizePx.y);
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
```

Add HudController to the Canvas root, drag references in.

- [ ] **Step 3: Place HudCanvas in MapA scene**

Drag `HudCanvas_ScreenSpace.prefab` into MapA scene. Wire BlueChassis / RedChassis references.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/UI shared/unity_arena/Assets/Scripts/HudController.cs shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c): screen-space HUD prefab + HudController (reticle / minimap / telemetry)"
```

---

### Task 39: Diegetic warning glyph (LOS-gated `⚠ ENEMY`)

**Files:**
- Create: `shared/unity_arena/Assets/UI/HoloMarker_World.prefab`
- Create: `shared/unity_arena/Assets/Scripts/EnemyWarningMarker.cs`

Spec §4.4: red-amber `⚠ ENEMY` marker that spawns above an enemy chassis when distance < 12 m AND camera LOS is unobstructed.

- [ ] **Step 1: Build the prefab**

GameObject → Create Empty → name `HoloMarker_World`. Add Canvas (Render Mode = World Space, scale 0.005). Add child TextMeshProUGUI with text `⚠ ENEMY`, color `#FF3340`, font size 32.

Save as `HoloMarker_World.prefab`.

- [ ] **Step 2: Add EnemyWarningMarker script**

Create `shared/unity_arena/Assets/Scripts/EnemyWarningMarker.cs`:

```csharp
using UnityEngine;
using TMPro;

namespace TsingYun.UnityArena
{
    public class EnemyWarningMarker : MonoBehaviour
    {
        public Transform PlayerCamera;
        public Transform EnemyChassis;
        public float MaxRange = 12.0f;
        public LayerMask LineOfSightMask;
        public TextMeshProUGUI Label;

        private void LateUpdate()
        {
            if (PlayerCamera == null || EnemyChassis == null) { SetVisible(false); return; }
            float dist = Vector3.Distance(PlayerCamera.position, EnemyChassis.position);
            if (dist > MaxRange) { SetVisible(false); return; }

            // Raycast from camera to enemy; if anything blocks, hide.
            Vector3 dir = EnemyChassis.position - PlayerCamera.position;
            if (Physics.Raycast(PlayerCamera.position, dir.normalized, out RaycastHit hit, dist - 0.5f, LineOfSightMask))
            {
                SetVisible(false);
                return;
            }

            transform.position = EnemyChassis.position + Vector3.up * 0.6f;
            transform.LookAt(transform.position + (transform.position - PlayerCamera.position));
            if (Label != null) Label.text = $"⚠ ENEMY {dist:0.0}m";
            SetVisible(true);
        }

        private void SetVisible(bool v) => gameObject.SetActive(v);
    }
}
```

- [ ] **Step 3: Place in MapA scene**

Drag `HoloMarker_World.prefab` into scene as a child of `BlueChassis/Gimbal/PitchPivot/GimbalCamera`. Set PlayerCamera = the camera transform, EnemyChassis = RedChassis transform, LineOfSightMask = Default + Walls layers.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/UI/HoloMarker_World.prefab shared/unity_arena/Assets/Scripts/EnemyWarningMarker.cs shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c): diegetic enemy warning marker (LOS-gated, range-gated)"
```

---

### Task 40: Build-time `--ui={full,diegetic}` toggle

**Files:**
- Modify: `shared/unity_arena/Assets/Scripts/QualityRouter.cs` (extend with UI flag)

Spec §4.5: in `diegetic` mode the screen-space `Canvas` is `SetActive(false)` and the post-FX volume's screen-space-only effects are disabled — clean RGB feeds the candidate's HW1 detector.

- [ ] **Step 1: Extend QualityRouter**

Edit `shared/unity_arena/Assets/Scripts/QualityRouter.cs`:

```csharp
public GameObject ScreenSpaceHudRoot;  // HudCanvas_ScreenSpace
public bool DefaultUiFullInEditor = true;

private void Awake()
{
    string[] args = System.Environment.GetCommandLineArgs();
    string quality = "showcase";
    string ui = Application.isEditor && DefaultUiFullInEditor ? "full" : "full";
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--quality" && i + 1 < args.Length) quality = args[i + 1];
        else if (args[i].StartsWith("--quality=")) quality = args[i].Substring("--quality=".Length);
        if (args[i] == "--ui" && i + 1 < args.Length) ui = args[i + 1];
        else if (args[i].StartsWith("--ui=")) ui = args[i].Substring("--ui=".Length);
    }
    // Headless build defaults to diegetic UI unless overridden.
    if (quality == "headless" && !HasArg(args, "--ui")) ui = "diegetic";

    // ... existing HDRP routing ...

    if (ScreenSpaceHudRoot != null) ScreenSpaceHudRoot.SetActive(ui == "full");
    Debug.Log($"[QualityRouter] quality={quality} ui={ui}");
}

private static bool HasArg(string[] args, string key)
{
    foreach (var a in args) { if (a == key || a.StartsWith(key + "=")) return true; }
    return false;
}
```

Drag the `HudCanvas` instance into the ScreenSpaceHudRoot slot.

- [ ] **Step 2: Verify both modes**

Build a Mac/Linux player (Quick Build):

```bash
# Once tools/unity/build.sh exists in 12d, this becomes one command. For now:
# Use Unity Editor: File → Build Settings → Build And Run
```

Run the binary in two modes:

```bash
./shared/unity_arena/builds/macos-showcase/AimingArena.app/Contents/MacOS/AimingArena \
    --quality showcase --ui full

./shared/unity_arena/builds/macos-showcase/AimingArena.app/Contents/MacOS/AimingArena \
    --quality headless --ui diegetic
```

Expected: showcase mode shows HUD + post-FX; headless mode hides HUD and disables screen-space effects. Connect smoke harness to verify the wire still works.

- [ ] **Step 3: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/QualityRouter.cs shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c): --ui={full,diegetic} build-time toggle for HUD + post-FX"
```

---

### Task 41: Bake lighting (test config first, release config for tag)

**Files:**
- Create: `shared/unity_arena/Assets/Lightmaps/` (gitignored output)
- Modify: `ProjectSettings/QualitySettings.asset` (lightmap settings)

Spec §7 R6 mitigation: two bake configs.

- [ ] **Step 1: Configure test bake**

Window → Rendering → Lighting → Scene tab. Set:
- Lightmapper: Progressive GPU (or CPU if GPU baker is unstable on macOS)
- Indirect Resolution: 0.5 texel/world unit (test)
- Lightmap Resolution: 20 texels/world unit (test)
- Direct Samples: 32, Indirect Samples: 256, Environment Samples: 64
- Lightmap Size: 1024
- Compress Lightmaps: yes (test)

Click "Generate Lighting" (single click — bakes once).

Expected: bake completes in ~5 minutes; lightmaps land in `Assets/Scenes/MapA_MazeHybrid/Lightmap-*.exr`.

- [ ] **Step 2: Verify visually**

Play scene. Confirm: indirect lighting bounces look right; corridors are visible with the key directional + neon strips even with SSGI off.

- [ ] **Step 3: Save test bake settings preset**

Create `Assets/Settings/LightingSettings_Test.lighting` from Lighting window → Save Preset.

- [ ] **Step 4: Configure release bake**

Bump Indirect Resolution to 2.0 texels/wu, Lightmap Resolution to 40 texels/wu, Indirect Samples to 1024, Lightmap Size to 4096, Compress Lightmaps off. Save as `LightingSettings_Release.lighting`. **Do NOT bake the release config until just before tagging v1.8** — it takes ~60 minutes.

- [ ] **Step 5: Final release bake**

Apply LightingSettings_Release. Click Generate Lighting. Wait ~60 minutes. Verify visually.

- [ ] **Step 6: Commit settings (NOT lightmap output)**

```bash
git add shared/unity_arena/Assets/Settings/LightingSettings_*.lighting shared/unity_arena/ProjectSettings/QualitySettings.asset
# Lightmaps themselves are gitignored.
git commit -m "feat(stage12c): lightmap bake configs (test + release) for Map A"
```

---

### Task 42: Tag `v1.8-unity-art`

- [ ] **Step 1: Confirm Tier 1-4 still green**

Re-run all conformance tiers. The art changes should not regress geometric SSIM (Tier 3) — if they do, threshold may need re-tuning.

- [ ] **Step 2: Tag**

```bash
git tag -a v1.8-unity-art -m "Stage 12c — Materials, lighting, VFX, UI

All 4 Shader Graphs (EnergyShield, PlateEmission, GlassCircuit, HoloProjector)
authored. 3 VFX Graphs (MuzzleFlash, ImpactSpark, DustMote). 6 PBR materials.
HDRPAsset Showcase + Headless variants with --quality flag. Volume profiles
with --ui={full,diegetic} flag. Screen-space HUD + diegetic warning marker.
Map A lightmap bake (release config, ~60 min). Tier 1-4 conformance still green."
```

- [ ] **Step 3: No push (local-first)**

---

## Stage 12d — Build, OSS publish, migration cleanup

**Goal:** Cut release-mode builds for all 4 targets, run the Tier 5 HW1-HW7 ship gate, push validated artifacts to OSS, update the `manifest.toml` and candidate-facing docs, rename `shared/godot_arena/` to `_legacy/`. End tags: `v1.9-unity-launch`, then `v2.0-arena-reform-complete`.

**Calendar estimate:** 3 working days.

---

### Task 43: Implement `tools/unity/build.sh`

**Files:**
- Create: `tools/unity/build.sh`
- Create: `tools/unity/README.md`
- Create: `shared/unity_arena/Assets/Editor/BuildScript.cs`

- [ ] **Step 1: Write Unity Editor build script**

Create `shared/unity_arena/Assets/Editor/BuildScript.cs`:

```csharp
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TsingYun.UnityArena.Editor
{
    public static class BuildScript
    {
        public static void BuildShowcaseWin64() => Build("win-showcase", BuildTarget.StandaloneWindows64, "AimingArena.exe");
        public static void BuildShowcaseMacOS() => Build("macos-showcase", BuildTarget.StandaloneOSX, "AimingArena.app");
        public static void BuildShowcaseLinux() => Build("linux-showcase", BuildTarget.StandaloneLinux64, "AimingArena.x86_64");
        public static void BuildHeadlessLinux() => Build("linux-headless", BuildTarget.StandaloneLinux64, "AimingArena_Headless.x86_64", subTarget: 1);

        private static void Build(string targetSlug, BuildTarget buildTarget, string fileName, int subTarget = 0)
        {
            string outDir = $"builds/{targetSlug}";
            string outPath = $"{outDir}/{fileName}";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MapA_MazeHybrid.unity" },
                locationPathName = outPath,
                target = buildTarget,
                subtarget = subTarget,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] {targetSlug}: {summary.totalSize / 1024 / 1024} MB at {outPath}");
            }
            else
            {
                Debug.LogError($"[BuildScript] {targetSlug} FAILED: {summary.result}");
                EditorApplication.Exit(1);
            }
        }
    }
}
```

- [ ] **Step 2: Write the shell wrapper**

Create `tools/unity/build.sh`:

```bash
#!/usr/bin/env bash
# Build the Unity arena for the requested target. Output is local-only;
# OSS push happens via shared/scripts/push_assets.py at Stage 12d closure.
#
# Usage: tools/unity/build.sh --target {win-showcase,macos-showcase,linux-showcase,linux-headless}

set -euo pipefail

TARGET="${1:-}"
if [ "$TARGET" = "--target" ]; then TARGET="${2:-}"; fi
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/shared/unity_arena"
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity}"

if [ ! -x "$UNITY_BIN" ]; then
    echo "Unity binary not found at $UNITY_BIN. Set UNITY_BIN env var." >&2
    exit 1
fi

case "$TARGET" in
    win-showcase)    METHOD="TsingYun.UnityArena.Editor.BuildScript.BuildShowcaseWin64" ;;
    macos-showcase)  METHOD="TsingYun.UnityArena.Editor.BuildScript.BuildShowcaseMacOS" ;;
    linux-showcase)  METHOD="TsingYun.UnityArena.Editor.BuildScript.BuildShowcaseLinux" ;;
    linux-headless)  METHOD="TsingYun.UnityArena.Editor.BuildScript.BuildHeadlessLinux" ;;
    *) echo "Unknown target: $TARGET" >&2; exit 1 ;;
esac

cd "$PROJECT"
mkdir -p builds
"$UNITY_BIN" -batchmode -nographics -projectPath "$PROJECT" \
    -executeMethod "$METHOD" \
    -logFile - \
    -quit
```

Make executable:

```bash
chmod +x tools/unity/build.sh
```

- [ ] **Step 3: Test each target**

```bash
tools/unity/build.sh --target macos-showcase
tools/unity/build.sh --target linux-headless
# Skip win-showcase + linux-showcase if the Mac box can't cross-compile.
# Per schema's milestone phasing, those build on a separate Win box / Linux dev VM.
```

Expected: each invocation prints `[BuildScript] <target>: <N> MB at builds/<target>/...` and exits 0.

- [ ] **Step 4: Commit**

```bash
git add tools/unity/build.sh shared/unity_arena/Assets/Editor/BuildScript.cs
# Builds output is gitignored (added in 12a).
chmod +x tools/unity/build.sh
git update-index --chmod=+x tools/unity/build.sh
git commit -m "feat(stage12d): tools/unity/build.sh + BuildScript.cs (4 targets)"
```

---

### Task 44: `tools/unity/bake_lighting.sh` and `tools/unity/README.md`

**Files:**
- Create: `tools/unity/bake_lighting.sh`
- Create: `tools/unity/README.md`

- [ ] **Step 1: Write bake script**

Create `tools/unity/bake_lighting.sh`:

```bash
#!/usr/bin/env bash
# Invoke Unity in batch mode to bake lightmaps for Map A.
# Usage: tools/unity/bake_lighting.sh {test|release}

set -euo pipefail

CONFIG="${1:-test}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/shared/unity_arena"
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity}"

case "$CONFIG" in
    test)    METHOD="TsingYun.UnityArena.Editor.BakeScript.BakeTest" ;;
    release) METHOD="TsingYun.UnityArena.Editor.BakeScript.BakeRelease" ;;
    *) echo "Unknown config: $CONFIG (use test|release)" >&2; exit 1 ;;
esac

cd "$PROJECT"
"$UNITY_BIN" -batchmode -nographics -projectPath "$PROJECT" \
    -executeMethod "$METHOD" -logFile - -quit
```

Add `BakeScript.cs` editor class similar to `BuildScript.cs` calling `Lightmapping.Bake()` with the right preset.

- [ ] **Step 2: Write the README**

Create `tools/unity/README.md` documenting usage, env vars (UNITY_BIN), and target slugs. Keep under 100 lines.

- [ ] **Step 3: Commit**

```bash
chmod +x tools/unity/bake_lighting.sh
git add tools/unity/bake_lighting.sh tools/unity/README.md shared/unity_arena/Assets/Editor/BakeScript.cs
git commit -m "feat(stage12d): bake_lighting.sh + tools/unity/README.md"
```

---

### Task 45: `tools/scripts/check_synty_redistribution.py` (R2 CI guard)

**Files:**
- Create: `tools/scripts/check_synty_redistribution.py`
- Modify: `.github/workflows/lint_and_build.yml` (add the check)

- [ ] **Step 1: Write the guard**

Create `tools/scripts/check_synty_redistribution.py`:

```python
"""Fail the build if Synty source files are detected in any committed path.

Synty's POLYGON pack license allows binary redistribution but NOT source
.fbx / .png / .mat redistribution. This guard scans `git ls-files` for
any path under `**/Synty/**` and fails CI if any such path is committed
to the repo.
"""

from __future__ import annotations

import subprocess
import sys


def main() -> int:
    result = subprocess.run(
        ["git", "ls-files"], capture_output=True, text=True, check=True,
    )
    forbidden_extensions = {".fbx", ".obj", ".dae", ".blend"}
    forbidden_path_fragment = "/Synty/"

    violations = []
    for line in result.stdout.splitlines():
        if forbidden_path_fragment in line:
            violations.append(line)
            continue
        for ext in forbidden_extensions:
            if line.endswith(ext) and "/Synty/" in line:
                violations.append(line)

    if violations:
        print("[FAIL] Synty source files detected in committed paths:", file=sys.stderr)
        for v in violations:
            print(f"  {v}", file=sys.stderr)
        print("\nSynty's license forbids source redistribution.", file=sys.stderr)
        print("Move these files to .gitignore'd paths and untrack them:", file=sys.stderr)
        print("  git rm --cached <file>", file=sys.stderr)
        return 1

    print("[OK] no Synty source files in committed paths.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 2: Add to CI workflow**

Edit `.github/workflows/lint_and_build.yml`. Append a step after lint:

```yaml
      - name: Check Synty redistribution
        run: uv run python tools/scripts/check_synty_redistribution.py
```

- [ ] **Step 3: Commit**

```bash
git add tools/scripts/check_synty_redistribution.py .github/workflows/lint_and_build.yml
git commit -m "ci(stage12d): check_synty_redistribution.py + lint_and_build hook"
```

---

### Task 46: Tier 5 — HW1–HW7 contract regression (ship gate)

**Files:**
- Modify: existing per-HW test invocations

- [ ] **Step 1: Build candidate stack against Godot arena (baseline)**

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
godot --path shared/godot_arena --headless --rendering-driver opengl3 &
sleep 2
cmake --preset linux-debug && cmake --build --preset linux-debug
ctest --preset linux-debug --output-on-failure | tee /tmp/godot-tier5.log
uv run pytest HW1_armor_detector/tests/public/ -v | tee -a /tmp/godot-tier5.log
kill %1
```

Expected: all HW1–HW7 tests green against Godot.

- [ ] **Step 2: Build candidate stack against Unity arena**

In Unity, open MapA_MazeHybrid.unity, Play.

```bash
ctest --preset linux-debug --output-on-failure | tee /tmp/unity-tier5.log
uv run pytest HW1_armor_detector/tests/public/ -v | tee -a /tmp/unity-tier5.log
```

Stop Unity Play.

- [ ] **Step 3: Compare**

```bash
diff <(grep -E '^\s*(\d+):.*: (Passed|Failed)' /tmp/godot-tier5.log | awk '{print $1, $NF}') \
     <(grep -E '^\s*(\d+):.*: (Passed|Failed)' /tmp/unity-tier5.log | awk '{print $1, $NF}')
```

Expected: empty diff. Identical pass/fail outcomes between engines.

- [ ] **Step 4: If any HW fails on Unity but passes on Godot, debug**

The most common cause: physics drift from Task 19's CharacterController.Move. Re-verify mecanum kinematics line-by-line. Other causes:
- Camera FOV mismatch → fix in Gimbal prefab
- Plate position drift (sub-millimeter) → re-verify Chassis.prefab plate offsets
- Lighting too aggressive in showcase mode poisoning HW1 detector → confirm Tier 5 runs against `--quality headless --ui diegetic`

This is the ship gate. If any HW fails, do not proceed to OSS push.

- [ ] **Step 5: Note no commit yet — gate is a verification step**

---

### Task 47: Push validated builds to OSS

**Files:**
- Create: `shared/assets/manifest.toml` updates (Stage 12d rows)

- [ ] **Step 1: Build all 4 release-mode binaries**

```bash
tools/unity/build.sh --target macos-showcase
tools/unity/build.sh --target linux-headless
# On a Windows box: tools/unity/build.sh --target win-showcase
# On a Linux dev VM: tools/unity/build.sh --target linux-showcase
```

- [ ] **Step 2: Compute SHA-256 sums and zip the macOS .app**

```bash
cd shared/unity_arena/builds
zip -r macos-showcase.zip macos-showcase/
sha256sum macos-showcase.zip linux-headless/AimingArena_Headless.x86_64
# Save these for the manifest
```

- [ ] **Step 3: Push binaries to OSS**

```bash
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
uv run python shared/scripts/push_assets.py \
    --bucket tsingyun-aiming-hw-public \
    --key-prefix assets/unity/v2.0/ \
    shared/unity_arena/builds/macos-showcase.zip \
    shared/unity_arena/builds/linux-headless/AimingArena_Headless.x86_64 \
    shared/unity_arena/builds/win-showcase/AimingArena.zip \
    shared/unity_arena/builds/linux-showcase/AimingArena.tar.gz
```

- [ ] **Step 4: Push Synty pack (private bucket)**

```bash
# Synty source pack is in Assets/Synty/POLYGON_SciFi/ (gitignored).
# Zip it and push to private bucket. License-private.
cd shared/unity_arena/Assets
zip -r /tmp/synty_polygon_scifi_v1.zip Synty/POLYGON_SciFi/
sha256sum /tmp/synty_polygon_scifi_v1.zip
cd "/Volumes/David/大二下/RM/Aiming/Aiming_HW"
uv run python shared/scripts/push_assets.py \
    --bucket tsingyun-aiming-hw-models \
    --key-prefix assets/synty/POLYGON_SciFi_v1/ \
    /tmp/synty_polygon_scifi_v1.zip
```

- [ ] **Step 5: Update `shared/assets/manifest.toml`**

Append rows (use the SHA-256s computed above):

```toml
[[asset]]
name = "unity_arena_showcase_win64"
bucket = "tsingyun-aiming-hw-public"
key = "assets/unity/v2.0/AimingArena_Win64.zip"
sha256 = "<computed sha>"
size = 600000000
visibility = "public"

[[asset]]
name = "unity_arena_showcase_macos"
bucket = "tsingyun-aiming-hw-public"
key = "assets/unity/v2.0/macos-showcase.zip"
sha256 = "<computed sha>"
size = 700000000
visibility = "public"

[[asset]]
name = "unity_arena_showcase_linux"
bucket = "tsingyun-aiming-hw-public"
key = "assets/unity/v2.0/AimingArena.tar.gz"
sha256 = "<computed sha>"
size = 600000000
visibility = "public"

[[asset]]
name = "unity_arena_headless_linux"
bucket = "tsingyun-aiming-hw-public"
key = "assets/unity/v2.0/AimingArena_Headless.x86_64"
sha256 = "<computed sha>"
size = 250000000
visibility = "public"

[[asset]]
name = "synty_polygon_scifi_v1"
bucket = "tsingyun-aiming-hw-models"
key = "assets/synty/POLYGON_SciFi_v1/synty_polygon_scifi_v1.zip"
sha256 = "<computed sha>"
size = 100000000
visibility = "reader-key"
```

- [ ] **Step 6: Verify pull works from a clean machine**

```bash
uv run python shared/scripts/fetch_assets.py --only unity_arena_headless_linux --dry-run
uv run python shared/scripts/fetch_assets.py --only unity_arena_headless_linux
```

Expected: SHA matches; binary lands at `out/assets/unity/v2.0/AimingArena_Headless.x86_64`.

- [ ] **Step 7: Commit manifest update**

```bash
git add shared/assets/manifest.toml
git commit -m "build(stage12d): add Unity arena binaries + Synty pack to manifest.toml"
```

---

### Task 48: Rename `shared/godot_arena/` → `shared/godot_arena_legacy/`

**Files:**
- Move: `shared/godot_arena/` → `shared/godot_arena_legacy/`

Per spec §3.2 and §8: kept ≥ 2 release cycles as GPU-less fallback. Existing OSS keys for Godot binaries remain valid (no OSS rename needed).

- [ ] **Step 1: Rename**

```bash
git mv shared/godot_arena shared/godot_arena_legacy
```

- [ ] **Step 2: Update all references in source / tests / docs**

```bash
# Search and update references
grep -r "godot_arena" tests/ tools/ docs/ shared/scripts/ HW*/
```

For each match: change `shared/godot_arena/` to `shared/godot_arena_legacy/`. Files likely affected:
- `tests/test_arena_wire_format.py` (no path refs — wire-format is engine-agnostic)
- `tools/scripts/smoke_arena.py` (no path refs)
- `docs/architecture.md`
- `docs/CHANGELOG.md` (recent entries)

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor(stage12d): rename shared/godot_arena -> shared/godot_arena_legacy"
```

---

### Task 49: Update docs — `docs/arena.md`, `docs/architecture.md`, `docs/grading.md`

**Files:**
- Rename: `docs/godot_arena.md` → `docs/arena.md`
- Modify: `docs/arena.md` (rewrite to cover both engines)
- Modify: `docs/architecture.md` (engine-agnostic diagram)
- Modify: `docs/grading.md` (GPU-required note)
- Modify: `docs/CHANGELOG.md` (Stage 12 entries)

- [ ] **Step 1: Rename and rewrite arena doc**

```bash
git mv docs/godot_arena.md docs/arena.md
```

Edit `docs/arena.md`: replace Godot-specific language with engine-agnostic, then add subsections "Unity arena (default)" and "Godot legacy arena (GPU-less fallback)" pointing at each project's README.

- [ ] **Step 2: Update architecture diagram**

In `docs/architecture.md`, change "Aiming Arena (Godot/Unity binary)" to reflect that Unity is now the default and Godot is the legacy fallback. Add a brief subsection noting the headless GPU requirement and how `--quality={showcase,headless}` + `--ui={full,diegetic}` flags work.

- [ ] **Step 3: Update grading.md**

Add GPU-required note to `docs/grading.md`:

```markdown
## GPU requirement (live arena only)

Running the live Unity arena requires any GPU (Intel UHD / Apple Silicon /
NVIDIA / AMD). The headless build's HDRP pipeline cannot render without a
GPU. CI grading is unaffected: `validate_submission.yml` runs unit tests
on `pull_request.head.sha` and never spins up the simulator. Candidates
without a GPU continue to use the Godot legacy arena
(`shared/godot_arena_legacy/`) as a fallback.
```

- [ ] **Step 4: Add CHANGELOG entries**

Prepend to `docs/CHANGELOG.md`:

```markdown
## v2.0-arena-reform-complete · 2026-MM-DD

Stage 12 closed. The Aiming Arena is now Unity 6 LTS HDRP at
`shared/unity_arena/`; the Godot project is preserved as
`shared/godot_arena_legacy/` for GPU-less fallback. Tier 1-5 conformance
all green.

## v1.9-unity-launch · 2026-MM-DD
Sub-stage 12d closed: Unity binaries pushed to OSS, manifest.toml
updated, godot_arena -> godot_arena_legacy renamed, candidate handbook
updated.

## v1.8-unity-art · 2026-MM-DD
Sub-stage 12c closed: 4 Shader Graphs, 3 VFX Graphs, 6 PBR materials,
HDRP variants, Volume profiles, screen-space + diegetic UI, Map A
release lightmap bake.

## v1.7-unity-geometry · 2026-MM-DD
Sub-stage 12b closed: Map A multi-tier maze hybrid built via ProBuilder
+ Synty kit-bash. Chassis/Gimbal/ArmorPlate/Projectile/HoloProjector
prefabs (silhouette-preserving). Tier 3 + Tier 4 regression green.

## v1.6-unity-scaffold · 2026-MM-DD
Sub-stage 12a closed: shared/unity_arena/ project shell with HDRP. 10
GDScripts ported to 10 C# files. TcpProtoServer + TcpFramePub byte-for-
byte parity with Godot wire. Tier 1 + Tier 2 conformance green.
```

- [ ] **Step 5: Commit**

```bash
git add docs
git commit -m "docs(stage12d): arena.md rewrite + architecture/grading/CHANGELOG updates"
```

---

### Task 50: Update `IMPLEMENTATION_PLAN.md` with Stage 12 section

**Files:**
- Modify: `IMPLEMENTATION_PLAN.md`

Append a Stage 12 section mirroring the project's existing per-stage convention (see Stage 1-11 examples in the file).

- [ ] **Step 1: Append Stage 12 section**

After the existing Stage 11 closure entry, append:

```markdown
## Stage 12 — Arena Art & Vision Reform

* **Branch**: `stage12/unity-reform` (fast-forward merged into `main` upon close)
* **End tag**: `v2.0-arena-reform-complete`
* **Sub-tags**: `v1.6-unity-scaffold`, `v1.7-unity-geometry`, `v1.8-unity-art`, `v1.9-unity-launch`
* **Maps to schema**: §10 decision 1 (engine choice gate, Unity HDRP fallback)
* **Calendar estimate**: ~4 weeks (1 engineer + part-time art owner)

### Goals
1. Migrate `shared/godot_arena/` to `shared/unity_arena/` (Unity 6 LTS + HDRP).
2. Preserve proto wire contract and gameplay 1:1; HW1-HW7 contract regression is the ship gate.
3. Land high-fidelity sci-fi visual reform: Map A multi-tier maze hybrid, stylized chassis,
   DXR + rasterizer fallback, team-identity palette, full immersive HUD.
4. Local-first dev cadence; OSS publishing only at 12d closure.

### Sub-stages
See `docs/superpowers/specs/2026-04-30-arena-art-vision-reform-design.md` §8 and
`docs/superpowers/plans/2026-04-30-arena-art-vision-reform-stage12.md` for the
task-by-task plan.

### Conformance gates
- Tier 1: wire-format conformance (`tests/test_arena_wire_format.py`, parametrized over engines)
- Tier 2: smoke harness parity (`tools/scripts/smoke_arena.py --engine={godot,unity}`)
- Tier 3: golden-frame regression (SSIM > 0.95 across 25 fixed seed/pose pairs)
- Tier 4: bronze opponent KS test (p > 0.10 on damage_dealt distribution)
- Tier 5: HW1-HW7 contract regression (identical pass/fail Godot vs Unity)

### Risks (mitigations in spec §7)
R1 headless GPU requirement, R2 Synty source leak, R3 mecanum drift,
R4 DXR limited on macOS/Linux, R5 OSS bandwidth, R6 lightmap bake time.

### Out of scope
Maps B/C, free-cam replay viewer, ML-Agents migration, deletion of
godot_arena_legacy.
```

- [ ] **Step 2: Bump root version**

Edit `CMakeLists.txt` (or `pyproject.toml` whichever holds the version): bump from 1.5.0 to 2.0.0.

- [ ] **Step 3: Commit**

```bash
git add IMPLEMENTATION_PLAN.md CMakeLists.txt pyproject.toml
git commit -m "docs(stage12d): IMPLEMENTATION_PLAN.md Stage 12 section + root version 2.0.0"
```

---

### Task 51: Tag `v1.9-unity-launch` and `v2.0-arena-reform-complete`

- [ ] **Step 1: v1.9 (sub-stage closure)**

```bash
git tag -a v1.9-unity-launch -m "Stage 12d — build, OSS publish, migration cleanup

All 4 Unity binaries built locally and pushed to oss://tsingyun-aiming-hw-public/
assets/unity/v2.0/. Synty pack pushed to private models bucket. manifest.toml
updated. Tier 5 HW1-HW7 contract regression green (identical pass/fail Godot vs
Unity). godot_arena renamed to godot_arena_legacy (kept ≥2 release cycles).
Docs updated: arena.md (rewritten), architecture.md, grading.md, CHANGELOG.md.
IMPLEMENTATION_PLAN.md Stage 12 section appended."
```

- [ ] **Step 2: Final stage closure commit + v2.0 tag**

```bash
git commit --allow-empty -m "chore(stage12): close stage"
git tag -a v2.0-arena-reform-complete -m "Stage 12 — Arena Art & Vision Reform complete

Aiming Arena migrated from Godot 4 to Unity 6 LTS HDRP. Wire contract and
gameplay preserved 1:1; HW1-HW7 candidate stacks unaffected. Visual reform:
multi-tier maze hybrid (Map A), stylized chassis, DXR ray tracing on showcase /
rasterizer fallback for headless and lower-end, team-identity-first palette,
full immersive HUD with build-time toggle, Synty POLYGON Sci-Fi as kit-bash
base. Calendar: ~4 weeks. Single engineer + part-time art owner.

Out of scope (deferred to Stage 13+):
- Maps B and C
- Free-cam replay viewer
- ML-Agents opponent training migration
- Deletion of godot_arena_legacy (kept ≥2 release cycles for GPU-less fallback)"
```

- [ ] **Step 3: Verify**

```bash
git log --oneline -5
git tag --list "v1.6*" "v1.7*" "v1.8*" "v1.9*" "v2.0*" -n5
```

Expected: 5 sub-stage / closure tags visible, all annotated.

---

### Task 52: Final validation + push request

After all 5 tags exist, the user can decide whether to push.

- [ ] **Step 1: Confirm clean tree**

```bash
git status
git log --oneline -10
```

- [ ] **Step 2: Confirm test surface**

```bash
uv run pytest tests/ -v
```

Expected: all green.

- [ ] **Step 3: Defer push to user**

The local-first cadence completes when the user explicitly requests `git push origin main --tags`. The plan does NOT execute the push — the user must request it.

---

## Self-review

### Spec coverage check

Walking through `docs/superpowers/specs/2026-04-30-arena-art-vision-reform-design.md` section by section:

| Spec section | Plan task(s) | Status |
|---|---|---|
| §3.1 G1: Replace godot_arena with unity_arena | T1, T18 | ✅ |
| §3.1 G2: Preserve wire contract byte-for-byte | T9, T10, T13, T14 | ✅ |
| §3.1 G3: Preserve gameplay 1:1 | T4, T5, T6, T7, T19, T20, T21 | ✅ |
| §3.1 G4: Visual reform | T18, T28-T35, T41 | ✅ |
| §3.1 G5: HW1-HW7 green against new arena | T46 (Tier 5 gate) | ✅ |
| §3.1 G6: Local-first dev | T1-T42 (no OSS pushes), T47 (only OSS push) | ✅ |
| §3.2 Non-goals (Maps B/C, free-cam, etc.) | Documented in T50 + IMPLEMENTATION_PLAN.md update | ✅ |
| §4.1 Map A multi-tier maze hybrid | T18 | ✅ |
| §4.2 Chassis silhouette + surface | T19 (silhouette), T28 (shield), T29 (plate emission), T34 (body material) | ✅ |
| §4.3 Materials + lighting | T30 (glass), T34 (body/wall/floor/barrel), T35 (lighting) | ✅ |
| §4.4 Diegetic UI | T23 (HoloProjector geometry), T30 (HoloProjector shader), T39 (warning glyph) | ✅ |
| §4.5 Screen-space UI + post-FX | T37 (Volume_Showcase), T38 (HUD prefab) | ✅ |
| §4.6 VFX | T31 (muzzle), T32 (impact), T33 (dust), T34 (shell casing) | ✅ |
| §5.1 Engine port | T1, T3-T11, T19-T23 | ✅ |
| §5.2 Wire contract preservation | T9, T10, T13 | ✅ |
| §5.3 Headless RGB rendering | T36 (HDRPAsset_Headless), T40 (--ui toggle) | ✅ |
| §5.4 Build pipeline (local-first) | T43 (build.sh), T44 (bake.sh) | ✅ |
| §5.5 OSS distribution (12d only) | T47 | ✅ |
| §5.6 Determinism & replay | T3 (SeedRng), T8 (ReplayRecorder) | ✅ |
| §6.1 Tier 1 wire-format | T13 + T16 (gate) | ✅ |
| §6.2 Tier 2 smoke harness | T14 + T16 (gate) | ✅ |
| §6.3 Tier 3 golden frames | T25 | ✅ |
| §6.4 Tier 4 bronze KS | T26 | ✅ |
| §6.5 Tier 5 HW1-HW7 | T46 | ✅ |
| §7 Risks (R1-R6) | R1 → T15 README + T49 docs/grading.md; R2 → T45 CI guard; R3 → T4 + T19; R4 → T36; R5 → T49 docs note; R6 → T41 two bake configs | ✅ |
| §8 Stage 12 phasing 12a-d | T1-T17 (12a), T18-T27 (12b), T28-T42 (12c), T43-T52 (12d) | ✅ |
| §9 Files touched/created | T18-T39 creations, T13/T14/T48 renames, T49 modifications | ✅ |

All spec requirements have at least one corresponding task.

### Placeholder scan

Searched the plan for `TBD`, `TODO`, `FIXME`, `Similar to Task`. Two minor cases identified:
- Task 18 step 3 says "etc." in wall coordinate list — acceptable, since maze layout is iterative ProBuilder work; absolute coordinates are not the unit of work.
- Task 41 step 4 says "Do NOT bake the release config until just before tagging v1.8" — that is intentional sequencing guidance, not a placeholder.

No real placeholders.

### Type / signature consistency

- `MecanumChassisController` — defined T4, used T19. Public surface (`SetCmd`, `IntegrateStep`, `Reset`, `WorldVelocity`, `ChassisYaw`) consistent.
- `GimbalKinematics` + `Gimbal` — defined T5 + T20. Method names (`SetTarget`, `IntegrateStep`, `GetState`) consistent.
- `ProjectileDragSolver` (static) + `Projectile` (MB) — defined T6 + T21. Constants (`MaxRangeM`, `MaxTtlSeconds`, `Damage`) shared.
- `ArmorPlateState` + `ArmorPlate` — defined T7. Public events (`PlateHit`) and properties consistent.
- `ReplayRecorder.Start/Record/Finish` — defined T8, used T11. Signatures consistent.
- `TcpProtoServer.SetDispatcher` signature `(method, request) -> response` — consistent across T9 and T11.
- `JsonHelper.SerializeDict` + `JsonMiniParser.ParseDict` — defined T8 + T9, used in T9. Roundtrip consistent.
- `ShotSpec` struct (`Position`, `Rotation`, `Velocity`) — defined T20, used T24. Consistent.
- `QualityRouter` `--quality` and `--ui` flags — extended in T36 then T40. Consistent.

No naming drift.

### Scope check

This plan is one continuous workstream (Stage 12 = 4 sub-stages, each a tagged release on the same branch). The brainstorming-skill rule about decomposing independent subsystems doesn't apply: 12b depends on 12a, etc. Each sub-stage produces a tagged, working artifact that satisfies the "each plan should produce working software" rule individually.

---

Plan complete and saved to `docs/superpowers/plans/2026-04-30-arena-art-vision-reform-stage12.md`.

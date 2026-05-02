# Training RL And Visual Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the old combined training/visual cleanup item into two ordered, testable milestones: Milestone 4 builds the training ground, baseline non-player aiming, and RL scaffold; Milestone 5 polishes visual effects while preserving gameplay and perception readability.

**Architecture:** Milestone 4 comes first because RL needs a deterministic scene, target motion controls, baseline policy metrics, and a stable wire contract before useful training can begin. Milestone 5 comes after Milestone 4 because lighting, materials, particles, and post-processing can change camera observations; the visual pass must be validated against readability and smoke checks rather than changing the training semantics.

**Tech Stack:** Unity 6000.3.14f1, C# MonoBehaviours plus pure-C# solvers, Unity Test Framework EditMode/PlayMode tests, current length-prefixed TCP JSON wire protocol, protobuf-compatible schemas in `shared/proto`, Python 3.11 tooling managed by `uv`, HDRP 17.3.0, TextMesh Pro, ProBuilder, Synty assets kept in gitignored paths.

---

## Milestone Order Decision

The roadmap should use exactly two milestones for this split:

1. **Milestone 4: Training Ground And RL Scaffold**
   This includes the dedicated training scene, target translation and rotation controls, baseline non-player aiming, deterministic telemetry, and a minimal RL reset/step/training loop. The RL work is important enough to be named in this milestone, but the first tasks deliberately build training-ground and baseline prerequisites before creating the trainer.

2. **Milestone 5: Visual Effects And Readability Polish**
   This includes sci-fi industrial lighting, materials, particles, holographic markers, and screenshot/readability QA. It comes second so camera observations, reward fields, and training telemetry are stable before the art pass changes frame distribution.

During Milestone 4, keep visual work to minimal readability constraints: armor plates remain pure `#FF0000` and `#0000FF`, MNIST stickers remain visible, target silhouettes are not hidden by fog, and rule-zone markers remain distinguishable.

## File Structure

### Already Modified By This Planning Pass

- Modify: `docs/cleanup-roadmap.md`
  Records the split into Milestone 4 and Milestone 5 with concrete acceptance bullets.

### Milestone 4 Future Implementation Files

- Modify: `shared/proto/aiming.proto`
  Adds `TrainingConfig` to `EnvResetRequest` so target translation speed, rotation speed, path size, and baseline opponent mode are typed and protobuf-compatible.

- Modify: `shared/proto/sensor.proto`
  Adds `TrainingTelemetry` to `SensorBundle` so Python RL tooling can read target pose, target motion, player damage, hit rate, and per-step reward without scraping replay events.

- Modify: `docs/unity-wire-contract.md`
  Documents the new optional `training_config` request field and optional `training` sensor bundle field.

- Modify: `tests/test_arena_wire_format.py`
  Adds no-Unity protobuf JSON conformance tests for `TrainingConfig` and `TrainingTelemetry`.

- Modify: `tests/proto_roundtrip_test.cpp`
  Adds C++ protobuf round-trip coverage for new training fields.

- Create: `shared/unity_arena/Assets/Scripts/Training/TrainingTargetMotion.cs`
  Pure-C# deterministic ping-pong translation and yaw integration logic.

- Create: `shared/unity_arena/Assets/Scripts/Training/TrainingTargetController.cs`
  MonoBehaviour that applies `TrainingTargetMotion` to the target chassis.

- Create: `shared/unity_arena/Assets/Scripts/Training/BaselineAimSolver.cs`
  Pure-C# geometric-center aim solver for non-player aiming.

- Create: `shared/unity_arena/Assets/Scripts/Training/BaselineOpponentController.cs`
  MonoBehaviour that uses `BaselineAimSolver` to aim the non-player gimbal and fire under rate/heat constraints.

- Create: `shared/unity_arena/Assets/Scripts/Training/TrainingTelemetryBuilder.cs`
  Converts arena state and match metrics into the protobuf-compatible JSON dictionary carried in `SensorBundle.training`.

- Create: `shared/unity_arena/Assets/Scripts/UI/TrainingGroundPanel.cs`
  Small in-game UI with sliders for target translation speed and target rotation speed.

- Modify: `shared/unity_arena/Assets/Scripts/ArenaMain.cs`
  Parses `training_config`, enables training components on reset, steps training target/baseline components, and injects training telemetry into sensor bundles.

- Create: `shared/unity_arena/Assets/Editor/TrainingGroundSceneBuilder.cs`
  Editor script that creates or updates `Assets/Scenes/TrainingGround.unity` from existing prefabs and wiring conventions.

- Create: `shared/unity_arena/Assets/Tests/EditMode/TrainingTargetMotionTests.cs`
  EditMode coverage for deterministic target translation and yaw integration.

- Create: `shared/unity_arena/Assets/Tests/EditMode/BaselineAimSolverTests.cs`
  EditMode coverage for yaw/pitch math and pitch clamping.

- Create: `shared/unity_arena/Assets/Tests/PlayMode/TrainingGroundEpisodeTests.cs`
  PlayMode coverage for reset config, telemetry, baseline aiming, and target motion during an episode.

- Modify: `tools/scripts/smoke_arena.py`
  Adds training-mode CLI flags and validates training telemetry when requested.

- Create: `tools/rl/unity_training_client.py`
  Reusable TCP JSON client for RL scripts.

- Create: `tools/rl/aiming_env.py`
  Small Gym-like environment wrapper around Unity reset/step/fire/finish.

- Create: `tools/rl/random_policy_smoke.py`
  Deterministic random-policy smoke trainer that runs short training episodes without requiring serious RL convergence.

- Create: `tests/test_rl_training_client.py`
  Unit tests for request payload construction, reward extraction, and deterministic random action generation without launching Unity.

- Create: `docs/training-rl.md`
  Student/team-facing description of training-ground controls, baseline opponent behavior, RL observation fields, action fields, reward definition, and local smoke commands.

### Milestone 5 Future Implementation Files

- Create: `shared/unity_arena/Assets/Scripts/Visual/ArenaReadabilityMetrics.cs`
  Pure-C# helpers for color luminance, contrast, and threshold checks used by tests and QA scripts.

- Create: `shared/unity_arena/Assets/Scripts/Visual/VisualPolishProfile.cs`
  ScriptableObject schema for neon colors, fog density, bloom intensity, projectile trail colors, and rule-zone marker colors.

- Create: `shared/unity_arena/Assets/Editor/VisualPolishInstaller.cs`
  Editor script that creates HDRP materials, assigns allowed scene references, configures lights/volumes, and updates `MapA_MazeHybrid.unity` without committing Synty source assets.

- Modify: `shared/unity_arena/Assets/Scripts/HoloProjector.cs`
  Adds explicit material/color refresh so holographic labels remain visible after scene lighting changes.

- Modify: `shared/unity_arena/Assets/Scripts/RuleZoneMarkerRenderer.cs`
  Uses the visual profile colors while keeping boost/healing markers distinct in camera frames.

- Create: `shared/unity_arena/Assets/Scripts/Visual/ProjectileTrailVisual.cs`
  Adds projectile trail and muzzle flash hooks that do not affect projectile physics.

- Modify: `shared/unity_arena/Assets/Prefabs/Projectile.prefab`
  Adds `TrailRenderer`, a small muzzle/impact visual child, and `ProjectileTrailVisual`.

- Modify: `shared/unity_arena/Assets/Prefabs/Chassis.prefab`
  Adds non-gameplay emissive team identifiers while preserving pure red/blue armor plate base colors.

- Modify: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`
  Applies lighting, materials, fog, reflection, hologram, and marker updates.

- Create: `shared/unity_arena/Assets/Tests/EditMode/ArenaReadabilityMetricsTests.cs`
  EditMode coverage for contrast and color-threshold math.

- Create: `shared/unity_arena/Assets/Tests/PlayMode/VisualReadabilitySceneTests.cs`
  PlayMode coverage that checks required scene objects and marker/material assignments.

- Create: `tools/scripts/capture_unity_visual_qa.py`
  Captures frames from the Unity frame TCP stream and checks nonblank frames, red/blue armor visibility, neon highlight ratio, and overexposure ratio.

- Create: `docs/visual-polish-qa.md`
  Visual style and QA checklist for the sci-fi industrial pass.

---

## Milestone 4: Training Ground And RL Scaffold

### Task 1: Extend Typed Training Wire Fields

**Files:**
- Modify: `shared/proto/aiming.proto`
- Modify: `shared/proto/sensor.proto`
- Modify: `docs/unity-wire-contract.md`
- Modify: `tests/test_arena_wire_format.py`
- Modify: `tests/proto_roundtrip_test.cpp`

- [ ] **Step 1: Add Python conformance tests first**

Append these tests to `tests/test_arena_wire_format.py`:

```python
def test_training_config_parses() -> None:
    payload = {
        "seed": 42,
        "opponent_tier": "bronze",
        "oracle_hints": True,
        "duration_ns": 300_000_000_000,
        "training_config": {
            "enabled": True,
            "target_translation_speed_mps": 1.25,
            "target_rotation_speed_rad_s": 2.0,
            "target_path_half_extent_m": 2.5,
            "baseline_opponent_enabled": True,
        },
    }
    req = json_format.ParseDict(payload, aiming_pb2.EnvResetRequest())
    assert req.training_config.enabled is True
    assert req.training_config.target_translation_speed_mps == pytest.approx(1.25)
    assert req.training_config.target_rotation_speed_rad_s == pytest.approx(2.0)
    assert req.training_config.target_path_half_extent_m == pytest.approx(2.5)
    assert req.training_config.baseline_opponent_enabled is True


def test_sensor_bundle_with_training_telemetry_parses() -> None:
    payload = _unity_bundle(oracle=True, frame_id=3)
    payload["training"] = {
        "stamp_ns": 48_000_000,
        "target_position_world": {"x": 3.5, "y": 0.0, "z": 0.25},
        "target_velocity_world": {"x": 0.5, "y": 0.0, "z": 0.0},
        "target_yaw_world": 1.25,
        "target_yaw_rate": 2.0,
        "damage_dealt": 40,
        "projectiles_fired": 6,
        "armor_hits": 2,
        "player_hit_rate": 0.33333334,
        "step_reward": 0.4,
        "episode_done": False,
    }
    bundle = json_format.ParseDict(payload, sensor_pb2.SensorBundle())
    assert bundle.training.target_position_world.x == pytest.approx(3.5)
    assert bundle.training.target_velocity_world.x == pytest.approx(0.5)
    assert bundle.training.target_yaw_world == pytest.approx(1.25)
    assert bundle.training.target_yaw_rate == pytest.approx(2.0)
    assert bundle.training.damage_dealt == 40
    assert bundle.training.projectiles_fired == 6
    assert bundle.training.armor_hits == 2
    assert bundle.training.player_hit_rate == pytest.approx(1.0 / 3.0)
    assert bundle.training.step_reward == pytest.approx(0.4)
    assert bundle.training.episode_done is False
```

- [ ] **Step 2: Run the focused Python test and confirm it fails**

Run:

```bash
uv run pytest tests/test_arena_wire_format.py::test_training_config_parses tests/test_arena_wire_format.py::test_sensor_bundle_with_training_telemetry_parses -q
```

Expected: FAIL because `training_config` and `training` are not yet defined in the protobuf descriptors.

- [ ] **Step 3: Add `TrainingConfig` to `shared/proto/aiming.proto`**

Change `EnvResetRequest` to include field 5 and add this message below it:

```proto
message EnvResetRequest {
    uint64 seed = 1;
    string opponent_tier = 2;
    bool oracle_hints = 3;
    uint64 duration_ns = 4;
    TrainingConfig training_config = 5;
}

message TrainingConfig {
    bool enabled = 1;
    double target_translation_speed_mps = 2;
    double target_rotation_speed_rad_s = 3;
    double target_path_half_extent_m = 4;
    bool baseline_opponent_enabled = 5;
}
```

- [ ] **Step 4: Add `TrainingTelemetry` to `shared/proto/sensor.proto`**

Insert this message after `OracleHints`, then add `TrainingTelemetry training = 6;` to `SensorBundle`:

```proto
message TrainingTelemetry {
    uint64 stamp_ns = 1;
    Vector3 target_position_world = 2;
    Vector3 target_velocity_world = 3;
    double target_yaw_world = 4;
    double target_yaw_rate = 5;
    uint32 damage_dealt = 6;
    uint32 projectiles_fired = 7;
    uint32 armor_hits = 8;
    float player_hit_rate = 9;
    double step_reward = 10;
    bool episode_done = 11;
}

message SensorBundle {
    FrameRef     frame        = 1;
    Imu          imu          = 2;
    GimbalState  gimbal       = 3;
    ChassisOdom  odom         = 4;
    OracleHints  oracle       = 5;
    TrainingTelemetry training = 6;
}
```

- [ ] **Step 5: Update C++ protobuf round-trip coverage**

Append this test to `tests/proto_roundtrip_test.cpp`:

```cpp
TEST(ProtoRoundTrip, TrainingFieldsSurviveRoundTrip) {
    tsingyun::aiming::v1::EnvResetRequest req;
    req.set_seed(42);
    req.set_opponent_tier("bronze");
    req.mutable_training_config()->set_enabled(true);
    req.mutable_training_config()->set_target_translation_speed_mps(1.25);
    req.mutable_training_config()->set_target_rotation_speed_rad_s(2.0);
    req.mutable_training_config()->set_target_path_half_extent_m(2.5);
    req.mutable_training_config()->set_baseline_opponent_enabled(true);

    std::string bytes;
    ASSERT_TRUE(req.SerializeToString(&bytes));

    tsingyun::aiming::v1::EnvResetRequest out;
    ASSERT_TRUE(out.ParseFromString(bytes));
    EXPECT_TRUE(out.training_config().enabled());
    EXPECT_DOUBLE_EQ(out.training_config().target_translation_speed_mps(), 1.25);
    EXPECT_DOUBLE_EQ(out.training_config().target_rotation_speed_rad_s(), 2.0);
    EXPECT_DOUBLE_EQ(out.training_config().target_path_half_extent_m(), 2.5);
    EXPECT_TRUE(out.training_config().baseline_opponent_enabled());

    tsingyun::aiming::v1::SensorBundle bundle;
    bundle.mutable_training()->mutable_target_position_world()->set_x(3.5);
    bundle.mutable_training()->set_target_yaw_world(1.25);
    bundle.mutable_training()->set_damage_dealt(40);
    bundle.mutable_training()->set_step_reward(0.4);

    ASSERT_TRUE(bundle.SerializeToString(&bytes));

    tsingyun::aiming::v1::SensorBundle bundle_out;
    ASSERT_TRUE(bundle_out.ParseFromString(bytes));
    EXPECT_DOUBLE_EQ(bundle_out.training().target_position_world().x(), 3.5);
    EXPECT_DOUBLE_EQ(bundle_out.training().target_yaw_world(), 1.25);
    EXPECT_EQ(bundle_out.training().damage_dealt(), 40u);
    EXPECT_NEAR(bundle_out.training().step_reward(), 0.4, 1e-6);
}
```

- [ ] **Step 6: Document the new request and response fields**

Add a `Training Mode` section to `docs/unity-wire-contract.md`:

```markdown
## Training Mode

`env_reset` accepts an optional `training_config` object. Omit it, or set
`enabled=false`, for normal match behavior.

| Field | Type | Notes |
|-------|------|-------|
| `enabled` | bool | Enables the dedicated training-ground runtime helpers. |
| `target_translation_speed_mps` | double | Target ping-pong speed in meters per second. Values below zero are clamped to zero. |
| `target_rotation_speed_rad_s` | double | Target chassis yaw speed in radians per second. |
| `target_path_half_extent_m` | double | Half-width of the target's ping-pong path around its spawn point. Values below `0.1` use `0.1`. |
| `baseline_opponent_enabled` | bool | Enables geometric-center non-player aiming and firing. |

When training mode is enabled, each `SensorBundle` includes `training` telemetry
with target pose, target velocity, target yaw, target yaw rate, damage metrics,
hit rate, reward, and terminal status. These fields are for team tooling and RL
experiments; candidate assignments should still use the normal `frame`,
`gimbal`, `odom`, and optional `oracle` fields.
```

- [ ] **Step 7: Re-run protocol checks**

Run:

```bash
uv run pytest tests/test_arena_wire_format.py -q
cmake --build build/cpp -j
ctest --test-dir build/cpp --output-on-failure -R proto
```

Expected: Python wire tests pass, C++ build succeeds, and proto round-trip tests pass.

- [ ] **Step 8: Commit protocol changes**

Run:

```bash
git add shared/proto/aiming.proto shared/proto/sensor.proto docs/unity-wire-contract.md tests/test_arena_wire_format.py tests/proto_roundtrip_test.cpp
git commit -m "feat: add training mode wire fields"
```

### Task 2: Add Deterministic Target Motion

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/Training/TrainingTargetMotion.cs`
- Create: `shared/unity_arena/Assets/Scripts/Training/TrainingTargetController.cs`
- Create: `shared/unity_arena/Assets/Tests/EditMode/TrainingTargetMotionTests.cs`

- [ ] **Step 1: Create failing EditMode tests**

Create `shared/unity_arena/Assets/Tests/EditMode/TrainingTargetMotionTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class TrainingTargetMotionTests
    {
        [Test]
        public void Motion_AdvancesPositionAndYawDeterministically()
        {
            var motion = new TrainingTargetMotion(
                origin: new Vector3(3f, 0f, 0f),
                halfExtentMeters: 2f,
                translationSpeedMps: 1f,
                yawRateRadPerSecond: 2f);

            TrainingTargetSample first = motion.Step(0.5f);
            TrainingTargetSample second = motion.Step(0.5f);

            Assert.AreEqual(new Vector3(3.5f, 0f, 0f), first.Position);
            Assert.AreEqual(new Vector3(4.0f, 0f, 0f), second.Position);
            Assert.AreEqual(1.0f, first.YawRad, 1e-6f);
            Assert.AreEqual(2.0f, second.YawRad, 1e-6f);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), second.VelocityWorld);
            Assert.AreEqual(2.0f, second.YawRateRadPerSecond, 1e-6f);
        }

        [Test]
        public void Motion_BouncesAtConfiguredExtent()
        {
            var motion = new TrainingTargetMotion(
                origin: Vector3.zero,
                halfExtentMeters: 1f,
                translationSpeedMps: 2f,
                yawRateRadPerSecond: 0f);

            TrainingTargetSample first = motion.Step(0.75f);
            TrainingTargetSample second = motion.Step(0.5f);

            Assert.AreEqual(new Vector3(0.5f, 0f, 0f), first.Position);
            Assert.AreEqual(new Vector3(-0.5f, 0f, 0f), second.Position);
            Assert.AreEqual(new Vector3(-2f, 0f, 0f), second.VelocityWorld);
        }

        [Test]
        public void Motion_ClampsNegativeSpeeds()
        {
            var motion = new TrainingTargetMotion(
                origin: Vector3.zero,
                halfExtentMeters: -5f,
                translationSpeedMps: -3f,
                yawRateRadPerSecond: -4f);

            TrainingTargetSample sample = motion.Step(1f);

            Assert.AreEqual(Vector3.zero, sample.Position);
            Assert.AreEqual(Vector3.zero, sample.VelocityWorld);
            Assert.AreEqual(0f, sample.YawRad, 1e-6f);
            Assert.AreEqual(0f, sample.YawRateRadPerSecond, 1e-6f);
        }
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform EditMode -testResults /tmp/aiming-training-motion-editmode.xml -quit
```

Expected: FAIL because `TrainingTargetMotion` and `TrainingTargetSample` do not exist.

- [ ] **Step 3: Create the pure motion solver**

Create `shared/unity_arena/Assets/Scripts/Training/TrainingTargetMotion.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    public readonly struct TrainingTargetSample
    {
        public readonly Vector3 Position;
        public readonly Vector3 VelocityWorld;
        public readonly float YawRad;
        public readonly float YawRateRadPerSecond;

        public TrainingTargetSample(
            Vector3 position,
            Vector3 velocityWorld,
            float yawRad,
            float yawRateRadPerSecond)
        {
            Position = position;
            VelocityWorld = velocityWorld;
            YawRad = yawRad;
            YawRateRadPerSecond = yawRateRadPerSecond;
        }
    }

    public sealed class TrainingTargetMotion
    {
        private readonly Vector3 _origin;
        private readonly float _halfExtentMeters;
        private readonly float _translationSpeedMps;
        private readonly float _yawRateRadPerSecond;
        private float _offsetMeters;
        private float _direction = 1f;
        private float _yawRad;

        public TrainingTargetMotion(
            Vector3 origin,
            float halfExtentMeters,
            float translationSpeedMps,
            float yawRateRadPerSecond)
        {
            _origin = origin;
            _halfExtentMeters = Mathf.Max(0.1f, halfExtentMeters);
            _translationSpeedMps = Mathf.Max(0f, translationSpeedMps);
            _yawRateRadPerSecond = Mathf.Max(0f, yawRateRadPerSecond);
        }

        public TrainingTargetSample Step(float deltaSeconds)
        {
            float dt = Mathf.Max(0f, deltaSeconds);
            float previousOffset = _offsetMeters;
            float nextOffset = _offsetMeters + _direction * _translationSpeedMps * dt;

            while (nextOffset > _halfExtentMeters || nextOffset < -_halfExtentMeters)
            {
                if (nextOffset > _halfExtentMeters)
                {
                    nextOffset = _halfExtentMeters - (nextOffset - _halfExtentMeters);
                    _direction = -1f;
                }
                else
                {
                    nextOffset = -_halfExtentMeters + (-_halfExtentMeters - nextOffset);
                    _direction = 1f;
                }
            }

            _offsetMeters = nextOffset;
            _yawRad = WrapTwoPi(_yawRad + _yawRateRadPerSecond * dt);

            float velocityX = dt > 0f ? (_offsetMeters - previousOffset) / dt : 0f;
            return new TrainingTargetSample(
                _origin + new Vector3(_offsetMeters, 0f, 0f),
                new Vector3(velocityX, 0f, 0f),
                _yawRad,
                _yawRateRadPerSecond);
        }

        private static float WrapTwoPi(float angle)
        {
            float twoPi = Mathf.PI * 2f;
            angle %= twoPi;
            return angle < 0f ? angle + twoPi : angle;
        }
    }
}
```

- [ ] **Step 4: Create the target controller**

Create `shared/unity_arena/Assets/Scripts/Training/TrainingTargetController.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    public class TrainingTargetController : MonoBehaviour
    {
        public Chassis TargetChassis;
        public float TargetTranslationSpeedMps = 1f;
        public float TargetRotationSpeedRadPerSecond = 1f;
        public float TargetPathHalfExtentMeters = 2f;

        public TrainingTargetSample LatestSample { get; private set; }

        private TrainingTargetMotion _motion;
        private Vector3 _origin;

        public void Configure(
            Chassis targetChassis,
            float translationSpeedMps,
            float rotationSpeedRadPerSecond,
            float pathHalfExtentMeters)
        {
            TargetChassis = targetChassis;
            TargetTranslationSpeedMps = Mathf.Max(0f, translationSpeedMps);
            TargetRotationSpeedRadPerSecond = Mathf.Max(0f, rotationSpeedRadPerSecond);
            TargetPathHalfExtentMeters = Mathf.Max(0.1f, pathHalfExtentMeters);
            ResetMotion();
        }

        public void ResetMotion()
        {
            if (TargetChassis == null) return;
            _origin = TargetChassis.transform.position;
            _motion = new TrainingTargetMotion(
                _origin,
                TargetPathHalfExtentMeters,
                TargetTranslationSpeedMps,
                TargetRotationSpeedRadPerSecond);
            LatestSample = new TrainingTargetSample(_origin, Vector3.zero, TargetChassis.ChassisYaw, 0f);
        }

        private void FixedUpdate()
        {
            if (TargetChassis == null) return;
            if (_motion == null) ResetMotion();
            LatestSample = _motion.Step(Time.fixedDeltaTime);

            Transform targetTransform = TargetChassis.transform;
            targetTransform.position = LatestSample.Position;
            targetTransform.rotation = Quaternion.Euler(0f, LatestSample.YawRad * Mathf.Rad2Deg, 0f);
        }
    }
}
```

- [ ] **Step 5: Re-run EditMode tests**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform EditMode -testResults /tmp/aiming-training-motion-editmode.xml -quit
```

Expected: PASS for `TrainingTargetMotionTests`.

- [ ] **Step 6: Commit deterministic target motion**

Run:

```bash
git add shared/unity_arena/Assets/Scripts/Training shared/unity_arena/Assets/Tests/EditMode/TrainingTargetMotionTests.cs
git commit -m "feat: add deterministic training target motion"
```

### Task 3: Add Baseline Geometric-Center Aiming

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/Training/BaselineAimSolver.cs`
- Create: `shared/unity_arena/Assets/Scripts/Training/BaselineOpponentController.cs`
- Create: `shared/unity_arena/Assets/Tests/EditMode/BaselineAimSolverTests.cs`

- [ ] **Step 1: Create failing aim solver tests**

Create `shared/unity_arena/Assets/Tests/EditMode/BaselineAimSolverTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class BaselineAimSolverTests
    {
        [Test]
        public void Solve_AimsAtTargetCenterInYaw()
        {
            BaselineAimCommand cmd = BaselineAimSolver.Solve(
                shooterPosition: Vector3.zero,
                targetCenterWorld: new Vector3(3f, 0f, 3f));

            Assert.AreEqual(Mathf.PI / 4f, cmd.TargetYawRad, 1e-5f);
            Assert.AreEqual(0f, cmd.TargetPitchRad, 1e-5f);
            Assert.IsTrue(cmd.TargetVisible);
        }

        [Test]
        public void Solve_ComputesPositivePitchForHigherTarget()
        {
            BaselineAimCommand cmd = BaselineAimSolver.Solve(
                shooterPosition: Vector3.zero,
                targetCenterWorld: new Vector3(0f, 2f, 4f));

            Assert.AreEqual(0f, cmd.TargetYawRad, 1e-5f);
            Assert.Greater(cmd.TargetPitchRad, 0f);
            Assert.LessOrEqual(cmd.TargetPitchRad, GameConstants.GimbalPitchMaxRadians);
        }

        [Test]
        public void Solve_ClampsPitchToGimbalLimits()
        {
            BaselineAimCommand cmd = BaselineAimSolver.Solve(
                shooterPosition: Vector3.zero,
                targetCenterWorld: new Vector3(0f, 100f, 1f));

            Assert.AreEqual(GameConstants.GimbalPitchMaxRadians, cmd.TargetPitchRad, 1e-5f);
        }

        [Test]
        public void Solve_ReportsInvisibleWhenTargetOverlapsShooter()
        {
            BaselineAimCommand cmd = BaselineAimSolver.Solve(Vector3.zero, Vector3.zero);

            Assert.IsFalse(cmd.TargetVisible);
            Assert.AreEqual(0f, cmd.TargetYawRad, 1e-6f);
            Assert.AreEqual(0f, cmd.TargetPitchRad, 1e-6f);
        }
    }
}
```

- [ ] **Step 2: Run the solver tests and confirm they fail**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform EditMode -testResults /tmp/aiming-baseline-aim-editmode.xml -quit
```

Expected: FAIL because `BaselineAimSolver` and `BaselineAimCommand` do not exist.

- [ ] **Step 3: Create `BaselineAimSolver.cs`**

Create `shared/unity_arena/Assets/Scripts/Training/BaselineAimSolver.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    public readonly struct BaselineAimCommand
    {
        public readonly float TargetYawRad;
        public readonly float TargetPitchRad;
        public readonly bool TargetVisible;

        public BaselineAimCommand(float targetYawRad, float targetPitchRad, bool targetVisible)
        {
            TargetYawRad = targetYawRad;
            TargetPitchRad = targetPitchRad;
            TargetVisible = targetVisible;
        }
    }

    public static class BaselineAimSolver
    {
        public static BaselineAimCommand Solve(Vector3 shooterPosition, Vector3 targetCenterWorld)
        {
            Vector3 delta = targetCenterWorld - shooterPosition;
            Vector2 horizontal = new Vector2(delta.x, delta.z);
            float horizontalDistance = horizontal.magnitude;
            if (horizontalDistance < 1e-4f && Mathf.Abs(delta.y) < 1e-4f)
            {
                return new BaselineAimCommand(0f, 0f, false);
            }

            float yaw = Mathf.Atan2(delta.x, delta.z);
            float pitch = Mathf.Atan2(delta.y, Mathf.Max(horizontalDistance, 1e-4f));
            pitch = Mathf.Clamp(
                pitch,
                GameConstants.GimbalPitchMinRadians,
                GameConstants.GimbalPitchMaxRadians);

            return new BaselineAimCommand(yaw, pitch, true);
        }
    }
}
```

- [ ] **Step 4: Create `BaselineOpponentController.cs`**

Create `shared/unity_arena/Assets/Scripts/Training/BaselineOpponentController.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    public class BaselineOpponentController : MonoBehaviour
    {
        public Chassis Shooter;
        public Chassis Target;
        public bool FireWhenAligned = true;
        public float AlignmentToleranceRad = 0.05f;
        public float BurstIntervalSeconds = 0.4f;

        private float _nextFireTime;

        public BaselineAimCommand LatestCommand { get; private set; }
        public bool WantsFire { get; private set; }

        public void Configure(Chassis shooter, Chassis target, bool fireWhenAligned)
        {
            Shooter = shooter;
            Target = target;
            FireWhenAligned = fireWhenAligned;
            _nextFireTime = 0f;
            WantsFire = false;
        }

        private void FixedUpdate()
        {
            WantsFire = false;
            if (Shooter == null || Target == null || Shooter.Gimbal == null) return;
            if (Shooter.IsDestroyed || Target.IsDestroyed) return;

            LatestCommand = BaselineAimSolver.Solve(
                Shooter.Gimbal.transform.position,
                Target.transform.position + Vector3.up * 0.6f);

            if (!LatestCommand.TargetVisible) return;

            Shooter.Gimbal.SetTarget(LatestCommand.TargetYawRad, LatestCommand.TargetPitchRad, 0f, 0f);
            if (!FireWhenAligned || Time.time < _nextFireTime) return;

            GimbalState state = Shooter.Gimbal.GetState();
            float yawError = Mathf.Abs(Mathf.DeltaAngle(
                state.Yaw * Mathf.Rad2Deg,
                LatestCommand.TargetYawRad * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float pitchError = Mathf.Abs(state.Pitch - LatestCommand.TargetPitchRad);
            WantsFire = yawError <= AlignmentToleranceRad && pitchError <= AlignmentToleranceRad;
            if (WantsFire) _nextFireTime = Time.time + BurstIntervalSeconds;
        }
    }
}
```

- [ ] **Step 5: Re-run EditMode tests**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform EditMode -testResults /tmp/aiming-baseline-aim-editmode.xml -quit
```

Expected: PASS for `BaselineAimSolverTests`.

- [ ] **Step 6: Commit baseline aiming**

Run:

```bash
git add shared/unity_arena/Assets/Scripts/Training/BaselineAimSolver.cs shared/unity_arena/Assets/Scripts/Training/BaselineOpponentController.cs shared/unity_arena/Assets/Tests/EditMode/BaselineAimSolverTests.cs
git commit -m "feat: add baseline geometric aim solver"
```

### Task 4: Wire Training Mode Into Arena Runtime

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/Training/TrainingTelemetryBuilder.cs`
- Modify: `shared/unity_arena/Assets/Scripts/ArenaMain.cs`
- Create: `shared/unity_arena/Assets/Tests/PlayMode/TrainingGroundEpisodeTests.cs`

- [ ] **Step 1: Create failing PlayMode tests**

Create `shared/unity_arena/Assets/Tests/PlayMode/TrainingGroundEpisodeTests.cs`:

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
    public class TrainingGroundEpisodeTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();
        private int _sceneSerial;

        [UnityTest]
        public IEnumerator EnvReset_TrainingConfigEnablesTelemetry()
        {
            ArenaMain arena = CreateArena();
            yield return null;

            Dictionary<string, object> reset = arena.EnvReset(new Dictionary<string, object>
            {
                { "seed", 42L },
                { "oracle_hints", true },
                { "training_config", new Dictionary<string, object>
                {
                    { "enabled", true },
                    { "target_translation_speed_mps", 1.0 },
                    { "target_rotation_speed_rad_s", 2.0 },
                    { "target_path_half_extent_m", 2.0 },
                    { "baseline_opponent_enabled", true },
                }},
            });

            Dictionary<string, object> bundle = (Dictionary<string, object>)reset["bundle"];
            Assert.IsTrue(bundle.ContainsKey("training"));

            Dictionary<string, object> training = (Dictionary<string, object>)bundle["training"];
            Assert.AreEqual(0L, training["damage_dealt"]);
            Assert.AreEqual(false, training["episode_done"]);
        }

        [UnityTest]
        public IEnumerator EnvStep_TrainingTargetMovesAndReportsVelocity()
        {
            ArenaMain arena = CreateArena();
            yield return null;

            arena.EnvReset(new Dictionary<string, object>
            {
                { "seed", 43L },
                { "training_config", new Dictionary<string, object>
                {
                    { "enabled", true },
                    { "target_translation_speed_mps", 1.0 },
                    { "target_rotation_speed_rad_s", 1.0 },
                    { "target_path_half_extent_m", 2.0 },
                    { "baseline_opponent_enabled", false },
                }},
            });

            yield return new WaitForFixedUpdate();
            Dictionary<string, object> step = arena.EnvStep(new Dictionary<string, object>());
            Dictionary<string, object> training = (Dictionary<string, object>)step["training"];
            Dictionary<string, object> velocity = (Dictionary<string, object>)training["target_velocity_world"];

            Assert.Greater((double)velocity["x"], 0.0);
            Assert.Greater((double)training["target_yaw_world"], 0.0);
        }

        [UnityTest]
        public IEnumerator EnvFinish_TrainingTelemetryMarksEpisodeDone()
        {
            ArenaMain arena = CreateArena();
            yield return null;

            arena.EnvReset(new Dictionary<string, object>
            {
                { "seed", 44L },
                { "training_config", new Dictionary<string, object>
                {
                    { "enabled", true },
                    { "target_translation_speed_mps", 0.5 },
                    { "target_rotation_speed_rad_s", 0.5 },
                    { "target_path_half_extent_m", 1.0 },
                    { "baseline_opponent_enabled", true },
                }},
            });
            Dictionary<string, object> finish = arena.EnvFinish(new Dictionary<string, object>());

            Assert.AreEqual("ep-000000000000002c", finish["episode_id"]);
            Assert.IsTrue(finish.ContainsKey("damage_dealt"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject obj in _createdObjects)
            {
                if (obj != null) Object.Destroy(obj);
            }
            _createdObjects.Clear();
            yield return null;
        }

        private ArenaMain CreateArena()
        {
            SceneManager.CreateScene($"TrainingGroundEpisodeTest_{_sceneSerial++}");
            Chassis blue = CreateChassis("BlueChassis", "blue", new Vector3(-3f, 0f, 0f));
            Chassis red = CreateChassis("RedChassis", "red", new Vector3(3f, 0f, 0f));
            Transform blueSpawn = CreateTransform("SpawnPoint_Blue", new Vector3(-3f, 0f, 0f), 45f);
            Transform redSpawn = CreateTransform("SpawnPoint_Red", new Vector3(3f, 0f, 0f), -135f);
            Transform projectileRoot = CreateTransform("ProjectileRoot", Vector3.zero, 0f);

            var cameraObject = new GameObject("GimbalCamera");
            _createdObjects.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();

            var arenaObject = new GameObject("ArenaMain");
            _createdObjects.Add(arenaObject);
            arenaObject.SetActive(false);
            var arena = arenaObject.AddComponent<ArenaMain>();
            arena.ControlPort = 18654 + _sceneSerial * 2;
            arena.FramePort = arena.ControlPort + 1;
            arena.BlueChassis = blue;
            arena.RedChassis = red;
            arena.GimbalCamera = camera;
            arena.ProjectileRoot = projectileRoot;
            arena.SpawnPointBlue = blueSpawn;
            arena.SpawnPointRed = redSpawn;
            arenaObject.SetActive(true);
            return arena;
        }

        private Chassis CreateChassis(string name, string team, Vector3 position)
        {
            var chassisObject = new GameObject(name);
            _createdObjects.Add(chassisObject);
            chassisObject.transform.position = position;
            chassisObject.AddComponent<CharacterController>();

            var gimbalObject = new GameObject("Gimbal");
            gimbalObject.transform.SetParent(chassisObject.transform, false);
            gimbalObject.AddComponent<Gimbal>();

            CreateArmorPlate(chassisObject.transform, "ArmorPlateFront");
            CreateArmorPlate(chassisObject.transform, "ArmorPlateBack");
            CreateArmorPlate(chassisObject.transform, "ArmorPlateLeft");
            CreateArmorPlate(chassisObject.transform, "ArmorPlateRight");

            var chassis = chassisObject.AddComponent<Chassis>();
            chassis.Team = team;
            chassis.MaxHp = GameConstants.VehicleHpOneVsOne;
            return chassis;
        }

        private static void CreateArmorPlate(Transform parent, string name)
        {
            var plateObject = new GameObject(name);
            plateObject.transform.SetParent(parent, false);
            plateObject.AddComponent<ArmorPlate>();
        }

        private Transform CreateTransform(string name, Vector3 position, float yawDegrees)
        {
            var obj = new GameObject(name);
            _createdObjects.Add(obj);
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            return obj.transform;
        }
    }
}
```

- [ ] **Step 2: Run PlayMode tests and confirm they fail**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform PlayMode -testResults /tmp/aiming-training-ground-playmode.xml -quit
```

Expected: FAIL because `ArenaMain` does not parse `training_config` and does not emit `training`.

- [ ] **Step 3: Create `TrainingTelemetryBuilder.cs`**

Create `shared/unity_arena/Assets/Scripts/Training/TrainingTelemetryBuilder.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    public static class TrainingTelemetryBuilder
    {
        public static Dictionary<string, object> Build(
            long stampNs,
            TrainingTargetSample targetSample,
            MatchRuntimeState runtimeState,
            string playerTeam,
            bool episodeDone)
        {
            int damageDealt = runtimeState.DamageDealtByTeam(playerTeam);
            float hitRate = runtimeState.PlayerHitRate;
            double reward = damageDealt * 0.01
                + runtimeState.PlayerArmorHits * 0.1
                - runtimeState.PlayerProjectilesFired * 0.01;

            return new Dictionary<string, object>
            {
                { "stamp_ns", stampNs },
                { "target_position_world", Vec3Dict(targetSample.Position) },
                { "target_velocity_world", Vec3Dict(targetSample.VelocityWorld) },
                { "target_yaw_world", (double)targetSample.YawRad },
                { "target_yaw_rate", (double)targetSample.YawRateRadPerSecond },
                { "damage_dealt", damageDealt },
                { "projectiles_fired", runtimeState.PlayerProjectilesFired },
                { "armor_hits", runtimeState.PlayerArmorHits },
                { "player_hit_rate", (double)hitRate },
                { "step_reward", reward },
                { "episode_done", episodeDone },
            };
        }

        private static Dictionary<string, object> Vec3Dict(Vector3 v)
            => new Dictionary<string, object>
            {
                { "x", (double)v.x },
                { "y", (double)v.y },
                { "z", (double)v.z },
            };
    }
}
```

- [ ] **Step 4: Modify `ArenaMain.cs` training fields and reset parsing**

Add these fields near the existing private runtime fields:

```csharp
private bool _trainingEnabled;
private TrainingTargetController _trainingTarget;
private BaselineOpponentController _baselineOpponent;
private TrainingTargetSample _latestTrainingTargetSample;
```

Add this helper method near the existing `AsBool` helper:

```csharp
private static Dictionary<string, object> AsDict(Dictionary<string, object> dict, string key)
{
    if (!dict.TryGetValue(key, out var value) || value == null) return null;
    return value as Dictionary<string, object>;
}
```

Inside `EnvReset`, after red and blue chassis reset, parse and configure training mode:

```csharp
Dictionary<string, object> training = AsDict(request, "training_config");
_trainingEnabled = training != null && AsBool(training, "enabled", false);
if (_trainingEnabled)
{
    float targetTranslationSpeed = (float)AsDouble(training, "target_translation_speed_mps", 1.0);
    float targetRotationSpeed = (float)AsDouble(training, "target_rotation_speed_rad_s", 1.0);
    float targetPathHalfExtent = (float)AsDouble(training, "target_path_half_extent_m", 2.0);
    bool baselineEnabled = AsBool(training, "baseline_opponent_enabled", false);

    _trainingTarget = gameObject.GetComponent<TrainingTargetController>();
    if (_trainingTarget == null) _trainingTarget = gameObject.AddComponent<TrainingTargetController>();
    _trainingTarget.Configure(RedChassis, targetTranslationSpeed, targetRotationSpeed, targetPathHalfExtent);
    _latestTrainingTargetSample = _trainingTarget.LatestSample;

    _baselineOpponent = gameObject.GetComponent<BaselineOpponentController>();
    if (_baselineOpponent == null) _baselineOpponent = gameObject.AddComponent<BaselineOpponentController>();
    _baselineOpponent.enabled = baselineEnabled;
    _baselineOpponent.Configure(RedChassis, BlueChassis, fireWhenAligned: baselineEnabled);
}
else
{
    if (_trainingTarget != null) _trainingTarget.enabled = false;
    if (_baselineOpponent != null) _baselineOpponent.enabled = false;
    _latestTrainingTargetSample = new TrainingTargetSample(RedChassis.transform.position, RedChassis.LinearVelocity, RedChassis.ChassisYaw, 0f);
}
```

At the start of `BuildSensorBundle`, refresh target telemetry:

```csharp
if (_trainingEnabled && _trainingTarget != null)
{
    _latestTrainingTargetSample = _trainingTarget.LatestSample;
}
else
{
    _latestTrainingTargetSample = new TrainingTargetSample(
        RedChassis.transform.position,
        RedChassis.LinearVelocity,
        RedChassis.ChassisYaw,
        0f);
}
```

Then store the existing builder result in a local variable, add training telemetry only when enabled, and return it:

```csharp
var bundle = ArenaWirePayloadBuilder.BuildSensorBundle(
    FrameId,
    $"frames.{SeedRng.CurrentSeed()}",
    stamp,
    BlueChassis.GimbalState(),
    BlueChassis.OdomState(),
    OracleHints,
    RedChassis.transform.position,
    RedChassis.LinearVelocity);

if (_trainingEnabled)
{
    bundle["training"] = TrainingTelemetryBuilder.Build(
        stamp,
        _latestTrainingTargetSample,
        _rules.State,
        BlueChassis.Team,
        State != EpisodeState.Running);
}

return bundle;
```

- [ ] **Step 5: Re-run PlayMode training tests**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform PlayMode -testResults /tmp/aiming-training-ground-playmode.xml -quit
```

Expected: PASS for `TrainingGroundEpisodeTests`, with existing PlayMode tests still passing.

- [ ] **Step 6: Commit arena training runtime**

Run:

```bash
git add shared/unity_arena/Assets/Scripts/ArenaMain.cs shared/unity_arena/Assets/Scripts/Training/TrainingTelemetryBuilder.cs shared/unity_arena/Assets/Tests/PlayMode/TrainingGroundEpisodeTests.cs
git commit -m "feat: wire training mode into unity arena"
```

### Task 5: Build Training Ground Scene And UI

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/UI/TrainingGroundPanel.cs`
- Create: `shared/unity_arena/Assets/Editor/TrainingGroundSceneBuilder.cs`
- Create: `shared/unity_arena/Assets/Scenes/TrainingGround.unity`
- Modify: `shared/unity_arena/README.md`
- Modify: `docs/training-rl.md`

- [ ] **Step 1: Create the UI controller**

Create `shared/unity_arena/Assets/Scripts/UI/TrainingGroundPanel.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TsingYun.UnityArena
{
    public class TrainingGroundPanel : MonoBehaviour
    {
        public TrainingTargetController TargetController;
        public Slider TranslationSpeedSlider;
        public Slider RotationSpeedSlider;
        public TMP_Text TranslationSpeedLabel;
        public TMP_Text RotationSpeedLabel;

        private void Start()
        {
            if (TranslationSpeedSlider != null)
            {
                TranslationSpeedSlider.minValue = 0f;
                TranslationSpeedSlider.maxValue = 3f;
                TranslationSpeedSlider.value = TargetController != null ? TargetController.TargetTranslationSpeedMps : 1f;
                TranslationSpeedSlider.onValueChanged.AddListener(OnTranslationSpeedChanged);
            }

            if (RotationSpeedSlider != null)
            {
                RotationSpeedSlider.minValue = 0f;
                RotationSpeedSlider.maxValue = 8f;
                RotationSpeedSlider.value = TargetController != null ? TargetController.TargetRotationSpeedRadPerSecond : 1f;
                RotationSpeedSlider.onValueChanged.AddListener(OnRotationSpeedChanged);
            }

            RefreshLabels();
        }

        private void OnDestroy()
        {
            if (TranslationSpeedSlider != null) TranslationSpeedSlider.onValueChanged.RemoveListener(OnTranslationSpeedChanged);
            if (RotationSpeedSlider != null) RotationSpeedSlider.onValueChanged.RemoveListener(OnRotationSpeedChanged);
        }

        private void OnTranslationSpeedChanged(float value)
        {
            if (TargetController != null)
            {
                TargetController.TargetTranslationSpeedMps = value;
                TargetController.ResetMotion();
            }
            RefreshLabels();
        }

        private void OnRotationSpeedChanged(float value)
        {
            if (TargetController != null)
            {
                TargetController.TargetRotationSpeedRadPerSecond = value;
                TargetController.ResetMotion();
            }
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (TranslationSpeedLabel != null && TranslationSpeedSlider != null)
                TranslationSpeedLabel.text = $"Target speed {TranslationSpeedSlider.value:0.00} m/s";
            if (RotationSpeedLabel != null && RotationSpeedSlider != null)
                RotationSpeedLabel.text = $"Target spin {RotationSpeedSlider.value:0.00} rad/s";
        }
    }
}
```

- [ ] **Step 2: Create the scene builder**

Create `shared/unity_arena/Assets/Editor/TrainingGroundSceneBuilder.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TsingYun.UnityArena.EditorTools
{
    public static class TrainingGroundSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/TrainingGround.unity";

        [MenuItem("TsingYun/Build Training Ground Scene")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject arena = new GameObject("ArenaMain");
            var arenaMain = arena.AddComponent<ArenaMain>();
            var trainingTarget = arena.AddComponent<TrainingTargetController>();
            var baseline = arena.AddComponent<BaselineOpponentController>();
            arena.AddComponent<RuleZonePresentation>();

            GameObject blue = InstantiatePrefab("Assets/Prefabs/Chassis.prefab", "BlueChassis", new Vector3(-3f, 0f, 0f));
            GameObject red = InstantiatePrefab("Assets/Prefabs/Chassis.prefab", "RedChassis", new Vector3(3f, 0f, 0f));
            Chassis blueChassis = blue.GetComponent<Chassis>();
            Chassis redChassis = red.GetComponent<Chassis>();
            blueChassis.Team = "blue";
            redChassis.Team = "red";

            arenaMain.BlueChassis = blueChassis;
            arenaMain.RedChassis = redChassis;
            arenaMain.SpawnPointBlue = CreateMarker("SpawnPoint_Blue", new Vector3(-3f, 0f, 0f), 45f).transform;
            arenaMain.SpawnPointRed = CreateMarker("SpawnPoint_Red", new Vector3(3f, 0f, 0f), -135f).transform;
            arenaMain.ProjectileRoot = new GameObject("ProjectileRoot").transform;
            arenaMain.ProjectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Projectile.prefab");

            Camera camera = new GameObject("GimbalCamera").AddComponent<Camera>();
            arenaMain.GimbalCamera = camera;

            trainingTarget.Configure(redChassis, 1f, 1f, 2f);
            baseline.Configure(redChassis, blueChassis, fireWhenAligned: true);

            GameObject light = new GameObject("KeyLight");
            var directional = light.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 2.5f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            CreateFloor();
            CreatePanel(trainingTarget);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        private static GameObject InstantiatePrefab(string path, string name, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            instance.name = name;
            instance.transform.position = position;
            return instance;
        }

        private static GameObject CreateMarker(string name, Vector3 position, float yawDegrees)
        {
            var marker = new GameObject(name);
            marker.transform.position = position;
            marker.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            return marker;
        }

        private static void CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "TrainingFloor";
            floor.transform.localScale = new Vector3(1.2f, 1f, 0.8f);
        }

        private static void CreatePanel(TrainingTargetController targetController)
        {
            GameObject canvasObject = new GameObject("TrainingGroundCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = canvasObject.AddComponent<TrainingGroundPanel>();
            panel.TargetController = targetController;
            panel.TranslationSpeedSlider = CreateSlider(canvasObject.transform, "TranslationSpeedSlider", new Vector2(180f, -40f));
            panel.RotationSpeedSlider = CreateSlider(canvasObject.transform, "RotationSpeedSlider", new Vector2(180f, -90f));
            panel.TranslationSpeedLabel = CreateLabel(canvasObject.transform, "TranslationSpeedLabel", new Vector2(180f, -15f));
            panel.RotationSpeedLabel = CreateLabel(canvasObject.transform, "RotationSpeedLabel", new Vector2(180f, -65f));
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPosition)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(240f, 24f);
            return obj.AddComponent<Slider>();
        }

        private static TMP_Text CreateLabel(Transform parent, string name, Vector2 anchoredPosition)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(260f, 24f);
            var text = obj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.color = Color.white;
            return text;
        }
    }
}
```

- [ ] **Step 3: Generate the scene in Unity**

Run from Unity Editor:

```text
TsingYun -> Build Training Ground Scene
```

Then open `Assets/Scenes/TrainingGround.unity`, click Play, and confirm that:

- the blue chassis spawns on the left
- the red target spawns on the right
- the UI sliders are visible in the top-left
- changing sliders changes target motion after the next target reset

- [ ] **Step 4: Add training documentation**

Create `docs/training-rl.md`:

```markdown
# Training Ground And RL Scaffold

`Assets/Scenes/TrainingGround.unity` is the dedicated auto-aim and policy
training scene. It keeps the Unity runtime contract from
`docs/unity-wire-contract.md` and adds optional `training_config` on
`env_reset`.

## Local training-ground smoke

1. Open `shared/unity_arena` in Unity 6000.3.14f1.
2. Open `Assets/Scenes/TrainingGround.unity`.
3. Click Play.
4. In a terminal from the repo root, run:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/smoke_arena.py \
  --seed 42 \
  --ticks 30 \
  --training \
  --target-translation-speed 1.0 \
  --target-rotation-speed 2.0
```

The smoke should print `[training]` lines with target position, velocity,
damage, hit rate, and reward.

## Baseline opponent

The baseline opponent aims at the geometric center of the enemy chassis. It is
not an RL policy and does not lead moving targets. Its purpose is to give the
training scene a reproducible non-player behavior and a comparison point for
future RL policies.

## RL observation and action fields

The first RL wrapper uses these observations:

- own gimbal yaw, pitch, yaw rate, and pitch rate
- own chassis world position and velocity
- target world position and velocity from training telemetry
- target yaw and yaw rate from training telemetry
- damage dealt, projectiles fired, armor hits, and hit rate

The first action vector controls:

- target yaw command in radians
- target pitch command in radians
- fire request as a binary action

The first reward is:

`damage_dealt * 0.01 + armor_hits * 0.1 - projectiles_fired * 0.01`

This reward is intentionally simple so the smoke trainer validates the loop
before policy quality becomes the focus.
```

Add a link to this file in `shared/unity_arena/README.md` under `Tests`:

```markdown
Training ground and RL scaffold notes: [`docs/training-rl.md`](../../docs/training-rl.md)
```

- [ ] **Step 5: Commit scene and docs**

Run:

```bash
git add shared/unity_arena/Assets/Scripts/UI/TrainingGroundPanel.cs shared/unity_arena/Assets/Editor/TrainingGroundSceneBuilder.cs shared/unity_arena/Assets/Scenes/TrainingGround.unity shared/unity_arena/README.md docs/training-rl.md
git commit -m "feat: add training ground scene and controls"
```

### Task 6: Add Training Smoke CLI And RL Client

**Files:**
- Modify: `tools/scripts/smoke_arena.py`
- Create: `tools/rl/unity_training_client.py`
- Create: `tools/rl/aiming_env.py`
- Create: `tools/rl/random_policy_smoke.py`
- Create: `tests/test_rl_training_client.py`

- [ ] **Step 1: Create Python unit tests for RL helper behavior**

Create `tests/test_rl_training_client.py`:

```python
from __future__ import annotations

import random

from tools.rl.aiming_env import AimingAction, build_training_reset, reward_from_training
from tools.rl.random_policy_smoke import sample_action


def test_build_training_reset_payload() -> None:
    payload = build_training_reset(
        seed=42,
        target_translation_speed=1.25,
        target_rotation_speed=2.0,
        baseline_opponent=True,
    )
    assert payload["seed"] == 42
    assert payload["opponent_tier"] == "bronze"
    assert payload["oracle_hints"] is True
    assert payload["training_config"] == {
        "enabled": True,
        "target_translation_speed_mps": 1.25,
        "target_rotation_speed_rad_s": 2.0,
        "target_path_half_extent_m": 2.0,
        "baseline_opponent_enabled": True,
    }


def test_reward_from_training_uses_step_reward() -> None:
    bundle = {"training": {"step_reward": 1.75}}
    assert reward_from_training(bundle) == 1.75


def test_sample_action_is_deterministic_for_seeded_rng() -> None:
    rng_a = random.Random(123)
    rng_b = random.Random(123)
    assert sample_action(rng_a) == sample_action(rng_b)


def test_action_to_step_payload() -> None:
    action = AimingAction(target_yaw=0.5, target_pitch=-0.1, fire=True)
    assert action.to_step_payload(stamp_ns=16_000_000) == {
        "stamp_ns": 16_000_000,
        "target_yaw": 0.5,
        "target_pitch": -0.1,
        "yaw_rate_ff": 0.0,
        "pitch_rate_ff": 0.0,
    }
    assert action.to_fire_payload(stamp_ns=16_000_000) == {
        "stamp_ns": 16_000_000,
        "burst_count": 1,
    }
```

- [ ] **Step 2: Run Python tests and confirm they fail**

Run:

```bash
uv run pytest tests/test_rl_training_client.py -q
```

Expected: FAIL because `tools.rl` modules do not exist.

- [ ] **Step 3: Create the TCP training client**

Create `tools/rl/unity_training_client.py`:

```python
from __future__ import annotations

import json
import socket
import struct
from dataclasses import dataclass
from typing import Any


@dataclass
class UnityTrainingClient:
    host: str = "127.0.0.1"
    port: int = 7654
    timeout_seconds: float = 5.0

    def __enter__(self) -> "UnityTrainingClient":
        self._sock = socket.create_connection((self.host, self.port), timeout=self.timeout_seconds)
        return self

    def __exit__(self, exc_type: object, exc: object, tb: object) -> None:
        self.close()

    def close(self) -> None:
        sock = getattr(self, "_sock", None)
        if sock is not None:
            sock.close()
            self._sock = None

    def call(self, method: str, request: dict[str, Any]) -> dict[str, Any]:
        sock = self._sock
        body = json.dumps({"method": method, "request": request}).encode("utf-8")
        sock.sendall(struct.pack(">I", len(body)) + body)
        header = self._recv_exact(4)
        (length,) = struct.unpack(">I", header)
        response = json.loads(self._recv_exact(length).decode("utf-8"))
        if not response.get("ok"):
            raise RuntimeError(response.get("error", f"{method} failed"))
        return response["response"]

    def _recv_exact(self, n: int) -> bytes:
        chunks: list[bytes] = []
        remaining = n
        while remaining:
            chunk = self._sock.recv(remaining)
            if not chunk:
                raise ConnectionError("Unity arena closed the TCP connection")
            chunks.append(chunk)
            remaining -= len(chunk)
        return b"".join(chunks)
```

- [ ] **Step 4: Create the Gym-like wrapper**

Create `tools/rl/aiming_env.py`:

```python
from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from tools.rl.unity_training_client import UnityTrainingClient


@dataclass(frozen=True)
class AimingAction:
    target_yaw: float
    target_pitch: float
    fire: bool

    def to_step_payload(self, stamp_ns: int) -> dict[str, Any]:
        return {
            "stamp_ns": stamp_ns,
            "target_yaw": self.target_yaw,
            "target_pitch": self.target_pitch,
            "yaw_rate_ff": 0.0,
            "pitch_rate_ff": 0.0,
        }

    def to_fire_payload(self, stamp_ns: int) -> dict[str, Any]:
        return {"stamp_ns": stamp_ns, "burst_count": 1 if self.fire else 0}


def build_training_reset(
    *,
    seed: int,
    target_translation_speed: float,
    target_rotation_speed: float,
    baseline_opponent: bool,
) -> dict[str, Any]:
    return {
        "seed": seed,
        "opponent_tier": "bronze",
        "oracle_hints": True,
        "duration_ns": 300_000_000_000,
        "training_config": {
            "enabled": True,
            "target_translation_speed_mps": target_translation_speed,
            "target_rotation_speed_rad_s": target_rotation_speed,
            "target_path_half_extent_m": 2.0,
            "baseline_opponent_enabled": baseline_opponent,
        },
    }


def reward_from_training(bundle: dict[str, Any]) -> float:
    return float(bundle["training"]["step_reward"])


class AimingTrainingEnv:
    def __init__(
        self,
        client: UnityTrainingClient,
        *,
        seed: int,
        target_translation_speed: float,
        target_rotation_speed: float,
        baseline_opponent: bool,
    ) -> None:
        self.client = client
        self.seed = seed
        self.target_translation_speed = target_translation_speed
        self.target_rotation_speed = target_rotation_speed
        self.baseline_opponent = baseline_opponent
        self.tick = 0

    def reset(self) -> dict[str, Any]:
        self.tick = 0
        response = self.client.call(
            "env_reset",
            build_training_reset(
                seed=self.seed,
                target_translation_speed=self.target_translation_speed,
                target_rotation_speed=self.target_rotation_speed,
                baseline_opponent=self.baseline_opponent,
            ),
        )
        return response["bundle"]

    def step(self, action: AimingAction) -> tuple[dict[str, Any], float, bool]:
        stamp_ns = self.tick * 16_000_000
        bundle = self.client.call("env_step", action.to_step_payload(stamp_ns))
        if action.fire:
            self.client.call("env_push_fire", action.to_fire_payload(stamp_ns))
        self.tick += 1
        reward = reward_from_training(bundle)
        done = bool(bundle["training"]["episode_done"])
        return bundle, reward, done

    def finish(self) -> dict[str, Any]:
        return self.client.call("env_finish", {"flush_replay": True})
```

- [ ] **Step 5: Create the random policy smoke script**

Create `tools/rl/random_policy_smoke.py`:

```python
from __future__ import annotations

import argparse
import random

from tools.rl.aiming_env import AimingAction, AimingTrainingEnv
from tools.rl.unity_training_client import UnityTrainingClient


def sample_action(rng: random.Random) -> AimingAction:
    return AimingAction(
        target_yaw=rng.uniform(-0.75, 0.75),
        target_pitch=rng.uniform(-0.2, 0.2),
        fire=rng.random() < 0.25,
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=7654)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--ticks", type=int, default=60)
    parser.add_argument("--target-translation-speed", type=float, default=1.0)
    parser.add_argument("--target-rotation-speed", type=float, default=2.0)
    parser.add_argument("--baseline-opponent", action="store_true")
    args = parser.parse_args()

    rng = random.Random(args.seed)
    with UnityTrainingClient(args.host, args.port) as client:
        env = AimingTrainingEnv(
            client,
            seed=args.seed,
            target_translation_speed=args.target_translation_speed,
            target_rotation_speed=args.target_rotation_speed,
            baseline_opponent=args.baseline_opponent,
        )
        env.reset()
        total_reward = 0.0
        for _ in range(args.ticks):
            _, reward, done = env.step(sample_action(rng))
            total_reward += reward
            if done:
                break
        stats = env.finish()

    print(
        f"[rl-smoke] seed={args.seed} ticks={args.ticks} "
        f"total_reward={total_reward:.3f} damage_dealt={stats['damage_dealt']} "
        f"projectiles_fired={stats['projectiles_fired']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 6: Extend `smoke_arena.py` with training flags**

Add arguments in `main()`:

```python
parser.add_argument("--training", action="store_true")
parser.add_argument("--target-translation-speed", default=1.0, type=float)
parser.add_argument("--target-rotation-speed", default=2.0, type=float)
parser.add_argument("--baseline-opponent", action="store_true")
```

Build the reset request before `env_reset`:

```python
reset_request = {
    "seed": args.seed,
    "opponent_tier": "bronze",
    "oracle_hints": True,
    "duration_ns": 5_000_000_000,
}
if args.training:
    reset_request["training_config"] = {
        "enabled": True,
        "target_translation_speed_mps": args.target_translation_speed,
        "target_rotation_speed_rad_s": args.target_rotation_speed,
        "target_path_half_extent_m": 2.0,
        "baseline_opponent_enabled": args.baseline_opponent,
    }
```

Replace the hard-coded reset payload with `reset_request`. After each parsed step bundle, add:

```python
if args.training:
    training = reply["response"]["training"]
    print(
        f"[training {tick:03d}] "
        f"target_x={training['target_position_world']['x']:+.3f} "
        f"target_vx={training['target_velocity_world']['x']:+.3f} "
        f"reward={training['step_reward']:+.3f} "
        f"hit_rate={training['player_hit_rate']:.3f}"
    )
```

- [ ] **Step 7: Run Python tests**

Run:

```bash
uv run pytest tests/test_rl_training_client.py tests/test_arena_wire_format.py -q
```

Expected: PASS.

- [ ] **Step 8: Run local Unity training smokes**

With `TrainingGround.unity` open and Play mode running, run:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30 --training --target-translation-speed 1.0 --target-rotation-speed 2.0
UV_CACHE_DIR=.uv-cache uv run python tools/rl/random_policy_smoke.py --seed 42 --ticks 30 --target-translation-speed 1.0 --target-rotation-speed 2.0
```

Expected: first command prints `[training ...]` lines; second command prints one `[rl-smoke]` summary and exits with status 0.

- [ ] **Step 9: Commit smoke and RL scaffold**

Run:

```bash
git add tools/scripts/smoke_arena.py tools/rl tests/test_rl_training_client.py
git commit -m "feat: add rl training smoke scaffold"
```

---

## Milestone 5: Visual Effects And Readability Polish

### Task 7: Add Visual Readability Metrics Before Art Changes

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/Visual/ArenaReadabilityMetrics.cs`
- Create: `shared/unity_arena/Assets/Tests/EditMode/ArenaReadabilityMetricsTests.cs`
- Create: `docs/visual-polish-qa.md`

- [ ] **Step 1: Create failing readability tests**

Create `shared/unity_arena/Assets/Tests/EditMode/ArenaReadabilityMetricsTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class ArenaReadabilityMetricsTests
    {
        [Test]
        public void PureTeamColorsPassSchemaThresholds()
        {
            Assert.IsTrue(ArenaReadabilityMetrics.IsPureRedArmor(new Color(1f, 0f, 0f, 1f)));
            Assert.IsTrue(ArenaReadabilityMetrics.IsPureBlueArmor(new Color(0f, 0f, 1f, 1f)));
        }

        [Test]
        public void TeamColorsHaveDifferentDominantChannels()
        {
            Assert.Greater(
                ArenaReadabilityMetrics.DominantChannelGap(new Color(1f, 0f, 0f, 1f)),
                0.9f);
            Assert.Greater(
                ArenaReadabilityMetrics.DominantChannelGap(new Color(0f, 0f, 1f, 1f)),
                0.9f);
        }

        [Test]
        public void DarkBackgroundKeepsNeonContrastReadable()
        {
            float contrast = ArenaReadabilityMetrics.ContrastRatio(
                foreground: new Color(0f, 0.85f, 1f, 1f),
                background: new Color(0.02f, 0.02f, 0.025f, 1f));

            Assert.GreaterOrEqual(contrast, 4.5f);
        }
    }
}
```

- [ ] **Step 2: Run EditMode tests and confirm they fail**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform EditMode -testResults /tmp/aiming-readability-editmode.xml -quit
```

Expected: FAIL because `ArenaReadabilityMetrics` does not exist.

- [ ] **Step 3: Create readability metric helpers**

Create `shared/unity_arena/Assets/Scripts/Visual/ArenaReadabilityMetrics.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    public static class ArenaReadabilityMetrics
    {
        public static bool IsPureRedArmor(Color color)
            => color.r >= 0.98f && color.g <= 0.02f && color.b <= 0.02f;

        public static bool IsPureBlueArmor(Color color)
            => color.b >= 0.98f && color.r <= 0.02f && color.g <= 0.02f;

        public static float DominantChannelGap(Color color)
        {
            float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            float mid = color.r + color.g + color.b
                - max
                - Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            return max - mid;
        }

        public static float RelativeLuminance(Color color)
        {
            float r = Linearize(color.r);
            float g = Linearize(color.g);
            float b = Linearize(color.b);
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        public static float ContrastRatio(Color foreground, Color background)
        {
            float a = RelativeLuminance(foreground);
            float b = RelativeLuminance(background);
            float lighter = Mathf.Max(a, b);
            float darker = Mathf.Min(a, b);
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        private static float Linearize(float channel)
            => channel <= 0.03928f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }
}
```

- [ ] **Step 4: Create visual QA documentation**

Create `docs/visual-polish-qa.md`:

```markdown
# Visual Polish QA

The visual pass targets the sci-fi industrial style in `schema.md`, but it must
not reduce gameplay readability.

## Non-negotiable readability gates

- Red armor base color remains pure `#FF0000`.
- Blue armor base color remains pure `#0000FF`.
- MNIST stickers remain visible on every armor plate.
- Boost and healing markers use distinct colors and shapes.
- Fog and bloom do not hide target silhouettes in the gimbal camera.
- Projectile trails and muzzle flashes do not change projectile physics.
- Synty source assets remain under gitignored `Assets/Synty/` paths.

## Local visual QA command

With Unity Play mode running in `MapA_MazeHybrid.unity`:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/capture_unity_visual_qa.py --frames 20
```

The command must report nonblank frames, visible red and blue pixel clusters,
bounded neon ratio, and bounded overexposure ratio.
```

- [ ] **Step 5: Re-run readability tests**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform EditMode -testResults /tmp/aiming-readability-editmode.xml -quit
```

Expected: PASS for `ArenaReadabilityMetricsTests`.

- [ ] **Step 6: Commit readability gates**

Run:

```bash
git add shared/unity_arena/Assets/Scripts/Visual/ArenaReadabilityMetrics.cs shared/unity_arena/Assets/Tests/EditMode/ArenaReadabilityMetricsTests.cs docs/visual-polish-qa.md
git commit -m "test: add visual readability gates"
```

### Task 8: Install Visual Polish Profile And Scene Materials

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/Visual/VisualPolishProfile.cs`
- Create: `shared/unity_arena/Assets/Editor/VisualPolishInstaller.cs`
- Modify: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`
- Modify: `shared/unity_arena/Assets/Prefabs/Chassis.prefab`
- Modify: `shared/unity_arena/Assets/Prefabs/Projectile.prefab`

- [ ] **Step 1: Create the visual profile type**

Create `shared/unity_arena/Assets/Scripts/Visual/VisualPolishProfile.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    [CreateAssetMenu(menuName = "TsingYun/Arena Visual Polish Profile")]
    public class VisualPolishProfile : ScriptableObject
    {
        public Color WallBaseColor = new Color(0.03f, 0.035f, 0.04f, 1f);
        public Color CarbonPanelColor = new Color(0.005f, 0.007f, 0.009f, 1f);
        public Color CyanNeonColor = new Color(0f, 0.85f, 1f, 1f);
        public Color MagentaNeonColor = new Color(1f, 0.05f, 0.75f, 1f);
        public Color BoostMarkerColor = new Color(1f, 0.86f, 0.1f, 1f);
        public Color HealingMarkerColor = new Color(0.1f, 1f, 0.55f, 1f);
        public float BloomIntensity = 0.35f;
        public float FogDensity = 0.012f;
        public float ProjectileTrailLifetime = 0.18f;
    }
}
```

- [ ] **Step 2: Create the installer**

Create `shared/unity_arena/Assets/Editor/VisualPolishInstaller.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TsingYun.UnityArena.EditorTools
{
    public static class VisualPolishInstaller
    {
        private const string ProfilePath = "Assets/Rendering/VisualPolishProfile.asset";
        private const string ScenePath = "Assets/Scenes/MapA_MazeHybrid.unity";

        [MenuItem("TsingYun/Install Visual Polish Profile")]
        public static void Install()
        {
            Directory.CreateDirectory("Assets/Rendering");
            VisualPolishProfile profile = AssetDatabase.LoadAssetAtPath<VisualPolishProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VisualPolishProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            CreateHdrpMaterial("Assets/Materials/IndustrialWall_Dark.mat", profile.WallBaseColor, metallic: 0.8f, smoothness: 0.45f);
            CreateHdrpMaterial("Assets/Materials/CarbonPanel_Gloss.mat", profile.CarbonPanelColor, metallic: 0.5f, smoothness: 0.8f);
            CreateHdrpMaterial("Assets/Materials/Neon_Cyan.mat", profile.CyanNeonColor, metallic: 0f, smoothness: 0.6f);
            CreateHdrpMaterial("Assets/Materials/Neon_Magenta.mat", profile.MagentaNeonColor, metallic: 0f, smoothness: 0.6f);

            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                EnsureLight("NeonKey_Cyan", profile.CyanNeonColor, new Vector3(-4f, 3f, -2f), 6f);
                EnsureLight("NeonKey_Magenta", profile.MagentaNeonColor, new Vector3(4f, 3f, 2f), 4f);
                EditorSceneManager.SaveOpenScenes();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateHdrpMaterial(string path, Color color, float metallic, float smoothness)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_EmissiveColor") && (color.r > 0.5f || color.g > 0.5f || color.b > 0.5f))
            {
                material.SetColor("_EmissiveColor", color * 1.5f);
            }
        }

        private static void EnsureLight(string name, Color color, Vector3 position, float intensity)
        {
            GameObject existing = GameObject.Find(name);
            GameObject lightObject = existing != null ? existing : new GameObject(name);
            lightObject.transform.position = position;
            Light light = lightObject.GetComponent<Light>();
            if (light == null) light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 8f;
        }
    }
}
```

- [ ] **Step 3: Run installer in Unity Editor**

Run from Unity Editor:

```text
TsingYun -> Install Visual Polish Profile
```

Confirm these assets exist:

- `Assets/Rendering/VisualPolishProfile.asset`
- `Assets/Materials/IndustrialWall_Dark.mat`
- `Assets/Materials/CarbonPanel_Gloss.mat`
- `Assets/Materials/Neon_Cyan.mat`
- `Assets/Materials/Neon_Magenta.mat`

- [ ] **Step 4: Update prefabs in Unity Editor**

In Prefab Mode:

- Open `Assets/Prefabs/Chassis.prefab`.
- Keep armor plate base colors pure red and pure blue.
- Add small emissive team identifier strips to chassis body children, not to armor plate base material slots.
- Open `Assets/Prefabs/Projectile.prefab`.
- Add `TrailRenderer` with lifetime `0.18`, width `0.025`, and cyan/magenta gradient based on owning team.
- Add `ProjectileTrailVisual` in Task 9 before relying on team-specific trail coloring.

- [ ] **Step 5: Commit visual profile assets**

Run:

```bash
git add shared/unity_arena/Assets/Scripts/Visual/VisualPolishProfile.cs shared/unity_arena/Assets/Editor/VisualPolishInstaller.cs shared/unity_arena/Assets/Rendering/VisualPolishProfile.asset shared/unity_arena/Assets/Materials shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity shared/unity_arena/Assets/Prefabs/Chassis.prefab shared/unity_arena/Assets/Prefabs/Projectile.prefab
git commit -m "feat: install arena visual polish profile"
```

### Task 9: Add Projectile, Hologram, And Rule-Zone Visual Hooks

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/Visual/ProjectileTrailVisual.cs`
- Modify: `shared/unity_arena/Assets/Scripts/HoloProjector.cs`
- Modify: `shared/unity_arena/Assets/Scripts/RuleZoneMarkerRenderer.cs`
- Create: `shared/unity_arena/Assets/Tests/PlayMode/VisualReadabilitySceneTests.cs`

- [ ] **Step 1: Create PlayMode scene wiring tests**

Create `shared/unity_arena/Assets/Tests/PlayMode/VisualReadabilitySceneTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.PlayMode
{
    public class VisualReadabilitySceneTests
    {
        [UnityTest]
        public IEnumerator MapA_HasReadableRuleZoneAndProjectileVisualHooks()
        {
            yield return SceneManager.LoadSceneAsync("MapA_MazeHybrid", LoadSceneMode.Single);

            RuleZoneMarkerRenderer markerRenderer = Object.FindFirstObjectByType<RuleZoneMarkerRenderer>();
            Assert.IsNotNull(markerRenderer);

            ProjectileTrailVisual projectileVisual = Object.FindFirstObjectByType<ProjectileTrailVisual>(FindObjectsInactive.Include);
            Assert.IsNotNull(projectileVisual);
        }
    }
}
```

- [ ] **Step 2: Run PlayMode test and confirm it fails before hooks are wired**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform PlayMode -testResults /tmp/aiming-visual-scene-playmode.xml -quit
```

Expected: FAIL until scene/prefab wiring exists.

- [ ] **Step 3: Create projectile trail visual hook**

Create `shared/unity_arena/Assets/Scripts/Visual/ProjectileTrailVisual.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    [RequireComponent(typeof(Projectile))]
    public class ProjectileTrailVisual : MonoBehaviour
    {
        public TrailRenderer Trail;
        public Color BlueTrailColor = new Color(0f, 0.85f, 1f, 1f);
        public Color RedTrailColor = new Color(1f, 0.05f, 0.35f, 1f);

        private Projectile _projectile;

        private void Awake()
        {
            _projectile = GetComponent<Projectile>();
            if (Trail == null) Trail = GetComponentInChildren<TrailRenderer>();
        }

        private void Start()
        {
            RefreshTrailColor();
        }

        public void RefreshTrailColor()
        {
            if (Trail == null || _projectile == null) return;
            Color color = _projectile.Team == "red" ? RedTrailColor : BlueTrailColor;
            Trail.startColor = color;
            Trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
```

- [ ] **Step 4: Update hologram and marker scripts**

Modify `HoloProjector.cs` with a public color refresh method:

```csharp
public Color LabelColor = new Color(0f, 0.85f, 1f, 1f);

public void RefreshVisuals()
{
    if (LabelText != null)
    {
        LabelText.text = $"{JunctionId}\n({GridCoords.x:+0.0;-0.0}, {GridCoords.y:+0.0;-0.0})";
        LabelText.color = LabelColor;
    }
}

private void Start()
{
    RefreshVisuals();
}
```

Modify `RuleZoneMarkerRenderer.cs` so boost markers use gold and healing markers use green from `VisualPolishProfile` when a profile is assigned. Keep current marker names and hierarchy unchanged so existing tests keep passing.

- [ ] **Step 5: Wire prefabs and scene**

In Unity Editor:

- Add `ProjectileTrailVisual` to `Assets/Prefabs/Projectile.prefab`.
- Add or assign a `TrailRenderer` child to the projectile prefab.
- Ensure `MapA_MazeHybrid.unity` contains one `RuleZoneMarkerRenderer` through `ArenaMain`.
- Add at least one active `HoloProjector` at a maze junction.

- [ ] **Step 6: Re-run PlayMode visual tests**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform PlayMode -testResults /tmp/aiming-visual-scene-playmode.xml -quit
```

Expected: PASS for `VisualReadabilitySceneTests` and existing PlayMode tests.

- [ ] **Step 7: Commit VFX hooks**

Run:

```bash
git add shared/unity_arena/Assets/Scripts/Visual/ProjectileTrailVisual.cs shared/unity_arena/Assets/Scripts/HoloProjector.cs shared/unity_arena/Assets/Scripts/RuleZoneMarkerRenderer.cs shared/unity_arena/Assets/Tests/PlayMode/VisualReadabilitySceneTests.cs shared/unity_arena/Assets/Prefabs/Projectile.prefab shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat: add arena visual effects hooks"
```

### Task 10: Add Screenshot-Based Visual QA

**Files:**
- Create: `tools/scripts/capture_unity_visual_qa.py`
- Modify: `docs/visual-polish-qa.md`
- Modify: `shared/unity_arena/README.md`

- [ ] **Step 1: Create the visual QA script**

Create `tools/scripts/capture_unity_visual_qa.py`:

```python
from __future__ import annotations

import argparse
import socket
import struct
from dataclasses import dataclass

import numpy as np


@dataclass(frozen=True)
class FrameStats:
    mean: float
    std: float
    red_ratio: float
    blue_ratio: float
    neon_ratio: float
    overexposed_ratio: float


def read_frame(sock: socket.socket, width: int, height: int) -> np.ndarray:
    header = _recv_exact(sock, 16)
    struct.unpack("<QQ", header)
    payload = _recv_exact(sock, width * height * 3)
    return np.frombuffer(payload, dtype=np.uint8).reshape((height, width, 3))


def _recv_exact(sock: socket.socket, n: int) -> bytes:
    chunks: list[bytes] = []
    remaining = n
    while remaining:
        chunk = sock.recv(remaining)
        if not chunk:
            raise ConnectionError("Unity frame stream closed")
        chunks.append(chunk)
        remaining -= len(chunk)
    return b"".join(chunks)


def compute_stats(frame: np.ndarray) -> FrameStats:
    rgb = frame.astype(np.float32) / 255.0
    red_mask = (rgb[:, :, 0] > 0.8) & (rgb[:, :, 1] < 0.25) & (rgb[:, :, 2] < 0.25)
    blue_mask = (rgb[:, :, 2] > 0.8) & (rgb[:, :, 0] < 0.25) & (rgb[:, :, 1] < 0.25)
    neon_mask = ((rgb[:, :, 1] > 0.65) & (rgb[:, :, 2] > 0.65)) | (
        (rgb[:, :, 0] > 0.75) & (rgb[:, :, 2] > 0.45)
    )
    overexposed_mask = (rgb[:, :, 0] > 0.98) & (rgb[:, :, 1] > 0.98) & (rgb[:, :, 2] > 0.98)
    total = float(frame.shape[0] * frame.shape[1])
    return FrameStats(
        mean=float(frame.mean()),
        std=float(frame.std()),
        red_ratio=float(red_mask.sum() / total),
        blue_ratio=float(blue_mask.sum() / total),
        neon_ratio=float(neon_mask.sum() / total),
        overexposed_ratio=float(overexposed_mask.sum() / total),
    )


def assert_stats(stats: FrameStats) -> None:
    if stats.std < 3.0:
        raise AssertionError(f"frame appears blank: std={stats.std:.3f}")
    if stats.red_ratio < 0.00001:
        raise AssertionError(f"red armor pixels not visible: red_ratio={stats.red_ratio:.8f}")
    if stats.blue_ratio < 0.00001:
        raise AssertionError(f"blue armor pixels not visible: blue_ratio={stats.blue_ratio:.8f}")
    if stats.neon_ratio > 0.35:
        raise AssertionError(f"neon dominates frame: neon_ratio={stats.neon_ratio:.4f}")
    if stats.overexposed_ratio > 0.05:
        raise AssertionError(f"frame is overexposed: overexposed_ratio={stats.overexposed_ratio:.4f}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=7655)
    parser.add_argument("--width", type=int, default=1280)
    parser.add_argument("--height", type=int, default=720)
    parser.add_argument("--frames", type=int, default=20)
    args = parser.parse_args()

    stats: list[FrameStats] = []
    with socket.create_connection((args.host, args.port), timeout=5) as sock:
        for _ in range(args.frames):
            stats.append(compute_stats(read_frame(sock, args.width, args.height)))

    aggregate = FrameStats(
        mean=sum(s.mean for s in stats) / len(stats),
        std=sum(s.std for s in stats) / len(stats),
        red_ratio=max(s.red_ratio for s in stats),
        blue_ratio=max(s.blue_ratio for s in stats),
        neon_ratio=sum(s.neon_ratio for s in stats) / len(stats),
        overexposed_ratio=sum(s.overexposed_ratio for s in stats) / len(stats),
    )
    assert_stats(aggregate)
    print(
        "[visual-qa] "
        f"mean={aggregate.mean:.2f} std={aggregate.std:.2f} "
        f"red_ratio={aggregate.red_ratio:.6f} blue_ratio={aggregate.blue_ratio:.6f} "
        f"neon_ratio={aggregate.neon_ratio:.4f} "
        f"overexposed_ratio={aggregate.overexposed_ratio:.4f}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 2: Add unit coverage for frame stats**

Append to `tests/test_arena_wire_format.py` or create `tests/test_visual_qa_metrics.py`:

```python
import numpy as np

from tools.scripts.capture_unity_visual_qa import assert_stats, compute_stats


def test_visual_qa_stats_detect_red_blue_and_nonblank_frame() -> None:
    frame = np.zeros((32, 32, 3), dtype=np.uint8)
    frame[:, :, :] = [10, 12, 14]
    frame[4:8, 4:8, :] = [255, 0, 0]
    frame[16:20, 16:20, :] = [0, 0, 255]
    frame[24:26, 8:12, :] = [0, 220, 255]

    stats = compute_stats(frame)

    assert stats.std > 3.0
    assert stats.red_ratio > 0.0
    assert stats.blue_ratio > 0.0
    assert stats.neon_ratio > 0.0
    assert_stats(stats)
```

- [ ] **Step 3: Run Python visual QA unit test**

Run:

```bash
uv run pytest tests/test_visual_qa_metrics.py -q
```

Expected: PASS.

- [ ] **Step 4: Run live visual QA**

With `MapA_MazeHybrid.unity` open in Play mode:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/capture_unity_visual_qa.py --frames 20
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/check_synty_redistribution.py
```

Expected: `[visual-qa]` summary and `[OK] no Synty source files in committed paths.`

- [ ] **Step 5: Document QA command in README**

Add this line under `shared/unity_arena/README.md` `Tests`:

```markdown
Visual QA: `UV_CACHE_DIR=.uv-cache uv run python tools/scripts/capture_unity_visual_qa.py --frames 20`
```

- [ ] **Step 6: Commit visual QA**

Run:

```bash
git add tools/scripts/capture_unity_visual_qa.py tests/test_visual_qa_metrics.py docs/visual-polish-qa.md shared/unity_arena/README.md
git commit -m "test: add visual polish qa smoke"
```

---

## Final Verification For Both Milestones

- [ ] **Step 1: Run no-Unity Python checks**

Run:

```bash
uv run pytest tests/test_arena_wire_format.py tests/test_rl_training_client.py tests/test_visual_qa_metrics.py tests/test_assignment_design.py tests/test_assignment_mini_commands.py -q
```

Expected: PASS.

- [ ] **Step 2: Run C++ checks**

Run:

```bash
cmake --build build/cpp -j
ctest --test-dir build/cpp --output-on-failure
```

Expected: build succeeds; available tests pass; candidate blank tests that are intentionally marked expected-fail remain expected-fail.

- [ ] **Step 3: Run Unity EditMode and PlayMode checks**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform EditMode -testResults /tmp/aiming-all-editmode.xml -quit
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath shared/unity_arena -runTests -testPlatform PlayMode -testResults /tmp/aiming-all-playmode.xml -quit
```

Expected: EditMode and PlayMode tests pass.

- [ ] **Step 4: Run live Unity smokes**

With the matching scene open in Unity Play mode:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30 --training --target-translation-speed 1.0 --target-rotation-speed 2.0
UV_CACHE_DIR=.uv-cache uv run python tools/rl/random_policy_smoke.py --seed 42 --ticks 30 --target-translation-speed 1.0 --target-rotation-speed 2.0
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/capture_unity_visual_qa.py --frames 20
```

Expected: all commands exit with status 0; training commands print training/reward summaries; visual QA reports readable nonblank frames.

- [ ] **Step 5: Check redistribution and git status**

Run:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/check_synty_redistribution.py
git status --short --branch
```

Expected: no Synty source violations; branch contains only intended committed milestone changes.

## Acceptance Criteria

Milestone 4 is complete when:

- `TrainingGround.unity` exists and runs in Play mode.
- Target translation speed and rotation speed can be set from UI and from `env_reset.training_config`.
- Baseline non-player aiming targets the geometric center of the enemy chassis and has deterministic unit tests.
- Training telemetry appears only when training mode is enabled.
- `tools/rl/random_policy_smoke.py` can run a short episode and print reward/damage metrics.
- Protocol, Python, C++, Unity EditMode, Unity PlayMode, and live training smoke checks pass.

Milestone 5 is complete when:

- `MapA_MazeHybrid.unity` visibly moves toward the sci-fi industrial style in `schema.md`.
- Armor plate base colors remain pure red and pure blue.
- MNIST stickers, target silhouettes, projectiles, boost zones, and healing zones remain readable in camera frames.
- Projectile trails, muzzle/impact effects, holograms, fog, bloom, and neon lighting do not change gameplay physics or wire semantics.
- `capture_unity_visual_qa.py` and Unity visual tests pass.
- `check_synty_redistribution.py` confirms no Synty source assets are committed.

## Self-Review Notes

- Spec coverage: training ground, adjustable target translation/rotation, baseline non-player aim, RL scaffold, and sci-fi visual polish are each assigned to concrete tasks.
- Ordering: Milestone 4 precedes Milestone 5 because training telemetry and RL observations must stabilize before the visual pass changes camera frames.
- Unresolved-marker scan: no incomplete requirement markers are left in this plan.
- Type consistency: `training_config` and `training` field names match across proto, Unity JSON dictionaries, Python tests, and documentation.

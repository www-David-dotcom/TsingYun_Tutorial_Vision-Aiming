# Training Ground And RL Scaffold

This is team/backend tooling for non-player opponent policies in the solitary
Unity game. It is not part of the HW1-HW7 student assignment path, and students
should not train their own vehicle RL policies.

The active wire details live in [`docs/unity-wire-contract.md`](unity-wire-contract.md).
Training runs add optional `training_config` to `env_reset` and read optional
`SensorBundle.training` telemetry from Unity responses.

## Local Training Smoke

1. Open `shared/unity_arena` in Unity 6000.3.14f1.
2. Open the training scene when it exists, or run the existing arena scene with
   training mode enabled.
3. Click Play.
4. From the repo root, run:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/smoke_arena.py \
  --seed 42 \
  --ticks 30 \
  --training \
  --target-translation-speed 1.0 \
  --target-rotation-speed 2.0
```

The smoke prints `[training ...]` lines containing target position, velocity,
reward, and hit-rate telemetry.

## Backend RL Smoke

The first RL scaffold is intentionally small. It validates reset/step/fire
plumbing and deterministic action sampling before policy quality matters:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/rl/random_policy_smoke.py \
  --seed 42 \
  --ticks 30 \
  --target-translation-speed 1.0 \
  --target-rotation-speed 2.0
```

The script prints one `[rl-smoke]` summary with total reward, damage, and
projectile counts.

## Baseline Opponent

The baseline opponent aims at the geometric center of the enemy chassis. It is
not an RL policy and does not lead moving targets. Its purpose is to give the
training scene a reproducible non-player behavior and a comparison point for
future uploaded policies.

## Observation And Reward Fields

The backend wrapper reads:

- target world position and velocity from `SensorBundle.training`
- target yaw and yaw rate from `SensorBundle.training`
- damage dealt, projectiles fired, armor hits, and player hit rate
- the per-step reward emitted by Unity

The first action controls:

- target yaw command in radians
- target pitch command in radians
- optional fire request

The current reward is emitted by Unity as `training.step_reward`:

```text
damage_dealt * 0.01 + armor_hits * 0.1 - projectiles_fired * 0.01
```

Keep this scaffold backend-only. Do not wire it into homework READMEs or require
students to train RL policies.

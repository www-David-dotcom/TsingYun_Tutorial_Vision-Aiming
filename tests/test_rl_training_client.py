from __future__ import annotations

import random
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT))

from tools.rl.aiming_env import AimingAction, build_training_reset, reward_from_training
from tools.rl.random_policy_smoke import sample_action


def test_build_training_reset_payload_is_backend_only() -> None:
    payload = build_training_reset(
        seed=42,
        target_translation_speed=1.25,
        target_rotation_speed=2.0,
        baseline_opponent=True,
    )

    assert payload["seed"] == 42
    assert payload["opponent_tier"] == "bronze"
    assert payload["oracle_hints"] is True
    assert payload["duration_ns"] == 300_000_000_000
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


def test_action_to_step_and_fire_payloads() -> None:
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

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

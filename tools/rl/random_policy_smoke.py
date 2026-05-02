from __future__ import annotations

import argparse
import random
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT))

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
        completed_ticks = 0
        for completed_ticks in range(1, args.ticks + 1):
            _, reward, done = env.step(sample_action(rng))
            total_reward += reward
            if done:
                break
        stats = env.finish()

    print(
        f"[rl-smoke] seed={args.seed} ticks={completed_ticks} "
        f"total_reward={total_reward:.3f} damage_dealt={stats['damage_dealt']} "
        f"projectiles_fired={stats['projectiles_fired']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

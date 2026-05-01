"""Tier 3 — intra-Unity determinism regression.

Drives the running Unity arena through 25 (seed, pose) combinations
(5 seeds × 5 gimbal poses) and verifies the rendered frame for each
combination is *visually identical* to a previously-captured baseline.

The earlier byte-hash version was too strict: Unity's FixedUpdate runs
continuously between TCP-driven env_steps, so the exact gimbal pose at
the moment of capture drifts by a fraction of a degree per run, which
flips a handful of pixels along the motion edge. The actual rendered
content is the same.

The current gate: per-pixel mean absolute difference between current
and baseline frames must be ≤ MAD_THRESHOLD (default 5.0 on the 0–255
scale). Genuine regressions (changed geometry / shader / camera) blow
through this immediately; timing-noise pixel jitter stays well under it.

Workflow:
  # Bootstrap once (writes PNG + JSON):
  uv run python tests/intra_unity_determinism.py --update-baseline

  # Subsequent runs verify against the snapshot:
  uv run python tests/intra_unity_determinism.py
  uv run python tests/intra_unity_determinism.py --threshold 3.0
"""

from __future__ import annotations

import argparse
import hashlib
import json
import socket
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image

REPO_ROOT = Path(__file__).resolve().parents[1]

SEEDS = [42, 99, 137, 256, 1024]
POSES = [
    (0.0, 0.0),     # neutral
    (0.5, 0.0),     # right
    (-0.5, 0.0),    # left
    (0.0, 0.3),     # up
    (0.0, -0.2),    # down
]

DEFAULT_BASELINE_DIR = REPO_ROOT / "tests" / "golden_frames_unity_baseline"
DEFAULT_THRESHOLD = 5.0   # mean-abs-diff per pixel-channel, 0–255 scale


def send_request(sock: socket.socket, method: str, request: dict) -> dict:
    payload = json.dumps({"method": method, "request": request}).encode("utf-8")
    sock.sendall(struct.pack(">I", len(payload)) + payload)
    header = recv_exact(sock, 4)
    (n,) = struct.unpack(">I", header)
    body = recv_exact(sock, n)
    return json.loads(body.decode("utf-8"))


def recv_exact(sock: socket.socket, n: int) -> bytes:
    chunks: list[bytes] = []
    while n:
        chunk = sock.recv(n)
        if not chunk:
            raise ConnectionError("closed mid-message")
        chunks.append(chunk)
        n -= len(chunk)
    return b"".join(chunks)


def read_one_frame(frame_sock: socket.socket, width: int, height: int) -> np.ndarray:
    recv_exact(frame_sock, 16)
    body = recv_exact(frame_sock, width * height * 3)
    return np.frombuffer(body, dtype=np.uint8).reshape((height, width, 3))


def capture_frames(host: str, control_port: int, frame_port: int,
                   width: int = 1280, height: int = 720) -> dict[str, np.ndarray]:
    """Return dict mapping `seed_<S>_pose_<P>` -> RGB888 ndarray (H, W, 3)."""
    frames: dict[str, np.ndarray] = {}
    with socket.create_connection((host, control_port), timeout=10) as sock:
        for seed in SEEDS:
            for pose_idx, (yaw, pitch) in enumerate(POSES):
                send_request(sock, "env_reset", {
                    "seed": seed, "opponent_tier": "bronze",
                    "oracle_hints": False, "duration_ns": 5_000_000_000,
                })
                for _ in range(20):
                    send_request(sock, "env_step", {
                        "stamp_ns": 0, "target_yaw": yaw, "target_pitch": pitch,
                    })
                with socket.create_connection((host, frame_port), timeout=10) as fsock:
                    for _ in range(3):
                        read_one_frame(fsock, width, height)
                    frame = read_one_frame(fsock, width, height)
                send_request(sock, "env_finish", {})
                key = f"seed_{seed:04d}_pose_{pose_idx}"
                frames[key] = frame
                fingerprint = hashlib.sha256(frame.tobytes()).hexdigest()[:12]
                print(f"[capture] {key} sha256={fingerprint}...")
    return frames


def write_baseline(baseline_dir: Path, frames: dict[str, np.ndarray]) -> None:
    baseline_dir.mkdir(parents=True, exist_ok=True)
    for key, frame in frames.items():
        Image.fromarray(frame).save(baseline_dir / f"{key}.png")
    print(f"[determinism] wrote {len(frames)} PNGs to {baseline_dir}")


def load_baseline(baseline_dir: Path) -> dict[str, np.ndarray]:
    frames: dict[str, np.ndarray] = {}
    for png in sorted(baseline_dir.glob("*.png")):
        frames[png.stem] = np.array(Image.open(png).convert("RGB"))
    return frames


def mean_abs_diff(a: np.ndarray, b: np.ndarray) -> float:
    return float(np.mean(np.abs(a.astype(np.int16) - b.astype(np.int16))))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--baseline-dir", type=Path, default=DEFAULT_BASELINE_DIR,
                        help="Directory holding baseline PNGs (one per seed_pose_key).")
    parser.add_argument("--update-baseline", action="store_true",
                        help="Write the current run as the new baseline (bootstrap).")
    parser.add_argument("--threshold", type=float, default=DEFAULT_THRESHOLD,
                        help="Mean-abs-diff per channel allowed (0..255 scale). "
                             f"Default {DEFAULT_THRESHOLD}.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--control-port", type=int, default=7654)
    parser.add_argument("--frame-port", type=int, default=7655)
    args = parser.parse_args()

    print("[determinism] capturing 25 frames from running Unity arena...")
    frames = capture_frames(args.host, args.control_port, args.frame_port)

    if args.update_baseline:
        write_baseline(args.baseline_dir, frames)
        return 0

    if not args.baseline_dir.exists():
        print(f"[determinism] FAIL: no baseline at {args.baseline_dir}; "
              f"run with --update-baseline to bootstrap.", file=sys.stderr)
        return 1

    baseline = load_baseline(args.baseline_dir)
    failures: list[str] = []
    summary: list[tuple[str, float]] = []
    for key, current in frames.items():
        prior = baseline.get(key)
        if prior is None:
            failures.append(f"missing baseline entry: {key}")
            continue
        if prior.shape != current.shape:
            failures.append(f"shape mismatch: {key} expected={prior.shape} got={current.shape}")
            continue
        mad = mean_abs_diff(prior, current)
        summary.append((key, mad))
        if mad > args.threshold:
            failures.append(f"diverged: {key} mean_abs_diff={mad:.3f} > {args.threshold}")

    summary.sort(key=lambda kv: -kv[1])
    print(f"\n[determinism] worst-case MAD entries:")
    for key, mad in summary[:5]:
        print(f"  {key}: {mad:.3f}")

    if failures:
        for f in failures:
            print(f"[FAIL] {f}", file=sys.stderr)
        return 1

    print(f"[OK] all 25 frames within MAD ≤ {args.threshold} of baseline.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

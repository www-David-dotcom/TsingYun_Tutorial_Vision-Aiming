"""Tier 3 — intra-Unity determinism regression.

Drives the running Unity arena through 25 (seed, pose) combinations
(5 seeds × 5 gimbal poses) and asserts the rendered frame for each
combination is byte-identical to a previously-captured baseline.
Hashes the raw RGB888 frame bytes via SHA-256 and snapshots the
mapping {seed_pose_key -> sha256_hex} into a JSON file under tests/.

Pass = every frame in the current run matches the baseline hash.
Fail = any hash diverges, or baseline missing (run --update-baseline
once to bootstrap).

This replaces the Godot-vs-Unity SSIM comparator originally planned
for Stage 12b Task 25. The Unity arena is the canonical engine; we
care that it produces deterministic output for any given seed, not
that it matches the legacy Godot reference.

Workflow:
  # Bootstrap once after authoring the scene / verifying the visuals
  # are what you want:
  uv run python tests/intra_unity_determinism.py --update-baseline

  # Subsequent runs (CI / pre-merge):
  uv run python tests/intra_unity_determinism.py

Required deps: numpy, pillow (already in pyproject).
"""

from __future__ import annotations

import argparse
import hashlib
import json
import socket
import struct
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]

SEEDS = [42, 99, 137, 256, 1024]
POSES = [
    (0.0, 0.0),     # neutral
    (0.5, 0.0),     # right
    (-0.5, 0.0),    # left
    (0.0, 0.3),     # up
    (0.0, -0.2),    # down
]

DEFAULT_BASELINE = REPO_ROOT / "tests" / "golden_frames_unity_baseline.json"


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


def read_one_frame(frame_sock: socket.socket, width: int, height: int) -> bytes:
    recv_exact(frame_sock, 16)  # discard 16-byte LE header (frame_id u64, stamp_ns u64)
    return recv_exact(frame_sock, width * height * 3)


def capture_frames(host: str, control_port: int, frame_port: int,
                   width: int = 1280, height: int = 720) -> dict[str, str]:
    """Return dict mapping `seed_<S>_pose_<P>` -> sha256 hex of RGB888 bytes.

    The frame socket is connected only during the brief capture window,
    not during env_reset / env_step. TcpFramePub stops publishing when
    no clients are connected, so its 2.7 MB/frame stream can't fill the
    kernel send buffer and block Unity's main thread on Stream.Write.
    """
    hashes: dict[str, str] = {}
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
                # Capture window: connect, drain a few stale frames so the
                # one we keep was rendered AFTER TcpFramePub registered the
                # new client and pushed fresh content, then close.
                with socket.create_connection((host, frame_port), timeout=10) as fsock:
                    for _ in range(3):
                        read_one_frame(fsock, width, height)
                    frame_bytes = read_one_frame(fsock, width, height)
                send_request(sock, "env_finish", {})
                key = f"seed_{seed:04d}_pose_{pose_idx}"
                hashes[key] = hashlib.sha256(frame_bytes).hexdigest()
                print(f"[capture] {key} sha256={hashes[key][:12]}...")
    return hashes


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE,
                        help="Path to the JSON snapshot of expected hashes.")
    parser.add_argument("--update-baseline", action="store_true",
                        help="Write the current run as the new baseline (bootstrap).")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--control-port", type=int, default=7654)
    parser.add_argument("--frame-port", type=int, default=7655)
    args = parser.parse_args()

    print("[determinism] capturing 25 frames from running Unity arena...")
    hashes = capture_frames(args.host, args.control_port, args.frame_port)

    if args.update_baseline:
        args.baseline.parent.mkdir(parents=True, exist_ok=True)
        args.baseline.write_text(json.dumps(hashes, indent=2, sort_keys=True) + "\n")
        print(f"[determinism] wrote new baseline: {args.baseline}")
        return 0

    if not args.baseline.exists():
        print(f"[determinism] FAIL: no baseline at {args.baseline}; "
              f"run with --update-baseline to bootstrap.", file=sys.stderr)
        return 1

    expected = json.loads(args.baseline.read_text())
    failures: list[str] = []
    for key, current in hashes.items():
        prior = expected.get(key)
        if prior is None:
            failures.append(f"missing baseline entry: {key}")
        elif prior != current:
            failures.append(f"hash diverged: {key} expected={prior[:12]} got={current[:12]}")
    extra = set(expected) - set(hashes)
    for key in sorted(extra):
        failures.append(f"baseline has key not produced this run: {key}")

    if failures:
        for f in failures:
            print(f"[FAIL] {f}", file=sys.stderr)
        return 1

    print(f"[OK] all 25 frames match baseline {args.baseline}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

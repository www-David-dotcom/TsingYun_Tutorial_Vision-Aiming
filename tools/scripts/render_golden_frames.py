"""Render a deterministic 1280x720 RGB frame from a running arena.

Usage:
  uv run python tools/scripts/render_golden_frames.py \
      --engine {godot|unity} --output-dir tests/golden_frames/

Iterates over 5 seeds × 5 gimbal poses, sends env_reset(seed),
env_step(target_yaw, target_pitch), reads one frame from the frame port,
and saves it as <engine>_seed_<seed>_pose_<idx>.png.
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
    recv_exact(frame_sock, 16)  # discard header
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

"""Smoke test the Godot arena's TCP control surface.

Connects to the running Godot arena (started via
`godot --path shared/godot_arena --headless`), drives a 4-step episode
(reset → step → push fire → finish), and prints the messages back.

The transport is the Stage-2 fallback: length-prefixed (4-byte BE) JSON
over TCP. The proto contract is preserved — every response dict here
parses through `google.protobuf.json_format.ParseDict` into the
matching message type.
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

from grpc_stub_server import proto_codegen  # noqa: E402

aiming_pb2 = proto_codegen.import_pb2("aiming")
sensor_pb2 = proto_codegen.import_pb2("sensor")
episode_pb2 = proto_codegen.import_pb2("episode")

from google.protobuf import json_format  # noqa: E402


def send_message(sock: socket.socket, method: str, request: dict) -> dict:
    payload = json.dumps({"method": method, "request": request}).encode("utf-8")
    sock.sendall(struct.pack(">I", len(payload)) + payload)

    header = _recv_exact(sock, 4)
    (n,) = struct.unpack(">I", header)
    body = _recv_exact(sock, n)
    return json.loads(body.decode("utf-8"))


def _recv_exact(sock: socket.socket, n: int) -> bytes:
    chunks: list[bytes] = []
    remaining = n
    while remaining:
        chunk = sock.recv(remaining)
        if not chunk:
            raise ConnectionError("arena closed connection mid-message")
        chunks.append(chunk)
        remaining -= len(chunk)
    return b"".join(chunks)


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

    with socket.create_connection((args.host, args.port), timeout=5) as sock:
        # 1. Reset
        reply = send_message(sock, "env_reset", {
            "seed": args.seed,
            "opponent_tier": "bronze",
            "oracle_hints": True,
            "duration_ns": 5_000_000_000,
        })
        if not reply.get("ok"):
            print("env_reset failed:", reply, file=sys.stderr)
            return 1
        initial = json_format.ParseDict(reply["response"], aiming_pb2.InitialState())
        print(f"[reset] sim_sha={initial.simulator_build_sha256} "
              f"frame_endpoint={initial.zmq_frame_endpoint}")

        # 2. Step a handful of ticks
        for tick in range(args.ticks):
            reply = send_message(sock, "env_step", {
                "stamp_ns": tick * 16_000_000,
                "target_yaw": 0.05 * tick,
                "target_pitch": 0.0,
            })
            bundle = json_format.ParseDict(reply["response"], sensor_pb2.SensorBundle())
            print(f"[step {tick:03d}] gimbal_yaw={bundle.gimbal.yaw:+.3f} "
                  f"frame_id={bundle.frame.frame_id}")

        # 3. Fire a 3-pellet burst
        reply = send_message(sock, "env_push_fire", {"stamp_ns": 0, "burst_count": 3})
        fire = json_format.ParseDict(reply["response"], aiming_pb2.FireResult())
        print(f"[fire] accepted={fire.accepted} queued={fire.queued_count}")

        # 4. Finish
        reply = send_message(sock, "env_finish", {"flush_replay": True})
        stats = json_format.ParseDict(reply["response"], episode_pb2.EpisodeStats())
        print(f"[finish] episode_id={stats.episode_id} outcome={stats.outcome} "
              f"projectiles_fired={stats.projectiles_fired} "
              f"damage_dealt={stats.damage_dealt}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

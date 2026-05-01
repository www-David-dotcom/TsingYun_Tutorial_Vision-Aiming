"""Tier 4 — intra-Unity damage-distribution regression.

Drives 50 deterministic episodes on the running Unity arena and asserts
each one's `damage_dealt` matches a previously-captured baseline,
exactly. Replaces the cross-engine bronze KS test from the original
plan; Unity is the only canonical engine, so the right gate is "given
seed S, the gameplay outcome is reproducible run-to-run".

Per-episode flow:
  - env_reset(seed)
  - 5 env_step calls to settle the gimbal at neutral yaw (BlueChassis
    spawn rotation aims at RedChassis already, per Task 24 wiring)
  - env_push_fire(burst_count=20) — queued at the 5 Hz rate-limit
  - sleep ~5 s wall-clock for the burst to fully fire and bullets
    to impact RedChassis's plates (or miss into walls / floor)
  - env_finish, read damage_dealt from the response

Snapshot: tests/intra_unity_damage_baseline.json maps str(seed) ->
damage_dealt int. Per-seed exact equality is the pass condition; if
that proves too strict in practice (timing-induced bullet/wall race
conditions), we can relax to ±tolerance, but exact equality has
caught real determinism regressions in similar projects.

Workflow:
  # Bootstrap once (after Task 25 baseline is locked in):
  uv run python tests/intra_unity_damage_regression.py --update-baseline

  # Subsequent runs verify:
  uv run python tests/intra_unity_damage_regression.py
"""

from __future__ import annotations

import argparse
import json
import socket
import struct
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
SEEDS = list(range(1, 51))
DEFAULT_BASELINE = REPO_ROOT / "tests" / "intra_unity_damage_baseline.json"


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


def run_episode(sock: socket.socket, seed: int, settle_steps: int = 5,
                burst_count: int = 20, fire_drain_seconds: float = 5.0) -> int:
    """Run one full episode and return the captured damage_dealt."""
    reply = send_request(sock, "env_reset", {
        "seed": seed,
        "opponent_tier": "bronze",
        "oracle_hints": False,
        "duration_ns": 30_000_000_000,  # 30 s — well past fire_drain_seconds
    })
    if not reply.get("ok", True):
        raise RuntimeError(f"env_reset failed for seed={seed}: {reply}")

    for _ in range(settle_steps):
        send_request(sock, "env_step", {
            "stamp_ns": 0, "target_yaw": 0.0, "target_pitch": 0.0,
        })

    fire = send_request(sock, "env_push_fire", {
        "stamp_ns": 0, "burst_count": burst_count,
    })
    if not fire.get("response", {}).get("accepted", False):
        raise RuntimeError(f"env_push_fire rejected seed={seed}: {fire}")

    # Drain the rate-limited burst (5 Hz -> 4 s for 20 bullets) plus a
    # bit for the last bullet to fly its 30 m range / impact a plate.
    time.sleep(fire_drain_seconds)

    finish = send_request(sock, "env_finish", {"flush_replay": True})
    response = finish.get("response", finish)
    return int(response.get("damage_dealt", 0))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE)
    parser.add_argument("--update-baseline", action="store_true",
                        help="Write the current run as the new baseline.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--control-port", type=int, default=7654)
    parser.add_argument("--seeds", type=int, nargs="*", default=SEEDS,
                        help="Override the seed list (default = 1..50).")
    parser.add_argument("--fire-drain-seconds", type=float, default=5.0)
    args = parser.parse_args()

    print(f"[damage-regression] running {len(args.seeds)} episodes on Unity arena...")
    results: dict[str, int] = {}
    with socket.create_connection((args.host, args.control_port), timeout=20) as sock:
        for seed in args.seeds:
            damage = run_episode(sock, seed, fire_drain_seconds=args.fire_drain_seconds)
            results[str(seed)] = damage
            print(f"[seed {seed:3d}] damage_dealt={damage}")

    if args.update_baseline:
        args.baseline.parent.mkdir(parents=True, exist_ok=True)
        args.baseline.write_text(json.dumps(results, indent=2, sort_keys=True) + "\n")
        print(f"[damage-regression] wrote new baseline: {args.baseline}")
        return 0

    if not args.baseline.exists():
        print(f"[damage-regression] FAIL: no baseline at {args.baseline}; "
              f"run with --update-baseline to bootstrap.", file=sys.stderr)
        return 1

    expected: dict[str, int] = json.loads(args.baseline.read_text())
    failures: list[str] = []
    for seed, current in results.items():
        prior = expected.get(seed)
        if prior is None:
            failures.append(f"missing baseline entry for seed={seed}")
        elif prior != current:
            failures.append(f"seed={seed}: expected damage_dealt={prior} got {current}")
    extra = set(expected) - set(results)
    for seed in sorted(extra, key=int):
        failures.append(f"baseline has seed={seed} not produced this run")

    if failures:
        for f in failures:
            print(f"[FAIL] {f}", file=sys.stderr)
        return 1

    total = sum(results.values())
    print(f"[OK] all {len(results)} episodes match baseline. total damage = {total}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

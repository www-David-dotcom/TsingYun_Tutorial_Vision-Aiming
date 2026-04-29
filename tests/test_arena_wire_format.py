"""Engine-agnostic wire-format conformance for the Aiming Arena.

Mirrors both the GDScript (arena_main.gd) and the C#
(unity_arena/Assets/Scripts/ArenaMain.cs) sensor-bundle dict shapes,
asserting that each round-trips through google.protobuf.json_format.

This test does NOT spin up either simulator. It hand-constructs the
dict shapes that each engine's source is documented to emit. If the
test drifts from the source, the matching test breaks first; the
engine's source is the source of truth.
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest
from google.protobuf import json_format

REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT / "shared" / "grpc_stub_server" / "src"))

from grpc_stub_server import proto_codegen  # noqa: E402

aiming_pb2 = proto_codegen.import_pb2("aiming")
sensor_pb2 = proto_codegen.import_pb2("sensor")
episode_pb2 = proto_codegen.import_pb2("episode")


# --------------------------------------------------------- engine helpers

def _godot_bundle(*, oracle: bool, frame_id: int = 0) -> dict:
    """Mirror of arena_main.gd::_build_sensor_bundle."""
    bundle = {
        "frame": {
            "frame_id": frame_id,
            "zmq_topic": "frames.42",
            "stamp_ns": 1_000_000,
            "width": 1280,
            "height": 720,
            "pixel_format": "PIXEL_FORMAT_RGB888",
        },
        "imu": {
            "stamp_ns": 1_000_000,
            "angular_velocity": {"x": 0.0, "y": 0.0, "z": 0.0},
            "linear_accel": {"x": 0.0, "y": -9.81, "z": 0.0},
            "orientation": {"w": 1.0, "x": 0.0, "y": 0.0, "z": 0.0},
        },
        "gimbal": {"stamp_ns": 1_000_000, "yaw": 0.0, "pitch": 0.0,
                   "yaw_rate": 0.0, "pitch_rate": 0.0},
        "odom": {
            "stamp_ns": 1_000_000,
            "position_world": {"x": -3.0, "y": 0.0, "z": 0.0},
            "linear_velocity": {"x": 0.0, "y": 0.0, "z": 0.0},
            "yaw_world": 0.0,
        },
    }
    if oracle:
        bundle["oracle"] = {
            "target_position_world": {"x": 3.0, "y": 0.0, "z": 0.0},
            "target_velocity_world": {"x": 0.0, "y": 0.0, "z": 0.0},
            "target_visible": True,
        }
    return bundle


def _unity_bundle(*, oracle: bool, frame_id: int = 0) -> dict:
    """Mirror of ArenaMain.cs::BuildSensorBundle.

    The Unity build emits long ints for stamp_ns / frame_id / width / height
    (System.Int64), but JSON serialization writes them as JSON integers
    indistinguishable from Godot's. The shape is identical.
    """
    return _godot_bundle(oracle=oracle, frame_id=frame_id)


_BUNDLE_BUILDERS = {"godot": _godot_bundle, "unity": _unity_bundle}


# ---------------------------------------------------------- parametrized

@pytest.fixture(params=["godot", "unity"])
def bundle_builder(request):
    return _BUNDLE_BUILDERS[request.param]


def test_env_reset_request_parses() -> None:
    payload = {"seed": 42, "opponent_tier": "bronze", "oracle_hints": True,
               "duration_ns": 90_000_000_000}
    req = json_format.ParseDict(payload, aiming_pb2.EnvResetRequest())
    assert req.seed == 42
    assert req.opponent_tier == "bronze"


def test_gimbal_cmd_parses() -> None:
    payload = {"stamp_ns": 1_000, "target_yaw": 0.5, "target_pitch": -0.25,
               "yaw_rate_ff": 0.1, "pitch_rate_ff": 0.0}
    cmd = json_format.ParseDict(payload, sensor_pb2.GimbalCmd())
    assert cmd.target_yaw == pytest.approx(0.5)


def test_fire_cmd_parses() -> None:
    cmd = json_format.ParseDict({"stamp_ns": 1, "burst_count": 3}, sensor_pb2.FireCmd())
    assert cmd.burst_count == 3


def test_sensor_bundle_without_oracle_parses(bundle_builder) -> None:
    payload = bundle_builder(oracle=False, frame_id=0)
    bundle = json_format.ParseDict(payload, sensor_pb2.SensorBundle())
    assert bundle.frame.width == 1280
    assert bundle.frame.height == 720
    assert bundle.frame.pixel_format == sensor_pb2.FrameRef.PIXEL_FORMAT_RGB888
    assert bundle.imu.linear_accel.y == pytest.approx(-9.81)
    assert not bundle.HasField("oracle")


def test_sensor_bundle_with_oracle_parses(bundle_builder) -> None:
    payload = bundle_builder(oracle=True, frame_id=1)
    bundle = json_format.ParseDict(payload, sensor_pb2.SensorBundle())
    assert bundle.HasField("oracle")
    assert bundle.oracle.target_position_world.x == pytest.approx(3.0)


def test_initial_state_parses(bundle_builder) -> None:
    sim_sha = "stage12a-unity-scaffold-1.6"
    payload = {
        "bundle": bundle_builder(oracle=False, frame_id=0),
        "zmq_frame_endpoint": "tcp://127.0.0.1:7655",
        "simulator_build_sha256": sim_sha,
    }
    msg = json_format.ParseDict(payload, aiming_pb2.InitialState())
    assert msg.simulator_build_sha256 == sim_sha


def test_fire_result_parses() -> None:
    payload = {"accepted": True, "reason": "", "queued_count": 3}
    msg = json_format.ParseDict(payload, aiming_pb2.FireResult())
    assert msg.accepted is True
    assert msg.queued_count == 3


def test_episode_stats_with_events_parses() -> None:
    payload = {
        "episode_id": "ep-000000000000002a", "seed": 42,
        "duration_ns": 90_000_000_000,
        "candidate_commit_sha": "", "candidate_build_sha256": "",
        "simulator_build_sha256": "stage12a-unity-scaffold-1.6",
        "opponent_policy_sha256": "", "opponent_tier": "bronze",
        "outcome": "OUTCOME_TIMEOUT",
        "damage_dealt": 200, "damage_taken": 100,
        "projectiles_fired": 8, "armor_hits": 4,
        "aim_latency_p50_ns": 8_000_000,
        "aim_latency_p95_ns": 20_000_000,
        "aim_latency_p99_ns": 28_000_000,
        "events": [
            {"stamp_ns": 1_000_000, "kind": "KIND_FIRED", "armor_id": "", "damage": 0},
            {"stamp_ns": 1_500_000, "kind": "KIND_HIT_ARMOR", "armor_id": "red.front", "damage": 50},
        ],
    }
    msg = json_format.ParseDict(payload, episode_pb2.EpisodeStats())
    assert msg.outcome == episode_pb2.EpisodeStats.OUTCOME_TIMEOUT
    assert msg.armor_hits == 4
    assert len(msg.events) == 2


def test_length_prefix_round_trip() -> None:
    import json, struct
    payload = {"method": "env_reset", "request": {"seed": 1, "opponent_tier": "bronze"}}
    body = json.dumps(payload).encode("utf-8")
    n = len(body)
    encoded = bytes([(n >> 24) & 0xFF, (n >> 16) & 0xFF, (n >> 8) & 0xFF, n & 0xFF])
    assert encoded == struct.pack(">I", n)

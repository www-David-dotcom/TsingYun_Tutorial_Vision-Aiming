"""Unity wire-format conformance for the Aiming Arena.

Mirrors unity_arena/Assets/Scripts/ArenaMain.cs sensor-bundle dict shapes,
asserting that they round-trip through google.protobuf.json_format.

This test does NOT spin up Unity. It hand-constructs the dict shape that
ArenaMain.cs is documented to emit. If the test drifts from the source, this
test should break first; the Unity source is the source of truth.
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


# ----------------------------------------------------------- Unity helper

def _unity_bundle(*, oracle: bool, frame_id: int = 0) -> dict:
    """Mirror of ArenaMain.cs::BuildSensorBundle."""
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
        "gimbal": {
            "stamp_ns": 1_000_000,
            "yaw": 0.0,
            "pitch": 0.0,
            "yaw_rate": 0.0,
            "pitch_rate": 0.0,
        },
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


def test_env_reset_request_parses() -> None:
    payload = {"seed": 42, "opponent_tier": "bronze", "oracle_hints": True,
               "duration_ns": 300_000_000_000}
    req = json_format.ParseDict(payload, aiming_pb2.EnvResetRequest())
    assert req.seed == 42
    assert req.opponent_tier == "bronze"
    assert req.oracle_hints is True
    assert req.duration_ns == 300_000_000_000


def test_training_config_parses_for_backend_opponents() -> None:
    payload = {
        "seed": 42,
        "opponent_tier": "bronze",
        "oracle_hints": True,
        "duration_ns": 300_000_000_000,
        "training_config": {
            "enabled": True,
            "target_translation_speed_mps": 1.25,
            "target_rotation_speed_rad_s": 2.0,
            "target_path_half_extent_m": 2.5,
            "baseline_opponent_enabled": True,
        },
    }
    req = json_format.ParseDict(payload, aiming_pb2.EnvResetRequest())
    assert req.training_config.enabled is True
    assert req.training_config.target_translation_speed_mps == pytest.approx(1.25)
    assert req.training_config.target_rotation_speed_rad_s == pytest.approx(2.0)
    assert req.training_config.target_path_half_extent_m == pytest.approx(2.5)
    assert req.training_config.baseline_opponent_enabled is True


def test_gimbal_cmd_parses() -> None:
    payload = {"stamp_ns": 1_000, "target_yaw": 0.5, "target_pitch": -0.25,
               "yaw_rate_ff": 0.1, "pitch_rate_ff": 0.0}
    cmd = json_format.ParseDict(payload, sensor_pb2.GimbalCmd())
    assert cmd.target_yaw == pytest.approx(0.5)
    assert cmd.target_pitch == pytest.approx(-0.25)
    assert cmd.yaw_rate_ff == pytest.approx(0.1)


def test_fire_cmd_parses() -> None:
    cmd = json_format.ParseDict({"stamp_ns": 1, "burst_count": 3}, sensor_pb2.FireCmd())
    assert cmd.burst_count == 3


def test_sensor_bundle_without_oracle_parses() -> None:
    payload = _unity_bundle(oracle=False, frame_id=0)
    bundle = json_format.ParseDict(payload, sensor_pb2.SensorBundle())
    assert bundle.frame.width == 1280
    assert bundle.frame.height == 720
    assert bundle.frame.pixel_format == sensor_pb2.FrameRef.PIXEL_FORMAT_RGB888
    assert bundle.imu.linear_accel.y == pytest.approx(-9.81)
    assert bundle.odom.position_world.x == pytest.approx(-3.0)
    assert not bundle.HasField("oracle")


def test_sensor_bundle_with_oracle_parses() -> None:
    payload = _unity_bundle(oracle=True, frame_id=1)
    bundle = json_format.ParseDict(payload, sensor_pb2.SensorBundle())
    assert bundle.HasField("oracle")
    assert bundle.oracle.target_position_world.x == pytest.approx(3.0)
    assert bundle.oracle.target_visible is True


def test_sensor_bundle_with_training_telemetry_parses() -> None:
    payload = _unity_bundle(oracle=True, frame_id=3)
    payload["training"] = {
        "stamp_ns": 48_000_000,
        "target_position_world": {"x": 3.5, "y": 0.0, "z": 0.25},
        "target_velocity_world": {"x": 0.5, "y": 0.0, "z": 0.0},
        "target_yaw_world": 1.25,
        "target_yaw_rate": 2.0,
        "damage_dealt": 40,
        "projectiles_fired": 6,
        "armor_hits": 2,
        "player_hit_rate": 0.33333334,
        "step_reward": 0.4,
        "episode_done": False,
    }
    bundle = json_format.ParseDict(payload, sensor_pb2.SensorBundle())
    assert bundle.training.target_position_world.x == pytest.approx(3.5)
    assert bundle.training.target_velocity_world.x == pytest.approx(0.5)
    assert bundle.training.target_yaw_world == pytest.approx(1.25)
    assert bundle.training.target_yaw_rate == pytest.approx(2.0)
    assert bundle.training.damage_dealt == 40
    assert bundle.training.projectiles_fired == 6
    assert bundle.training.armor_hits == 2
    assert bundle.training.player_hit_rate == pytest.approx(1.0 / 3.0)
    assert bundle.training.step_reward == pytest.approx(0.4)
    assert bundle.training.episode_done is False


def test_initial_state_parses() -> None:
    sim_sha = "stage12a-unity-scaffold-1.6"
    payload = {
        "bundle": _unity_bundle(oracle=False, frame_id=0),
        "zmq_frame_endpoint": "tcp://127.0.0.1:7655",
        "simulator_build_sha256": sim_sha,
    }
    msg = json_format.ParseDict(payload, aiming_pb2.InitialState())
    assert msg.zmq_frame_endpoint == "tcp://127.0.0.1:7655"
    assert msg.simulator_build_sha256 == sim_sha


def test_fire_result_parses() -> None:
    payload = {"accepted": True, "reason": "", "queued_count": 3}
    msg = json_format.ParseDict(payload, aiming_pb2.FireResult())
    assert msg.accepted is True
    assert msg.queued_count == 3


def test_episode_stats_with_events_parses() -> None:
    payload = {
        "episode_id": "ep-000000000000002a", "seed": 42,
        "duration_ns": 300_000_000_000,
        "candidate_commit_sha": "", "candidate_build_sha256": "",
        "simulator_build_sha256": "stage12a-unity-scaffold-1.6",
        "opponent_policy_sha256": "", "opponent_tier": "bronze",
        "outcome": "OUTCOME_TIMEOUT",
        "damage_dealt": 200, "damage_taken": 100,
        "projectiles_fired": 8, "armor_hits": 4,
        "player_hit_rate": 0.5, "team_hit_rate": 0.5,
        "aim_latency_p50_ns": 8_000_000,
        "aim_latency_p95_ns": 20_000_000,
        "aim_latency_p99_ns": 28_000_000,
        "events": [
            {"stamp_ns": 1_000_000, "kind": "KIND_FIRED", "armor_id": "", "damage": 0},
            {"stamp_ns": 1_500_000, "kind": "KIND_HIT_ARMOR", "armor_id": "red.front", "damage": 20},
        ],
    }
    msg = json_format.ParseDict(payload, episode_pb2.EpisodeStats())
    assert msg.outcome == episode_pb2.EpisodeStats.OUTCOME_TIMEOUT
    assert msg.armor_hits == 4
    assert msg.player_hit_rate == pytest.approx(0.5)
    assert msg.team_hit_rate == pytest.approx(0.5)
    assert len(msg.events) == 2
    assert msg.events[0].kind == episode_pb2.ProjectileEvent.KIND_FIRED
    assert msg.events[1].kind == episode_pb2.ProjectileEvent.KIND_HIT_ARMOR
    assert msg.events[1].armor_id == "red.front"


def test_length_prefix_round_trip() -> None:
    """Sanity-check the 4-byte big-endian length prefix used by
    TcpProtoServer.cs.

    Both sides hand-encode the prefix; any mismatch (BE vs LE, signed
    vs unsigned, off-by-one) produces a hung connection at runtime.
    """
    import json
    import struct
    payload = {"method": "env_reset", "request": {"seed": 1, "opponent_tier": "bronze"}}
    body = json.dumps(payload).encode("utf-8")
    n = len(body)
    encoded = bytes([(n >> 24) & 0xFF, (n >> 16) & 0xFF, (n >> 8) & 0xFF, n & 0xFF])
    assert encoded == struct.pack(">I", n)

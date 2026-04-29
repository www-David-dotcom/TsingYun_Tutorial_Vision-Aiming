"""Synthetic AimingArena gRPC server.

Implements the contract from shared/proto/aiming.proto with a deterministic
stub world: fixed map, fixed dummy target, projectiles always miss, gimbal
state mirrors whatever the candidate commands. Useful for protocol-level
smoke tests and as a target for HW1+'s C++ client before the real Godot
arena ships in stage 2.
"""

from __future__ import annotations

import threading
import time
from concurrent import futures
from dataclasses import dataclass
from typing import Iterator

import grpc

from . import proto_codegen

# Trigger codegen + import the generated modules.
aiming_pb2 = proto_codegen.import_pb2("aiming")
sensor_pb2 = proto_codegen.import_pb2("sensor")
episode_pb2 = proto_codegen.import_pb2("episode")
aiming_pb2_grpc = proto_codegen.import_pb2_grpc("aiming")


@dataclass
class _EpisodeState:
    seed: int
    started_ns: int
    duration_ns: int
    opponent_tier: str
    last_yaw: float = 0.0
    last_pitch: float = 0.0
    fire_count: int = 0
    finished: bool = False


class AimingArenaStub(aiming_pb2_grpc.AimingArenaServicer):
    """Minimal AimingArena implementation. Deterministic, stateful per call."""

    SIM_BUILD_SHA = "stub-0.5.0"

    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._episode: _EpisodeState | None = None

    # ------------------------------------------------------------------ helpers

    def _now_ns(self) -> int:
        return time.monotonic_ns()

    def _make_bundle(self, frame_id: int, oracle_hints: bool) -> sensor_pb2.SensorBundle:
        ep = self._episode
        assert ep is not None
        stamp = self._now_ns() - ep.started_ns
        bundle = sensor_pb2.SensorBundle(
            frame=sensor_pb2.FrameRef(
                frame_id=frame_id,
                zmq_topic=f"frames.{ep.seed}",
                stamp_ns=stamp,
                width=1280,
                height=720,
                pixel_format=sensor_pb2.FrameRef.PIXEL_FORMAT_RGB888,
            ),
            imu=sensor_pb2.Imu(
                stamp_ns=stamp,
                angular_velocity=sensor_pb2.Vector3(),
                linear_accel=sensor_pb2.Vector3(z=-9.81),
                orientation=sensor_pb2.Quaternion(w=1.0),
            ),
            gimbal=sensor_pb2.GimbalState(
                stamp_ns=stamp,
                yaw=ep.last_yaw,
                pitch=ep.last_pitch,
            ),
            odom=sensor_pb2.ChassisOdom(
                stamp_ns=stamp,
                position_world=sensor_pb2.Vector3(),
                linear_velocity=sensor_pb2.Vector3(),
                yaw_world=0.0,
            ),
        )
        if oracle_hints:
            bundle.oracle.target_position_world.x = 5.0  # dummy at 5 m
            bundle.oracle.target_visible = True
        return bundle

    # ---------------------------------------------------------------- RPCs

    def EnvReset(  # noqa: N802 — matches gRPC service method casing
        self,
        request: aiming_pb2.EnvResetRequest,
        context: grpc.ServicerContext,
    ) -> aiming_pb2.InitialState:
        with self._lock:
            duration_ns = request.duration_ns or 90_000_000_000
            self._episode = _EpisodeState(
                seed=request.seed,
                started_ns=self._now_ns(),
                duration_ns=duration_ns,
                opponent_tier=request.opponent_tier or "bronze",
            )
            bundle = self._make_bundle(frame_id=0, oracle_hints=request.oracle_hints)
            return aiming_pb2.InitialState(
                bundle=bundle,
                zmq_frame_endpoint="ipc:///tmp/aiming-arena-frames",
                simulator_build_sha256=self.SIM_BUILD_SHA,
            )

    def EnvStep(  # noqa: N802
        self,
        request_iterator: Iterator[sensor_pb2.GimbalCmd],
        context: grpc.ServicerContext,
    ) -> Iterator[sensor_pb2.SensorBundle]:
        frame_id = 0
        for cmd in request_iterator:
            with self._lock:
                if self._episode is None:
                    context.abort(grpc.StatusCode.FAILED_PRECONDITION,
                                  "EnvReset must be called before EnvStep")
                self._episode.last_yaw = cmd.target_yaw
                self._episode.last_pitch = cmd.target_pitch
                frame_id += 1
                yield self._make_bundle(frame_id=frame_id, oracle_hints=False)

    def EnvPushFire(  # noqa: N802
        self,
        request: sensor_pb2.FireCmd,
        context: grpc.ServicerContext,
    ) -> aiming_pb2.FireResult:
        with self._lock:
            if self._episode is None:
                return aiming_pb2.FireResult(accepted=False, reason="no_episode")
            self._episode.fire_count += request.burst_count
            return aiming_pb2.FireResult(accepted=True, queued_count=request.burst_count)

    def EnvFinish(  # noqa: N802
        self,
        request: aiming_pb2.EnvFinishRequest,
        context: grpc.ServicerContext,
    ) -> episode_pb2.EpisodeStats:
        with self._lock:
            if self._episode is None:
                context.abort(grpc.StatusCode.FAILED_PRECONDITION,
                              "no episode in progress")
            ep = self._episode
            ep.finished = True
            stats = episode_pb2.EpisodeStats(
                episode_id=f"stub-{ep.seed}",
                seed=ep.seed,
                duration_ns=self._now_ns() - ep.started_ns,
                opponent_tier=ep.opponent_tier,
                simulator_build_sha256=self.SIM_BUILD_SHA,
                outcome=episode_pb2.EpisodeStats.OUTCOME_TIMEOUT,
                projectiles_fired=ep.fire_count,
            )
            self._episode = None
            return stats


def build_server(port: int) -> grpc.Server:
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=8))
    aiming_pb2_grpc.add_AimingArenaServicer_to_server(AimingArenaStub(), server)
    server.add_insecure_port(f"[::]:{port}")
    return server

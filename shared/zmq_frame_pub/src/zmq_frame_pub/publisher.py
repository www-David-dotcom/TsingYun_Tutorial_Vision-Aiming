"""Publish a synthetic 1280x720 RGB stream at 60 fps over ZMQ PUB.

Wire format per frame: a 16-byte header followed by raw RGB888 bytes.
Header layout (little-endian):

    bytes  0..7  : uint64 frame_id
    bytes  8..15 : uint64 stamp_ns (monotonic, since publisher start)

Subscribers should read header + (width*height*3) bytes per topic message.
The header is in-band rather than a separate message so that PUB/SUB drops
align frames correctly under back-pressure.
"""

from __future__ import annotations

import struct
import time
from dataclasses import dataclass

import numpy as np
import zmq

HEADER_FMT = "<QQ"
HEADER_SIZE = struct.calcsize(HEADER_FMT)


@dataclass
class FramePublisher:
    width: int = 1280
    height: int = 720
    target_fps: float = 60.0
    endpoint: str = "ipc:///tmp/aiming-arena-frames"
    topic: str = "frames.0"

    def __post_init__(self) -> None:
        self._context = zmq.Context.instance()
        self._socket = self._context.socket(zmq.PUB)
        # Drop new frames if subscribers can't keep up; we'd rather lose
        # frames than buffer unbounded RAM.
        self._socket.setsockopt(zmq.SNDHWM, 4)
        self._socket.bind(self.endpoint)
        self._frame_id = 0
        self._t0_ns = time.monotonic_ns()
        self._period_ns = int(1e9 / self.target_fps)

    @property
    def frame_id(self) -> int:
        return self._frame_id

    def _render(self, frame_id: int) -> np.ndarray:
        """Generate one synthetic RGB frame, no PIL dependency.

        Background is a slowly-moving radial gradient so that streams look
        animated; corner band carries the frame counter as a binary visual
        (8 bits per row, MSB on the left). Detectors won't find armor
        plates here — that's intentional, they shouldn't be running yet.
        """
        h, w = self.height, self.width
        yy, xx = np.indices((h, w), dtype=np.float32)
        cx, cy = w / 2 + 100 * np.sin(frame_id * 0.05), h / 2
        d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        d /= d.max()
        rgb = np.empty((h, w, 3), dtype=np.uint8)
        rgb[..., 0] = (d * 255).astype(np.uint8)                    # R
        rgb[..., 1] = ((1 - d) * 255).astype(np.uint8)              # G
        rgb[..., 2] = ((np.sin(d * 6.28 + frame_id * 0.1) + 1) * 127).astype(np.uint8)  # B
        # Frame counter strip: top-left 8x16 pixels per bit, 64 bits = 64 frames
        for bit in range(8):
            on = (frame_id >> (7 - bit)) & 1
            band = 255 if on else 0
            rgb[0:16, bit * 16:(bit + 1) * 16, :] = band
        return rgb

    def publish_one(self) -> None:
        frame_id = self._frame_id
        stamp_ns = time.monotonic_ns() - self._t0_ns
        rgb = self._render(frame_id)
        header = struct.pack(HEADER_FMT, frame_id, stamp_ns)
        self._socket.send_multipart([self.topic.encode("ascii"), header + rgb.tobytes()])
        self._frame_id += 1

    def run(self, max_frames: int | None = None) -> None:
        """Run the publish loop. Blocks; pass max_frames for tests."""
        next_due = time.monotonic_ns()
        while max_frames is None or self._frame_id < max_frames:
            now = time.monotonic_ns()
            if now < next_due:
                time.sleep((next_due - now) / 1e9)
            self.publish_one()
            next_due += self._period_ns

    def close(self) -> None:
        self._socket.close(linger=0)


def run_publisher(
    width: int = 1280,
    height: int = 720,
    fps: float = 60.0,
    endpoint: str = "ipc:///tmp/aiming-arena-frames",
    topic: str = "frames.0",
    max_frames: int | None = None,
) -> None:
    pub = FramePublisher(width=width, height=height, target_fps=fps,
                         endpoint=endpoint, topic=topic)
    try:
        pub.run(max_frames=max_frames)
    finally:
        pub.close()

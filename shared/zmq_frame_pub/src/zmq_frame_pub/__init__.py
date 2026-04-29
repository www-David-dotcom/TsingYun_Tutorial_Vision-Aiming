"""Synthetic ZMQ frame publisher.

Stage 1 needs the *wire* to exist before stage 2 fills the *content*. This
module publishes a synthetic 1280x720 RGB stream at 60 fps over ZMQ PUB,
matching the FrameRef descriptors that the gRPC stub server hands out.

The frames themselves are deliberately ugly — a moving gradient + a frame
counter rendered in the corner — so a candidate testing their detector
against the stub gets *something* to look at without any arena geometry.
"""

from .publisher import FramePublisher, run_publisher

__all__ = ["FramePublisher", "run_publisher"]
__version__ = "0.5.0"

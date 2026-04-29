"""Stand-in for the Godot arena's gRPC server.

Speaks the aiming.proto contract well enough that downstream HW1+ stages can
exercise the wire format end-to-end before the real simulator (stage 2)
exists. The "world" is purely synthetic: a stationary target dummy at a
fixed position, projectiles always miss, opponent never moves. The point
is the *protocol*, not the gameplay.

Once Stage 2 ships the Godot arena, this stub remains useful for CI smoke
tests where firing up a full headless game would be overkill.
"""

from .server import build_server, AimingArenaStub

__all__ = ["build_server", "AimingArenaStub"]
__version__ = "0.5.0"

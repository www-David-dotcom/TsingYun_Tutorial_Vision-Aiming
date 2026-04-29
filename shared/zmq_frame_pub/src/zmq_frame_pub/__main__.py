"""CLI:

    uv run aiming-frame-pub --endpoint ipc:///tmp/aiming-arena-frames
"""

from __future__ import annotations

import click
from rich.console import Console

from .publisher import run_publisher

console = Console()


@click.command()
@click.option("--endpoint", default="ipc:///tmp/aiming-arena-frames",
              show_default=True, help="ZMQ PUB endpoint to bind.")
@click.option("--topic", default="frames.0", show_default=True)
@click.option("--width", default=1280, show_default=True, type=int)
@click.option("--height", default=720, show_default=True, type=int)
@click.option("--fps", default=60.0, show_default=True, type=float)
@click.option("--max-frames", default=None, type=int,
              help="Stop after N frames (CI smoke). Default: run forever.")
def cli(endpoint: str, topic: str, width: int, height: int, fps: float,
        max_frames: int | None) -> None:
    """Publish a synthetic 720p RGB stream over ZMQ for HW1+ smoke tests."""
    console.log(
        f"[green]aiming-frame-pub[/green] bound to {endpoint!r} "
        f"(topic={topic!r}, {width}x{height}@{fps}fps)"
    )
    run_publisher(width=width, height=height, fps=fps,
                  endpoint=endpoint, topic=topic, max_frames=max_frames)


if __name__ == "__main__":
    cli()  # pragma: no cover

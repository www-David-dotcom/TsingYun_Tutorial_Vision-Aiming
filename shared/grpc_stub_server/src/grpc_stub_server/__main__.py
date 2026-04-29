"""CLI entry point.

    uv run aiming-stub-server --port 7654
"""

from __future__ import annotations

import signal
import sys
import time

import click
from rich.console import Console

from .server import build_server

console = Console()


@click.command()
@click.option("--port", default=7654, show_default=True,
              help="gRPC port to listen on.")
@click.option("--once", is_flag=True,
              help="Start the server, sleep for one second, exit. Used by CI.")
def cli(port: int, once: bool) -> None:
    """Run the stand-in AimingArena gRPC server."""
    server = build_server(port=port)
    server.start()
    console.log(f"[green]aiming-stub-server[/green] listening on :{port}")

    if once:
        time.sleep(1.0)
        server.stop(grace=0.5)
        return

    def _shutdown(*_args: object) -> None:
        console.log("shutdown signal received; draining …")
        server.stop(grace=2.0).wait()
        sys.exit(0)

    signal.signal(signal.SIGINT, _shutdown)
    signal.signal(signal.SIGTERM, _shutdown)
    server.wait_for_termination()


if __name__ == "__main__":
    cli()  # pragma: no cover

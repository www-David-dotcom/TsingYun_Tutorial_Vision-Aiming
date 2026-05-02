from __future__ import annotations

import json
import socket
import struct
from dataclasses import dataclass
from types import TracebackType
from typing import Any


@dataclass
class UnityTrainingClient:
    host: str = "127.0.0.1"
    port: int = 7654
    timeout_seconds: float = 5.0

    def __enter__(self) -> UnityTrainingClient:
        self._sock = socket.create_connection(
            (self.host, self.port),
            timeout=self.timeout_seconds,
        )
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        self.close()

    def close(self) -> None:
        sock = getattr(self, "_sock", None)
        if sock is not None:
            sock.close()
            self._sock = None

    def call(self, method: str, request: dict[str, Any]) -> dict[str, Any]:
        sock = self._sock
        body = json.dumps({"method": method, "request": request}).encode("utf-8")
        sock.sendall(struct.pack(">I", len(body)) + body)

        header = self._recv_exact(4)
        (length,) = struct.unpack(">I", header)
        response = json.loads(self._recv_exact(length).decode("utf-8"))
        if not response.get("ok"):
            raise RuntimeError(response.get("error", f"{method} failed"))
        return response["response"]

    def _recv_exact(self, n: int) -> bytes:
        chunks: list[bytes] = []
        remaining = n
        while remaining:
            chunk = self._sock.recv(remaining)
            if not chunk:
                raise ConnectionError("Unity arena closed the TCP connection")
            chunks.append(chunk)
            remaining -= len(chunk)
        return b"".join(chunks)

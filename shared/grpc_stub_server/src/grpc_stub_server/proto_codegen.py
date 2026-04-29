"""Lazy proto codegen.

Stage 1 ships the .proto files but no pre-generated _pb2.py. The first
invocation of any module that needs them shells out to `python -m
grpc_tools.protoc` and writes the generated modules into a
`_generated/` package. This avoids checking generated code into git and
keeps the codegen behaviour identical between the team box and CI.

Generated code lands under shared/grpc_stub_server/src/grpc_stub_server/_generated/.
"""

from __future__ import annotations

import sys
import subprocess
import importlib
from pathlib import Path
from types import ModuleType


_PROTO_FILES = ["aiming.proto", "sensor.proto", "episode.proto"]


def _repo_root() -> Path:
    # shared/grpc_stub_server/src/grpc_stub_server/proto_codegen.py
    #   parents[0] = grpc_stub_server (package)
    #   parents[1] = src
    #   parents[2] = grpc_stub_server (project)
    #   parents[3] = shared
    #   parents[4] = repo root
    return Path(__file__).resolve().parents[4]


def _proto_dir() -> Path:
    return _repo_root() / "shared" / "proto"


def _gen_dir() -> Path:
    return Path(__file__).resolve().parent / "_generated"


def regenerate(force: bool = False) -> None:
    """Regenerate the _generated/ package.

    With force=False this is a no-op when generated files already exist
    and are newer than the source .proto files; with force=True it rebuilds
    unconditionally.
    """
    gen_dir = _gen_dir()
    proto_dir = _proto_dir()
    gen_dir.mkdir(exist_ok=True)
    init_file = gen_dir / "__init__.py"
    if not init_file.exists():
        init_file.write_text("# auto-generated; do not edit by hand\n")

    if not force:
        any_stale = False
        for proto_name in _PROTO_FILES:
            stem = proto_name.removesuffix(".proto")
            generated = gen_dir / f"{stem}_pb2.py"
            source = proto_dir / proto_name
            if not generated.exists():
                any_stale = True
                break
            if source.stat().st_mtime > generated.stat().st_mtime:
                any_stale = True
                break
        if not any_stale:
            return

    proto_paths = [str(proto_dir / name) for name in _PROTO_FILES]
    cmd = [
        sys.executable, "-m", "grpc_tools.protoc",
        f"-I{proto_dir}",
        f"--python_out={gen_dir}",
        f"--grpc_python_out={gen_dir}",
        *proto_paths,
    ]
    subprocess.run(cmd, check=True)

    # grpc_tools generates absolute imports like `import aiming_pb2 as ...`,
    # which fails when the modules live in a sub-package. Rewrite to
    # relative imports.
    for stem in [name.removesuffix(".proto") for name in _PROTO_FILES]:
        for suffix in ("_pb2.py", "_pb2_grpc.py"):
            f = gen_dir / f"{stem}{suffix}"
            if not f.exists():
                continue
            text = f.read_text()
            for other_stem in [n.removesuffix(".proto") for n in _PROTO_FILES]:
                text = text.replace(
                    f"import {other_stem}_pb2 as",
                    f"from . import {other_stem}_pb2 as",
                )
            f.write_text(text)


def import_pb2(stem: str) -> ModuleType:
    """Lazily import a generated _pb2 module, regenerating if needed."""
    regenerate(force=False)
    return importlib.import_module(f"grpc_stub_server._generated.{stem}_pb2")


def import_pb2_grpc(stem: str) -> ModuleType:
    regenerate(force=False)
    return importlib.import_module(f"grpc_stub_server._generated.{stem}_pb2_grpc")

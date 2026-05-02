"""Team-only: upload one or more local files to OSS and append rows to manifest.toml.

Usage:

    OSS_ACCESS_KEY_ID=... OSS_ACCESS_KEY_SECRET=... \\
    uv run python shared/scripts/push_assets.py \\
        --bucket tsingyun-aiming-hw-public \\
        --visibility anonymous \\
        --key builds/unity/0.1.0/aiming_arena_linux_x86_64.zip \\
        --name unity-arena-linux-x64 \\
        --file out/builds/aiming_arena_linux_x86_64.zip \\
        --description "Unity arena build, Linux x86_64"

Computes sha256, uploads to OSS, then appends a TOML row to manifest.toml
that downstream fetch_assets.py invocations will resolve.

Idempotent on the same {bucket, key, sha256} — re-running won't duplicate
rows or re-upload an unchanged file.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import sys
from pathlib import Path

try:
    import tomllib
except ModuleNotFoundError:  # pragma: no cover
    import tomli as tomllib  # type: ignore


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def _append_manifest(manifest_path: Path, row: dict) -> None:
    """Append a row to manifest.toml. We write the row by hand rather
    than using a TOML emitter because toml-emitting libraries reorder
    keys and rewrite the existing file with their own formatting; that
    blows up diffs and clobbers the human-curated comments at the top
    of manifest.toml."""
    text = manifest_path.read_text()
    if not text.endswith("\n"):
        text += "\n"

    existing = tomllib.loads(text)
    for entry in existing.get("asset", []):
        if (entry["bucket"] == row["bucket"]
                and entry["key"] == row["key"]
                and entry["sha256"] == row["sha256"]):
            print(f"  [{'idempotent skip':^28}] {row['name']}: identical row already present")
            return

    serialised = "\n[[asset]]\n"
    for k in ["name", "bucket", "key", "sha256"]:
        serialised += f'{k:<11} = "{row[k]}"\n'
    serialised += f"size        = {row['size']}\n"
    for k in ["visibility", "local_path"]:
        serialised += f'{k:<11} = "{row[k]}"\n'
    if row.get("description"):
        serialised += f'description = "{row["description"]}"\n'

    manifest_path.write_text(text + serialised)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--name",        required=True, help="manifest entry name (unique)")
    parser.add_argument("--bucket",      required=True, choices=[
        "tsingyun-aiming-hw-public",
        "tsingyun-aiming-hw-models",
        "tsingyun-aiming-hw-cache",
    ])
    parser.add_argument("--key",         required=True, help="OSS object key")
    parser.add_argument("--file",        required=True, type=Path,
                        help="local file to upload")
    parser.add_argument("--visibility",  required=True,
                        choices=["anonymous", "private"])
    parser.add_argument("--local-path",  default=None,
                        help="manifest local_path; defaults to out/assets/<basename>")
    parser.add_argument("--description", default="")
    parser.add_argument("--manifest",    type=Path,
                        default=_repo_root() / "shared/assets/manifest.toml")
    parser.add_argument("--endpoint",    default="oss-cn-beijing.aliyuncs.com")
    args = parser.parse_args(argv)

    if not args.file.exists():
        print(f"file not found: {args.file}", file=sys.stderr)
        return 2

    key_id = os.environ.get("OSS_ACCESS_KEY_ID")
    key_secret = os.environ.get("OSS_ACCESS_KEY_SECRET")
    if not key_id or not key_secret:
        print("OSS_ACCESS_KEY_ID / OSS_ACCESS_KEY_SECRET must be set", file=sys.stderr)
        return 2

    try:
        import oss2  # type: ignore
    except ImportError:
        print("oss2 not installed; run `uv sync`", file=sys.stderr)
        return 2

    digest = _sha256_of(args.file)
    size = args.file.stat().st_size

    auth = oss2.Auth(key_id, key_secret)
    bucket = oss2.Bucket(auth, f"https://{args.endpoint}", args.bucket)
    print(f"uploading {args.file} ({size:,} bytes) → oss://{args.bucket}/{args.key}")
    bucket.put_object_from_file(args.key, str(args.file))

    local_path = args.local_path or f"out/assets/{args.file.name}"
    row = {
        "name":        args.name,
        "bucket":      args.bucket,
        "key":         args.key,
        "sha256":      digest,
        "size":        size,
        "visibility":  args.visibility,
        "local_path":  local_path,
        "description": args.description,
    }
    _append_manifest(args.manifest, row)
    print(f"appended manifest row: {args.name}  sha256={digest[:12]}…")
    return 0


if __name__ == "__main__":
    sys.exit(main())

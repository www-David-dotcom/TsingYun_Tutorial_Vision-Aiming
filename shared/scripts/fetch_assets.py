"""Resolve and download every blob declared in shared/assets/manifest.toml.

Usage from the repo root:

    uv run python shared/scripts/fetch_assets.py            # fetch everything
    uv run python shared/scripts/fetch_assets.py --dry-run  # parse, list, no I/O
    uv run python shared/scripts/fetch_assets.py --only sentinel-public

Authentication:
  - "anonymous" assets need no creds (public bucket, plain HTTPS).
  - "private" assets read OSS_ACCESS_KEY_ID and OSS_ACCESS_KEY_SECRET from
    the environment. If either is unset, private assets are skipped with
    a warning rather than failing the whole run.

Verification: every download is hashed (sha256) and compared against the
manifest. A mismatch deletes the offending file and exits non-zero.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import sys
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path

try:
    import tomllib  # py311+
except ModuleNotFoundError:  # pragma: no cover
    import tomli as tomllib  # type: ignore


@dataclass(frozen=True)
class Asset:
    name: str
    bucket: str
    key: str
    sha256: str
    size: int
    visibility: str  # "anonymous" | "private"
    local_path: Path
    description: str = ""


@dataclass(frozen=True)
class Manifest:
    oss_endpoint: str
    oss_region: str
    assets: tuple[Asset, ...]


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def parse_manifest(path: Path) -> Manifest:
    raw = tomllib.loads(path.read_text())
    endpoint = raw["oss_endpoint"]
    region = raw["oss_region"]
    rows = raw.get("asset", [])
    assets = tuple(
        Asset(
            name=row["name"],
            bucket=row["bucket"],
            key=row["key"],
            sha256=row["sha256"],
            size=int(row["size"]),
            visibility=row["visibility"],
            local_path=Path(row["local_path"]),
            description=row.get("description", ""),
        )
        for row in rows
    )
    return Manifest(oss_endpoint=endpoint, oss_region=region, assets=assets)


# ----------------------------------------------------------------- downloaders

def _public_url(endpoint: str, bucket: str, key: str) -> str:
    return f"https://{bucket}.{endpoint}/{key}"


def _download_anonymous(asset: Asset, endpoint: str, dest: Path) -> None:
    url = _public_url(endpoint, asset.bucket, asset.key)
    dest.parent.mkdir(parents=True, exist_ok=True)
    tmp = dest.with_suffix(dest.suffix + ".part")
    with urllib.request.urlopen(url, timeout=60) as resp, tmp.open("wb") as fh:
        while True:
            chunk = resp.read(1 << 16)
            if not chunk:
                break
            fh.write(chunk)
    tmp.replace(dest)


def _download_private(asset: Asset, endpoint: str, dest: Path) -> None:
    """Use oss2 for authenticated GETs. Imported lazily so users who only
    fetch anonymous assets don't need the SDK installed."""
    try:
        import oss2  # type: ignore
    except ImportError as exc:
        raise RuntimeError(
            "Private asset requires `oss2`. Install via `uv sync` or "
            "`pip install oss2`."
        ) from exc

    key_id = os.environ.get("OSS_ACCESS_KEY_ID")
    key_secret = os.environ.get("OSS_ACCESS_KEY_SECRET")
    if not key_id or not key_secret:
        raise RuntimeError(
            "OSS_ACCESS_KEY_ID / OSS_ACCESS_KEY_SECRET must be set to "
            "fetch private assets. Skipping."
        )

    auth = oss2.Auth(key_id, key_secret)
    bucket = oss2.Bucket(auth, f"https://{endpoint}", asset.bucket)
    dest.parent.mkdir(parents=True, exist_ok=True)
    tmp = dest.with_suffix(dest.suffix + ".part")
    bucket.get_object_to_file(asset.key, str(tmp))
    tmp.replace(dest)


# --------------------------------------------------------------------- helpers

def _sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def _is_placeholder_digest(digest: str) -> bool:
    """A manifest may carry a placeholder digest before the team uploads.
    Treat all-zeros as 'unknown, accept any' but warn the user."""
    return set(digest) == {"0"}


def _ensure_one(asset: Asset, manifest: Manifest, repo_root: Path,
                dry_run: bool) -> str:
    abs_dest = repo_root / asset.local_path

    if abs_dest.exists() and not _is_placeholder_digest(asset.sha256):
        if _sha256_of(abs_dest) == asset.sha256:
            return "cached"

    if dry_run:
        return "would-fetch"

    if asset.visibility == "anonymous":
        _download_anonymous(asset, manifest.oss_endpoint, abs_dest)
    elif asset.visibility == "private":
        _download_private(asset, manifest.oss_endpoint, abs_dest)
    else:
        raise ValueError(f"unknown visibility: {asset.visibility!r}")

    if _is_placeholder_digest(asset.sha256):
        return "fetched (placeholder digest accepted)"

    actual = _sha256_of(abs_dest)
    if actual != asset.sha256:
        abs_dest.unlink(missing_ok=True)
        raise RuntimeError(
            f"sha256 mismatch for {asset.name}: expected {asset.sha256}, "
            f"got {actual}. file deleted."
        )
    return "fetched"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Fetch assets from OSS per manifest.toml")
    parser.add_argument("--manifest", type=Path,
                        default=_repo_root() / "shared/assets/manifest.toml")
    parser.add_argument("--dry-run", action="store_true",
                        help="Parse the manifest, report per-asset status, do no I/O.")
    parser.add_argument("--only", action="append", default=[],
                        help="Restrict to named asset(s); can be repeated.")
    args = parser.parse_args(argv)

    manifest = parse_manifest(args.manifest)
    repo_root = _repo_root()
    selected = manifest.assets
    if args.only:
        wanted = set(args.only)
        selected = tuple(a for a in manifest.assets if a.name in wanted)
        missing = wanted - {a.name for a in selected}
        if missing:
            print(f"unknown asset(s): {sorted(missing)}", file=sys.stderr)
            return 2

    n_ok = n_skipped = n_err = 0
    for asset in selected:
        try:
            status = _ensure_one(asset, manifest, repo_root, args.dry_run)
            print(f"  [{status:^28}] {asset.name}  ({asset.bucket}/{asset.key})")
            n_ok += 1
        except RuntimeError as exc:
            if "Skipping" in str(exc):
                print(f"  [{'skipped':^28}] {asset.name}: {exc}")
                n_skipped += 1
            else:
                print(f"  [{'ERROR':^28}] {asset.name}: {exc}", file=sys.stderr)
                n_err += 1

    print(f"\n{n_ok} ok, {n_skipped} skipped, {n_err} error(s)")
    return 0 if n_err == 0 else 1


if __name__ == "__main__":
    sys.exit(main())

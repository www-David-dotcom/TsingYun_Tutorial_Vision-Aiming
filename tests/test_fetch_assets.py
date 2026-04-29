"""End-to-end test for shared/scripts/fetch_assets.py against a local minio.

The real OSS endpoint isn't reachable from CI without committed creds, so we
spin up minio (S3-compatible) on localhost and exercise the resolver against
it. Public-bucket fetches go through urllib (anonymous) and private-bucket
fetches go through oss2 with localhost creds; both code paths exercise the
sha256 verification logic.

Skipped unless `minio` binary is on PATH.
"""

from __future__ import annotations

import hashlib
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT / "shared" / "scripts"))

import fetch_assets  # noqa: E402


def _have_minio() -> bool:
    return shutil.which("minio") is not None


def _have_oss2() -> bool:
    try:
        import oss2  # noqa: F401
        return True
    except ImportError:
        return False


@pytest.fixture(scope="module")
def minio_endpoint(tmp_path_factory: pytest.TempPathFactory):
    if not _have_minio():
        pytest.skip("minio binary not on PATH")
    data_dir = tmp_path_factory.mktemp("minio-data")
    env = {**os.environ,
           "MINIO_ROOT_USER": "test-key-id",
           "MINIO_ROOT_PASSWORD": "test-key-secret"}
    proc = subprocess.Popen(
        ["minio", "server", str(data_dir), "--address", ":19000"],
        env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
    )
    # Wait for minio to start up
    for _ in range(40):
        time.sleep(0.1)
        try:
            import urllib.request
            urllib.request.urlopen("http://127.0.0.1:19000/minio/health/live", timeout=0.3).read()
            break
        except Exception:
            continue
    yield "127.0.0.1:19000"
    proc.terminate()
    proc.wait(timeout=5)


def test_parse_manifest_reads_real_file():
    m = fetch_assets.parse_manifest(REPO_ROOT / "shared/assets/manifest.toml")
    assert m.oss_region == "cn-beijing"
    assert m.oss_endpoint.endswith("aliyuncs.com")
    assert any(a.name == "sentinel-public" for a in m.assets)


def test_placeholder_digest_recognised():
    assert fetch_assets._is_placeholder_digest("0" * 64)
    assert not fetch_assets._is_placeholder_digest("0" * 63 + "1")
    assert not fetch_assets._is_placeholder_digest(
        "abcd1234" * 8
    )


def test_dry_run_reports_status_for_every_asset(capsys):
    rc = fetch_assets.main(["--dry-run"])
    out = capsys.readouterr().out
    assert rc == 0
    assert "sentinel-public" in out
    assert "would-fetch" in out or "cached" in out


@pytest.mark.skipif(not _have_oss2(), reason="oss2 not installed")
@pytest.mark.skipif(not _have_minio(), reason="minio not on PATH")
def test_fetch_anonymous_against_minio(minio_endpoint, tmp_path: Path, monkeypatch):
    """Drop a 1 KB blob into a minio-public bucket and verify fetch_assets
    pulls it down with the correct sha256 verification."""
    pytest.importorskip("oss2")
    import oss2

    body = b"hello-public-" * 64  # 832 bytes
    digest = hashlib.sha256(body).hexdigest()

    # Stand up a public bucket on minio with the body inside.
    auth = oss2.Auth("test-key-id", "test-key-secret")
    bucket = oss2.Bucket(auth, f"http://{minio_endpoint}", "tsingyun-aiming-hw-public")
    try:
        bucket.create_bucket("public-read")
    except Exception:
        # already exists from a previous test in the module
        pass
    bucket.put_object("sentinels/v0.5.0/hello-public.txt", body)

    # Point fetch_assets at our minio
    manifest_text = (REPO_ROOT / "shared/assets/manifest.toml").read_text()
    patched = manifest_text.replace(
        'oss_endpoint = "oss-cn-beijing.aliyuncs.com"',
        f'oss_endpoint = "{minio_endpoint}"',
    ).replace(
        '"0000000000000000000000000000000000000000000000000000000000000000"',
        f'"{digest}"',
    ).replace(
        "size        = 0",
        f"size        = {len(body)}",
    )
    fake_manifest = tmp_path / "manifest.toml"
    fake_manifest.write_text(patched)

    # Override the public-URL helper so it talks plain HTTP to minio.
    monkeypatch.setattr(fetch_assets, "_public_url",
                        lambda endpoint, bucket, key: f"http://{endpoint}/{bucket}/{key}")

    monkeypatch.chdir(tmp_path)
    rc = fetch_assets.main(["--manifest", str(fake_manifest)])
    assert rc == 0
    fetched = tmp_path / "out/assets/sentinels/hello-public.txt"
    assert fetched.exists()
    assert hashlib.sha256(fetched.read_bytes()).hexdigest() == digest

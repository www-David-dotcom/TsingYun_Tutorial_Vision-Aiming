# OSS asset distribution

All blob assets - datasets, opponent weights, exported Unity builds, and the
reference Docker image - live on Aliyun OSS in the `cn-beijing` region.
The repo carries text manifests; the resolver downloads on demand.

## Buckets

| Bucket | Visibility | Contents | ACL |
|---|---|---|---|
| `tsingyun-aiming-hw-public` | anonymous-read | Unity builds; HW1 eval set + real holdout; HW2/3/4 fixtures; candidate-facing PDFs | public-read |
| `tsingyun-aiming-hw-models` | private | bronze/silver/gold opponent `.pt` + checkpoints; reference detector ONNX; replay-bag fixtures > 50 MB | private; SSE-OSS |
| `tsingyun-aiming-hw-cache`  | private | reference Docker image (`docker/toolchain/<tag>/`); vcpkg/uv caches | private |

Endpoint: `oss-cn-beijing.aliyuncs.com`. Use
`oss-cn-beijing-internal.aliyuncs.com` from inside the same Aliyun
region's VPC to avoid public-egress charges.

## Authentication

`shared/scripts/fetch_assets.py` reads two environment variables:

```bash
export OSS_ACCESS_KEY_ID=...
export OSS_ACCESS_KEY_SECRET=...
```

Recommended: stash these in `~/.envrc.local` and source via
[`direnv`](https://direnv.net/), or fetch from 1Password / system
keychain. Don't commit them.

The public bucket needs no creds. If `OSS_ACCESS_KEY_ID` is unset,
fetch_assets prints a warning and skips private rows rather than
failing the whole run.

## Adding a new asset

```bash
# (one-time) export creds
source ~/.envrc.local

uv run python shared/scripts/push_assets.py \
    --bucket tsingyun-aiming-hw-public \
    --visibility anonymous \
    --key builds/unity/0.1.0/aiming_arena_linux_x86_64.zip \
    --name unity-arena-linux-x64 \
    --file out/builds/aiming_arena_linux_x86_64.zip \
    --description "Unity arena build, Linux x86_64"
```

That uploads the file, computes its sha256, and appends an idempotent
row to `shared/assets/manifest.toml`. Commit the manifest change; the
file itself is deliberately not in git.

## Pulling assets

```bash
# parses manifest, lists every asset, no I/O
uv run python shared/scripts/fetch_assets.py --dry-run

# actually download (skips already-correct files via sha256 check)
uv run python shared/scripts/fetch_assets.py

# narrow to one or two
uv run python shared/scripts/fetch_assets.py --only unity-arena-linux-x64
```

Files land under `out/assets/<local_path>/...` (gitignored).

## Rotating the AccessKey

The `OSS_ACCESS_KEY_ID` we currently use is a team-level RAM user; if
ever leaked, rotate via the Aliyun RAM console:

1. Console → RAM → Users → the team user → AccessKey tab → rotate.
2. Update everyone's `~/.envrc.local`.
3. No code or manifest change needed; the resolver reads env vars only.

(The previous v0.3 plan mentioned a "shared read-only AccessKey
committed to the candidate repo"; that was tied to candidate-side
grading, which is deferred per schema v0.4. For now, only the team
needs OSS creds.)

## Multi-arch Docker image

The reference toolchain image is a manifest-list pointing at both
`linux/amd64` and `linux/arm64` blobs. `docker pull` picks the right
one for the host automatically:

```bash
docker pull tsingyun-aiming-hw-cache.oss-cn-beijing.aliyuncs.com/docker/toolchain/0.5.0:latest
```

If you're on a Jetson / Orin (aarch64), this just works. If you're on
an Apple Silicon Mac and want the arm64 build for native speed, also
just works. To force a specific arch (e.g. for cross-arch testing):

```bash
docker pull --platform linux/amd64 tsingyun-aiming-hw-cache.oss-cn-beijing.aliyuncs.com/docker/toolchain/0.5.0:latest
```

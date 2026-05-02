# Reference toolchain image

This directory builds and ships **the** reproducible build environment for
the recruitment assignment. Anyone — candidate, TA, or future grading host
— compiling the codebase inside `aiming-hw-toolchain` gets a byte-identical
toolchain regardless of their host OS.

Schema cross-reference: §6.3 of [`schema.md`](../../schema.md).

## What's inside

* Ubuntu 22.04 base
* C++: g++-12 (default), clang-15, cmake, ninja, Eigen 3.4
* Protobuf + gRPC (system packages, currently pinned to whatever 22.04 ships)
* Python 3.11 + uv (pinned)
* Multi-arch: `linux/amd64` + `linux/arm64` so the same tag works on
  candidates' x86_64 dev machines and on Jetson-class ARM hardware.

## Build (maintainer only)

```bash
docker buildx create --name aiming-hw --use 2>/dev/null || true

docker buildx build \
    --platform linux/amd64,linux/arm64 \
    -t tsingyun-aiming-hw-cache.oss-cn-beijing.aliyuncs.com/docker/toolchain/0.5.0:latest \
    -f shared/docker/toolchain.Dockerfile \
    --push \
    .
```

Then update `tools/grader/image.lock` (will exist as of HW1) with the new
digest. **Bumping the tag without bumping `image.lock` is a smell** — it
means the manifest of "what image did we use to grade" drifts from reality.

## Pull (anyone)

```bash
docker pull tsingyun-aiming-hw-cache.oss-cn-beijing.aliyuncs.com/docker/toolchain/0.5.0:latest
```

Or via the compose shortcut:

```bash
cd Aiming_HW
docker compose -f shared/docker/toolchain.compose.yaml run --rm dev
# now you're inside /workspace inside the image; cmake --preset linux-debug etc.
```

## Why we don't push to ghcr.io

Aliyun OSS is the team's chosen object store (see `docs/oss_assets.md`)
and CN-region pull speeds are dramatically better than ghcr.io for the
candidate population. Pushing to OSS via the `cache` bucket lets us treat
the image as just another versioned artifact alongside Unity builds, datasets,
and opponent weights.

If we ever need to expose the image internationally, we can mirror to
ghcr.io as a secondary; the OSS path stays canonical.

## Why we don't ship ONNX Runtime / acados here

HW1 (detector) needs ONNX Runtime; HW5 (MPC) needs acados. Neither is
required for Stage 1's gRPC + ZMQ smoke. Adding them prematurely costs ~5
minutes per build for no gain. The HW1 / HW5 stages add them as overlays
(or as pinned wheels via uv).

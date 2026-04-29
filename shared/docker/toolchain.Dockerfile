# syntax=docker/dockerfile:1.7
# Reference toolchain image for the recruitment assignment.
# Purpose: build environment reproducibility (schema §6.3). Anyone building
# the codebase inside this image gets a byte-identical toolchain regardless
# of host OS / CUDA version.
#
# Built once on the maintainer's workstation, pushed to
# oss://tsingyun-aiming-hw-cache/docker/toolchain/<tag>/, pulled by every
# candidate. Multi-arch (linux/amd64 + linux/arm64) so the same tag works
# on both x86_64 dev machines and Jetson-class ARM hardware.
#
# Build:
#     docker buildx build \\
#         --platform linux/amd64,linux/arm64 \\
#         -t tsingyun-aiming-hw-cache.oss-cn-beijing.aliyuncs.com/docker/toolchain/0.5.0:latest \\
#         -f shared/docker/toolchain.Dockerfile .

ARG BASE=ubuntu:22.04
FROM ${BASE}

LABEL org.opencontainers.image.title="aiming-hw-toolchain"
LABEL org.opencontainers.image.version="0.5.0"
LABEL org.opencontainers.image.source="https://github.com/www-David-dotcom/TsingYun_Tutorial_Vision-Aiming"
LABEL org.opencontainers.image.description="Reference toolchain for the TsingYun aiming HW assignment"

ENV DEBIAN_FRONTEND=noninteractive \
    LANG=C.UTF-8 \
    PATH="/root/.local/bin:${PATH}"

# --- system deps --------------------------------------------------------------

RUN apt-get update && apt-get install -y --no-install-recommends \
        build-essential \
        ca-certificates \
        clang-15 \
        clang-format-15 \
        cmake \
        curl \
        g++-12 \
        git \
        libeigen3-dev \
        libssl-dev \
        ninja-build \
        pkg-config \
        protobuf-compiler \
        protobuf-compiler-grpc \
        libprotobuf-dev \
        libgrpc++-dev \
        libabsl-dev \
        python3 \
        python3-dev \
        python3-pip \
        python3-venv \
        unzip \
        zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

# Pin g++-12 / clang-15 as defaults so candidate builds match across host
# distros that ship different defaults.
RUN update-alternatives --install /usr/bin/cc  cc  /usr/bin/gcc-12  100 && \
    update-alternatives --install /usr/bin/c++ c++ /usr/bin/g++-12  100

# --- uv -----------------------------------------------------------------------

# Pinned uv version; bump in lockstep with the root pyproject.toml's
# requires-uv constraint. Installs to /root/.local/bin which is on PATH.
ARG UV_VERSION=0.5.30
RUN curl -LsSf "https://astral.sh/uv/${UV_VERSION}/install.sh" | sh

# --- ONNX Runtime + acados (deferred) -----------------------------------------

# Stage 1 doesn't need ONNX Runtime or acados yet. HW1 (detector) and HW5
# (MPC) will install them — likely as a pinned wheel via uv and a
# pre-built static library mirrored to the OSS cache bucket. Adding them
# here pre-emptively would slow Stage 1 builds by ~5 minutes for no gain.

# --- workspace layout ---------------------------------------------------------

WORKDIR /workspace
COPY pyproject.toml ./
COPY shared/grpc_stub_server/pyproject.toml ./shared/grpc_stub_server/
COPY shared/zmq_frame_pub/pyproject.toml ./shared/zmq_frame_pub/

# Pre-resolve the Python deps so downstream stages don't reinstall them on
# every image rebuild. The actual sources are mounted at runtime via
# docker compose; this layer just warms the uv cache.
RUN uv sync --frozen 2>/dev/null || uv sync --no-install-project

# --- entrypoint ---------------------------------------------------------------

# Default: drop into a shell. CI invocations override CMD to run a specific
# build/test command.
CMD ["/bin/bash"]

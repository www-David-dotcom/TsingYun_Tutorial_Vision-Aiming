# godot_rl_agents (vendored — Stage 2 placeholder)

Per `IMPLEMENTATION_PLAN.md` Resolved Decision 1, the
[godot_rl_agents](https://github.com/edbeeching/godot_rl_agents) addon is
**vendored at a pinned commit** under this directory. Stage 2 ships the
placeholder; the actual addon files land when the `bronze` opponent
training pipeline (Stage 3 / HW1) needs them.

## Why vendored, not a submodule

* Submodules add a clone-time fetch step that breaks the
  `git clone && open in Godot` flow we want for candidates.
* The upstream repo is small (< 5 MB); the cost of vendoring is low.
* Pinning a commit gives us deterministic builds without depending on
  upstream tag stability.

## How to populate

```bash
# Pinning to the v1.x line that matches Godot 4.3.
git clone --depth=1 --branch v1.0.0 \
    https://github.com/edbeeching/godot_rl_agents \
    /tmp/grla

# Copy just the addon directory; LICENSE and README come along.
rsync -a /tmp/grla/addons/godot_rl_agents/ \
    shared/godot_arena/addons/godot_rl_agents/
rsync -a /tmp/grla/LICENSE \
    shared/godot_arena/addons/godot_rl_agents/UPSTREAM_LICENSE
```

Record the upstream commit SHA in `shared/godot_arena/addons/godot_rl_agents/UPSTREAM_COMMIT.txt`
so future updates can be diffed cleanly.

## Stage 2 status

Empty. The arena does not currently need the addon — the only "agent"
in Stage 2 is the candidate's own gRPC client. Stage 3 will populate
this directory as part of HW1's training-pipeline work.

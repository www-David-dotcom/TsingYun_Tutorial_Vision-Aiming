# generated_solver/

This directory is **populated by acados codegen**, not by hand. It
ships empty in git; the team runs

```bash
uv sync --group hw5
uv run python HW5_mpc_gimbal/src/generate_acados.py \
    --weights HW5_mpc_gimbal/configs/mpc_weights.yaml
```

once per weight change and pushes the resulting tarball to OSS via
`shared/scripts/push_assets.py` under the
`acados-solver-hw5-vX.Y` manifest entry. Candidates pull it back via
`shared/scripts/fetch_assets.py --only acados-solver-hw5-vX.Y`.

After running codegen (or pulling the tarball), this directory looks
like:

```
generated_solver/
├── README.md                                  ← this file
├── acados_ocp.json                            ← codegen-time config
└── acados_aiming_mpc/
    ├── CMakeLists.txt                         ← built into hw5_acados_solver
    ├── acados_solver_aiming_mpc.{h,c}
    ├── acados_sim_solver_aiming_mpc.{h,c}
    └── ... (hpipm + blasfeo internal artefacts)
```

`HW5_mpc_gimbal/CMakeLists.txt` looks for
`acados_aiming_mpc/CMakeLists.txt` at configure time and only adds
the MPC target when it finds it. Without the codegen, the project
still builds the PID baseline and its public tests — just not
`hw5_mpc_controller`.

## Why not commit the codegened files?

The acados output is a few hundred kB of generated C, partially
machine-specific (the codegen embeds absolute paths to acados
includes), and changes whenever the candidate edits the weights or
the model. Committing it would either:
* Produce a repo full of generated noise that conflicts on every
  weight tweak, or
* Pin a specific weight setting in git, which fights the candidate's
  iteration loop.

OSS-hosted with a SHA-256 in the manifest is the same pattern used for
large generated assets.

# Cleanup Roadmap

`schema.md` is the source of truth. HW1-HW7 are now the Unity-first A1-A7 assignment modules; legacy-only workflows remain archived or explicitly marked.

## Milestone 1: Unity Rule Runtime

- [x] Retire non-Unity arena runtime from active code paths.
- [x] Move schema constants into named code constants.
- [x] Implement core match rules: fixed 5-minute timer, HP, damage, healing, boost scoring, hit rates, and respawn.
- [x] Split match score/runtime/world state out of `ArenaMain`.
- [x] Split Unity rule marker rendering out of `ArenaMain`.
- [x] Add explicit fire heat/lock behavior from schema.

## Milestone 2: Runtime Boundaries

- [x] Keep `ArenaMain` focused on episode orchestration and transport.
- [x] Move scene presentation helpers into small Unity components.
- [x] Reduce reflection-heavy PlayMode test seams.
- [x] Document the stable Unity wire contract for candidate runners.

## Milestone 3: Assignment Redesign

- [x] Redesign HW1-HW7 into a Unity-first assignment path.
- [x] Remove overlap between old tasks or mark it as legacy-only.
- [x] Ensure every candidate blank starts with `TODO`.
- [x] Add lightweight mini-tests for partial task completion.

## Milestone 4: Training Ground And RL Scaffold

- [x] Add training-ground scene with adjustable target translation and rotation.
- [x] Add baseline non-player aiming strategy.
- [x] Add deterministic training telemetry and smoke checks for target motion, baseline aim, hit rate, and reset behavior.
- [x] Add backend-only RL training scaffold after rule runtime stabilizes.

## Milestone 5: Visual Effects And Readability Polish

- [ ] Upgrade scene visuals toward the sci-fi industrial style in `schema.md`.
- [ ] Preserve armor, team color, target, rule-zone, and frame-stream readability while adding lighting, material, and VFX polish.
- [ ] Add screenshot or PlayMode visual QA checks before treating the visual pass as complete.

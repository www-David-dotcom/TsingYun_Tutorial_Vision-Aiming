# Engine quality gate — Stage 7 review (template)

`schema.md` §10 decision 1 calls for a 30-minute meeting at the
M3 / Stage 7 milestone where art + product review the Godot arena
shipped at `v0.6-arena-poc` and decide:

* **keep Godot** → continue to Stage 8 on schedule, or
* **port to Unity** → open `stage7b/unity-port`, eat 3 weeks of
  buffer reserved for this branch, ship `v1.1b-unity-port`.

This document is the **template** for the meeting outcome. The
actual decision lands as a follow-up commit signed off by the
reviewing leads.

## Date

> Schedule the review for the start of Stage 7 calendar, not the
> end. Cutting back to Unity on day 11 of a 12-day stage costs
> the same buffer either way; doing it on day 1 saves rework.

## Attendees

| Role | Name |
|------|------|
| Vision lead | _TBD_ |
| Engine lead | _TBD_ |
| Art lead    | _TBD_ |
| Product / mentor | _TBD_ |

## Inputs

* Stage 2 PoC: `shared/godot_arena/`. Live demo in
  `--headless` mode + 60-Hz frame stream (port 7655).
* Frame samples: `out/replays/sample_<seed>.mp4` (rendered via
  `godot --write-movie` once the team builds the binaries).
* Performance numbers: 60 Hz frame stream sustained for 5 min on
  the team's reference workstation; per-tick gRPC round-trip
  latency p95 < 8 ms.
* Risks: GDExtension gRPC deferred (Stage 2 fallback to JSON-over-TCP);
  no kenney_scifi prop pack yet; plates are primitive boxes with
  emission shading.

## Decision criteria

A "keep Godot" outcome requires **all** of:

1. Visual fidelity — does the arena look like a RoboMaster scene
   recognizable to a candidate who has watched a real match? Stage 7
   is the natural place to import `kenney_scifi/` + custom plate
   icons; the box-primitives PoC is not the bar to judge against.
2. Throughput — 60 Hz × 720p RGB frame stream sustained, no frame
   drops over a 90-second match.
3. Determinism — same `EnvReset.seed` produces byte-identical replay
   JSONs. The Stage-2 SeedRng autoload should already give us this.
4. Multi-OS export — Linux + Windows + macOS binaries reproducible
   via `godot --export-release`.

A "port to Unity" outcome triggers if **any** fail. Per the schema,
the proto contract is unchanged across engines; the candidate-facing
surface (`shared/proto/*.proto`) doesn't move.

## Decision

> **TBD.** Fill this in after the meeting:
>
> * [ ] Keep Godot. Stage 7 proceeds with `stage7/hw5-mpc`. Art
>       backlog items added to `shared/godot_arena/assets/README.md`.
> * [ ] Port to Unity. Open `stage7b/unity-port`. Stage 7 calendar
>       extends by 3 weeks; M3 rollover documented in
>       `IMPLEMENTATION_PLAN.md`.

## Action items

> Log here per attendee. Examples (delete or replace):
>
> * **Engine lead:** import `kenney_scifi/` prop pack and ship
>   `v1.1.1-arena-art` by end of Stage 7.
> * **Vision lead:** wire HW1 detector against the new visuals;
>   re-run the synthetic-to-real holdout on the upgraded frames.
> * **Art lead:** produce the four armor-plate icon SVGs (Hero /
>   Engineer / Standard / Sentry); land under
>   `shared/godot_arena/assets/icons/`.

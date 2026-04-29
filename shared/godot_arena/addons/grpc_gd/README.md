# grpc_gd — gRPC GDExtension wrapper (deferred)

`IMPLEMENTATION_PLAN.md` Stage 2 listed a `grpc_gd` GDExtension as a
goal: wrap the C++ gRPC client/server libraries so the arena could
serve `aiming.proto` directly over HTTP/2. The plan also flagged it as
**the** Stage 2 risk, with an explicit fallback ("plain TCP +
protobuf framing") authorized if the work slipped beyond three days.

## Stage 2 decision: defer the GDExtension

The fallback is what shipped. The arena's control surface is exposed
via `scripts/tcp_proto_server.gd` (length-prefixed JSON over TCP) and
the frame stream via `scripts/tcp_frame_pub.gd` (length-prefixed
RGB888 over TCP). The proto contract is preserved — JSON field names
match `shared/proto/*.proto` exactly, so messages round-trip through
`google.protobuf.json_format.Parse` on the candidate side.

## What this addon would contain (when revisited)

* `grpc_gd.gdextension` — manifest pointing at the per-OS shared
  libraries.
* `src/` — C++ wrapper translating GDScript-callable methods into
  `grpc::Server` registrations.
* Pre-built binaries for `linux-x86_64`, `windows-x86_64`, and
  `macos-universal`, hosted in `tsingyun-aiming-hw-cache` and pulled
  via `shared/scripts/fetch_assets.py`.

## When to reopen

Likely Stage 7 (visual review milestone) or whenever a candidate's
inference-loop latency budget needs the binary-proto path. The proto
schema doesn't change; only the on-the-wire encoding does. Until
then, the JSON path is the canonical Stage 2+ transport.

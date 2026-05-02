"""DSL → C++ codegen for HW7 behaviour trees.

Reads a YAML file shaped like `configs/example_bt.yaml` and emits a
self-contained `.cpp` that constructs the matching tree using the
factory functions in `aiming_hw/strategy/behavior_tree.hpp`. The
emitted file declares one entry point:

    NodePtr build_tree();

which the runner (HW6) calls during episode init.

Three node kinds are supported: sequence, selector, action. Action
nodes reference a leaf by name. Six leaf names are recognised:

    engage / retreat_to_cover / patrol / reload
        — direct calls into leaf_actions.hpp.
    should_retreat_check
        - wraps the candidate `should_retreat` blank; emits a
          lambda that returns Success when retreat is true.
    engage_or_patrol
        — convenience: calls engage; if it returns Failure, falls
          through to patrol. Saves wrapping in a Selector when the
          BT is small.

Usage:
    uv run python HW7_strategy/src/dsl_to_cpp.py \
        HW7_strategy/configs/example_bt.yaml \
        > /tmp/example_bt.cpp
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import yaml

PREAMBLE = """\
// AUTO-GENERATED — do not edit by hand. Regenerate with
// `python HW7_strategy/src/dsl_to_cpp.py <path>`.
//
// Source: {source}

#include "aiming_hw/strategy/behavior_tree.hpp"
#include "aiming_hw/strategy/leaf_actions.hpp"
#include "aiming_hw/strategy/strategy.hpp"

#include <vector>

namespace aiming_hw {{
namespace strategy {{

NodePtr build_tree() {{
"""

EPILOGUE = """\
    return root;
}}

}}  // namespace strategy
}}  // namespace aiming_hw
"""

LEAF_NAMES = {
    "engage", "retreat_to_cover", "patrol", "reload",
    "should_retreat_check", "engage_or_patrol",
}


def _emit(node: dict, var: str, depth: int = 1) -> list[str]:
    indent = "    " * depth
    kind = node["kind"]
    label = node.get("label", kind)
    out: list[str] = []
    if kind == "action":
        leaf = node["leaf"]
        if leaf not in LEAF_NAMES:
            raise SystemExit(f"unknown leaf action: {leaf}")
        if leaf == "should_retreat_check":
            out.append(
                f"{indent}auto {var} = action(\"{label}\", "
                "[](Blackboard& bb) {")
            out.append(f"{indent}    SelfInfo self;")
            out.append(f"{indent}    self.x  = bb.get<double>(\"self.x\");")
            out.append(f"{indent}    self.y  = bb.get<double>(\"self.y\");")
            out.append(f"{indent}    self.hp = bb.get<double>(\"self.hp\", 100.0);")
            out.append(f"{indent}    self.ammo = bb.get<int>(\"self.ammo\", 200);")
            out.append(f"{indent}    std::vector<TrackInfo> empty;")
            out.append(f"{indent}    return should_retreat(self, empty)")
            out.append(f"{indent}        ? Status::Success : Status::Failure;")
            out.append(f"{indent}}});")
        elif leaf == "engage_or_patrol":
            out.append(f"{indent}auto {var} = action(\"{label}\", "
                       "[](Blackboard& bb) {")
            out.append(f"{indent}    auto s = engage(bb);")
            out.append(f"{indent}    if (s == Status::Failure) return patrol(bb);")
            out.append(f"{indent}    return s;")
            out.append(f"{indent}}});")
        else:
            out.append(
                f"{indent}auto {var} = action(\"{label}\", "
                f"[](Blackboard& bb) {{ return {leaf}(bb); }});")
        return out

    factory = "sequence" if kind == "sequence" else "selector"
    out.append(f"{indent}auto {var} = {factory}(\"{label}\");")
    for i, child in enumerate(node.get("children", [])):
        child_var = f"{var}_c{i}"
        out.extend(_emit(child, child_var, depth))
        out.append(f"{indent}{var}->add(std::move({child_var}));")
    return out


def main() -> int:
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--out", type=Path,
                        help="write to this path instead of stdout")
    args = parser.parse_args()

    spec = yaml.safe_load(args.source.read_text())
    root = spec["root"]

    lines = [PREAMBLE.format(source=args.source)]
    body = _emit(root, "root", depth=1)
    lines.extend(body)
    lines.append(EPILOGUE)

    output = "\n".join(lines)
    if args.out:
        args.out.write_text(output)
    else:
        sys.stdout.write(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

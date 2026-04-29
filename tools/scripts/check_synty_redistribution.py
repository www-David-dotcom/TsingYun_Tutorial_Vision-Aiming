"""Fail the build if Synty source files are detected in any committed path.

Synty's POLYGON pack license allows binary redistribution but NOT source
.fbx / .png / .mat redistribution. This guard scans `git ls-files` for
any path under `**/Synty/**` and fails CI if any such path is committed
to the repo.
"""

from __future__ import annotations

import subprocess
import sys


def main() -> int:
    result = subprocess.run(
        ["git", "ls-files"], capture_output=True, text=True, check=True,
    )
    forbidden_extensions = {".fbx", ".obj", ".dae", ".blend"}
    forbidden_path_fragment = "/Synty/"

    violations = []
    for line in result.stdout.splitlines():
        if forbidden_path_fragment in line:
            violations.append(line)
            continue
        for ext in forbidden_extensions:
            if line.endswith(ext) and "/Synty/" in line:
                violations.append(line)

    if violations:
        print("[FAIL] Synty source files detected in committed paths:", file=sys.stderr)
        for v in violations:
            print(f"  {v}", file=sys.stderr)
        print("\nSynty's license forbids source redistribution.", file=sys.stderr)
        print("Move these files to .gitignore'd paths and untrack them:", file=sys.stderr)
        print("  git rm --cached <file>", file=sys.stderr)
        return 1

    print("[OK] no Synty source files in committed paths.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Walk every open PR's most-recent validate_submission run, pull the
attached score artefact, and write a leaderboard.

Per Stage 10 v1 (Proposal A), the leaderboard is keyed by PR (one
candidate = one PR; PR title carries `姓名 - 学号`). Ranking is by
total passing public tests, ties broken by HW spread (a candidate
who passes tests across all 7 HWs ranks above one who passes the
same total across only HW2).

Run via `regenerate_leaderboard.yml` daily at 19:17 Beijing-local;
also invocable locally with `gh auth login` set up.

Inputs:
  --repo  owner/name of the candidate-facing repo
  --out-csv / --out-md / --out-json — output paths
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

HW_LABELS = ["HW1", "HW2", "HW3", "HW4", "HW5", "HW6", "HW7"]


@dataclass
class Submission:
    pr_number: int
    pr_title: str
    pr_author: str
    pr_url: str
    head_sha: str
    overall_passed: int
    overall_failed: int
    overall_skipped: int
    by_hw: dict[str, dict]
    last_run_iso: str


def _gh(*args: str) -> str:
    """Shell out to `gh` and return stdout. Inherit GH_TOKEN from env."""
    result = subprocess.run(
        ["gh", *args], capture_output=True, text=True, check=False)
    if result.returncode != 0:
        sys.stderr.write(f"gh {' '.join(args)} failed: {result.stderr}\n")
        return ""
    return result.stdout


def list_open_prs(repo: str) -> list[dict]:
    raw = _gh("pr", "list", "--repo", repo,
              "--state", "open",
              "--limit", "200",
              "--json", "number,title,author,url,headRefOid")
    if not raw:
        return []
    return json.loads(raw)


def latest_validate_run(repo: str, pr_number: int, head_sha: str) -> dict | None:
    """Find the most recent validate_submission workflow run for the
    PR's head commit. Returns the API run record or None."""
    raw = _gh("run", "list", "--repo", repo,
              "--workflow", "validate submission",
              "--commit", head_sha,
              "--limit", "1",
              "--json", "databaseId,headSha,conclusion,updatedAt,status")
    if not raw:
        return None
    runs = json.loads(raw)
    return runs[0] if runs else None


def download_score(repo: str, run_id: int, dest_dir: Path) -> dict | None:
    """Pull the `submission-score-*` artefact from the run and parse
    its JSON. Returns the parsed score dict or None on miss."""
    dest_dir.mkdir(parents=True, exist_ok=True)
    raw = _gh("api", f"repos/{repo}/actions/runs/{run_id}/artifacts",
              "--jq", ".artifacts[] | select(.name|startswith(\"submission-score\")) | "
                     "{id, archive_download_url, name}")
    if not raw.strip():
        return None
    art = json.loads(raw.splitlines()[0])
    # `gh run download` handles the zip extraction; we don't unzip
    # ourselves because `gh api` over the artefact archive URL writes
    # binary to stdout and that's awkward.
    _gh("run", "download", str(run_id),
        "--repo", repo,
        "--name", art["name"],
        "--dir", str(dest_dir))
    score_path = dest_dir / "submission_score.json"
    if not score_path.exists():
        # `gh run download` leaves files at the artefact root; check
        # one level deeper.
        for p in dest_dir.glob("**/submission_score.json"):
            score_path = p
            break
    if not score_path.exists():
        return None
    return json.loads(score_path.read_text())


def collect(repo: str) -> list[Submission]:
    out: list[Submission] = []
    workdir = Path("/tmp/aiming_leaderboard")
    workdir.mkdir(parents=True, exist_ok=True)
    for pr in list_open_prs(repo):
        head_sha = pr["headRefOid"]
        run = latest_validate_run(repo, pr["number"], head_sha)
        if run is None:
            continue
        if run.get("status") != "completed":
            continue
        score = download_score(repo, run["databaseId"], workdir / str(pr["number"]))
        if score is None:
            continue
        overall = score["overall"]
        out.append(Submission(
            pr_number=pr["number"],
            pr_title=pr.get("title", ""),
            pr_author=pr.get("author", {}).get("login", ""),
            pr_url=pr.get("url", ""),
            head_sha=head_sha,
            overall_passed=int(overall["passed"]),
            overall_failed=int(overall["failed"]),
            overall_skipped=int(overall["skipped"]),
            by_hw=score.get("by_hw", {}),
            last_run_iso=run.get("updatedAt", ""),
        ))
    return out


def hw_breadth(s: Submission) -> int:
    """How many HWs has the candidate scored at least one passing test on?
    Tie-breaker: a wider candidate ranks higher at equal totals."""
    return sum(
        1 for hw in HW_LABELS
        if s.by_hw.get(hw, {}).get("passed", 0) > 0
    )


def write_csv(submissions: list[Submission], path: Path) -> None:
    cols = ["rank", "pr", "title", "author", "passed_total"]
    cols += [f"{hw}_passed" for hw in HW_LABELS]
    cols += ["failed_total", "skipped_total", "head_sha", "last_run_iso", "pr_url"]
    lines = [",".join(cols)]
    for rank, s in enumerate(submissions, start=1):
        row = [
            str(rank),
            f"#{s.pr_number}",
            f"\"{s.pr_title.replace(chr(34), chr(39))}\"",
            s.pr_author,
            str(s.overall_passed),
        ]
        for hw in HW_LABELS:
            row.append(str(s.by_hw.get(hw, {}).get("passed", 0)))
        row += [
            str(s.overall_failed),
            str(s.overall_skipped),
            s.head_sha[:12],
            s.last_run_iso,
            s.pr_url,
        ]
        lines.append(",".join(row))
    path.write_text("\n".join(lines) + "\n")


def write_markdown(submissions: list[Submission], path: Path) -> None:
    lines: list[str] = []
    lines.append("# Aiming HW leaderboard")
    lines.append("")
    if not submissions:
        lines.append("_No PRs with completed validate_submission runs yet._")
        path.write_text("\n".join(lines) + "\n")
        return
    lines.append("Score = passing public tests on `ubuntu-latest`. ")
    lines.append("Ties broken by HW breadth (passing across more HWs > "
                 "passing more inside one HW).")
    lines.append("")
    header = ["rank", "PR", "title", "author", "total", *HW_LABELS]
    lines.append("| " + " | ".join(header) + " |")
    lines.append("|" + "|".join(["---:", *(["---"] * (len(header) - 1))]) + "|")
    for rank, s in enumerate(submissions, start=1):
        cells = [
            str(rank),
            f"[#{s.pr_number}]({s.pr_url})",
            s.pr_title.replace("|", "\\|"),
            s.pr_author,
            f"**{s.overall_passed}**",
        ]
        for hw in HW_LABELS:
            cells.append(str(s.by_hw.get(hw, {}).get("passed", 0)))
        lines.append("| " + " | ".join(cells) + " |")
    lines.append("")
    lines.append("Generated by `tools/leaderboard/aggregate.py`. ")
    lines.append("Anti-cheat posture: scores derived from CI re-runs on "
                 "each PR's `head.sha`, never from the PR description.")
    path.write_text("\n".join(lines) + "\n")


def write_json(submissions: list[Submission], path: Path) -> None:
    out = {
        "schema_version": 1,
        "entries": [
            {
                "rank": rank,
                "pr_number": s.pr_number,
                "pr_title": s.pr_title,
                "pr_author": s.pr_author,
                "pr_url": s.pr_url,
                "head_sha": s.head_sha,
                "overall_passed": s.overall_passed,
                "overall_failed": s.overall_failed,
                "overall_skipped": s.overall_skipped,
                "by_hw": s.by_hw,
                "last_run_iso": s.last_run_iso,
            }
            for rank, s in enumerate(submissions, start=1)
        ],
    }
    path.write_text(json.dumps(out, indent=2))


def main() -> int:
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--repo", required=True,
                        help="owner/name of the candidate-facing repo")
    parser.add_argument("--out-csv",  type=Path, required=True)
    parser.add_argument("--out-md",   type=Path, required=True)
    parser.add_argument("--out-json", type=Path, required=True)
    args = parser.parse_args()

    submissions = collect(args.repo)
    submissions.sort(
        key=lambda s: (-s.overall_passed, -hw_breadth(s), s.last_run_iso),
    )
    write_csv(submissions, args.out_csv)
    write_markdown(submissions, args.out_md)
    write_json(submissions, args.out_json)
    print(f"wrote {len(submissions)} entries → {args.out_csv}, "
          f"{args.out_md}, {args.out_json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Stop hook for Codex and Claude Code: do not end a turn with the repo dirty.

Both harnesses call this when the agent is about to finish a turn and accept
the same answer: {"decision": "block", "reason": ...} makes the agent keep
working with the reason as its next prompt. Both also send stop_hook_active,
so a turn is pushed back at most once and can never loop.

Why it exists (2026-10-07): Codex drove every number the freshness check
printed to zero, 38 runs in one session, and left everything the check did not
look at — journal entries duplicated by a merge, a 1.9 GB scratch directory, a
merged branch and worktree, round narration in state files. What is checked
gets done; this makes the full check run at the end of every turn in every
harness, instead of only when an agent remembers /wrap.

Only blocks when the working tree has changes: a turn that touched nothing has
nothing to tidy. Never fails the turn itself.
"""

import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CHECK = ROOT / "tools" / "Check-StateFreshness.py"


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except ValueError:
        payload = {}
    if payload.get("stop_hook_active"):
        return 0

    status = subprocess.run(["git", "status", "--porcelain"], cwd=str(ROOT),
                            capture_output=True, text=True, encoding="utf-8")
    if status.returncode != 0 or not status.stdout.strip():
        return 0

    done = subprocess.run([sys.executable, str(CHECK), "--strict"], cwd=str(ROOT),
                          capture_output=True, text=True, encoding="utf-8",
                          errors="replace", timeout=110)
    if done.returncode == 0:
        return 0

    lines = done.stdout.splitlines()
    findings = [ln for ln in lines if ln.startswith("[!]") or ln.startswith("必须清零")
                or ln.startswith("[STRICT]")]
    reason = ("收尾检查没过（python tools/Check-StateFreshness.py）。按 AGENTS.md「每轮收尾」"
              "处理完再结束；如果某项是用户还在进行中的工作、这轮不该动，就在回复里逐条说明。\n"
              + "\n".join(findings[:20]))
    # ASCII escapes: a Windows console pipe may not be UTF-8, and JSON's escaped form survives any code page.
    print(json.dumps({"decision": "block", "reason": reason}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

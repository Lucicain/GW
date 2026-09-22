"""PostToolUse hook: keep docs/state/code-map.md true after every C# edit.

A generated map that is only regenerated when someone remembers is just a
hand-written map with extra steps. This runs on the harness's schedule, not
the model's, so the map cannot silently fall behind the source.

Reads the hook payload on stdin, does nothing unless a module .cs file was
touched, and never fails the tool call.
"""

import json
import subprocess
import sys
from pathlib import Path

HOOK_DIR = Path(__file__).resolve().parent
PROJECT = HOOK_DIR.parent.parent
GENERATOR = PROJECT / "tools" / "Generate-CodeMap.py"


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except Exception:
        return 0

    path = (payload.get("tool_input") or {}).get("file_path") or ""
    norm = path.replace("\\", "/")
    if not norm.endswith(".cs") or "/GreyWardenPolicePurity/" not in norm:
        return 0
    # tools/*Tests/*.cs do not change the shipped module's shape
    if "/tools/" in norm:
        return 0

    try:
        done = subprocess.run(
            [sys.executable, str(GENERATOR)],
            cwd=str(PROJECT), capture_output=True, text=True, timeout=60)
    except Exception as exc:                      # never break the edit
        print(f"code-map refresh skipped: {exc}", file=sys.stderr)
        return 0

    if done.returncode != 0:
        print(f"code-map refresh failed: {done.stderr.strip()[:400]}",
              file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

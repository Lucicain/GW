"""Report which docs/state files may have fallen behind the code they describe.

code-map.md cannot rot — it is generated. The behaviour files are hand-written,
so they can. This compares each one's "最后验收" commit against the git history
of the source it claims to cover, and says which ones have source changes the
document has not been updated for.

It proves nothing about whether the prose is correct. It only says where to
look. Run from the repository root:  python tools/Check-StateFreshness.py

Exit code 0 always: this is a report, not a gate.
"""

import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
STATE = ROOT / "GreyWardenPolicePurity" / "docs" / "state"
MODULE = "GreyWardenPolicePurity"

ACCEPT_RE = re.compile(r"^>\s*最后验收：(.*)$", re.M)
COVER_RE = re.compile(r"^>\s*覆盖源码：(.*)$", re.M)
# "最后验收" is the last time a human confirmed the behaviour in game and must
# not move for a refactor. "已复核至" records a later commit whose changes were
# read and found not to affect this document, so the diff base can advance.
REVIEW_RE = re.compile(r"^>\s*已复核至：(.*)$", re.M)
HASH_RE = re.compile(r"`([0-9a-f]{7,40})`")
GLOB_RE = re.compile(r"`([^`]+)`")

SKIP_COVER = ("不做自动核对", "不适用")
SKIP_ACCEPT = ("不适用", "未建立检查点")


# A GBK console cannot encode the report; never let that crash the check.
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass


def git(*args: str) -> str:
    done = subprocess.run(["git", *args], cwd=str(ROOT),
                          capture_output=True, text=True)
    return done.stdout.strip() if done.returncode == 0 else ""


def resolve(glob: str) -> str:
    """Bare globs are relative to the module directory; explicit paths are not."""
    return glob if "/" in glob else f"{MODULE}/{glob}"


def main() -> int:
    files = sorted(p for p in STATE.glob("*.md") if p.name != "README.md")
    if not files:
        print("no state files found", file=sys.stderr)
        return 0

    head = git("rev-parse", "--short", "HEAD")
    print(f"HEAD = {head}\n")
    stale, skipped, fresh = [], [], []

    for p in files:
        text = p.read_text(encoding="utf-8")
        if "自动生成" in text[:400]:
            skipped.append((p.name, "生成文件，不会过期"))
            continue

        am, cm = ACCEPT_RE.search(text), COVER_RE.search(text)
        if not am or not cm:
            skipped.append((p.name, "缺 最后验收 或 覆盖源码 字段"))
            continue
        accept_raw, cover_raw = am.group(1), cm.group(1)

        if any(s in cover_raw for s in SKIP_COVER):
            skipped.append((p.name, "按设计不做自动核对"))
            continue
        if any(s in accept_raw for s in SKIP_ACCEPT):
            skipped.append((p.name, "无验收基线，无法比较"))
            continue

        rm = REVIEW_RE.search(text)
        base_raw = rm.group(1) if rm else accept_raw
        hm = HASH_RE.search(base_raw)
        if not hm:
            skipped.append((p.name, "最后验收/已复核至 里没有 commit hash"))
            continue
        base = hm.group(1)
        if not git("cat-file", "-t", base):
            skipped.append((p.name, f"commit {base} 不在本仓库"))
            continue

        paths = [resolve(g) for g in GLOB_RE.findall(cover_raw)]
        if not paths:
            skipped.append((p.name, "覆盖源码 里没有可解析的路径"))
            continue

        commits = git("log", "--oneline", f"{base}..HEAD", "--", *paths)
        dirty = git("status", "--porcelain", "--", *paths)
        if commits or dirty:
            n = len(commits.splitlines()) if commits else 0
            d = len(dirty.splitlines()) if dirty else 0
            stale.append((p.name, base, n, d, commits))
        else:
            fresh.append((p.name, base))

    if stale:
        print("[!] 可能已过期 —— 覆盖的源码在验收之后又改过：\n")
        for name, base, n, d, commits in stale:
            bits = []
            if n:
                bits.append(f"{n} 个提交")
            if d:
                bits.append(f"{d} 个未提交改动")
            print(f"  {name}  (验收于 {base}，之后 {'、'.join(bits)})")
            for line in commits.splitlines()[:4]:
                print(f"      {line}")
            if n > 4:
                print(f"      … 另有 {n - 4} 个")
        print()

    if fresh:
        print("[OK] 与验收基线一致：")
        for name, base in fresh:
            print(f"  {name}  ({base})")
        print()

    if skipped:
        print("— 未核对：")
        for name, why in skipped:
            print(f"  {name}  ({why})")
        print()

    print(f"{len(stale)} 个可能过期 / {len(fresh)} 个一致 / {len(skipped)} 个未核对")
    print("\n这只说明去哪里看，不说明文字对不对。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

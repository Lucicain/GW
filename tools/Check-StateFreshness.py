"""Report where docs/state has fallen behind the code, or started to rot.

code-map.md cannot rot — it is generated. The behaviour files are hand-written,
so they can, in five ways this script looks for:

1. Covered source changed and the document did not follow.
   Commits are rare here (only when the user declares a feature finished), so
   most drift lives in the working tree: a covered source file is modified but
   the state file is not. Committed drift is measured from the newest of the
   file's 已复核至 / 最后验收 commit and the last commit that touched the state
   file itself.
2. New source that no state file claims (orphans), against a baseline of the
   subsystems known to be unextracted.
3. Source in that baseline being edited — the moment AGENTS.md says to extract
   the subsystem, which is silent otherwise.
4. A state file turning back into a log: dated sections piling up, or length
   past the point where it is still "the current conclusion".

5. Hard rules ("不要…", "不得…") that do not say what evidence they rest on.
   When a stronger investigation method arrives, the rules resting on weaker
   ground are the ones to re-check — which is impossible if nobody wrote it down.

It also counts the files that declared themselves exempt, so the escape hatch
stays visible instead of quietly growing.

It proves nothing about whether the prose is correct. It only says where to
look. Run from the repository root:  python tools/Check-StateFreshness.py

Exit code 0 always: this is a report, not a gate.
"""

import re
import subprocess
import sys
from fnmatch import fnmatch
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MODULE = "GreyWardenPolicePurity"
STATE = ROOT / MODULE / "docs" / "state"
BASELINE = STATE / "uncovered-baseline.txt"

ACCEPT_RE = re.compile(r"^>\s*最后验收：(.*)$", re.M)
# "最后验收" is the last time a human confirmed the behaviour in game and must
# not move for a refactor. "已复核至" records a later commit whose changes were
# read and found not to affect this document.
REVIEW_RE = re.compile(r"^>\s*已复核至：(.*)$", re.M)
COVER_RE = re.compile(r"^>\s*覆盖源码：(.*)$", re.M)
HASH_RE = re.compile(r"`([0-9a-f]{7,40})`")
GLOB_RE = re.compile(r"`([^`]+)`")
HEADING_RE = re.compile(r"^#{2,4} .*$", re.M)
DATED_RE = re.compile(r"20\d\d-\d\d-\d\d|\b\d\d:\d\d\b")

EXEMPT = ("不做自动核对", "不适用")
# Described by the generated code-map (registration order), and edited by
# nearly every feature. Treating it as an unextracted subsystem is pure noise,
# and a checker that is always noisy gets ignored.
MAP_DESCRIBED = {f"{MODULE}/SubModule.cs"}
# Constraints on future work. 不要求 ("does not require") is not a rule.
HARD_RE = re.compile(r"不要(?!求)|不得|禁止|别再|推翻")
MAX_LINES = 250          # past this a state file is usually carrying history
MAX_DATED_HEADINGS = 2   # one "as of" date is fine; a run of them is a log

try:  # a GBK console cannot encode the report; never let that crash the check
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


def rel(p: Path) -> str:
    return str(p.relative_to(ROOT)).replace("\\", "/")


def newer(a: str, b: str) -> str:
    """Return whichever of two commits is later in history (b if unrelated)."""
    if not a:
        return b
    if not b:
        return a
    ok = subprocess.run(["git", "merge-base", "--is-ancestor", a, b],
                        cwd=str(ROOT)).returncode == 0
    return b if ok else a


def dirty(*paths: str) -> list:
    # Not via git(): porcelain lines start with a status column that may be a
    # space (" M path"), and stripping the whole output would eat it on the
    # first line and shift that filename by one character.
    done = subprocess.run(["git", "status", "--porcelain", "--", *paths],
                          cwd=str(ROOT), capture_output=True, text=True)
    if done.returncode != 0:
        return []
    return [ln[3:] for ln in done.stdout.splitlines() if len(ln) > 3]


def main() -> int:
    files = sorted(p for p in STATE.glob("*.md") if p.name != "README.md")
    print(f"HEAD = {git('rev-parse', '--short', 'HEAD')}\n")

    stale, fresh, exempt, unchecked, bloated = [], [], [], [], []
    unsourced = []   # hard rules with no 依据：tag
    hand_written = 0

    for p in files:
        text = p.read_text(encoding="utf-8")
        if "自动生成" in text[:400]:
            continue
        hand_written += 1

        # 5. hard rules that do not say what they rest on
        for n, line in enumerate(text.splitlines(), 1):
            if line.startswith(">") or line.startswith("|---"):
                continue
            if HARD_RE.search(line) and "依据：" not in line:
                unsourced.append((p.name, n, line.strip()[:70]))

        # 4. is it turning back into a log?
        lines = text.count("\n") + 1
        dated = [h for h in HEADING_RE.findall(text) if DATED_RE.search(h)]
        if lines > MAX_LINES or len(dated) > MAX_DATED_HEADINGS:
            bloated.append((p.name, lines, len(dated)))

        cm = COVER_RE.search(text)
        if not cm:
            unchecked.append((p.name, "缺 覆盖源码 字段"))
            continue
        if any(s in cm.group(1) for s in EXEMPT):
            exempt.append(p.name)
            continue
        paths = [resolve(g) for g in GLOB_RE.findall(cm.group(1))]
        if not paths:
            unchecked.append((p.name, "覆盖源码 里没有可解析的路径"))
            continue

        # 1a. working tree: source moved, document did not
        me = rel(p)
        src_dirty = dirty(*paths)
        doc_dirty = bool(dirty(me))

        # 1b. history: commits to covered source after the newest baseline
        declared = ""
        for rx in (REVIEW_RE, ACCEPT_RE):
            m = rx.search(text)
            h = HASH_RE.search(m.group(1)) if m else None
            if h and git("cat-file", "-t", h.group(1)):
                declared = h.group(1)
                break
        base = newer(declared, git("log", "-1", "--format=%h", "--", me))
        commits = git("log", "--oneline", f"{base}..HEAD", "--", *paths) if base else ""

        reasons = []
        if commits:
            reasons.append(f"{len(commits.splitlines())} 个提交在本文件之后改了源码")
        if src_dirty and not doc_dirty:
            reasons.append(f"{len(src_dirty)} 个源码文件有未提交改动，本文件没跟着改")
        if reasons:
            stale.append((p.name, base or "未入库", reasons, commits, src_dirty))
        else:
            note = "与源码一起在改" if src_dirty and doc_dirty else (base or "新文件")
            fresh.append((p.name, note))

    if stale:
        print("[!] 可能已过期：\n")
        for name, base, reasons, commits, src in stale:
            print(f"  {name}  (基准 {base})")
            for r in reasons:
                print(f"      - {r}")
            for line in commits.splitlines()[:3]:
                print(f"        {line}")
            if not commits:
                for s in src[:3]:
                    print(f"        {s}")
        print()

    if bloated:
        print("[!] state 文件在堆积流水 —— 应只写当前结论，过程移到 journal：\n")
        for name, lines, nd in bloated:
            bits = []
            if lines > MAX_LINES:
                bits.append(f"{lines} 行（>{MAX_LINES}）")
            if nd > MAX_DATED_HEADINGS:
                bits.append(f"{nd} 个带日期/时刻的小节")
            print(f"  {name}  {'，'.join(bits)}")
        print()

    if unsourced:
        print(f"[!] {len(unsourced)} 条硬规则没有标依据 —— 有了更强的调查方法时，分不清该复查哪条：")
        print()
        for name, n, line in unsourced[:12]:
            print(f"  {name}:{n}  {line}")
        if len(unsourced) > 12:
            print(f"  … 另有 {len(unsourced) - 12} 条")
        print()
        print("  依据写法见 docs/reference/investigation-methods.md 末节。")
        print()

    orphans_new, baseline_touched = check_baseline()

    if fresh:
        print("[OK] 一致：")
        for name, note in fresh:
            print(f"  {name}  ({note})")
        print()

    if exempt:
        print(f"— 声明不核对（{len(exempt)}/{hand_written} 个手写 state 文件）：")
        print("  " + "、".join(exempt))
        print("  这是出口，不是默认。专项调查应进 journal，不应作为 state 文件豁免核对。\n")
    if unchecked:
        print("— 无法核对：")
        for name, why in unchecked:
            print(f"  {name}  ({why})")
        print()

    print(f"{len(stale)} 个可能过期 / {len(bloated)} 个在堆积流水 / "
          f"{len(orphans_new)} 个新增未归属 / {len(baseline_touched)} 个未提取子系统文件被改动 / "
          f"{len(exempt)} 个声明不核对 / {len(unsourced)} 条硬规则缺依据")
    print("\n这只说明去哪里看，不说明文字对不对。")
    return 0


def check_baseline():
    """Orphans against the baseline, and edits landing inside the baseline."""
    covered = set()
    for p in STATE.glob("*.md"):
        if p.name == "README.md":
            continue
        cm = COVER_RE.search(p.read_text(encoding="utf-8"))
        if cm:
            covered.update(resolve(g) for g in GLOB_RE.findall(cm.group(1)))

    sources = sorted(rel(q) for q in (ROOT / MODULE).glob("*.cs"))
    orphans = [s for s in sources
               if s not in MAP_DESCRIBED and not any(fnmatch(s, pat) for pat in covered)]

    baseline = set()
    if BASELINE.exists():
        baseline = {ln.strip() for ln in BASELINE.read_text(encoding="utf-8").splitlines()
                    if ln.strip() and not ln.startswith("#")}

    new = [o for o in orphans if o not in baseline]
    gone = sorted(baseline - set(orphans))
    touched = sorted(set(dirty(*sorted(baseline))) & baseline) if baseline else []

    if new:
        print("[!] 新增未归属源码 —— 没有任何 state 文件声明覆盖它：\n")
        for o in new:
            print(f"  {o}")
        print("\n  新子系统就是这样开始漂移的。要么写一个 state 文件覆盖它，")
        print("  要么扩展某个已有文件的 `覆盖源码`。不要往基线里加。\n")
    if touched:
        print("[!] 改到了尚未提取的子系统文件 —— 按 AGENTS.md 应顺手提取成 state：\n")
        for t in touched:
            print(f"  {t}")
        print("\n  只是跟着别处改名/改调用的小改动可以不提取，但要在 journal 里说一句。\n")
    if gone:
        print(f"— 基线里有 {len(gone)} 个文件已被覆盖或删除，可以从基线移除：")
        for o in gone[:8]:
            print(f"  {o}")
        if len(gone) > 8:
            print(f"  … 另有 {len(gone) - 8} 个")
        print()
    return new, touched


if __name__ == "__main__":
    raise SystemExit(main())

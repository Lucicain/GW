"""Report where docs/state has fallen behind the code, or started to rot.

code-map.md cannot rot — it is generated. The behaviour files are hand-written,
so they can, in five ways this script looks for:

1. Covered source changed and the document did not follow.
   Check both work in progress and completed checkpoints. Working-tree drift
   means a covered source file is modified but
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

Three things it writes or checks because the next agent depends on them:

- It regenerates code-map.md first. Only Claude Code has the edit hook, so for
  Codex and Antigravity this is the refresh AGENTS.md promises.
- It regenerates the 待实测 roll-up in progress.md from every state file's
  header. That page is the first one a new agent reads and it is exempt from
  the drift check, so a hand-kept list fell behind within three days
  (2026-10-05/06: three commits awaiting play were missing from it).
- It reads the Claude Code memory directory for this repository, if present.
  Only Claude sees it; a project rule kept only there is invisible to Codex and
  Antigravity. Each project/feedback memory must point back with 仓库位置：.

It proves nothing about whether the prose is correct. It only says where to
look. Run from the repository root:  python tools/Check-StateFreshness.py

Also reported (2026-10-07, after a Codex round left them behind): known
defects rolled up into progress.md from 「已知缺陷：」 lines, state lines naming
scratch paths that no longer exist, duplicate journal entries from a branch
merge, leftover branches, worktrees and stashes, and scratch entries that
local-scratch.md does not claim. These are a tool for the agent, not an
audit (user ruling 2026-10-07).

Exit code 0 by default. With --strict (the pre-commit hook) it exits 1 only on
mechanical errors no one would keep on purpose: duplicate journal entries and
code hygiene.
"""

import re
from datetime import date
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
REVIEW_VALUE_RE = re.compile(r"`([0-9a-f]{7,40})`（(\d{4}-\d{2}-\d{2})）")
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
# A header field is one line saying where things stand. 已复核至 grew into a
# dated changelog in eight files (up to 1,287 characters) before this check.
HEADER_RE = re.compile(r"^>\s*(当前状态|最后验收|已复核至|待实测)：(.*)$", re.M)
MAX_HEADER_CHARS = 200

PROGRESS = STATE / "progress.md"
ROLLUP_BEGIN = "<!-- 待实测汇总：由 tools/Check-StateFreshness.py 从各 state 文件头部生成，手改会被覆盖 -->"
ROLLUP_END = "<!-- /待实测汇总 -->"
PENDING_SECTION_RE = re.compile(r"^## 待实测\s*\n(.*?)(?=^## |\Z)", re.M | re.S)
STATUS_RE = re.compile(r"^>\s*当前状态：(.*)$", re.M)
PENDING_RE = re.compile(r"^>\s*待实测：(.*)$", re.M)

DEFECT_BEGIN = "<!-- 已知缺陷汇总：由 tools/Check-StateFreshness.py 从各 state 的「已知缺陷：」行生成，手改会被覆盖 -->"
DEFECT_END = "<!-- /已知缺陷汇总 -->"
DEFECT_RE = re.compile(r"已知缺陷[*]*：\s*(.*)$")

JOURNAL = ROOT / MODULE / "docs" / "journal"
SCRATCH_LIST = ROOT / MODULE / "docs" / "reference" / "local-scratch.md"
SCRATCH_DIRS = (".codex_tmp", "build-check")
# A state line naming a scratch path that is gone: someone deleted the backup
# and left the rollback instruction pointing at nothing.
SCRATCH_REF_RE = re.compile(r"(?:\.codex_tmp|build-check)/[A-Za-z0-9_.-]*[A-Za-z0-9_.](?![-<A-Za-z0-9_.])")

MEMORY_DIR = (Path.home() / ".claude" / "projects"
              / re.sub(r"[:\\/]", "-", str(ROOT)) / "memory")
MEMORY_TYPE_RE = re.compile(r"^\s*type:\s*(\w+)", re.M)
MEMORY_POINTER_RE = re.compile(r"仓库位置：\s*`?([^`\s]+)`?")

try:  # a GBK console cannot encode the report; never let that crash the check
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass


def git(*args: str) -> str:
    done = subprocess.run(["git", *args], cwd=str(ROOT),
                          capture_output=True, text=True)
    return done.stdout.strip() if done.returncode == 0 else ""


def resolve(glob: str) -> str:
    """Bare globs and _Module/ paths live in the module directory; tools/ and
    other explicit paths are relative to the repository root.

    _Module/ used to resolve against the root, where nothing exists, so edits
    to items.xml, the language files and the GUI images never marked their
    state files stale (found 2026-10-06 by the missing-path check below)."""
    if "/" not in glob or glob.startswith("_Module/"):
        return f"{MODULE}/{glob}"
    return glob


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


def review_header_problem(text: str) -> str:
    """Advisory only; missing optional headers are allowed, changelog prose is not."""
    headers = REVIEW_RE.findall(text)
    if not headers:
        return ""
    if len(headers) != 1:
        return "已复核至只能有一行"
    match = REVIEW_VALUE_RE.fullmatch(headers[0].strip())
    if not match:
        return "应为 `提交号`（YYYY-MM-DD），改动说明放正文或 journal"
    try:
        date.fromisoformat(match[2])
    except ValueError:
        return "复核日期无效"
    return ""


def main() -> int:
    sys.stdout.flush()
    subprocess.run([sys.executable, str(ROOT / "tools" / "Generate-CodeMap.py")])
    files = sorted(p for p in STATE.glob("*.md") if p.name != "README.md")
    print(f"HEAD = {git('rev-parse', '--short', 'HEAD')}\n")
    rollup_problems = refresh_pending_rollup(files)
    refresh_defect_rollup(files)
    memory_unmirrored = check_memory()

    stale, fresh, exempt, unchecked, bloated = [], [], [], [], []
    unsourced = []   # hard rules with no 依据：tag
    dangling = []    # scratch paths named in state that no longer exist
    review_problems = []  # advisory, never a pre-commit gate
    hand_written = 0

    for p in files:
        text = p.read_text(encoding="utf-8")
        if "自动生成" in text[:400]:
            continue
        hand_written += 1

        problem = review_header_problem(text)
        if problem:
            review_problems.append((p.name, problem))
        elif match := REVIEW_RE.search(text):
            commit = HASH_RE.search(match[1])[1]
            if not git("rev-parse", "--verify", f"{commit}^{{commit}}"):
                review_problems.append((p.name, f"复核提交 {commit} 不存在"))

        # 5. hard rules that do not say what they rest on
        for n, line in enumerate(text.splitlines(), 1):
            if line.startswith(">") or line.startswith("|---"):
                continue
            if HARD_RE.search(line) and "依据：" not in line:
                unsourced.append((p.name, n, line.strip()[:70]))

        for n, line in enumerate(text.splitlines(), 1):
            for ref in SCRATCH_REF_RE.findall(line):
                if not (ROOT / ref).exists():
                    dangling.append((p.name, n, ref))


        # 4. is it turning back into a log?
        lines = text.count("\n") + 1
        dated = [h for h in HEADING_RE.findall(text) if DATED_RE.search(h)]
        long_fields = [f for f, v in HEADER_RE.findall(text) if len(v) > MAX_HEADER_CHARS]
        if lines > MAX_LINES or len(dated) > MAX_DATED_HEADINGS or long_fields:
            bloated.append((p.name, lines, len(dated), long_fields))

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
        # A covered file that was deleted keeps "covering" nothing, and the
        # prose about it stays behind (GwpLegacySave.cs, deleted in 1f7d49d).
        for g in paths:
            if not any(ROOT.glob(g)):
                unchecked.append((p.name, f"覆盖源码 `{g}` 已不存在——文件被删或改名，正文可能还在描述它"))

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
        for name, lines, nd, long_fields in bloated:
            bits = []
            if lines > MAX_LINES:
                bits.append(f"{lines} 行（>{MAX_LINES}）")
            if nd > MAX_DATED_HEADINGS:
                bits.append(f"{nd} 个带日期/时刻的小节")
            if long_fields:
                bits.append(f"头部 {'、'.join(long_fields)} 超过 {MAX_HEADER_CHARS} 字"
                            "（只留最新一次，经过写 journal）")
            print(f"  {name}  {'，'.join(bits)}")
        print()

    if review_problems:
        print("[!] 复核头部需整理（提示，不拦提交）：\n")
        for name, problem in review_problems:
            print(f"  {name}: {problem}")
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

    if dangling:
        print("[!] state 引用的临时文件已不存在 —— 改成提交号回退，或删掉这句：\n")
        for name, n, ref in dangling:
            print(f"  {name}:{n}  {ref}")
        print()

    orphans_new, baseline_touched = check_baseline()
    journal_dups = check_journal()
    leftovers = check_leftovers()
    scratch = check_scratch()

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

    findings = {
        "可能过期": stale, "堆积流水": bloated, "新增未归属": orphans_new,
        "未提取子系统被改": baseline_touched, "无法核对": unchecked,
        "硬规则缺依据": unsourced, "复核头部有误": review_problems, "待实测写法有误": rollup_problems,
        "引用的临时文件不存在": dangling, "journal 重复条目": journal_dups,
        "遗留分支/工作树/stash": leftovers, "临时目录无主项": scratch,
        "记忆只有 Claude 看得到": memory_unmirrored,
    }
    print(" / ".join(f"{k} {len(v)}" for k, v in findings.items()))
    print(f"（另有 {len(exempt)} 个 state 声明不核对，只计数）")
    print("\n这只说明去哪里看，不说明文字对不对。")

    print()
    sys.stdout.flush()
    hygiene = subprocess.run([sys.executable, str(ROOT / "tools" / "Check-CodeHygiene.py")])
    if "--strict" in sys.argv and (journal_dups or hygiene.returncode):
        print("\n[STRICT] journal 有重复条目或代码卫生未过，提交被拒。")
        return 1
    return 0


def replace_block(path: Path, begin: str, end: str, block: str, label: str) -> bool:
    text = path.read_text(encoding="utf-8")
    start, stop = text.find(begin), text.find(end)
    if start < 0 or stop < start:
        return False
    new = text[:start] + block + text[stop + len(end):]
    if new != text:
        path.write_text(new, encoding="utf-8")
        print(f"[更新] progress.md 的{label}已重新生成。\n")
    return True


def refresh_defect_rollup(files):
    """Every 「已知缺陷：」 line in a state body, listed in progress.md."""
    out = [DEFECT_BEGIN, ""]
    for p in files:
        if p == PROGRESS:
            continue
        for line in p.read_text(encoding="utf-8").splitlines():
            m = DEFECT_RE.search(line)
            if m and not line.startswith(">"):
                out.append(f"- [{p.name}]({p.name})：{m.group(1).strip()}")
    if len(out) == 2:
        out.append("- 无")
    out += ["", DEFECT_END]
    if not replace_block(PROGRESS, DEFECT_BEGIN, DEFECT_END, "\n".join(out), "已知缺陷汇总"):
        print("[!] progress.md 缺少已知缺陷汇总的起止标记，未生成\n")


def check_journal():
    """The same entry appended twice — a branch merge did this to ten entries."""
    seen, dups = {}, []
    for p in sorted(JOURNAL.glob("*.md")):
        for n, line in enumerate(p.read_text(encoding="utf-8").splitlines(), 1):
            if line.startswith("## "):
                key = line.strip()
                if key in seen:
                    dups.append(f"{p.name}:{n} 与 {seen[key]} 标题相同：{key[3:60]}")
                else:
                    seen[key] = f"{p.name}:{n}"
    if dups:
        print("[!] journal 有重复条目（合并分支时两边都追加过）—— 留信息全的那条，删另一条：\n")
        for d in dups[:12]:
            print(f"  {d}")
        if len(dups) > 12:
            print(f"  … 另有 {len(dups) - 12} 条")
        print()
    return dups


def check_leftovers():
    """Branches, worktrees and stashes nobody documents.

    A branch or worktree in active use is fine as long as a state or reference
    file names it; once merged and no longer named, it is debris."""
    docs = "".join(p.read_text(encoding="utf-8")
                   for d in (STATE, SCRATCH_LIST.parent) for p in d.glob("*.md"))
    found = []
    for b in git("branch", "--format=%(refname:short)").splitlines():
        if b and b != "main" and b not in docs:
            merged = b in git("branch", "--merged", "main", "--format=%(refname:short)").split()
            found.append(f"分支 {b}{'（已并入 main）' if merged else ''}")
    main_tree = str(ROOT).replace("\\", "/").lower()
    for line in git("worktree", "list", "--porcelain").splitlines():
        if line.startswith("worktree "):
            path = line[9:].strip()
            if path.replace("\\", "/").lower() != main_tree and path not in docs:
                found.append(f"工作树 {path}")
    for s in git("stash", "list").splitlines():
        found.append(f"stash {s[:70]}")
    if found:
        print("[!] 遗留的分支 / 工作树 / stash —— 合并完删掉；还在用就在 state 里写明它是什么：\n")
        for f in found:
            print(f"  {f}")
        print()
    return found


def check_scratch():
    """Top-level entries of the ignored scratch directories that local-scratch.md
    does not claim. Nothing there is in git; an unclaimed entry is either junk
    or the only copy of something, and only a listed one is safe to judge."""
    listed = GLOB_RE.findall(SCRATCH_LIST.read_text(encoding="utf-8")) if SCRATCH_LIST.exists() else []
    pats = [g.rstrip("/").split("/")[0] for g in listed]
    found = []
    for d in SCRATCH_DIRS:
        base = ROOT / d
        if not base.is_dir():
            continue
        for e in sorted(base.iterdir()):
            if not any(fnmatch(e.name, pat) for pat in pats):
                size = sum(f.stat().st_size for f in e.rglob("*") if f.is_file()) if e.is_dir() else e.stat().st_size
                found.append(f"{d}/{e.name}  {size / 1048576:.1f} MB")
    if found:
        print("[!] 临时目录里有 local-scratch.md 没认领的项 —— 结论写进 state/journal 后删掉，要留就登记：\n")
        for f in found[:20]:
            print(f"  {f}")
        if len(found) > 20:
            print(f"  … 另有 {len(found) - 20} 项")
        print()
    return found


def refresh_pending_rollup(files):
    """Rewrite the 待实测 block in progress.md from every state header."""
    entries, problems = [], []
    for p in files:
        if p == PROGRESS:
            continue
        text = p.read_text(encoding="utf-8")
        pm = PENDING_RE.search(text)
        if not pm:
            continue
        pending = pm.group(1).strip()
        if pending.startswith("无") or pending.startswith("<"):
            continue
        if re.match(r"见\s*`[^`]+\.md`", pending):
            continue   # points at another state file, which lists it itself
        sm = STATUS_RE.search(text)
        status = sm.group(1).strip() if sm else ""
        items = [pending]
        if "见文末" in pending:
            sec = PENDING_SECTION_RE.search(text)
            if not sec:
                problems.append(f"{p.name}：头部写「见文末」，但没有「## 待实测」一节")
                continue
            items = [ln.strip()[2:].strip() for ln in sec.group(1).splitlines()
                     if ln.strip().startswith("- ")]
        entries.append((p.name, status, items))

    out = [ROLLUP_BEGIN, ""]
    for name, status, items in entries:
        out.append(f"- [{name}]({name})" + (f"（{status}）" if status else ""))
        out.extend(f"  - {it}" for it in items)
    if not entries:
        out.append("- 无")
    out += ["", ROLLUP_END]
    block = "\n".join(out)

    if not replace_block(PROGRESS, ROLLUP_BEGIN, ROLLUP_END, block, "待实测汇总"):
        problems.append("progress.md 缺少待实测汇总的起止标记，未生成")

    if problems:
        print("[!] 待实测写法有误，汇总漏掉了它们：\n")
        for line in problems:
            print(f"  {line}")
        print()
    return problems


def check_memory():
    """Project rules kept only in Claude Code's private memory."""
    if not MEMORY_DIR.is_dir():
        return []
    missing = []
    for p in sorted(MEMORY_DIR.glob("*.md")):
        if p.name == "MEMORY.md":
            continue
        text = p.read_text(encoding="utf-8")
        tm = MEMORY_TYPE_RE.search(text)
        if not tm or tm.group(1) not in ("project", "feedback"):
            continue
        ptr = MEMORY_POINTER_RE.search(text)
        if not ptr:
            missing.append(f"{p.name}（没写 仓库位置：）")
        elif not (ROOT / ptr.group(1).split("#")[0]).exists():
            missing.append(f"{p.name}（仓库位置 {ptr.group(1)} 不存在）")
    if missing:
        print("[!] Claude 私有记忆里的项目规则没指回仓库 —— Codex / Antigravity 看不到：\n")
        for m in missing:
            print(f"  {m}")
        print(f"\n  目录：{MEMORY_DIR}")
        print("  把规则写进 AGENTS.md 或对应 state 文件，记忆里加一行 `仓库位置：<相对路径>`，")
        print("  或者干脆删掉这条记忆。\n")
    return missing


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

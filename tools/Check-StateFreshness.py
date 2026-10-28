"""Report where docs/state has fallen behind the code, or started to rot.

code-map.md cannot rot — it is generated. The behaviour files are hand-written,
so they can, in five ways this script looks for:

1. The document names a file, type or member that no longer exists.
   Until 2026-10-21 this was "covered source changed and the document did not
   follow", which made every source edit demand a document edit: the docs
   churn outgrew the code churn (+2034 doc lines against +1152 code lines in
   one batch). A document only has to change when its conclusion changes, and
   whether a rule still holds is asked by Check-ChangeImpact at the moment of
   the edit. What can be checked mechanically is a reference gone dead, using
   the CodeGraph index when it exists (user ruling 2026-10-21).
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
import subprocess
import sys
from fnmatch import fnmatch
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MODULE = "GreyWardenPolicePurity"
STATE = ROOT / MODULE / "docs" / "state"
BASELINE = STATE / "uncovered-baseline.txt"

# 已复核至 was the per-round "I re-read this against commit X" stamp that fed
# the old drift check; it went with that check (user ruling 2026-10-21).
RETIRED_HEADER_RE = re.compile(r"^>\s*已复核至：", re.M)
COVER_RE = re.compile(r"^>\s*覆盖源码：(.*)$", re.M)
GLOB_RE = re.compile(r"`([^`]+)`")
# References a reader would follow: a source file by name, or a mod type /
# member. Engine types are not checked; the index only knows this repository.
FILE_REF_RE = re.compile(r"^[\w./-]+\.(?:cs|py|ps1)$")
# A member is PascalCase; `GreyWardenPolicePurity.csproj` is a file, not a member.
SYMBOL_REF_RE = re.compile(r"^((?:Gwp|Police|GreyWarden)\w*)(?:\.([A-Z]\w*\*?))?(?:\(\))?$")
IDENT_RE = re.compile(r"\b(?:Gwp|Police|GreyWarden)\w*")
# Where a mod name can be defined: C# types, MSBuild properties and targets,
# XML ids, tool scripts. Docs are not a definition.
SOURCE_SUFFIXES = (".cs", ".csproj", ".props", ".targets", ".xml", ".ps1", ".py")
CODEGRAPH_DB = ROOT / ".codegraph" / "codegraph.db"
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
HEADER_RE = re.compile(r"^>\s*(当前状态|最后验收|待实测)：(.*)$", re.M)
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


class KnownCode:
    """What exists now: repository files, every mod identifier written in a
    source file, and (from the CodeGraph index, when present) which members
    each type has. Without the index a member only has to exist somewhere."""

    def __init__(self, files=None, idents=None, members=None):
        if files is None or idents is None:
            listed = git("ls-files", "-co", "--exclude-standard").splitlines()
            files = {f for f in listed} | {f.rsplit("/", 1)[-1] for f in listed}
            idents = set()
            for f in listed:
                if f.endswith(SOURCE_SUFFIXES) and (ROOT / f).is_file():
                    idents.update(IDENT_RE.findall((ROOT / f).read_text(encoding="utf-8", errors="replace")))
        self.files, self.idents = files, idents
        if members is None and CODEGRAPH_DB.exists():
            import sqlite3
            db = sqlite3.connect(f"file:{CODEGRAPH_DB.as_posix()}?mode=ro", uri=True)
            members = {}
            for (q,) in db.execute("select qualified_name from nodes where language = 'csharp'"):
                parts = q.split("::")
                for parent, child in zip(parts, parts[1:]):
                    members.setdefault(parent, set()).add(child)
            db.close()
        self.members = members  # {"GwpTuning": {"LastStand", ...}} or None

    def missing(self, token: str) -> bool:
        if FILE_REF_RE.match(token) and not any(c in token for c in "*?[{"):
            return token not in self.files and token.rsplit("/", 1)[-1] not in self.files
        m = SYMBOL_REF_RE.match(token)
        if not m:
            return False
        type_name, member = m[1], m[2]
        if type_name not in self.idents and f"{type_name}.cs" not in self.files:
            return True
        if not member or f"{type_name}.{member}.cs" in self.files:
            return False  # a partial-class file such as PolicePatrolBehavior.Helpers.cs
        if member.endswith("*"):
            prefix = member[:-1]
            pool = self.members.get(type_name, set()) if self.members is not None else self.idents_bare()
            return not any(name.startswith(prefix) for name in pool)
        if self.members is not None and type_name in self.members:
            return member not in self.members[type_name]
        return member not in self.idents_bare()

    def idents_bare(self) -> set:
        if not hasattr(self, "_bare"):
            text_names = set()
            for f in self.files:
                if f.endswith(".cs") and "/" in f and (ROOT / f).is_file():
                    text_names.update(re.findall(r"\b[A-Z]\w*", (ROOT / f).read_text(encoding="utf-8", errors="replace")))
            self._bare = text_names
        return self._bare


def dead_references(text: str, known: KnownCode) -> list:
    """[(line number, token)] for backticked references to code that is gone."""
    found = []
    for n, line in enumerate(text.splitlines(), 1):
        if line.startswith(">"):
            continue  # 覆盖源码 has its own check
        for token in GLOB_RE.findall(line):
            if known.missing(token.strip()):
                found.append((n, token.strip()))
    return found


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
    sys.stdout.flush()
    subprocess.run([sys.executable, str(ROOT / "tools" / "Generate-CodeMap.py")])
    files = sorted(p for p in STATE.glob("*.md") if p.name != "README.md")
    print(f"HEAD = {git('rev-parse', '--short', 'HEAD')}\n")
    rollup_problems = refresh_pending_rollup(files)
    refresh_defect_rollup(files)
    memory_unmirrored = check_memory()

    exempt, unchecked, bloated = [], [], []
    unsourced = []   # hard rules with no 依据：tag
    dangling = []    # scratch paths named in state that no longer exist
    dead = []        # files, types and members named in state that no longer exist
    retired = []     # state files still carrying the 已复核至 header
    hand_written = 0
    known = KnownCode()

    for p in files:
        text = p.read_text(encoding="utf-8")
        if "自动生成" in text[:400]:
            continue
        hand_written += 1

        if RETIRED_HEADER_RE.search(text):
            retired.append(p.name)
        dead.extend((p.name, n, token) for n, token in dead_references(text, known))

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

    if dead:
        print("[!] state 提到的文件或符号已不存在 —— 规则还成立就改成现在的名字，机制没了就删掉这句：\n")
        for name, n, token in dead[:30]:
            print(f"  {name}:{n}  `{token}`")
        if len(dead) > 30:
            print(f"  … 另有 {len(dead) - 30} 处")
        if known.members is None:
            print("  （没有 .codegraph/ 索引，只核对了文件名）")
        print()

    if retired:
        print("[!] `已复核至` 头部已取消（2026-10-21）—— 删掉这一行：\n")
        print("  " + "、".join(retired) + "\n")

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
        "引用失效": dead, "堆积流水": bloated, "新增未归属": orphans_new,
        "未提取子系统被改": baseline_touched, "无法核对": unchecked,
        "硬规则缺依据": unsourced, "已取消的复核头部": retired, "待实测写法有误": rollup_problems,
        "引用的临时文件不存在": dangling, "journal 重复条目": journal_dups,
        "遗留分支/工作树/stash": leftovers, "临时目录无主项": scratch,
        "记忆只有 Claude 看得到": memory_unmirrored,
    }
    print(" / ".join(f"{k} {len(v)}" for k, v in findings.items()))
    print(f"（另有 {len(exempt)} 个 state 声明不核对，只计数）")
    print(cleanup_ledger())
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


def cleanup_ledger() -> str:
    """Report the shared cleanup ledger in workflow.md: commits since the last
    cleanup and open cleanup items. A reminder only; it never fails a commit."""
    ledger = STATE / "workflow.md"
    text = ledger.read_text(encoding="utf-8-sig") if ledger.exists() else ""
    base = re.search(r"上次集中清理：`([0-9a-f]{7,40})`", text)
    if not base:
        return "— 集中清理：workflow.md 缺少「上次集中清理」台账行"
    items = []
    todo = re.search(r"清理待办：\s*\n(.*?)(?:\n#|\Z)", text, re.S)
    if todo:
        items = [line for line in todo.group(1).splitlines()
                 if line.startswith("- ") and line.strip() != "- （无）"]
    since = git("rev-list", "--count", base.group(1) + "..HEAD")
    if not since:
        return f"— 集中清理：台账基线 {base.group(1)} 不在历史里，清理时改成实际的清理提交"
    return (f"— 集中清理：上次在 {base.group(1)}，之后 {since} 个提交，清理待办 {len(items)} 项（见 workflow.md「清理台账」）\n"
            + rule_count(text))


def rule_count(ledger: str) -> str:
    """The entropy metric of workflow.md「清理台账」: rules carrying 依据 in the
    hand-written state files. It may only fall; a vanilla-manual page replaces
    the rules that existed because of that vanilla mechanism (user ruling
    2026-10-28). progress.md repeats other files' lines in its roll-ups."""
    count = sum(p.read_text(encoding="utf-8").count("依据")
                for p in STATE.glob("*.md")
                if p.name not in ("progress.md", "code-map.md"))
    base = re.search(r"熵指标：.*?基线 (\d+)", ledger)
    if not base:
        return f"— 规则总数 {count}（workflow.md 缺少熵指标基线）"
    limit = int(base.group(1))
    trend = "高于基线：集中清理时用原版手册或合并压回" if count > limit else "未超基线"
    return f"— 规则总数 {count}，基线 {limit}，{trend}"


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

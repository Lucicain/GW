"""List the subsystems an uncommitted change reaches, and the rules to re-check.

Many mechanisms in this mod lean on each other: a change to strength, case
state or duty occupation is felt by dispatch, envoys, rituals and the affairs
screen. The state file of the code you edited is the one you think to re-read;
the ones of the code that calls it are the ones that get missed. This script
closes that gap:

1. Read the changed lines of every .cs file (git diff against --base, default
   HEAD, plus untracked files).
2. Find the innermost CodeGraph symbol around each changed line.
3. Follow calls / references / instantiates / extends / implements edges
   backwards to the code that depends on those symbols (--depth hops).
4. Map both the edited files and the dependent files to state documents via
   their 覆盖源码 header, and print each document's hard rules.
5. Flag edits to a shared-concept owner (Check-CodeHygiene OWNERS): every user
   of that concept is affected.

It only says where to look. The graph misses Harmony bindings, reflection and
native calls; a subsystem not listed is not proof of no effect.
Run from the repository root:  python tools/Check-ChangeImpact.py
"""

import argparse
import importlib.util
import re
import sqlite3
import subprocess
import sys
from collections import defaultdict
from fnmatch import fnmatch
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DB = ROOT / ".codegraph" / "codegraph.db"
HUNK_RE = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@", re.M)
SYMBOL_KINDS = ("method", "property", "field", "constant", "function",
                "class", "struct", "enum", "interface", "enum_member")
TYPE_KINDS = ("class", "struct", "enum", "interface")
DEPENDENCY_EDGES =("calls", "references", "instantiates", "extends", "implements")
RULE_WIDTH = 160
OWNER_RE = re.compile(r"\b(Gwp\w+|Police\w+|GreyWarden\w+)(?:\.(\w+\*?))?")


def load(name: str):
    spec = importlib.util.spec_from_file_location(name.replace("-", "_"), ROOT / "tools" / f"{name}.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


freshness = load("Check-StateFreshness")
hygiene = load("Check-CodeHygiene")


def run(*args: str) -> str:
    done = subprocess.run(list(args), cwd=str(ROOT), capture_output=True,
                          text=True, encoding="utf-8", errors="replace")
    return done.stdout if done.returncode == 0 else ""


def parse_hunks(diff: str) -> dict:
    """{path: set of new-side line numbers} from `git diff -U0`; a pure
    deletion marks the line it left, so the enclosing symbol still counts."""
    out = defaultdict(set)
    current = None
    for line in diff.splitlines():
        if line.startswith("+++ "):
            current = None if line.endswith("/dev/null") else line[6:]
        elif current and (m := HUNK_RE.match(line)):
            start, count = int(m[1]), int(m[2] or 1)
            out[current].update(range(start, start + max(count, 1)))
    return out


def owner_hits(changed_qualified, owners) -> dict:
    """{(concept, use): {member names}} for edits inside a shared-concept owner.
    OWNERS names the owner as "Type" or "Type.Member*" in its last field; its
    file field lists files allowed to use the pattern, which are not owners."""
    hits = {}
    for concept, _, _, use in owners:
        for type_name, member in OWNER_RE.findall(use):
            pattern = f"*::{type_name}" + (f"::{member}" if member else "")
            touched = {q.split("::")[-1] for q in changed_qualified
                       if fnmatch(q, pattern) or fnmatch(q, pattern + "::*")}
            if touched:
                hits.setdefault((concept, use), set()).update(touched)
    return hits


def changed_lines(base: str) -> dict:
    out = parse_hunks(run("git", "diff", "-U0", "--no-color", base, "--", "*.cs"))
    for path in run("git", "ls-files", "--others", "--exclude-standard", "--", "*.cs").splitlines():
        out[path].add(0)  # a new file: every symbol in it counts as changed
    return out


def deleted_files(base: str) -> list:
    return run("git", "diff", "--name-only", "--diff-filter=D", base, "--", "*.cs").split()


def cover_map() -> dict:
    """{source path: [state document names]} from every 覆盖源码 header."""
    owners = defaultdict(list)
    for doc in sorted(freshness.STATE.glob("*.md")):
        text = doc.read_text(encoding="utf-8")
        cm = freshness.COVER_RE.search(text)
        if not cm or any(s in cm.group(1) for s in freshness.EXEMPT):
            continue
        for glob in freshness.GLOB_RE.findall(cm.group(1)):
            for path in ROOT.glob(freshness.resolve(glob)):
                owners[freshness.rel(path)].append(doc.name)
    return owners


def hard_rules(doc: str) -> list:
    rules = []
    for n, line in enumerate((freshness.STATE / doc).read_text(encoding="utf-8").splitlines(), 1):
        if line.startswith(">") or line.startswith("|---"):
            continue
        if freshness.HARD_RE.search(line):
            text = line.strip().lstrip("-* ").strip()
            rules.append((n, text if len(text) <= RULE_WIDTH else text[:RULE_WIDTH] + "…"))
    return rules


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--base", default="HEAD", help="compare the working tree with this commit")
    parser.add_argument("--depth", type=int, default=1, help="hops of dependents to follow")
    parser.add_argument("--no-rules", action="store_true", help="list documents without their rules")
    args = parser.parse_args()

    if not DB.exists():
        print("没有 .codegraph/ 索引：跳过影响分析（建索引：codegraph init --yes）。")
        return 0
    # The index must include the edits being analysed; cmd /c because codegraph is a .cmd shim.
    subprocess.run(["cmd", "/c", "codegraph", "sync"] if sys.platform == "win32" else ["codegraph", "sync"],
                   cwd=str(ROOT), capture_output=True)

    edits = changed_lines(args.base)
    if not edits:
        print(f"相对 {args.base} 没有 .cs 改动。")
        return 0

    db = sqlite3.connect(f"file:{DB.as_posix()}?mode=ro", uri=True)
    marks = ",".join("?" * len(SYMBOL_KINDS))
    changed = {}  # node id -> qualified name
    for path, lines in edits.items():
        nodes = db.execute(
            f"select id, qualified_name, start_line, end_line, kind from nodes "
            f"where file_path = ? and kind in ({marks})",
            (path, *SYMBOL_KINDS)).fetchall()
        for line in lines:
            hits = [n for n in nodes if line == 0 or n[2] <= line <= n[3]]
            if line != 0:
                hits = sorted(hits, key=lambda n: n[3] - n[2])[:1]  # innermost only
                # A line between members (comment, blank, removed member) is not a
                # change to the whole type: GwpTuning would drag in every user.
                hits = [n for n in hits if n[4] not in TYPE_KINDS or n[2] == line]
            for node_id, qualified, *_ in hits:
                changed[node_id] = qualified

    edges = ",".join("?" * len(DEPENDENCY_EDGES))
    reached = defaultdict(set)  # dependent file -> {"Caller → Changed"}
    frontier, seen = set(changed), set(changed)
    for _ in range(max(args.depth, 1)):
        if not frontier:
            break
        nxt = set()
        ids = list(frontier)
        for i in range(0, len(ids), 500):
            chunk = ids[i:i + 500]
            rows = db.execute(
                f"select s.id, s.name, s.file_path, t.name from edges e "
                f"join nodes s on s.id = e.source join nodes t on t.id = e.target "
                f"where e.kind in ({edges}) and e.target in ({','.join('?' * len(chunk))})",
                (*DEPENDENCY_EDGES, *chunk)).fetchall()
            for source_id, source_name, source_file, target_name in rows:
                if source_file not in edits:
                    reached[source_file].add(f"{source_name} → {target_name}")
                if source_id not in seen:
                    seen.add(source_id)
                    nxt.add(source_id)
        frontier = nxt

    covers = cover_map()
    docs_edited = defaultdict(set)
    docs_reached = defaultdict(set)
    for path in edits:
        for doc in covers.get(path, []):
            docs_edited[doc].add(path)
    for path, why in reached.items():
        for doc in covers.get(path, []):
            if doc not in docs_edited:
                docs_reached[doc].update(f"{Path(path).name}: {w}" for w in sorted(why)[:3])

    print(f"相对 {args.base}：{len(edits)} 个 .cs 文件改动，落在 {len(changed)} 个符号上；"
          f"依赖它们的代码分布在 {len(reached)} 个其他文件（深度 {args.depth}）。\n")

    owners = owner_hits(changed.values(), hygiene.OWNERS)
    if owners:
        print("[!] 改到了共享概念的归属方——这个概念的所有使用方都受影响：")
        for (concept, use), touched in owners.items():
            print(f"  {concept}（{use}）：{', '.join(sorted(touched)[:6])}")
        print()

    gone = deleted_files(args.base)
    if gone:
        print("[!] 删除的文件——索引里已没有它们的调用方，用 rg 查残留引用和 state 正文：")
        for path in gone:
            print(f"  {path}  （state：{', '.join(covers.get(path, [])) or '无覆盖'}）")
        print()

    def section(title, docs, detail):
        if not docs:
            return
        print(title)
        for doc in sorted(docs):
            print(f"  {doc} —— {detail(doc)}")
            if not args.no_rules:
                for n, rule in hard_rules(doc):
                    print(f"      L{n}: {rule}")
        print()

    section("== 直接改到的子系统：确认改后仍符合这些规则 ==", docs_edited,
            lambda d: "改了 " + ", ".join(sorted(Path(p).name for p in docs_edited[d])))
    section("== 被牵连的子系统：它们调用了你改的代码，复核这些规则是否仍成立 ==", docs_reached,
            lambda d: "; ".join(sorted(docs_reached[d])[:3]))

    orphans = sorted(p for p in reached if p not in covers and p.endswith(".cs"))
    if orphans:
        print("== 被牵连但没有 state 覆盖的文件：直接读调用处 ==")
        for path in orphans[:20]:
            print(f"  {path}: {'; '.join(sorted(reached[path])[:2])}")
        if len(orphans) > 20:
            print(f"  …另有 {len(orphans) - 20} 个")
        print()

    print("调用图不含 Harmony 绑定、反射和原生调用；没列出的子系统不等于不受影响。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

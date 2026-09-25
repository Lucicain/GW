"""Regenerate docs/state/code-map.md from the actual source.

A hand-written architecture note rots within days. This one is derived, so it
cannot disagree with the code: if it is wrong, the extractor is wrong.

Run from the repository root:  python tools/Generate-CodeMap.py
"""

import re
import sys
from pathlib import Path
from collections import defaultdict

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "GreyWardenPolicePurity"
OUT = SRC / "docs" / "state" / "code-map.md"

TYPE_RE = re.compile(
    r"^\s*(?:public|internal)\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*"
    r"(class|struct|enum|interface)\s+(\w+)", re.M)
BASE_RE = re.compile(r"^\s*(?:public|internal)\s+(?:\w+\s+)*"
                     r"(?:class|struct)\s+(\w+)\s*:\s*([^\r\n{]+)", re.M)
PATCH_RE = re.compile(
    r"\[HarmonyPatch\(\s*typeof\(([\w.]+)\)\s*,\s*"
    r"(?:nameof\([\w.]*?(\w+)\)|\"(\w+)\")"
    r"(?:\s*,\s*MethodType\.(\w+))?", re.S)
PATCH_CLASS_RE = re.compile(r"(?:public|internal)\s+static\s+class\s+(\w+)")
# Harmony accepts either an explicit attribute or a method simply named
# Prefix/Postfix/Transpiler/Finalizer. Both forms appear in this repo.
KIND_RE = re.compile(
    r"\[Harmony(Prefix|Postfix|Transpiler|Finalizer)\]"
    r"|\b(?:private|internal|public|static|\s)+\s"
    r"(?:\w[\w<>,.\[\]\?]*)\s+(Prefix|Postfix|Transpiler|Finalizer)\s*\(")
ADD_BEHAVIOR_RE = re.compile(r"AddMissionBehavior\(\s*new\s+(\w+)")
ADD_CAMPAIGN_RE = re.compile(r"AddBehavior\(\s*new\s+(\w+)")
ADD_MODEL_RE = re.compile(r"AddModel\(\s*new\s+(\w+)")

# file-name prefix -> subsystem bucket, first match wins
BUCKETS = [
    ("GwpMusic",        "音乐"),
    ("GwpSyndicate",    "音乐"),
    ("GwpBattleScene",  "战场援军"),
    ("GwpBattleSupport", "战场援军"),
    ("GwpBattleReinforcement", "战场援军"),
    ("GwpWardenResolve", "战场：死战不退"),
    ("GwpDualBlade",    "战场：双刀/近战"),
    ("GwpKick",         "战场：双刀/近战"),
    ("GwpShieldBash",   "战场：双刀/近战"),
    ("GwpAlternative",  "战场：双刀/近战"),
    ("GwpPassiveShield", "战场：双刀/近战"),
    ("GwpBattleCommand", "诊断"),
    ("GwpAiDiagnostics", "诊断"),
    ("GwpFault",        "诊断"),
    ("GwpRuntimeFault", "诊断"),
    ("GwpEngineAssert", "诊断"),
    ("GwpDispatchBarterFault", "诊断"),
    ("Police",          "执法：案件与巡逻"),
    ("Crime",           "执法：案件与巡逻"),
    ("GwpFieldArrest",  "执法：案件与巡逻"),
    ("PlayerBounty",    "执法：玩家悬赏"),
    ("GwpCaseArchive",  "执法：玩家悬赏"),
    ("GwpWardenDispatch", "使者与交兵"),
    ("GreyWardenTroopRequest", "使者与交兵"),
    ("GwpBribe",        "使者与交兵"),
    ("GreyWardenTraining", "练兵与切磋"),
    ("GreyWardenSparring", "练兵与切磋"),
    ("GreyWardenFieldSparring", "练兵与切磋"),
    ("GreyWardenFamily", "家族与村庄"),
    ("GreyWardenVillage", "家族与村庄"),
    ("GreyWardenMarriage", "家族与村庄"),
    ("GwpAgentApplyDamage", "战斗数值模型"),
    ("GwpAiDeterrence", "执法：案件与巡逻"),
    ("GreyWardenParty", "大地图欲望与 AI"),
    ("GwpArmyExit",     "大地图欲望与 AI"),
    ("PlayerBehaviorMonitor", "大地图欲望与 AI"),
    ("GreyWardenPlayerRequest", "玩家交互"),
    ("GwpData",         "数据与共用"),
    ("GwpCommon",       "数据与共用"),
    ("GwpIds",          "数据与共用"),
    ("GwpTuning",       "数据与共用"),
    ("SubModule",       "入口"),
]


def bucket(name: str) -> str:
    for prefix, label in BUCKETS:
        if name.startswith(prefix):
            return label
    return "其他"


def main() -> int:
    files = sorted(p for p in SRC.glob("*.cs"))
    if not files:
        print("no source files found", file=sys.stderr)
        return 1

    rows = []            # (bucket, filename, loc, [types], [bases])
    patches = []         # (vanilla_type, member, method_type, patch_class, kinds, file)
    total_loc = 0

    for path in files:
        text = path.read_text(encoding="utf-8-sig", errors="replace")
        loc = text.count("\n") + 1
        total_loc += loc
        types = [m.group(2) for m in TYPE_RE.finditer(text)]
        bases = {m.group(1): m.group(2).strip() for m in BASE_RE.finditer(text)}
        rows.append((bucket(path.stem), path.name, loc, types, bases))

        # Harmony patches: the attribute, then the class it decorates. Scope the
        # search for prefix/postfix markers to that class, i.e. up to the next
        # [HarmonyPatch( — a fixed character window misses long class bodies.
        starts = [m.start() for m in PATCH_RE.finditer(text)]
        for i, m in enumerate(PATCH_RE.finditer(text)):
            end = starts[i + 1] if i + 1 < len(starts) else len(text)
            body = text[m.end():end]
            cls = PATCH_CLASS_RE.search(body)
            kinds = sorted({g for pair in KIND_RE.findall(body) for g in pair if g})
            member = m.group(2) or m.group(3)
            patches.append((m.group(1), member, m.group(4) or "",
                            cls.group(1) if cls else "?",
                            "/".join(kinds) or "?", path.name))

    sub = (SRC / "SubModule.cs").read_text(encoding="utf-8-sig", errors="replace")
    mission_behaviors = ADD_BEHAVIOR_RE.findall(sub)
    campaign_behaviors = ADD_CAMPAIGN_RE.findall(sub)
    models = ADD_MODEL_RE.findall(sub)

    by_bucket = defaultdict(list)
    for b, name, loc, types, bases in rows:
        by_bucket[b].append((name, loc, types, bases))

    order = []
    for _, label in BUCKETS:
        if label in by_bucket and label not in order:
            order.append(label)
    for label in sorted(by_bucket):
        if label not in order:
            order.append(label)

    L = []
    w = L.append
    w("# 代码地图（自动生成）")
    w("")
    w("> **这个文件由 `tools/Generate-CodeMap.py` 生成，不要手改。**")
    w("> 源码改了就重新生成：`python tools/Generate-CodeMap.py`")
    w("> 它描述代码**的形状**，不描述行为——行为看同目录其他 state 文件。")
    w(">")
    w("> **读法：前两节（补丁点、注册顺序）通读；「类型索引」「文件索引」是查表用的，")
    w("> 用 grep 查某个类型或文件，不要整份读进上下文。**")
    w("")
    patch_classes = {p[3] for p in patches if p[3] != "?"}
    w(f"{len(files)} 个源文件、{total_loc:,} 行、"
      f"{sum(len(r[3]) for r in rows)} 个公开类型、"
      f"{len(patches)} 个 Harmony 补丁点（分布在 {len(patch_classes)} 个补丁类里）。")
    w("")
    w("## 原版接触面（Harmony 补丁）")
    w("")
    w("**这张表是这个模组与 Bannerlord 的全部契约。** 换游戏版本时先核对它。")
    w("")
    w("这里数的是源码里 `[HarmonyPatch(typeof(...))]` 的**补丁点**。")
    w("`Verify-GameCompat.ps1` 的 `PATCH_OK` 数的是**成功绑定的补丁类**，口径不同，")
    w("两个数字不应直接相减。")
    w("")
    w("| 原版类型 | 成员 | 方式 | 补丁类 | 文件 |")
    w("|---|---|---|---|---|")
    for v, member, mtype, cls, kinds, f in sorted(patches):
        mt = f" ({mtype})" if mtype else ""
        w(f"| `{v}` | `{member}`{mt} | {kinds} | `{cls}` | {f} |")
    w("")
    w("## 注册入口（SubModule.cs）")
    w("")
    if campaign_behaviors:
        w("**战役行为**，按注册顺序：")
        w("")
        for n in campaign_behaviors:
            w(f"- `{n}`")
        w("")
    if models:
        w("**模型覆盖**：")
        w("")
        for n in models:
            w(f"- `{n}`")
        w("")
    if mission_behaviors:
        w("**任务行为**，按注册顺序（顺序影响生效时机）：")
        w("")
        for n in mission_behaviors:
            w(f"- `{n}`")
        w("")
    w("## 类型索引")
    w("")
    w("**文件名不预测内容。** `CrimePool` 在 `GwpData.cs`、`GwpFaultTrace` 在")
    w("`GwpDualBladeActionSetPatch.cs`、`GwpPassiveShieldBreakBehavior` 在")
    w("`GwpShieldBashGuardPatch.cs`。要找一个类型就查这张表，不要照名字猜文件。")
    w("")
    where = {}
    for _, name, _, types, _ in rows:
        for t in types:
            where.setdefault(t, []).append(name)
    surprising = 0
    for t in sorted(where, key=str.lower):
        homes = where[t]
        odd = all(t not in Path(h).stem for h in homes)
        surprising += odd
        mark = " ←" if odd else ""
        w(f"- `{t}` → {' , '.join(homes)}{mark}")
    w("")
    w(f"`←` 标出文件名里找不到该类型名的 {surprising} 个，它们靠 Glob 找不到。")
    w("")
    w("## 文件索引")
    w("")
    for label in order:
        entries = sorted(by_bucket[label], key=lambda e: -e[1])
        w(f"### {label}（{len(entries)} 个文件）")
        w("")
        for name, loc, types, bases in entries:
            base = ""
            for t in types:
                if t in bases:
                    first = bases[t].split(",")[0].strip()
                    base = f" : {first}"
                    break
            extra = f"，另含 {len(types) - 1} 个类型" if len(types) > 1 else ""
            head = types[0] if types else "—"
            w(f"- `{name}` — {loc} 行 · `{head}`{base}{extra}")
        w("")

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(L).rstrip() + "\n", encoding="utf-8")
    print(f"{OUT.relative_to(ROOT)}: {len(L)} lines, {OUT.stat().st_size:,} bytes, "
          f"{len(patches)} patches, {len(files)} files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

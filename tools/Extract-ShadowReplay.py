#!/usr/bin/env python3
"""把诊断日志里影子运行记下的「局面 >> 旧代码的决定」提取成逻辑层的回放用例。

分层迁移时，旧代码照常执行，诊断版把同一局面也交给逻辑层算一遍并记一行（见
docs/state/code-health.md「分层架构」）。这里去重后写成 `局面 | 决定`，放进
GreyWardenPolicePurity.Core.Tests/Replay/<子系统>/，单元测试逐行回放。

    python tools/Extract-ShadowReplay.py CITY_TEAM_SHADOW CitySupport
"""
import argparse
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LOG = Path(os.path.expanduser("~")) / "Documents" / "Mount and Blade II Bannerlord" / "GreyWarden-AI-Diagnostics.log"


def extract(log: Path, stage: str) -> tuple[list[str], list[str]]:
    """返回（去重后的回放行，影子不一致的原始行）。"""
    cases, mismatches = [], []
    seen = set()
    for raw in log.read_text(encoding="utf-8", errors="replace").splitlines():
        if f"stage={stage}_MISMATCH" in raw:
            mismatches.append(raw)
            continue
        if f"stage={stage} " not in raw and not raw.rstrip().endswith(f"stage={stage}"):
            continue
        details = raw.split(" | ")[-1]
        parts = details.split(" >> ")
        if len(parts) < 2:
            continue
        case = parts[0].strip() + " | " + parts[1].strip()
        if case not in seen:
            seen.add(case)
            cases.append(case)
    return cases, mismatches


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("stage", help="影子运行的日志 stage，例如 CITY_TEAM_SHADOW")
    parser.add_argument("subsystem", help="回放目录名，例如 CitySupport")
    parser.add_argument("--log", type=Path, default=LOG)
    parser.add_argument("--name", default="recorded", help="输出文件名（不含扩展名）")
    args = parser.parse_args()
    logs = [p for p in (args.log, Path(str(args.log) + ".previous")) if p.exists()]
    cases, mismatches = [], []
    for log in logs:
        c, m = extract(log, args.stage)
        cases += [x for x in c if x not in cases]
        mismatches += m
    out_dir = ROOT / "GreyWardenPolicePurity.Core.Tests" / "Replay" / args.subsystem
    if cases:
        out_dir.mkdir(parents=True, exist_ok=True)
        out = out_dir / f"{args.name}.log"
        existing = out.read_text(encoding="utf-8").splitlines() if out.exists() else []
        merged = existing + [c for c in cases if c not in existing]
        out.write_text("\n".join(merged) + "\n", encoding="utf-8")
        print(f"REPLAY: {len(merged)} cases ({len(merged) - len(existing)} new) -> {out.relative_to(ROOT)}")
    else:
        print("REPLAY: no shadow lines found")
    print(f"MISMATCH: {len(mismatches)}")
    for line in mismatches[:20]:
        print("  " + line)
    return 1 if mismatches else 0


if __name__ == "__main__":
    raise SystemExit(main())

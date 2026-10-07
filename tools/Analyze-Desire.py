"""灰袍领主为什么想去哪儿：汇总 AI 日志里的 DESIRE 行（见 docs/state/warden-ai.md「进聚落的意愿」）。

  python tools/Analyze-Desire.py           # 最近一次启动游戏以来
  python tools/Analyze-Desire.py --all     # 全部日志

每位领主：各种结果（去办差、进城、原地待命）各几小时；原地待命是在哪儿、因为谁赢；
赢下来的进城是什么原因（原版因子里偏离 1 的那几项 + 灰袍改了什么）；原版分和照原版重算的分对不上的次数。
只读日志，不改任何东西。
"""
import argparse
import collections
import datetime as dt
import glob
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
DOCS = os.path.join(os.path.expanduser("~"), "Documents", "Mount and Blade II Bannerlord")
LINE = re.compile(r"^(\S+) \| campaignHour=([\d.]+) \| DESIRE \| .*?party=(\S+) .*?\| outcome=(\w+) \| winner=(\S+) \| "
                  r"intent=(.*?) duty=([\d.]+) \| here=(\S*) \| party=(\S+) \| visits=(.*)$")
VISIT = re.compile(r"(\S+?)\{vanilla=([\d.]+) (ok|MISMATCH\(mirror=[\d.]+\)) \[(.*?)\] gwp=(\S+) final=([\d.]+)\}")
FACTOR = re.compile(r"([a-zA-Z]+)([\d.]+)")


def last_launch():
    stamp = None
    with open(os.path.join(DOCS, "GreyWarden-Faults.log"), encoding="utf-8", errors="replace") as handle:
        for line in handle:
            if "| FAULT_WATCH_ARMED |" in line:
                stamp = line.split(" | ", 1)[0]
    return dt.datetime.fromisoformat(stamp[:26]) if stamp else None


def read(all_logs):
    files = sorted(glob.glob(os.path.join(DOCS, "GreyWarden-Diagnostics-Archive", "GreyWarden-AI-Diagnostics.log.*")))
    files.append(os.path.join(DOCS, "GreyWarden-AI-Diagnostics.log"))
    since = None if all_logs else last_launch()
    rows = []
    for path in files:
        if not os.path.exists(path):
            continue
        if since and dt.datetime.fromtimestamp(os.path.getmtime(path)) < since:
            continue
        with open(path, encoding="utf-8", errors="replace") as handle:
            for line in handle:
                if "| DESIRE |" not in line:
                    continue
                match = LINE.match(line.rstrip("\n"))
                if not match or (since and dt.datetime.fromisoformat(match.group(1)[:26]) < since):
                    continue
                rows.append(match.groups())
    return rows


def reasons(factors):
    """原版因子里偏离 1 的那几项（距离、底分不算）。"""
    out = []
    for name, value in FACTOR.findall(factors.split("(")[0] + " " + " ".join(factors.split(")")[1:])):
        if name in ("base", "dist", "d", "vol", "allied") or name.endswith("Break"):
            continue
        try:
            number = float(value)
        except ValueError:
            continue
        if abs(number - 1.0) > 0.01:
            out.append(f"{name}{number:g}")
    return out


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--all", action="store_true")
    rows = read(parser.parse_args().all)
    if not rows:
        print("没有 DESIRE 行。")
        return
    by_party = collections.defaultdict(list)
    for row in rows:
        by_party[row[2]].append(row)
    mismatches = 0
    for party, items in sorted(by_party.items()):
        outcomes = collections.Counter(item[3] for item in items)
        print(f"\n== {party}：{len(items)} 小时  " + "，".join(f"{k} {v}" for k, v in outcomes.most_common()))
        holds = collections.Counter(item[7] + " 因为 " + item[4] for item in items if item[3] == "NO_OP_HOLD_AT_CURRENT")
        for text, count in holds.most_common(3):
            print(f"   原地待命 {count} 小时：{text}")
        why = collections.Counter()
        for item in items:
            visits = VISIT.findall(item[9])
            mismatches += sum(1 for visit in visits if visit[2] != "ok")
            if item[3] == "VISIT" and visits:
                top = visits[0]
                why[top[0] + " ← " + " ".join(reasons(top[3]) or ["底分"]) + " | gwp " + top[4]] += 1
        for text, count in why.most_common(5):
            print(f"   进城 {count} 次：{text}")
    print(f"\n原版分和重算分对不上：{mismatches} 处" + ("（重算公式和原版有出入，要查）" if mismatches else ""))


if __name__ == "__main__":
    main()

"""火誓者 / 信众村经济的平衡与配合报告。

读 Documents 下的 GreyWarden-AI-Diagnostics.log（含归档），只取最近一次启动游戏以来的行，
汇总 `| FIRESWORN |` 监控行（见 docs/state/firesworn-economy.md「监控」）：

  python tools/Analyze-Firesworn.py              # 最近一次启动以来
  python tools/Analyze-Firesworn.py --all        # 全部日志

输出：总览、每天的经济、每座信众村、每位灰袍领主、常驻队、异常（ANOMALY）汇总，
最后是按阈值主动找出的平衡问题。只读日志，不改任何东西。
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
LINE = re.compile(r"^(\S+) \| campaignHour=([\d.]+) \| FIRESWORN \| stage=(\w+) \| (.*)$")


def fields(text):
    out = {}
    for part in text.split("; "):
        if "=" in part:
            key, value = part.split("=", 1)
            out[key.strip()] = value.strip()
    return out


def num(value, default=0.0):
    try:
        return float(str(value).split("/")[0])
    except (TypeError, ValueError):
        return default


def last_launch():
    path = os.path.join(DOCS, "GreyWarden-Faults.log")
    stamp = None
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            if "| FAULT_WATCH_ARMED |" in line:
                stamp = line.split(" | ", 1)[0]
    return dt.datetime.fromisoformat(stamp[:26]) if stamp else None


def read_events(all_logs):
    files = sorted(glob.glob(os.path.join(DOCS, "GreyWarden-Diagnostics-Archive", "GreyWarden-AI-Diagnostics.log.*")))
    files.append(os.path.join(DOCS, "GreyWarden-AI-Diagnostics.log"))
    since = None if all_logs else last_launch()
    events = []
    for path in files:
        if not os.path.exists(path):
            continue
        if since and dt.datetime.fromtimestamp(os.path.getmtime(path)) < since.replace(tzinfo=None):
            continue
        with open(path, encoding="utf-8", errors="replace") as handle:
            for line in handle:
                match = LINE.match(line.rstrip("\n"))
                if not match:
                    continue
                when = dt.datetime.fromisoformat(match.group(1)[:26])
                if since and when < since.replace(tzinfo=None):
                    continue
                events.append((float(match.group(2)), match.group(3), fields(match.group(4))))
    events.sort(key=lambda event: event[0])
    return events


def day(hour):
    return int(hour // 24)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--all", action="store_true", help="read every log, not only the latest game launch")
    args = parser.parse_args()
    events = read_events(args.all)
    if not events:
        print("没有 FIRESWORN 监控行。")
        return
    by_stage = collections.defaultdict(list)
    for hour, stage, data in events:
        by_stage[stage].append((hour, data))
    first_day, last_day = day(events[0][0]), day(events[-1][0])
    days = max(1, last_day - first_day)
    issues = []

    # ------------------------------------------------------------ 总览
    totals = by_stage["DAILY_TOTALS"]
    print(f"== 总览：游戏内第 {first_day} 天到第 {last_day} 天，共 {days} 天，{len(events)} 行监控")
    if totals:
        a, b = totals[0][1], totals[-1][1]
        for key in ("faithVillages", "zealots", "priests", "dungeon", "treasury"):
            print(f"   {key}: {a.get(key)} -> {b.get(key)}")
        treasury_rate = (num(b["treasury"]) - num(a["treasury"])) / max(1, day(totals[-1][0]) - day(totals[0][0]))
        print(f"   公库每天净变化约 {treasury_rate:+.0f}")
        if treasury_rate < 0:
            issues.append(f"公库在减少（每天 {treasury_rate:+.0f}）")

    # ------------------------------------------------------------ 经济
    income = collections.Counter()
    for hour, data in by_stage["PROTECTION_FEE"]:
        income["保护费"] += num(data.get("amount"))
    for hour, data in by_stage["CARAVAN_HOME"]:
        income["狂信者队卖货"] += num(data.get("proceedsToTreasury"))
    for hour, data in by_stage["DEFENCE_SPOILS"]:
        income["守村金币"] += num(data.get("goldToTreasury"))
    spend = sum(num(data.get("cost")) for _, data in by_stage["DAMNATION"])
    print("\n== 收支（这段时间合计）")
    for key, value in income.items():
        print(f"   {key}: {value:.0f}（每天 {value / days:.0f}）")
    print(f"   烈火责罚支出: {spend:.0f}")
    cargo = sum(num(data.get("cargoValue")) for _, data in by_stage["CARAVAN_DISPATCHED"])
    sold = sum(num(data.get("proceeds")) for _, data in by_stage["CARAVAN_SOLD"])
    if cargo:
        print(f"   狂信者队装车估值 {cargo:.0f}，实卖 {sold:.0f}（{sold / cargo:.0%}）")
        if sold / cargo < 0.2:
            issues.append(f"狂信者队只卖掉装车估值的 {sold / cargo:.0%}：城镇金库太少或货太贵，货仓会越堆越多")
    low_gold = [data for _, data in by_stage["CARAVAN_SOLD"] if num(data.get("townGoldLeft")) < 1000]
    if low_gold:
        issues.append(f"{len(low_gold)} 次卖货把城镇金库卖空了：{', '.join(sorted({d['town'] for d in low_gold}))}")

    # ------------------------------------------------------------ 信众村
    print("\n== 信众村（首日 -> 末日；增长来源）")
    villages = collections.defaultdict(list)
    for hour, data in by_stage["DAILY_VILLAGE"]:
        villages[data["village"]].append((hour, data))
    gains = collections.defaultdict(collections.Counter)
    for _, data in by_stage["CONGREGATION_COMPLETED"]:
        gains[data["village"]]["传教"] += num(data.get("gain"))
    for _, data in by_stage["DAMNATION"]:
        before, after = data["zealots"].split("->")
        gains[data["village"]]["烧人(" + data["mode"] + ")"] += num(after) - num(before)
    for _, data in by_stage["ZEALOTS_ENLISTED"]:
        hearth_before, hearth_after = data["hearth"].split("->")
        gains[data["village"]]["招走扣户"] += num(hearth_after) - num(hearth_before)
    for _, data in by_stage["ZEALOT_GROWTH"]:
        gains[data["village"]]["祭司自然增长"] += num(data.get("gain"))
    for _, data in by_stage["ARMORY_LOOT"]:
        gains[data["holder"]]["缴获件数"] += num(data.get("items"))
    for village, rows in sorted(villages.items()):
        a, b = rows[0][1], rows[-1][1]
        span = max(1, day(rows[-1][0]) - day(rows[0][0]))
        hearth_change = num(b["hearth"]) - num(a["hearth"])
        zealot_change = num(b["zealots"]) - num(a["zealots"])
        print(f"   {village}: 户 {a['hearth']}->{b['hearth']}  狂信者 {a['zealots']}->{b['zealots']}"
              f"  祭司 {b['priest'] != '-'}  地牢 {b['dungeon']}  军械库 {b['armoryItems']} 件 武装 {b['armed']}"
              f"  货仓 {b['goodsValue']}  常驻队 {b['parties']}")
        if gains[village]:
            print("      来源: " + "，".join(f"{k} {v:+.0f}" for k, v in gains[village].items()))
        if hearth_change / span < -3:
            issues.append(f"{village} 户数每天掉 {hearth_change / span:.1f}，招兵扣户太快")
        if span >= 3 and zealot_change <= 0:
            issues.append(f"{village} 狂信者 {span} 天没涨（{a['zealots']}->{b['zealots']}）")
        armed, total = (b["armed"].split("/") + ["0"])[:2]
        if num(total) >= 20 and num(armed) / num(total) < 0.25:
            issues.append(f"{village} 只有 {b['armed']} 的狂信者拿到兵器")

    # ------------------------------------------------------------ 领主
    print("\n== 灰袍领主")
    lords = collections.defaultdict(list)
    for hour, data in by_stage["DAILY_LORD"]:
        lords[data["party"]].append((hour, data))
    errands = collections.defaultdict(lambda: collections.Counter())
    errand_hours = collections.defaultdict(float)
    for _, data in by_stage["ERRAND_STARTED"]:
        errands[data["party"]]["开始:" + data["reason"]] += 1
    for _, data in by_stage["ERRAND_DONE"]:
        errands[data["party"]]["办完"] += 1
        errand_hours[data["party"]] += num(data.get("hours"))
    for _, data in by_stage["ERRAND_CANCELLED"]:
        errands[data["party"]]["取消:" + data["reason"]] += 1
        errand_hours[data["party"]] += num(data.get("hours"))
    recruits = collections.defaultdict(collections.Counter)
    for _, data in by_stage["PRIEST_RECRUITED"]:
        recruits[data["party"]]["祭司:" + data["troop"]] += num(data.get("count"))
    for _, data in by_stage["ZEALOTS_ENLISTED"]:
        recruits[data["party"]]["狂信者"] += num(data.get("enlisted"))
    for _, data in by_stage["ARMORY_LOOT"]:
        recruits[data["holder"]]["缴获尸体"] += num(data.get("bodies"))
        recruits[data["holder"]]["缴获件数"] += num(data.get("items"))
    for _, data in by_stage["HANDOVER"]:
        recruits[data["party"]]["交俘虏"] += num(data.get("prisoners"))
    for party, rows in sorted(lords.items()):
        a, b = rows[0][1], rows[-1][1]
        minimum = min(num(r["members"]) / max(1, num(r["members"].split("/")[1])) for _, r in rows)
        print(f"   {party}: 兵 {a['members']}->{b['members']}（最低 {minimum:.0%}） 俘虏 {b['prisoners']}"
              f"  军械库 {b['armoryItems']} 武装 {b['armed']}  背包 {b['inventoryValue']}  钱 {b.get('partyGold', '?')}")
        print(f"      兵种 {b['troops']}")
        if errands[party]:
            print("      差事: " + "，".join(f"{k}×{v}" for k, v in errands[party].items()) +
                  f"；差事累计 {errand_hours[party]:.0f} 小时（{errand_hours[party] / (days * 24):.0%} 的时间）")
        if recruits[party]:
            print("      兵源与俘虏: " + "，".join(f"{k} {v:.0f}" for k, v in recruits[party].items()))
        cancels = sum(v for k, v in errands[party].items() if k.startswith("取消"))
        starts = sum(v for k, v in errands[party].items() if k.startswith("开始"))
        if starts >= 3 and cancels / starts > 0.5:
            issues.append(f"{party} 差事 {starts} 次里取消了 {cancels} 次，看取消原因")
        if minimum < 0.6:
            issues.append(f"{party} 兵力最低到过 {minimum:.0%}")
        if errand_hours[party] / (days * 24) > 0.35:
            issues.append(f"{party} {errand_hours[party] / (days * 24):.0%} 的时间在跑信众村差事，可能挤掉了办案")

    # ------------------------------------------------------------ 常驻队
    print("\n== 常驻队")
    for stage in ("DISCIPLES_DISPATCHED", "CONGREGATION_COMPLETED", "CONGREGATION_CANCELLED", "DISCIPLES_HOME",
                  "DISCIPLES_TURNED_BACK", "DISCIPLES_LOST", "CARAVAN_DISPATCHED", "CARAVAN_SOLD", "CARAVAN_HOME",
                  "CARAVAN_TURNED_BACK", "CARAVAN_LOST", "PRIEST_SPAWNED", "RITUAL_MENU_SHOWN", "RITUAL_POPUP_NEARBY"):
        print(f"   {stage}: {len(by_stage[stage])}")
    bad_sound = [data for _, data in by_stage["RITUAL_MENU_SHOWN"] if num(data.get("soundId"), 0) < 0]
    if bad_sound:
        issues.append(f"仪式配音没找到声音事件 {len(bad_sound)} 次（soundId < 0）")
    loot = by_stage["ARMORY_LOOT"]
    if loot:
        bodies = sum(num(d.get("bodies")) for _, d in loot)
        items = sum(num(d.get("items")) for _, d in loot)
        print(f"   军械库缴获：{len(loot)} 场，{bodies:.0f} 具尸体，{items:.0f} 件（每具 {items / max(1, bodies):.2f} 件）")
    if by_stage["CONGREGATION_COMPLETED"] and days >= 10:
        per_day = len(by_stage["CONGREGATION_COMPLETED"]) / days
        print(f"   传教每天 {per_day:.2f} 次")

    # ------------------------------------------------------------ 异常
    print("\n== 异常（ANOMALY，每天检查一次）")
    anomalies = collections.defaultdict(list)
    for hour, data in by_stage["ANOMALY"]:
        anomalies[data["kind"]].append((hour, data))
    if not anomalies:
        print("   无")
    for kind, rows in sorted(anomalies.items(), key=lambda item: -len(item[1])):
        sample = {k: v for k, v in rows[-1][1].items() if k != "kind"}
        print(f"   {kind} ×{len(rows)}（第 {day(rows[0][0])}–{day(rows[-1][0])} 天）最近一次: {sample}")
        issues.append(f"异常 {kind} 出现 {len(rows)} 次")

    print("\n== 需要看的平衡问题")
    for issue in issues or ["没有发现"]:
        print("   - " + issue)


if __name__ == "__main__":
    main()

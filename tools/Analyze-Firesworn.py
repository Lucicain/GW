"""火誓者 / 信众村经济的平衡与配合报告。

读 Documents 下的 GreyWarden-AI-Diagnostics.log（含归档），只取最近一次启动游戏以来的行，
汇总 `| FIRESWORN |` 监控行（见 docs/state/firesworn-economy.md「监控」）：

  python tools/Analyze-Firesworn.py              # 最近一次启动以来
  python tools/Analyze-Firesworn.py --all        # 全部日志

输出：总览、每天的经济、每座有火誓者的村子、每位灰袍领主、无领主队与仪式、异常（ANOMALY）汇总，
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
    with open(path, encoding="utf-8-sig", errors="replace") as handle:
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
        with open(path, encoding="utf-8-sig", errors="replace") as handle:
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

    experience = collections.defaultdict(collections.Counter)
    for _, stage, data in events:
        if stage == "GUARD_EXPERIENCE":
            source = data.get("source", "unknown").split(":", 1)[0]
            for key in ("earned", "granted", "unused"):
                experience[source][key] += num(data.get(key))
    if experience:
        print("== 村内守村队经验（经验点，不是新增兵数）")
        for source, values in sorted(experience.items()):
            print(f"   {source}: 获得{values['earned']:.0f}，实际发放{values['granted']:.0f}，容量不足未用{values['unused']:.0f}")

    # ------------------------------------------------------------ 总览
    totals = by_stage["DAILY_TOTALS"]
    print(f"== 总览：游戏内第 {first_day} 天到第 {last_day} 天，共 {days} 天，{len(events)} 行监控")
    if totals:
        a, b = totals[0][1], totals[-1][1]
        for key in ("faithVillages", "swornVillages", "households", "guard", "strongestPriests", "dungeon", "treasury"):
            print(f"   {key}: {a.get(key)} -> {b.get(key)}")
        treasury_rate = (num(b["treasury"]) - num(a["treasury"])) / max(1, day(totals[-1][0]) - day(totals[0][0]))
        print(f"   公库每天净变化约 {treasury_rate:+.0f}")
        if treasury_rate < 0:
            issues.append(f"公库在减少（每天 {treasury_rate:+.0f}）")

    # ------------------------------------------------------------ 经济
    income = collections.Counter()
    for hour, data in by_stage["PROTECTION_FEE"]:
        income["保护费"] += num(data.get("amount"))
    for hour, data in by_stage["FAITH_PARTY_HOME"]:
        income["信众队卖货"] += num(data.get("proceedsToTreasury"))
    for hour, data in by_stage["DEFENCE_SPOILS"]:
        income["守村金币"] += num(data.get("goldToTreasury"))
    print("\n== 收支（这段时间合计）")
    for key, value in income.items():
        print(f"   {key}: {value:.0f}（每天 {value / days:.0f}）")
    cargo = sum(num(data.get("cargoValue")) for _, data in by_stage["MARKET_DISPATCHED"])
    sold = sum(num(data.get("proceeds")) for _, data in by_stage["MARKET_SOLD"])
    if cargo:
        print(f"   信众队装车估值 {cargo:.0f}，实卖 {sold:.0f}（{sold / cargo:.0%}）")
        if sold / cargo < 0.2:
            issues.append(f"信众队只卖掉装车估值的 {sold / cargo:.0%}：城镇金库太少或货太贵，货仓会越堆越多")
    low_gold = [data for _, data in by_stage["MARKET_SOLD"] if num(data.get("townGoldLeft")) < 1000]
    if low_gold:
        issues.append(f"{len(low_gold)} 次卖货把城镇金库卖空了：{', '.join(sorted({d['town'] for d in low_gold}))}")

    # ------------------------------------------------------------ 信众村
    print("\n== 有火誓者的村子（首日 -> 末日；来源注明户、兵和人次，不相加）")
    villages = collections.defaultdict(list)
    for hour, data in by_stage["DAILY_VILLAGE"]:
        villages[data["village"]].append((hour, data))
    gains = collections.defaultdict(collections.Counter)
    for _, data in by_stage["RECONSTRUCTION_SWORN"]:
        before, after = str(data["households"]).split("->")
        gains[data["village"]]["重建信众户"] += num(after) - num(before)
    for _, data in by_stage["CONGREGATION_COMPLETED"]:
        before, after = str(data["households"]).split("->")
        gains[data["village"]]["集会信众户"] += num(after) - num(before)
    for _, data in by_stage["MISSION_JOINED"]:
        gains[data["village"]]["外村来传教"] += num(data.get("joined"))
    for _, data in by_stage["BALANCE_DELIVERED"]:
        gains[data["village"]]["运械押车并入"] += num(data.get("joined"))
        gains[data["village"]]["运来兵器件数"] += num(data.get("kitItems"))
    for _, data in by_stage["BALANCE_DISPATCHED"]:
        gains[data["home"]]["派出运械押车"] -= num(data.get("men"))
    for _, data in by_stage["MISSION_DISPATCHED"]:
        gains[data["home"]]["派出传教"] -= num(data.get("men"))
    for _, data in by_stage["DAMNATION"]:
        gains[data["village"]]["烧人出兵"] += num(data.get("addedToGuard"))
        gains[data["village"]]["烧户立誓的户"] += num(data.get("householdsSworn"))
        gains[data["village"]]["烧掉的不信教户"] -= num(data.get("householdsBurned"))
    for _, data in by_stage["PRIEST_RECRUITED"]:
        gains[data["village"]]["祭司格子招走"] -= num(data.get("fromGuard"))
    for _, data in by_stage["GUARD_ENLISTED"]:
        gains[data["village"]]["直接招走"] -= num(data.get("enlisted"))
    for _, data in by_stage["SWORN_GROWTH"]:
        for field, label in (("fromWardensHouseholds", "驻留信众户"),
                             ("fromHearthHouseholds", "自然增长信众户"),
                             ("fromWardens", "旧日志驻留产兵"),
                             ("fromHearth", "旧日志自然增长产兵")):
            if field in data:
                gains[data["village"]][label] += num(data[field])
        gains[data["village"]]["守村队补兵"] += num(data.get("regrown"))
    for _, data in by_stage["ARMORY_LOOT"]:
        gains[data["holder"]]["缴获件数"] += num(data.get("items"))
    for village, rows in sorted(villages.items()):
        a, b = rows[0][1], rows[-1][1]
        span = max(1, day(rows[-1][0]) - day(rows[0][0]))
        hearth_change = num(b["hearth"]) - num(a["hearth"])
        sworn_change = num(b["households"]) - num(a["households"])
        print(f"   {village}: 户 {a['hearth']}->{b['hearth']}  信众户 {a['households']}->{b['households']}（{b['share']}）"
              f"  守村队 {a['guard']}->{b['guard']}"
              f"  原版民兵 {b['vanillaMilitia']}  祭司 {b['priest'] != '-'} 势力 {b['priestPower']} 其他 {b['others']}"
              f" 最强 {b['strongest']}")
        print(f"      守村队 {b['guard'] or '-'}  格子 {b['priestSlots']}  地牢 {b['dungeon']}"
              f"  军械库 {b['armoryItems']} 件（{b['armoryKinds']}） 武装 {b['armed']}  货仓 {b['goodsValue']}  信众队 {b['parties']}")
        if gains[village]:
            print("      来源: " + "，".join(f"{k} {v:+.0f}" for k, v in gains[village].items()))
        if hearth_change / span < -3:
            issues.append(f"{village} 户数每天掉 {hearth_change / span:.1f}")
        if b["priest"] != "-" and span >= 15 and sworn_change <= 0:
            issues.append(f"{village} 有祭司，{span} 天信众户没涨（{a['households']}->{b['households']}）")
        armed, total = (b["armed"].split("/") + ["0"])[:2]
        if num(total) >= 20 and num(armed) / num(total) < 0.25:
            issues.append(f"{village} 只有 {b['armed']} 的火誓者拿到兵器（{b['armoryKinds']}）")
    newly = [d["village"] for _, d in by_stage["PRIEST_SPAWNED"]]
    if newly:
        print(f"   新信众村（来了祭司）：{', '.join(newly)}，平均每 {days / len(newly):.1f} 天一座")

    # ------------------------------------------------------------ 领主
    print("\n== 灰袍领主")
    lords = collections.defaultdict(list)
    for hour, data in by_stage["DAILY_LORD"]:
        lords[data["party"]].append((hour, data))
    visits = collections.Counter(data["party"] for _, data in by_stage["HANDOVER"])
    recruits = collections.defaultdict(collections.Counter)
    for _, data in by_stage["PRIEST_RECRUITED"]:
        recruits[data["party"]]["祭司:" + data["troop"]] += num(data.get("count"))
    for _, data in by_stage["GUARD_ENLISTED"]:
        recruits[data["party"]]["守村队直招"] += num(data.get("enlisted"))
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
        print(f"      进信众村 {visits[party]} 次；最近一次意愿最高的信众村 {b.get('faithVisit', '-')}")
        if recruits[party]:
            print("      兵源与俘虏: " + "，".join(f"{k} {v:.0f}" for k, v in recruits[party].items()))
        if days >= 5 and visits[party] == 0:
            issues.append(f"{party} {days:.0f} 天没进过信众村（看 faithVisit 分数）")
        if minimum < 0.6:
            issues.append(f"{party} 兵力最低到过 {minimum:.0%}")

    # ------------------------------------------------------------ 无领主队与仪式
    print("\n== 无领主队与仪式")
    for stage in ("CONGREGATION_STARTED", "CONGREGATION_COMPLETED", "CONGREGATION_CANCELLED",
                  "MISSION_DISPATCHED", "MISSION_JOINED", "BALANCE_DISPATCHED", "BALANCE_DELIVERED",
                  "MARKET_DISPATCHED", "MARKET_SOLD", "FAITH_PARTY_HOME",
                  "FAITH_PARTY_TURNED_BACK", "FAITH_PARTY_LOST", "RECONSTRUCTION_SWORN",
                  "PRIEST_SPAWNED", "RITUAL_MENU_SHOWN"):
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

    print("\n== 需要看的问题")
    for issue in issues or ["没有发现"]:
        print("   - " + issue)


if __name__ == "__main__":
    main()

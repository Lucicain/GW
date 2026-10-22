#!/usr/bin/env python3
"""Fail when the mod carries a fallback or a declaration that has never earned its place.

User ruling 2026-10-05: a fallback must have actually caught something; one that never
fired is garbage. The same goes for constants and members nothing uses: they outlive the
mechanism they were written for, and then mislead whoever reads them.

Checks over GreyWardenPolicePurity/*.cs:

1. Every catch that does not rethrow carries "依据：" in its body, naming the recorded
   failure it caught (method, date, what). Without one, do not write the catch: let the
   exception surface, read the crash, fix the cause.
2. Every constant, field, property and method is referenced somewhere besides its own
   declaration (in C#, the module XML/GUI files, or tools/). Members the engine calls
   by convention are excluded below, each with its reason.

3. Shared judgements are accessed through their owner (OWNERS below).
4. Identical method bodies use one implementation, except engine overrides.

Exit status 1 when any check finds anything. The pre-commit hook blocks on it.
"""
import glob
import os
import re
import sys
from collections import Counter

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MOD = os.path.join(ROOT, "GreyWardenPolicePurity")

# Called by the engine through an interface or interop layout, never by name in our code.
ENGINE_CALLED = {
    "IsVisibleNotification": "IChatNotification member (GreyWardenAdoptionLogEntry)",
    "GetNotificationText": "IChatNotification member (GreyWardenAdoptionLogEntry)",
    "NumRemovedTroops": "IMissionTroopSupplier member (GreyWardenSafeTroopSupplier)",
    "GetAgentState": "IAgentStateDecider member (GwpCitySearchMissionController); Mission.GetAgentState invokes it",
    "User": "WAVEHDR interop layout (GwpMusicOutput)",
    "Reserved": "WAVEHDR interop layout (GwpMusicOutput)",
}


def skip_string(s, i):
    j = i
    verbatim = interp = False
    while s[j] in "@$":
        verbatim |= s[j] == "@"
        interp |= s[j] == "$"
        j += 1
    j += 1
    depth = 0
    while True:
        c = s[j]
        if interp and c == "{":
            if s[j + 1] == "{":
                j += 2
                continue
            depth += 1
        elif interp and c == "}" and depth:
            depth -= 1
        elif c == '"' and depth == 0:
            if verbatim and s[j + 1] == '"':
                j += 2
                continue
            return j + 1
        elif c == "\\" and not verbatim:
            j += 2
            continue
        elif c == '"' and depth:
            j = skip_string(s, j) - 1
        j += 1


def code_mask(s):
    """True where a character is code rather than a string, char literal or comment."""
    mask = [True] * len(s)
    i = 0
    while i < len(s):
        c = s[i]
        if s.startswith("//", i) or s.startswith("/*", i):
            j = s.find("\n", i) if s[i + 1] == "/" else s.find("*/", i) + 2
            j = len(s) if j < 0 else j
            mask[i:j] = [False] * (j - i)
            i = j
            continue
        if c == '"' or (c in "@$" and re.match(r'[@$]{1,2}"', s[i:i + 3])):
            j = skip_string(s, i)
            mask[i:j] = [False] * (j - i)
            i = j
            continue
        if c == "'":
            m = re.match(r"'(\\.|[^'\\])+'", s[i:])
            if m:
                mask[i:i + m.end()] = [False] * m.end()
                i += m.end()
                continue
        i += 1
    return mask


def match_brace(s, mask, i):
    depth = 0
    for j in range(i, len(s)):
        if mask[j]:
            if s[j] == "{":
                depth += 1
            elif s[j] == "}":
                depth -= 1
                if depth == 0:
                    return j
    raise ValueError("unbalanced braces")


def read(path):
    with open(path, encoding="utf-8-sig") as source:
        return source.read()


def unproven_catches(files):
    found = []
    for path in files:
        s = read(path)
        mask = code_mask(s)
        for m in re.finditer(r"\bcatch\b", s):
            if not mask[m.start()]:
                continue
            h = s.index("{", m.end())
            body = s[h + 1:match_brace(s, mask, h)]
            if re.search(r"\bthrow\b", body) or "依据：" in body:
                continue
            found.append(f"{os.path.relpath(path, ROOT)}:{s.count(chr(10), 0, m.start()) + 1}")
    return found


DECL = re.compile(
    r"^(?:(?:public|internal|private|protected|static|readonly|const|sealed|virtual|async|unsafe|extern)\s+)+"
    r"[\w\.]+(?:<[^>]*>)?(?:\[\])?\??\s+(\w+)\s*[(=;{]")


def unreferenced(files):
    texts = [read(p) for p in files]
    texts += [open(p, encoding="utf-8-sig", errors="replace").read()
              for p in glob.glob(os.path.join(MOD, "_Module", "**", "*.xml"), recursive=True)]
    texts += [read(p) for p in glob.glob(os.path.join(ROOT, "tools", "**", "*.cs"), recursive=True)
              if os.sep + "obj" + os.sep not in p and os.sep + "bin" + os.sep not in p]
    counts = Counter(re.findall(r"\b[A-Za-z_]\w*\b", "\n".join(texts)))
    found = []
    for path in files:
        lines = read(path).splitlines()
        for n, line in enumerate(lines, 1):
            t = line.strip()
            if "override" in t:
                continue
            m = DECL.match(t)
            if not m:
                continue
            name = m.group(1)
            if counts[name] > 1 or name in ENGINE_CALLED or name.startswith("AutoGenerated"):
                continue
            attributes = " ".join(l.strip() for l in lines[max(0, n - 4):n - 1])
            if re.search(r"\[Harmony|\[DataSource", attributes):
                continue  # Harmony hooks and Gauntlet bindings are called by attribute
            if re.match(r"public void Execute\w*\(\)", t):
                continue  # Gauntlet command binding
            found.append(f"{os.path.relpath(path, ROOT)}:{n} {name}")
    return found


# Each shared concept has one owner. Code elsewhere calls the owner instead of the raw
# primitive, because a judgement written twice drifts apart: on 2026-10-07 there were
# three strength formulas, eight private copies of "is this a Grey Warden", case state
# changed from eleven files and eight hand-kept lists of who blocks which duty. A new
# shared concept gets a row here when it gets an owner.
DUTY_PREDICATES = {
    "IsPartyReservedForPlayerRequest": "GreyWardenPlayerRequestBehavior*.cs",
    "IsTrainerReservedForPlayerOrder": "GreyWardenTroopRequestBehavior*.cs",
    "IsVillageReliefParty": "GreyWardenVillageAdoptionBehavior*.cs",
    "IsPartyOccupiedByAssistance": "PoliceEnforcementBehavior*.cs",
    "IsPartyHeldForDuel": "PoliceEnforcementBehavior*.cs",
    "TryGetReliefVisit": "GreyWardenVillageAdoptionBehavior.cs",
    "GetDuelVenue": "PoliceEnforcementBehavior*.cs",
    "IsReconstructionParty": "GreyWardenVillageReconstructionBehavior*.cs",
    "ShouldReserveFromOrdinaryCases": "GreyWardenVillageReconstructionBehavior*.cs|GreyWardenIssueResolutionBehavior*.cs",
    "IsIssueDutyParty": "GreyWardenIssueResolutionBehavior*.cs",
    "IsTrainingOccupied": "GreyWardenTrainingBehavior*.cs",
    "ShouldReserveFromNewDuties": "GreyWardenTrainingBehavior*.cs",
}
OWNERS = [
    ("玩家为本案宣战", r"\bDeclareWarAction\.Apply\w*\s*\(",
     "PlayerBountyBehavior.Support.cs", "PlayerBountyBehavior.DeclarePlayerCaseWar"),
    ("海陆交接资格", r"\.IsCurrentlyAtSea\s*==\s*\w+\.IsCurrentlyAtSea\b",
     "GwpCommon.cs", "GwpCommon.CanCompletePartyHandover"),
    ("信众村民队生命周期", r"\[HarmonyPatch\(typeof\((?:VillagerCampaignBehavior|SellGoodsForTradeAction)\)",
     "GwpFaithVillagers.cs", "GwpFireswornBehavior.IsFaithVillager / GwpFaithVillagers.cs"),
    ("玩家案件委派", r"\bPlayerBountyBehavior\.(?:IsBountyCaseHeldByPlayer|IsBountyReservedTarget)\s*\(|\??\.(?:HasCommissionAuthority|IsActiveBountyTarget|HoldsAtonementCase|IsActiveAtonementTarget|ReservesAtonementTarget)\s*\(",
     "GwpPlayerCaseAssignment.cs", "GwpPlayerCaseAssignment"),
    ("讲和", r"\b(?:FactionManager\.SetNeutral|MakePeaceAction\.Apply)\s*\(", "GwpPeace.cs", "GwpPeace.Make / EndPoliceWar"),
    ("临时队唯一编号", r"\bFindNextUniqueStringId\s*<\s*MobileParty\s*>\s*\(", "GwpPartyIdentity.cs", "GwpPartyIdentity.Next"),
    ("守村经验容量", r"\bCanTroopGainXp\s*\(", "GwpFireswornExperience.cs", "GwpFireswornBehavior.GrantGuardExperience"),
    ("战力", r"\.EstimatedStrength\b|\bGetPowerOfParty\s*\(|\bGetTroopPower\s*\(|\bGetTotalLandStrength\w*\s*\(",
     "GwpStrength.cs", "GwpStrength"),
    ("进聚落意愿", r"\[HarmonyPatch\(typeof\(AiVisitSettlementBehavior\)", "GwpWardenVisitDesire*.cs", "GwpWardenVisitDesire"),
    ("灰袍身份", r"\bPoliceClanId\b", "GwpCommon.cs|GwpIds.cs|GwpData.cs", "GwpCommon.IsGreyWarden*"),
    ("案件状态", r"\b(?:CrimePool|CrimeState)\.(?:EndTask|ReleaseTask|CloseCase|ReopenCase)\s*\(",
     "PoliceEnforcementBehavior*.cs|GwpData.cs|GwpRuntimeState.cs",
     "PoliceEnforcementBehavior.CloseCase / ReleaseWardensFromCase"),
] + [("差事占用", r"\b" + name + r"\b", owner + "|GwpWardenDuties.cs", "GwpWardenDuties")
     for name, owner in DUTY_PREDICATES.items()]


def owner_violations(files):
    from fnmatch import fnmatch
    found = []
    for path in files:
        name = os.path.basename(path)
        s = read(path)
        mask = code_mask(s)
        for concept, pattern, owners, use in OWNERS:
            if any(fnmatch(name, o) for o in owners.split("|")):
                continue
            for m in re.finditer(pattern, s):
                if not mask[m.start()]:
                    continue
                line_start = s.rfind("\n", 0, m.start()) + 1
                if re.search(r"\bbool\s+$", s[line_start:m.start()]):
                    continue  # a same-named declaration of its own, not a call
                found.append(f"{os.path.relpath(path, ROOT)}:{s.count(chr(10), 0, m.start()) + 1} "
                             f"{concept}：{m.group(0).strip('.( ')} → 用 {use}")
    return found


METHOD = re.compile(
    r"^[ \t]*(?:(?:public|internal|private|protected|static|override|sealed|virtual|async)\s+)+"
    r"[\w\.<>\[\]?,]+\s+(\w+)\s*\([^)]*\)\s*(\{|=>)", re.M)


def duplicate_bodies(files):
    """Two methods with the same body: someone copied a judgement instead of calling it."""
    seen = {}
    found = []
    for path in files:
        s = read(path)
        mask = code_mask(s)
        for m in METHOD.finditer(s):
            if not mask[m.start(1)]:
                continue
            if "override" in m.group(0):
                continue  # each subclass answers the same engine event; matching bodies are expected
            if m.group(2) == "{":
                end = match_brace(s, mask, m.end() - 1)
                body = s[m.end():end]
            else:
                end = s.find(";", m.end())
                body = s[m.end():end]
            # Drop comments but keep string literals: two methods that differ only in
            # their text (dialogue lines, log names) are not copies.
            norm = re.sub(r"\s+", "", re.sub(r"//[^\n]*|/\*.*?\*/", "", body, flags=re.S))
            if len(norm) < 60:
                continue  # one-line delegations and trivial guards are not copies worth chasing
            where = f"{os.path.relpath(path, ROOT)}:{s.count(chr(10), 0, m.start()) + 1} {m.group(1)}"
            if norm in seen:
                found.append(f"{where} 与 {seen[norm]} 函数体相同")
            else:
                seen[norm] = where
    return found


def main():
    files = sorted(glob.glob(os.path.join(MOD, "*.cs")))
    catches = unproven_catches(files)
    dead = unreferenced(files)
    owners = owner_violations(files)
    copies = duplicate_bodies(files)
    if owners:
        print("[!] 绕过了共享概念的归属方（同一个判断写两份迟早分叉）：\n")
        for o in owners:
            print("  " + o)
        print()
    if copies:
        print("[!] 函数体完全相同——调用已有的那一个，删掉这份：\n")
        for c in copies:
            print("  " + c)
        print()
    if catches:
        print("[!] catch 没写依据（它兜住过的那次故障）。没有记录就删掉这个 catch，让异常暴露：\n")
        for c in catches:
            print("  " + c)
        print()
    if dead:
        print("[!] 没人引用的常量、字段、属性或方法。删掉它（连同注释）：\n")
        for d in dead:
            print("  " + d)
        print()
    print(f"{len(catches)} 个 catch 缺依据 / {len(dead)} 个声明无人引用 / "
          f"{len(owners)} 处绕过归属方 / {len(copies)} 个重复函数体")
    return 1 if catches or dead or owners or copies else 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""Fail when the mod carries a fallback or a declaration that has never earned its place.

User ruling 2026-10-05: a fallback must have actually caught something; one that never
fired is garbage. The same goes for constants and members nothing uses: they outlive the
mechanism they were written for, and then mislead whoever reads them.

Two checks over GreyWardenPolicePurity/*.cs:

1. Every catch that does not rethrow carries "依据：" in its body, naming the recorded
   failure it caught (method, date, what). Without one, do not write the catch: let the
   exception surface, read the crash, fix the cause.
2. Every constant, field, property and method is referenced somewhere besides its own
   declaration (in C#, the module XML/GUI files, or tools/). Members the engine calls
   by convention are excluded below, each with its reason.

Exit status 1 when either check finds anything. The pre-commit hook blocks on it.
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
    return open(path, encoding="utf-8-sig").read()


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


def main():
    files = sorted(glob.glob(os.path.join(MOD, "*.cs")))
    catches = unproven_catches(files)
    dead = unreferenced(files)
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
    print(f"{len(catches)} 个 catch 缺依据 / {len(dead)} 个声明无人引用")
    return 1 if catches or dead else 0


if __name__ == "__main__":
    sys.exit(main())

"""Find where native code references given strings, and show the code around each hit.

Usage:
    python tools/native/find-string-refs.py <path-to-TaleWorlds.Native.dll> <string> [<string> ...]

Locates each ASCII string in the DLL, finds every `lea reg, [rip+disp32]` in .text
that points at one of them, and prints ~0x80 bytes of disassembly around each hit.
Engine log messages, asset names and assert texts are the usual way into an
otherwise unnamed C++ function: find the string, find who loads it, and the
function around that is the one you are looking for.

A string can exist with zero hits: it may be reached through a pointer table
rather than a direct lea. That is a real result, not a failure of the script.

Needs: pip install pefile capstone
Method and evidence grades: GreyWardenPolicePurity/docs/reference/investigation-methods.md

Moved here from .codex_tmp/analyze_native_refs.py (2026-09-25) so the method lives in
the repository instead of a gitignored scratch folder. The original swept all of
.text with capstone, which stops at the first undecodable byte and so silently
covered only the start of the section.
"""

import re
import struct
import sys

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

# REX.W (48 or 4C) + 8D + ModRM with mod=00, rm=101: lea r64, [rip+disp32].
# Matching bytes runs in C; decoding every instruction in Python does not finish.
LEA_RIP = re.compile(rb"[\x48\x4c]\x8d[\x05\x0d\x15\x1d\x25\x2d\x35\x3d]", re.S)
LEA_LEN = 7


def main() -> None:
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    path = sys.argv[1]
    needles = [value.encode("ascii") for value in sys.argv[2:]]
    pe = pefile.PE(path, fast_load=False)
    image_base = pe.OPTIONAL_HEADER.ImageBase
    data = open(path, "rb").read()

    text_section = next(
        section for section in pe.sections
        if section.Name.rstrip(b"\0") == b".text"
    )
    text = text_section.get_data()
    text_va = image_base + text_section.VirtualAddress

    targets: dict[int, str] = {}
    for needle in needles:
        start = 0
        while True:
            offset = data.find(needle + b"\0", start)
            if offset < 0:
                break
            rva = pe.get_rva_from_offset(offset)
            va = image_base + rva
            targets[va] = needle.decode("ascii")
            print(f"STRING {needle.decode()} file=0x{offset:x} rva=0x{rva:x} va=0x{va:x}")
            start = offset + 1

    hits: list[tuple[int, int, str]] = []
    for m in LEA_RIP.finditer(text):
        off = m.start()
        if off + LEA_LEN > len(text):
            continue
        disp = struct.unpack_from("<i", text, off + 3)[0]
        address = text_va + off
        target = address + LEA_LEN + disp
        if target in targets:
            hits.append((address, target, targets[target]))

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.skipdata = True  # the window may start mid-instruction or in padding

    print(f"XREF_COUNT {len(hits)}")
    for address, target, name in hits:
        print(f"XREF {name} instruction=0x{address:x} target=0x{target:x} "
              f"rva=0x{address - image_base:x}")
        start_va = address - 0x80
        start_offset = start_va - text_va
        window = text[max(start_offset, 0):start_offset + 0x180]
        for insn in md.disasm(window, max(start_va, text_va)):
            marker = "=>" if insn.address == address else "  "
            print(f"{marker} {insn.address:016x} {insn.mnemonic:8} {insn.op_str}")
        print()


if __name__ == "__main__":
    main()

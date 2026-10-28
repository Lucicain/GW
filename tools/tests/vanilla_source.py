"""Read the base game's decompiled source from the vanilla-source library.

The library is built outside the repository by tools/Build-VanillaSource.py
(workflow.md「原版知识」). Tests that run a vanilla method as written read it
from there, for the game version currently installed.
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GAME = Path(r"D:\steam\steamapps\common\Mount & Blade II Bannerlord")
LIBRARY = ROOT.parent / "BannerlordSource"


def vanilla(assembly: str, relative: str) -> str:
    """Source text of <assembly>/<namespace folder>/<file> in the installed version's library."""
    version = re.search(r'Singleplayer Value="([^"]+)"',
                        (GAME / "bin/Win64_Shipping_Client/Version.xml").read_text(encoding="utf-8")).group(1)
    path = LIBRARY / version / "managed" / assembly / relative
    if not path.exists():
        raise FileNotFoundError(f"{path} is missing; run python tools/Build-VanillaSource.py")
    return path.read_text(encoding="utf-8-sig")

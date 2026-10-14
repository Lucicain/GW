#!/usr/bin/env python3
"""Create and verify an explicitly requested local player ZIP from an isolated build."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import zipfile

ROOT = Path(__file__).resolve().parent.parent
MODULE = ROOT / "GreyWardenPolicePurity/_Module"
MARKERS = ("GreyWarden-AI-Diagnostics", "GreyWarden-Case-Events", "GreyWarden-Faults",
           "GreyWarden-Diagnostics-Archive", "Mount and Blade II Bannerlord")
EXCLUDED = {"Assets", "AssetSources", "RuntimeDataCache", "DsAssetPackages", "bin", "Shaders", "obj", "docs"}


def digest(handle):
    result = hashlib.sha256()
    while chunk := handle.read(1024 * 1024):
        result.update(chunk)
    return result.hexdigest()


def build(binary, label, output):
    if not re.fullmatch(r"v\d+\.\d+-r\d+", label):
        raise ValueError("Expected a version label such as v1.4-r15")
    data = binary.read_bytes()
    for marker in MARKERS:
        if marker.encode("ascii") in data or marker.encode("utf-16-le") in data:
            raise ValueError(f"Player assembly contains diagnostic marker: {marker}")
    files = {}
    for path in MODULE.rglob("*"):
        if not path.is_file():
            continue
        relative = path.relative_to(MODULE)
        if relative.parts[0] in EXCLUDED or path.suffix.lower() in {".zip", ".sha256", ".pdb", ".fbx", ".blend", ".psd"}:
            continue
        if relative.parts[0] == "AssetPackages" and relative.as_posix() != "AssetPackages/pack0.tpac":
            continue
        files["GreyWarden/" + relative.as_posix()] = path
    files["GreyWarden/bin/Win64_Shipping_Client/GreyWardenPolicePurity.dll"] = binary
    files["GreyWarden/bin/Win64_Shipping_Client/0Harmony.dll"] = binary.with_name("0Harmony.dll")
    required = {"GreyWarden/AssetPackages/pack0.tpac", "GreyWarden/README.md", "GreyWarden/README_EN.md",
                "GreyWarden/SubModule.xml", "GreyWarden/bin/Win64_Shipping_Client/0Harmony.dll"}
    if not required.issubset(files):
        raise ValueError(f"Package is missing required files: {required - files.keys()}")
    output.mkdir(parents=True, exist_ok=True)
    archive = output / f"GreyWarden-{label}.zip"
    temporary = archive.with_suffix(".zip.partial")
    manifest = []
    with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as package:
        for name, path in sorted(files.items()):
            with path.open("rb") as handle:
                checksum = digest(handle)
            manifest.append({"path": name, "bytes": path.stat().st_size, "sha256": checksum})
            package.write(path, name)
    with zipfile.ZipFile(temporary) as package:
        if set(package.namelist()) != set(files):
            raise ValueError("ZIP paths differ from the expected player layout")
        for item in manifest:
            with package.open(item["path"]) as handle:
                if digest(handle) != item["sha256"]:
                    raise ValueError(f"ZIP verification failed: {item['path']}")
    temporary.replace(archive)
    with archive.open("rb") as handle:
        checksum = digest(handle)
    archive.with_suffix(".zip.sha256").write_text(checksum + "  " + archive.name + "\n", encoding="ascii")
    evidence = ROOT / "build-check" / f"package-{label}"
    evidence.mkdir(parents=True, exist_ok=True)
    (evidence / "manifest.json").write_text(json.dumps({"version": label, "diagnostics": False,
        "archive": str(archive), "bytes": archive.stat().st_size, "sha256": checksum,
        "files": manifest}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    shutil.copy2(binary, evidence / binary.name)
    print(f"PLAYER PACKAGE: PASS ({len(manifest)} files, all decompressed hashes verified, diagnostic markers absent)")
    print(f"ZIP={archive}\nBYTES={archive.stat().st_size}\nSHA256={checksum}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--binary", type=Path, required=True)
    parser.add_argument("--label", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    build(args.binary.resolve(), args.label, args.output.resolve())

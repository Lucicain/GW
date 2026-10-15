"""Clone a native banner material record, preserving its metadata byte for byte.

The only metadata change is the diffuse texture GUID. The result has its own
stable name/resource/package identity, separate from the material open in the
Kit. Writes a staged single-material TPAC, never changes live assets.
"""

import argparse
import hashlib
import json
import struct
import uuid
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MATERIAL = uuid.UUID("1db01393-6902-4f19-83ba-b37a39830717")
TEXTURE = uuid.UUID("c974cbcb-5f1c-49f6-9a32-2b5b6c92c2e8")


def read_exact(stream, count):
    data = stream.read(count)
    if len(data) != count:
        raise ValueError("Truncated TPAC")
    return data


def scalar(stream, fmt):
    return struct.unpack(fmt, read_exact(stream, struct.calcsize(fmt)))[0]


def sized_string(stream):
    length = scalar(stream, "<I")
    if length > 65535:
        raise ValueError("Invalid asset name")
    return read_exact(stream, length).decode("utf-8")


def find_record(path, wanted_name, wanted_type):
    # TPAC header/record layout is the one read by AssetPackage.Load in the
    # project's TpacTool.Lib; no compressed geometry or pixel data is read.
    with path.open("rb") as stream:
        if read_exact(stream, 4) != b"TPAC":
            raise ValueError("Not a TPAC")
        version = scalar(stream, "<I")
        if version != 2:
            raise ValueError(f"Expected TPAC v2, got {version}")
        package_guid = read_exact(stream, 16)
        count = scalar(stream, "<I")
        read_exact(stream, 8)  # data offset, reserved
        for _ in range(count):
            kind = read_exact(stream, 16)
            guid = read_exact(stream, 16)
            asset_version = scalar(stream, "<I")
            name = sized_string(stream)
            metadata_size = scalar(stream, "<Q")
            metadata = read_exact(stream, metadata_size)
            checksum = read_exact(stream, 8)
            segment_count = scalar(stream, "<I")
            segments = read_exact(stream, 69 * segment_count)
            dependency_count = scalar(stream, "<I")
            dependencies = read_exact(stream, dependency_count * 48)
            if name == wanted_name and kind == wanted_type.bytes_le:
                return dict(
                    package_guid=package_guid, kind=kind, guid=guid,
                    asset_version=asset_version, name=name, metadata=metadata,
                    checksum=checksum, segment_count=segment_count,
                    segments=segments, dependencies=dependencies,
                )
    raise ValueError(f"Asset {wanted_name!r} not found in {path}")


def build_material_record(template, identity, texture_guid, old_texture_guid):
    if template["segment_count"] or template["dependencies"]:
        raise ValueError("Template is not a standalone metadata-only material")
    original = template["metadata"]
    if original.count(old_texture_guid.bytes_le) != 1:
        raise ValueError("Native diffuse texture GUID is not unique in metadata")
    metadata = original.replace(old_texture_guid.bytes_le, texture_guid)
    if metadata.replace(texture_guid, old_texture_guid.bytes_le) != original:
        raise ValueError("Metadata changed outside the diffuse GUID")
    name = b"gwp_commander_banner"
    record = (
        MATERIAL.bytes_le + identity["guid"] + struct.pack("<I", template["asset_version"])
        + struct.pack("<I", len(name)) + name
        + struct.pack("<Q", len(metadata)) + metadata
        + bytes(8) + struct.pack("<II", 0, 0)
    )
    # Match AssetPackage.Save: data offset is measured from byte 36.
    header = struct.pack("<4sI16sIII", b"TPAC", 2, identity["package_guid"], 1, len(record), 0)
    return header + record


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-folder", type=Path, default=Path(
        "D:/steam/steamapps/common/Mount & Blade II Bannerlord"))
    args = parser.parse_args()
    banner_dir = args.game_folder / "Modules/GreyWarden/Assets/BannerIcons"
    native = args.game_folder / "Modules/Native/AssetPackages/core.tpac"
    template = find_record(native, "custom_banner_icons_07", MATERIAL)
    native_texture = find_record(native, "custom_banner_icons_07", TEXTURE)
    identity = {
        "guid": uuid.uuid5(MATERIAL, "GreyWarden:gwp_commander_banner:material:v1").bytes_le,
        "package_guid": uuid.uuid5(MATERIAL, "GreyWarden:gwp_commander_banner:package:v1").bytes_le,
    }
    texture_path = banner_dir / "gwp_commander_banner_tex.tpac"
    texture = find_record(texture_path, "gwp_commander_banner", TEXTURE)
    data = build_material_record(
        template, identity, texture["guid"], uuid.UUID(bytes_le=native_texture["guid"])
    )
    output = ROOT / "GreyWardenPolicePurity/docs/reference/banner-review/kit-input"
    path = output / "gwp_commander_banner_mtl.tpac"
    path.write_bytes(data)
    result = find_record(path, "gwp_commander_banner", MATERIAL)
    assert result["guid"] == identity["guid"]
    assert result["package_guid"] == identity["package_guid"]
    assert result["metadata"].replace(texture["guid"], native_texture["guid"]) == template["metadata"]
    report = {
        "status": "Artifact generated; deployment is tracked in docs/state/clan-banner.md",
        "method": "Native material metadata clone; only diffuse GUID replaced",
        "template": "Native/custom_banner_icons_07",
        "texture_tpac_sha256": sha256(texture_path),
        "material_guid": str(uuid.UUID(bytes_le=identity["guid"])),
        "package_guid": str(uuid.UUID(bytes_le=identity["package_guid"])),
        "texture_guid": str(uuid.UUID(bytes_le=texture["guid"])),
        "output": str(path), "output_sha256": sha256(path),
        "native_metadata_bytes": len(template["metadata"]),
        "non_diffuse_metadata_comparison": "BYTE_IDENTICAL",
        "tpac_record_readback": "PASS",
    }
    (output / "material-build.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()

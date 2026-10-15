"""Encode the approved bird artwork for Bannerlord's gui_color_and_stroke.

This is texture-channel/atlas encoding, not a new drawing. It does not activate
the banner or write runtime XML. The user imports the DDS/material in the Kit.
"""

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
REVIEW = ROOT / "GreyWardenPolicePurity/docs/reference/banner-review"
SOURCE = REVIEW / "commander-banner-complete-v1.png"
NAME = "gwp_commander_banner"
BANNER_CODE = (
    "11.63001.63001.1528.1528.764.764.1.0.0."
    "63010.63002.63002.512.512.764.764.0.0.0"
)
BACKGROUND = (26, 26, 26)
GOLD = (253, 220, 136)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--live-module", type=Path,
        default=Path("D:/steam/steamapps/common/Mount & Blade II Bannerlord/Modules/GreyWarden"),
    )
    args = parser.parse_args()
    source = Image.open(SOURCE).convert("RGB")
    # The approved image has a near-constant red foreground (~253) and charcoal
    # background (~26). Unmix that channel to retain edge coverage and all cuts.
    # Interior lighting variations are flattened, as native faction tint supplies
    # one solid color. No feather geometry is invented or mirrored here.
    mask = source.getchannel("R").point(
        [round(max(0, min(1, (v - 32) / 203)) * 255) for v in range(256)]
    )
    # Native BannerVisual always samples one cell from a 4x4 texture atlas.
    # A 16px tile margin protects fine wing/tail tips from adjacent UV cells.
    tile_alpha = Image.new("L", (512, 512), 0)
    scaled = mask.resize((480, 480), Image.Resampling.LANCZOS)
    tile_alpha.paste(scaled, (16, 16))
    atlas_alpha = Image.new("L", (2048, 2048), 0)
    atlas_alpha.paste(tile_alpha, (0, 0))
    atlas = Image.new("RGBA", (2048, 2048), (0, 255, 0, 0))
    atlas.putalpha(atlas_alpha)

    output = REVIEW / "kit-input"
    output.mkdir(exist_ok=True)
    png = output / f"{NAME}.png"
    atlas.save(png)
    # Uncompressed DDS avoids block artifacts. One mip, RGBA, real alpha.
    live_source = args.live_module / "AssetSources/BannerIcons"
    live_source.mkdir(parents=True, exist_ok=True)
    (args.live_module / "Assets/BannerIcons").mkdir(parents=True, exist_ok=True)
    dds = live_source / f"{NAME}.dds"
    atlas.save(dds, format="DDS")

    xml = '''<?xml version="1.0" encoding="utf-8"?>
<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" type="string">
  <BannerIconData>
    <BannerIconGroup id="63" name="{=!}Grey Warden" is_pattern="false">
      <Icon id="63010" material_name="gwp_commander_banner" texture_index="0" is_reserved="true" />
    </BannerIconGroup>
    <BannerColors>
      <Color id="63001" hex="0xFF1A1A1A" />
      <Color id="63002" hex="0xFFFDDC88" />
    </BannerColors>
  </BannerIconData>
</base>
'''
    (output / "banner_icons.xml").write_text(xml, encoding="utf-8")
    (output / "banner-code.txt").write_text(BANNER_CODE + "\n", encoding="utf-8")
    # Preview only: the same mask/tint used by the native shader, not a game shot.
    preview = Image.new("RGB", (800, 600), "#eeeeee")
    draw = ImageDraw.Draw(preview)
    for size, x, y in [(512, 16, 35), (128, 610, 55), (64, 642, 260)]:
        alpha = tile_alpha.resize((size, size), Image.Resampling.LANCZOS)
        swatch = Image.new("RGB", (size, size), BACKGROUND)
        swatch.paste(Image.new("RGB", (size, size), GOLD), (0, 0), alpha)
        preview.paste(swatch, (x, y))
        draw.text((x, y + size + 8), f"{size} x {size}", fill="#222222")
    draw.text((16, 12), "Native mask + faction tint (offline preview)", fill="#222222")
    preview.save(output / "native-mask-preview.png")

    # Read the actual DDS back; verify engine tile/channel constraints, not merely
    # the pre-encoding image. This also catches writer/format changes in Pillow.
    decoded = Image.open(dds).convert("RGBA")
    assert decoded.size == (2048, 2048)
    assert decoded.getchannel("R").getextrema() == (0, 0)
    assert decoded.getchannel("G").getextrema() == (255, 255)
    assert decoded.getchannel("B").getextrema() == (0, 0)
    assert decoded.getchannel("A").tobytes() == atlas_alpha.tobytes()
    assert decoded.getchannel("A").getbbox()[2] <= 512
    assert decoded.getchannel("A").getbbox()[3] <= 512
    manifest = {
        "status": "Prepared; Kit texture/material import and runtime activation pending",
        "approved_art": str(SOURCE), "art_sha256": sha256(SOURCE),
        "live_dds": str(dds), "dds_sha256": sha256(dds),
        "atlas_png_sha256": sha256(png),
        "material_name": NAME, "shader": "gui_color_and_stroke",
        "native_shader_guid": "b96efd31-7127-46ec-8648-b28d23ece108",
        "texture_index": 0, "icon_id": 63010, "group_id": 63,
        "background_color_id": 63001, "sigil_color_id": 63002,
        "banner_code": BANNER_CODE,
        "dds_readback": "PASS: 2048 RGBA; red/blue zero, green full, alpha exact; only cell 0 occupied",
    }
    (output / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(json.dumps(manifest, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()

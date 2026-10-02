"""Build the map-bar "Grey Warden affairs" icon from the commander chest griffon.

Colour and tone follow the native map-bar icons: each luminance level is mapped to the
mean colour vanilla icons use at that level, and the tone distribution is matched to the
vanilla clan icon (mapbar_icon7). The engine applies the bar's ColorFactor at draw time,
so it is not baked in.

usage: python make_affairs_icon.py <wcommat_d.png> <vanilla icon dir> <out.png>
The vanilla icons (mapbar_icon3/6/7.png) are cropped from SandBox gauntlet_ui.tpac,
sheet ui_mapbar_1, at the coordinates in SandBoxSpriteData.xml.
"""
import sys
import numpy as np
from PIL import Image, ImageFilter

CHEST_REGION = (2245, 1220, 2535, 1850)   # griffon on the chest, wcommat_d 4096²
STRIPE_ROWS = (1550, 1686)                # gold stripe joining the griffon on its left
STRIPE_MAX_X = 2270
OUT_BOX = (180, 120)                      # 3x the 60x40 icon slot


def griffon_mask(path):
    rgb = np.asarray(Image.open(path).convert("RGB").crop(CHEST_REGION)).astype(int)
    m = (rgb[:, :, 0] > 150) & (rgb[:, :, 1] > 120) & (rgb[:, :, 0] - rgb[:, :, 2] > 40)
    x0, y0 = CHEST_REGION[0], CHEST_REGION[1]
    m[STRIPE_ROWS[0] - y0:STRIPE_ROWS[1] - y0, :STRIPE_MAX_X - x0] = False
    mask = Image.fromarray((m * 255).astype("uint8"))
    return mask.crop(mask.getbbox())


def vanilla_palette(icon_dir):
    px = []
    for n in ("mapbar_icon7", "mapbar_icon6", "mapbar_icon3"):
        q = np.asarray(Image.open(f"{icon_dir}/{n}.png").convert("RGBA")).astype(float)
        px.append(q[q[:, :, 3] > 200][:, :3])
    px = np.concatenate(px)
    lum = px.mean(1)
    lut = np.full((256, 3), np.nan)
    for v in range(256):
        sel = np.abs(lum - v) < 6
        if sel.sum() > 20:
            lut[v] = px[sel].mean(0)
    idx = np.arange(256)
    for c in range(3):
        ok = ~np.isnan(lut[:, c])
        lut[:, c] = np.interp(idx, idx[ok], lut[ok, c])
    lion = np.asarray(Image.open(f"{icon_dir}/mapbar_icon7.png").convert("RGBA")).astype(float)
    ref = np.sort(lion[lion[:, :, 3] > 200][:, :3].mean(1))
    return lut, ref


def render(mask, lut, ref):
    ss = 4
    s = min((OUT_BOX[0] - 8) / mask.width, (OUT_BOX[1] - 8) / mask.height)
    tw, th = round(mask.width * s), round(mask.height * s)
    big = mask.resize((tw * ss, th * ss), Image.LANCZOS)
    pad = 4 * ss
    M = Image.new("L", (big.width + 2 * pad, big.height + 2 * pad))
    M.paste(big, (pad, pad))
    w, h = M.size
    k = OUT_BOX[1] / 40 * ss                       # pixels per icon-slot pixel
    mf = np.asarray(M).astype(float) / 255
    height = np.asarray(M.filter(ImageFilter.GaussianBlur(1.4 * k))).astype(float) / 255
    gy, gx = np.gradient(height)
    shade = np.clip(0.5 + (-gx - gy) * k * 2.5, 0, 1)        # light from top-left
    inner = np.asarray(M.filter(ImageFilter.MinFilter(int(2 * k) | 1))
                       .filter(ImageFilter.GaussianBlur(k))).astype(float) / 255
    raw = shade * 0.7 + inner * 0.3 + np.linspace(0.08, -0.08, h)[:, None]
    outline = np.asarray(M.filter(ImageFilter.MaxFilter(int(k) | 1))
                         .filter(ImageFilter.GaussianBlur(k * 0.5))).astype(float) / 255
    out = np.zeros((h, w, 4))
    out[:, :, :3] = (raw * 255 * mf + 6 * (1 - mf))[:, :, None]   # near-black outline like vanilla
    out[:, :, 3] = np.clip(np.maximum(mf, outline), 0, 1) * 255
    icon = Image.fromarray(np.clip(out, 0, 255).astype("uint8"), "RGBA").resize((w // ss, h // ss), Image.LANCZOS)
    icon = icon.crop(icon.getbbox())
    # match tone to the vanilla clan icon, then colour by luminance
    a = np.asarray(icon).astype(float)
    op = a[:, :, 3] > 200
    l = a[:, :, :3].mean(2)
    vals = l[op]
    rank = np.argsort(np.argsort(vals)) / max(1, len(vals) - 1)
    l[op] = ref[(rank * (len(ref) - 1)).astype(int)]
    a[op, :3] = lut[np.clip(l[op], 0, 255).astype(int)]
    return Image.fromarray(a.astype("uint8"), "RGBA")


if __name__ == "__main__":
    lut, ref = vanilla_palette(sys.argv[2])
    render(griffon_mask(sys.argv[1]), lut, ref).save(sys.argv[3])

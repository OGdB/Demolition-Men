"""Bring any image onto the game's pixel grid and palette.

    python tools/artpipe/normalize.py in.png out.png --theme Port --size 32x32
        [--mode box|nearest] [--outline] [--trim] [--pivot center|bottom]

Steps: downscale to the target grid, hard alpha threshold, palette quantize (nearest in
CIELAB), optional 1 px inner outline in the darkest palette colour, optional trim and
re-pad so the size is a multiple of 32 around the chosen pivot.
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True  # no __pycache__ next to the tools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import palette  # noqa: E402

THEMES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "themes")


def load_theme(name):
    with open(os.path.join(THEMES, name.lower() + ".json"), encoding="utf-8") as f:
        return json.load(f)


def rgb_to_lab(rgb):
    """sRGB (..., 3) uint8/float 0..255 -> CIELAB (D65)."""
    c = np.asarray(rgb, dtype=np.float64) / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = c @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def downscale(img, size, mode="box"):
    """Resize an RGBA image to size (w, h) on the pixel grid."""
    resample = Image.BOX if mode == "box" else Image.NEAREST
    return img.convert("RGBA").resize(size, resample)


def threshold_alpha(arr, cut=128):
    """Hard alpha: every pixel fully opaque or fully transparent (RGB zeroed)."""
    arr = arr.copy()
    solid = arr[..., 3] >= cut
    arr[..., 3] = np.where(solid, 255, 0)
    arr[~solid, :3] = 0
    return arr


def quantize(arr, colors):
    """Map every opaque pixel to the nearest colour (CIELAB distance) of `colors`."""
    pal = np.array(sorted(colors), dtype=np.uint8)
    lab_pal = rgb_to_lab(pal)
    solid = arr[..., 3] > 0
    lab = rgb_to_lab(arr[solid][:, :3])
    idx = np.argmin(((lab[:, None, :] - lab_pal[None, :, :]) ** 2).sum(-1), axis=1)
    out = arr.copy()
    out[solid, :3] = pal[idx]
    return out


def outline(arr, color, edges=(True, True, True, True)):
    """1 px inner outline: opaque pixels touching transparency (4-neighbour) get `color`.

    `edges` = (top, bottom, left, right): whether the image border counts as transparency.
    Tileable edges pass False so the outline does not double up across a seam.
    """
    solid = arr[..., 3] > 0
    pad = np.pad(solid, 1, constant_values=False)
    top, bottom, left, right = edges
    if not top:
        pad[0, 1:-1] = solid[0]
    if not bottom:
        pad[-1, 1:-1] = solid[-1]
    if not left:
        pad[1:-1, 0] = solid[:, 0]
    if not right:
        pad[1:-1, -1] = solid[:, -1]
    inner = pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:]
    edge = solid & ~inner
    out = arr.copy()
    out[edge, :3] = color
    return out


def trim_and_pivot(arr, pivot="center", unit=32):
    """Crop to the opaque bounding box, then pad to a multiple of `unit` so the content
    sits on the pivot (centre, or bottom-centre for props standing on the ground)."""
    ys, xs = np.nonzero(arr[..., 3])
    if len(xs) == 0:
        return arr
    arr = arr[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h, w = arr.shape[:2]
    W, H = -(-w // unit) * unit, -(-h // unit) * unit
    out = np.zeros((H, W, 4), np.uint8)
    x0 = (W - w) // 2
    y0 = H - h if pivot == "bottom" else (H - h) // 2
    out[y0:y0 + h, x0:x0 + w] = arr
    return out


def normalize(img, theme, size=None, mode="box", do_outline=False, trim=False, pivot="center"):
    if size:
        img = downscale(img, size, mode)
    arr = threshold_alpha(np.array(img.convert("RGBA")))
    colors = palette.theme_colors(theme)
    arr = quantize(arr, colors)
    if do_outline:
        darkest = min(colors, key=lambda c: rgb_to_lab(c)[0])
        arr = outline(arr, darkest)
    if trim:
        arr = trim_and_pivot(arr, pivot)
    return arr


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("src")
    ap.add_argument("dst")
    ap.add_argument("--theme", required=True)
    ap.add_argument("--size", help="WxH target grid, e.g. 32x64")
    ap.add_argument("--mode", choices=["box", "nearest"], default="box")
    ap.add_argument("--outline", action="store_true")
    ap.add_argument("--trim", action="store_true")
    ap.add_argument("--pivot", choices=["center", "bottom"], default="center")
    a = ap.parse_args()
    size = tuple(int(v) for v in a.size.lower().split("x")) if a.size else None
    arr = normalize(Image.open(a.src), load_theme(a.theme), size, a.mode, a.outline, a.trim, a.pivot)
    Image.fromarray(arr, "RGBA").save(a.dst)
    print(f"{a.dst}: {arr.shape[1]}x{arr.shape[0]}")


if __name__ == "__main__":
    main()

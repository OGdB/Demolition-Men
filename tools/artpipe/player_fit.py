"""Fit a generated player pose to the player sheet.

The character's colours snap to the 18 colours of the hand-drawn player sheet; only
colours far from all of them (props: a vest, a blueprint, a radio) snap to the master
palette's material and accent ramps. Alpha becomes hard.

    python tools/artpipe/player_fit.py in.png out.png [--scale 0.5] [--sign-border]

--scale shrinks with a box filter first (a render whose warning sign is wider than the
sheet's 14 px). --sign-border adds the sheet's 1 px white ring around the sign.
"""
import argparse
import os

import numpy as np
from PIL import Image

from palette import RAMPS

SHEET_PNG = os.path.join(os.path.dirname(__file__), "..", "..", "Demolition Men", "Assets",
                         "Animations", "Player_1", "+Demolition_man.png")
PROP_RAMPS = ("brick", "wood", "steel", "concrete", "rust", "hazard", "sea", "warm", "leaf",
              "nightglass")
SIGN_YELLOW = {(0xFF, 0xC0, 0x55), (0xFF, 0xE0, 0x8B)}
NEAR_SHEET = 45  # RGB distance under which a colour belongs to the character


def sheet_colors():
    im = Image.open(SHEET_PNG).convert("RGBA")
    return np.array(sorted({p[:3] for p in im.get_flattened_data() if p[3] == 255}), float)


def snap(im, sheet):
    props = np.array([c for k in PROP_RAMPS for c in RAMPS[k]], float)
    both = np.vstack([sheet, props])
    a = np.array(im.convert("RGBA")).astype(float)
    rgb = a[:, :, :3].reshape(-1, 3)
    d = ((rgb[:, None] - sheet[None]) ** 2).sum(2)
    near = d.min(1) < NEAR_SHEET ** 2
    far = both[((rgb[:, None] - both[None]) ** 2).sum(2).argmin(1)]
    col = np.where(near[:, None], sheet[d.argmin(1)], far)
    out = np.zeros_like(a)
    out[:, :, :3] = col.reshape(a.shape[0], a.shape[1], 3)
    out[:, :, 3] = np.where(a[:, :, 3] >= 128, 255, 0)
    return Image.fromarray(out.astype("uint8"))


def sign_border(im):
    a = np.array(im)
    h, w = a.shape[:2]
    yellow = np.array([[tuple(a[y, x, :3]) in SIGN_YELLOW and a[y, x, 3] > 0 for x in range(w)]
                       for y in range(h)], bool)
    if not yellow.any():
        return im
    ys, xs = np.nonzero(yellow)
    box = np.zeros_like(yellow)
    box[ys.min():ys.max() + 1, xs.min():xs.max() + 1] = True
    black = (a[:, :, :3].sum(2) == 0) & (a[:, :, 3] > 0)
    sign = yellow | (black & box)
    grown = sign.copy()
    grown[1:] |= sign[:-1]
    grown[:-1] |= sign[1:]
    grown[:, 1:] |= sign[:, :-1]
    grown[:, :-1] |= sign[:, 1:]
    a[grown & ~sign] = (255, 255, 255, 255)
    return Image.fromarray(a)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("src")
    ap.add_argument("dst")
    ap.add_argument("--scale", type=float, default=1.0)
    ap.add_argument("--sign-border", action="store_true")
    args = ap.parse_args()

    im = Image.open(args.src).convert("RGBA")
    im = im.crop(im.getbbox())
    if args.scale != 1.0:
        im = im.resize((round(im.width * args.scale), round(im.height * args.scale)), Image.BOX)
    sheet = sheet_colors()
    im = snap(im, sheet)
    if args.sign_border:
        im = sign_border(im)
    allowed = {tuple(int(v) for v in c) for c in np.vstack([sheet, [c for k in PROP_RAMPS for c in RAMPS[k]]])}
    allowed.add((255, 255, 255))
    used = {p[:3] for p in im.get_flattened_data() if p[3]}
    assert used <= allowed, f"off-palette colours: {sorted(used - allowed)[:5]}"
    im.save(args.dst)
    print(f"{args.dst}: {im.width}x{im.height}, {len(used)} colours")


if __name__ == "__main__":
    main()

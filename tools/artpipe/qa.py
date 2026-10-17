"""QA gate for a theme's sprites. Exit code 1 on any failure.

    python tools/artpipe/qa.py <Theme>

Checks every PNG in Demolition Men/Assets/Content/<Theme>/Sprites/: 100% theme palette
compliance, hard alpha, exact expected size (multiple of 32 px), a .meta next to it, and
seam continuity on the axes a sprite tiles along. Writes content-work/<theme>/contact.png:
every sprite at 4x, labelled, beside a crop of the reference object set, plus tiled
previews and a mock street on the theme's sky colour.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True  # no __pycache__ next to the tools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import palette  # noqa: E402
from normalize import load_theme  # noqa: E402
from tiles import SPRITES  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(ROOT, "Demolition Men", "Assets")
REFERENCE = os.path.join(ASSETS, "Sprites", "Demolition men object set.png")


def sprite_dir(theme_name):
    return os.path.join(ASSETS, "Content", theme_name, "Sprites")


def seam_problem(arr, axis):
    """None if the wrap-around seam on `axis` looks like any interior pixel pair, else why."""
    a = arr.astype(int)
    if axis == "y":
        a = a.transpose(1, 0, 2)
    if not np.array_equal(a[:, 0, 3], a[:, -1, 3]):
        return f"{axis} seam: alpha differs between opposite edges"
    solid = (a[:, 0, 3] > 0) & (a[:, -1, 3] > 0)
    if not solid.any():
        return None
    interior = np.abs(a[solid, 1:, :3] - a[solid, :-1, :3]).sum(-1).mean(0)
    wrap = np.abs(a[solid, 0, :3] - a[solid, -1, :3]).sum(-1).mean()
    limit = np.percentile(interior, 90) + 1
    if wrap > limit:
        return f"{axis} seam: edge mismatch {wrap:.0f} > {limit:.0f}"
    return None


def check(path, allowed):
    """List of problems with one sprite."""
    name = os.path.splitext(os.path.basename(path))[0]
    arr = np.array(Image.open(path).convert("RGBA"))
    h, w = arr.shape[:2]
    problems = []
    if name in SPRITES:
        (ew, eh), axes, _ = SPRITES[name]
        if (w, h) != (ew, eh):
            problems.append(f"size {w}x{h}, expected {ew}x{eh}")
    else:
        axes = ""
    if w % 32 or h % 32:
        problems.append(f"size {w}x{h} not a multiple of 32")
    semi = int(((arr[..., 3] > 0) & (arr[..., 3] < 255)).sum())
    if semi:
        problems.append(f"{semi} semi-transparent pixels")
    solid = arr[..., 3] == 255
    colors = {tuple(c) for c in np.unique(arr[solid][:, :3], axis=0)}
    off = colors - allowed
    if off:
        problems.append(f"{len(off)} off-palette colours, e.g. {sorted(off)[:3]}")
    if not solid.any():
        problems.append("empty sprite")
    if not os.path.exists(path + ".meta"):
        problems.append("missing .meta")
    for axis in axes:
        p = seam_problem(arr, axis)
        if p:
            problems.append(p)
    return problems


# --- contact sheet ---------------------------------------------------------------------

def font(size):
    try:
        return ImageFont.load_default(size)
    except TypeError:  # Pillow < 10.1
        return ImageFont.load_default()


def scaled(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)


def sky_rgb(theme):
    return tuple(int(round(v * 255)) for v in theme.get("sky", (0.5, 0.5, 0.5)))


def mock_street(sprites, theme):
    """A small street built from the theme's sprites, on its sky colour (2x)."""
    cols, rows, ground = 15, 10, 3
    img = Image.new("RGBA", (cols * 32, rows * 32), sky_rgb(theme) + (255,))

    def place(name, x, y):  # x, y in cells; y counts up from the ground surface
        if name not in sprites:
            return
        s = sprites[name]
        img.alpha_composite(s, (x * 32, (rows - ground) * 32 - y * 32 - s.height))

    for x in range(cols):
        place("ground_top", x, -1)
        place("ground_fill", x, -2)
        place("ground_fill", x, -3)
    walls = ["brick", "brick_1", "brick_2"]
    for y in (0, 3):
        for x in range(1, 7):
            if (x, y) == (5, 0):  # open interior held by a support column
                continue
            place(walls[(x * 7 + y) % 3], x, y)
            place(walls[(x * 5 + y + 1) % 3], x, y + 1)
    for x in range(1, 7):
        place("plank", x, 2)
    place("support", 5, 0)
    place("support", 5, 1)
    place("door", 2, 0)
    for x, y in ((4, 0), (2, 3), (4, 3), (6, 3)):
        place("window_back", x, y)
        place("glass", x, y)
    for x in (1, 3, 5):
        place("brick_cap", x, 5)
    place("chimney", 5, 6)
    for y in range(3):
        place("metal_truss", 8, y)
    place("beam", 8, 3)
    place("brick_tall", 11, 0)
    place("concrete", 12, 0)
    place("concrete", 12, 1)
    place("wood", 13, 0)
    place("brick", 11, 2)
    return scaled(img, 2)


def contact_sheet(theme, sprites, results, out_path):
    f, fb = font(14), font(22)
    sky = sky_rgb(theme)
    order = [n for n in SPRITES if n in sprites] + sorted(n for n in sprites if n not in SPRITES)

    # sprite grid at 4x, flowing into rows
    max_w, pad, label_h = 1400, 16, 20
    tiles, x, y, row_h = [], 0, 0, 0
    for n in order:
        s = scaled(sprites[n], 4)
        if x + s.width > max_w:
            x, y, row_h = 0, y + row_h + label_h + pad, 0
        tiles.append((n, s, x, y))
        x += max(s.width, 128) + pad
        row_h = max(row_h, s.height)
    grid_h = y + row_h + label_h

    ref = Image.open(REFERENCE).convert("RGBA").crop((8, 24, 168, 152))
    ref_bg = Image.new("RGBA", ref.size, (135, 191, 235, 255))
    ref_bg.alpha_composite(ref)
    ref = scaled(ref_bg, 4)

    street = mock_street(sprites, theme)

    # tiled previews: each tileable sprite repeated 3x2 at 2x
    previews = []
    for n in order:
        axes = SPRITES.get(n, (None, ""))[1]
        if not axes:
            continue
        s = sprites[n]
        nx, ny = (3 if "x" in axes else 1), (2 if "y" in axes else 1)
        t = Image.new("RGBA", (s.width * nx, s.height * ny), sky + (255,))
        for i in range(nx):
            for j in range(ny):
                t.alpha_composite(s, (i * s.width, j * s.height))
        previews.append((n, scaled(t, 2)))

    left_w = ref.width + pad
    width = left_w + max_w + pad * 2
    prev_h = max(p.height for _, p in previews) + label_h if previews else 0
    height = 60 + max(ref.height, grid_h) + pad * 2 + max(street.height, prev_h * 2 + pad) + pad * 2
    sheet = Image.new("RGBA", (width, height), (40, 40, 48, 255))
    d = ImageDraw.Draw(sheet)

    failed = sum(1 for p in results.values() if p)
    d.text((pad, 14), f"{theme['name']}  ({len(sprites)} sprites, {failed} failing QA)", fill="white", font=fb)
    # palette swatches
    sx = 520
    for name in palette.theme_ramp_names(theme):
        for c in palette.RAMPS[name]:
            d.rectangle((sx, 14, sx + 18, 38), fill=c)
            sx += 20
        sx += 8

    top = 60
    sheet.alpha_composite(ref, (pad, top))
    d.text((pad, top + ref.height + 2), "reference: Demolition men object set (4x)", fill=(200, 200, 200), font=f)
    for n, s, x, y in tiles:
        ox, oy = left_w + pad + x, top + y
        bg = Image.new("RGBA", s.size, sky + (255,))
        bg.alpha_composite(s)
        sheet.alpha_composite(bg, (ox, oy))
        bad = results.get(n)
        d.text((ox, oy + s.height + 2), n + ("  FAIL" if bad else ""), fill=(255, 90, 90) if bad else "white", font=f)

    y2 = top + max(ref.height + label_h, grid_h) + pad * 2
    sheet.alpha_composite(street, (pad, y2))
    d.text((pad, y2 + street.height + 2), "mock street (2x) on theme sky", fill=(200, 200, 200), font=f)
    px, py = pad * 2 + street.width, y2
    for n, p in previews:
        if px + p.width > width - pad:
            px, py = pad * 2 + street.width, py + prev_h + pad
        sheet.alpha_composite(p, (px, py))
        d.text((px, py + p.height + 2), n + " tiled", fill=(200, 200, 200), font=f)
        px += p.width + pad
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    sheet.convert("RGB").save(out_path)


def run(theme_name):
    theme = load_theme(theme_name)
    name = theme["name"]
    folder = sprite_dir(name)
    allowed = palette.theme_colors(theme)
    pngs = sorted(p for p in os.listdir(folder) if p.lower().endswith(".png")) if os.path.isdir(folder) else []
    results, sprites = {}, {}
    for p in pngs:
        n = os.path.splitext(p)[0]
        results[n] = check(os.path.join(folder, p), allowed)
        sprites[n] = Image.open(os.path.join(folder, p)).convert("RGBA")
    for n in SPRITES:
        if n not in results:
            results[n] = ["missing"]
    ok = True
    for n, problems in results.items():
        print(f"  {'FAIL' if problems else 'ok  '} {n}" + (": " + "; ".join(problems) if problems else ""))
        ok &= not problems
    out = os.path.join(ROOT, "content-work", name, "contact.png")
    contact_sheet(theme, sprites, results, out)
    print(f"{name}: {len(pngs)} sprites, {'PASS' if ok else 'FAIL'}; contact sheet {os.path.relpath(out, ROOT)}")
    return ok


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    sys.exit(0 if run(sys.argv[1]) else 1)

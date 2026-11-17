"""Procedural material generators, deterministic from a seed.

Each generator draws with palette ramps only (step 0 = darkest = outline/mortar, light from
the top-left) and returns an RGBA numpy array with hard alpha. Which ramp and look each
material uses comes from the theme's "materials" block (themes/*.json).
"""
import glob
import json
import os
import zlib
from types import SimpleNamespace

import numpy as np

import palette
from backdrops import backdrop

CONTENT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Demolition Men", "Assets", "Content")

# name: (size w x h, tileable axes, foreground). Shared with qa.py.
SPRITES = {
    "brick":       ((32, 32), "xy", True),
    "brick_1":     ((32, 32), "xy", True),
    "brick_2":     ((32, 32), "xy", True),
    "brick_tall":  ((32, 64), "xy", True),
    "concrete":    ((32, 32), "xy", True),
    "concrete_1":  ((32, 32), "xy", True),
    "wood":        ((32, 32), "xy", True),
    # the building's corners (<wall>_l / _r): the same walls with the theme's corner trim
    "brick_l":     ((32, 32), "y", True),
    "brick_l_1":   ((32, 32), "y", True),
    "brick_r":     ((32, 32), "y", True),
    "brick_r_1":   ((32, 32), "y", True),
    "brick_tall_l": ((32, 64), "y", True),
    "brick_tall_r": ((32, 64), "y", True),
    "concrete_l":  ((32, 32), "y", True),
    "concrete_r":  ((32, 32), "y", True),
    "wood_l":      ((32, 32), "y", True),
    "wood_r":      ((32, 32), "y", True),
    "glass":       ((32, 64), "", True),
    "glass_1":     ((32, 64), "", True),
    "glass_2":     ((32, 64), "", True),
    "door":        ((32, 64), "", True),
    "plank":       ((32, 32), "x", True),
    "metal_truss": ((64, 32), "", True),
    "beam":        ((64, 32), "x", True),
    "brick_cap":   ((64, 32), "x", True),
    "support":     ((32, 32), "y", True),
    # a column of plated steel wall, a scaffold pole (walk-through), a ladder (behind the blocks)
    "steel":       ((32, 32), "y", True),
    "pole":        ((32, 32), "y", True),
    "ladder":      ((32, 32), "y", False),
    "chimney":     ((32, 32), "", True),
    "chimney_1":   ((32, 32), "", True),
    "chimney_2":   ((32, 32), "", True),
    # lit overlays of blocks (only the pixels that glow; the game draws them unlit at night)
    "glass_lit":   ((32, 64), "", True),
    "glass_1_lit": ((32, 64), "", True),
    "door_lit":    ((32, 64), "", True),
    "chimney_lit": ((32, 32), "", True),
    "chimney_1_lit": ((32, 32), "", True),
    "chimney_2_lit": ((32, 32), "", True),
    # back walls: one wallpaper per storey
    "interior":    ((32, 32), "xy", False),
    "interior_1":  ((32, 32), "xy", False),
    "interior_2":  ((32, 32), "xy", False),
    "interior_top": ((32, 32), "x", False),
    "interior_top_1": ((32, 32), "x", False),
    "interior_top_2": ((32, 32), "x", False),
    # street props (breakable) and dressing (cosmetic)
    "crate":       ((32, 32), "", True),
    "car":         ((64, 32), "", True),
    "barrel":      ((32, 32), "", True),
    "container":   ((96, 64), "", True),
    "lamp":        ((32, 96), "", True),
    "fence":       ((32, 32), "x", True),
    "bush":        ((32, 32), "", True),
    "bush_1":      ((32, 32), "", True),
    "bin":         ((32, 32), "", True),
    "bin_1":       ((32, 32), "", True),
    "sign":        ((32, 64), "", True),
    "sign_1":      ((32, 64), "", True),
    "sign_2":      ((32, 64), "", True),
    "ground_top":  ((32, 32), "x", True),
    "ground_fill": ((32, 32), "xy", True),
    "backdrop_sky":  ((512, 384), "x", False),
    "backdrop_sun":  ((64, 64), "", False),
    "backdrop_far":  ((512, 192), "x", False),
    "backdrop_mid":  ((512, 192), "x", False),
    "backdrop_near": ((512, 192), "x", False),
}

# Furnishing: the props of a building's rooms and its roof sign, sprites `prop_<name>` (and
# `prop_<name>_lit`). The catalog is the game's (Furnishing.cs; build_theme.py checks the two
# agree): name -> (cells wide, cells high, has a lit overlay).
PROPS = {
    # floor: stands on the sprite's bottom edge
    "bar": (2, 1, True), "speaker": (1, 2, False), "djdesk": (2, 1, True), "counter": (2, 1, False),
    "fridge": (1, 2, True), "rack": (1, 2, False), "cabinet": (1, 2, True), "slot": (1, 2, True),
    "desk": (2, 1, True), "sofa": (2, 1, False), "bed": (2, 1, False), "table": (2, 1, False),
    "plant": (1, 1, False), "filing": (1, 2, False), "workbench": (2, 1, False), "pallet": (2, 1, False),
    "racking": (1, 2, False), "machine": (2, 2, True), "tank": (1, 2, False), "vault": (2, 2, True),
    # wall: in the middle of its cell
    "poster": (1, 1, False), "picture": (1, 1, False), "clock": (1, 1, False), "shelf": (1, 1, False),
    "pipe": (1, 1, False), "board": (1, 1, False), "tv": (1, 1, True), "neon": (2, 1, True),
    "bottles": (2, 1, True),
    # ceiling: hangs from the sprite's top edge
    "lamp": (1, 1, True), "tube": (2, 1, True), "discoball": (1, 1, True), "spot": (1, 1, True),
}
# roof signs (`prop_sign_<word>`): stand on the bottom edge; a new word only needs its name here
SIGN_WORDS = ("club", "hotel", "kiosk", "arcade", "shop", "bank", "casino", "depot")
PROPS.update({"sign_" + _word: (2, 1, True) for _word in SIGN_WORDS})
for _name, (_w, _h, _glows) in PROPS.items():
    SPRITES["prop_" + _name] = ((_w * 32, _h * 32), "", False)
    if _glows:
        SPRITES["prop_" + _name + "_lit"] = ((_w * 32, _h * 32), "", False)


def step(r, i):
    """Ramp step i, clamped for 3-step ramps."""
    return r[min(i, len(r) - 1)]


def canvas(w, h):
    return np.zeros((h, w, 4), np.uint8)


def rect(a, x0, y0, x1, y1, c):
    """Fill [x0, x1) x [y0, y1) with colour c."""
    a[max(y0, 0):y1, max(x0, 0):x1] = (*c, 255)


def put(a, x, y, c, wrap=True):
    h, w = a.shape[:2]
    if wrap:
        a[y % h, x % w] = (*c, 255)
    elif 0 <= x < w and 0 <= y < h:
        a[y, x] = (*c, 255)


def speckle(a, rng, region, colors_probs):
    """Scatter single pixels of the given colours over the opaque pixels of a region mask."""
    ys, xs = np.nonzero(region)
    roll = rng.random(len(xs))
    acc = 0.0
    for c, p in colors_probs:
        hit = (roll >= acc) & (roll < acc + p)
        a[ys[hit], xs[hit]] = (*c, 255)
        acc += p


def shade(mask, r, edges=(True, True, True, True)):
    """Auto-shade a shape: outline r0, top/left inner ring r3, bottom/right ring r1, body r2.

    edges = (top, bottom, left, right): whether the image border counts as outside.
    """
    def outside_of(m):
        pad = np.pad(~m, 1, constant_values=True)
        top, bottom, left, right = edges
        if not top:
            pad[0, 1:-1] = ~m[0]
        if not bottom:
            pad[-1, 1:-1] = ~m[-1]
        if not left:
            pad[1:-1, 0] = ~m[:, 0]
        if not right:
            pad[1:-1, -1] = ~m[:, -1]
        return pad[:-2, 1:-1], pad[2:, 1:-1], pad[1:-1, :-2], pad[1:-1, 2:]

    up, down, left, right = outside_of(mask)
    edge = mask & (up | down | left | right)
    core = mask & ~edge
    up, down, left, right = outside_of(core)
    lit = core & (up | left)
    dark = core & (down | right) & ~lit
    a = canvas(mask.shape[1], mask.shape[0])
    for m, c in ((mask, step(r, 2)), (lit, step(r, 3)), (dark, r[1]), (edge, r[0])):
        a[m] = (*c, 255)
    return a


def neon_tube(a, x0, y0, x1, y1, ramp):
    """A neon strip: bright core with a 1 px glow either side (horizontal or vertical)."""
    if y1 - y0 <= x1 - x0:  # horizontal
        rect(a, x0, y0, x1, y0 + 3, ramp[1])
        rect(a, x0, y0 + 1, x1, y0 + 2, ramp[2])
    else:
        rect(a, x0, y0, x0 + 3, y1, ramp[1])
        rect(a, x0 + 1, y0, x0 + 2, y1, ramp[2])


def hazard_stripes(a, x0, y0, x1, y1, light, dark, width=3):
    for y in range(y0, y1):
        for x in range(x0, x1):
            a[y, x] = (*(light if ((x + y) // width) % 2 == 0 else dark), 255)


# --- walls -----------------------------------------------------------------------------

def draw_brick(a, rng, r, x, y0, ln, course, style):
    """One brick of a running bond at (x, y0), `ln` wide, `course` tall, x wrapping."""
    if style == "brick":
        # pillow brick: 2 px mortar all round (1 px each side), rounded corners, lit top-left
        dark = rng.random() < 0.18
        body, hi, lo = (r[1], r[2], r[0]) if dark else (r[2], r[3], r[1])
        x0, x1, y1 = x + 1, x + ln - 1, y0 + course - 1
        for j in range(y0 + 1, y1):
            for i in range(x0, x1):
                corner = (i in (x0, x1 - 1)) and (j in (y0 + 1, y1 - 1))
                if corner:
                    continue
                c = body
                if j == y0 + 1 or (i == x0 and j < y1 - 1):
                    c = hi
                elif j == y1 - 1 or i == x1 - 1:
                    c = lo
                put(a, i, j, c)
        put(a, x0 + 1, y0 + 2, hi)                      # the pillow's gleam
        for _ in range(rng.integers(0, 3)):             # chips
            put(a, x0 + rng.integers(2, max(3, ln - 4)), y0 + rng.integers(3, course - 2), lo)
        return
    # flat cast block or precast panel: 1 px joint, bevel top-left, shade bottom-right
    dark = rng.random() < 0.2
    body, hi, lo = (r[1], r[2], r[0]) if dark else (r[2], r[3], r[1])
    for j in range(y0 + 1, y0 + course):
        for i in range(x + 1, x + ln):
            c = body
            if j == y0 + 1 or i == x + 1:
                c = hi
            elif j == y0 + course - 1 or i == x + ln - 1:
                c = lo
            put(a, i, j, c)
    for _ in range(rng.integers(2, 6)):
        put(a, x + rng.integers(2, ln - 1), y0 + rng.integers(2, course - 1), lo)
    if style == "panel":
        if rng.random() < 0.6:  # weathering streak from the top joint
            sx = x + rng.integers(4, ln - 4)
            for k in range(rng.integers(3, course - 3)):
                if k % 2 == 0:
                    put(a, sx, y0 + 2 + k, lo)
        for hx in (x + 5, x + ln - 6):  # tie holes
            put(a, hx, y0 + 4, r[0])
            put(a, hx + 1, y0 + 4, r[0])
            put(a, hx + 1, y0 + 5, hi)


def edge_trim(a, t, side, material):
    """The building's corner on a wall tile: the theme's treatment (quoin stones, a steel
    corner post, a neon pilaster) on brick; a recessed pilaster on concrete when the theme
    uses quoins; a corner board on wood. 6 px flush with the outer edge, lit top-left."""
    h, w = a.shape[:2]
    m = t["materials"]["edge"]
    style, trim = m["style"], m.get("trim")
    if material == "wood":
        style, r = "board", palette.ramp(t, t["materials"]["wood"]["ramp"])
    elif material == "concrete" and style == "quoin":
        style, r = "pilaster", palette.ramp(t, t["materials"]["concrete"]["ramp"])
    else:
        r = palette.ramp(t, m["ramp"])
    x0 = 0 if side == "l" else w - 6
    if style == "quoin":
        for c in range(h // 8):
            wd = 7 if c % 2 == 0 else 5
            sx = 0 if side == "l" else w - wd
            y = c * 8
            rect(a, sx, y, sx + wd, y + 8, r[0])
            bx0 = sx if side == "l" else sx + 1
            bx1 = sx + wd - 1 if side == "l" else sx + wd
            rect(a, bx0, y, bx1, y + 6, r[2])
            rect(a, bx0, y, bx1, y + 1, r[3])
            rect(a, bx0, y, bx0 + 1, y + 5, r[3])
            rect(a, bx0 + 1, y + 5, bx1, y + 6, r[1])
            rect(a, bx1 - 1, y + 1, bx1, y + 6, r[1])
        return
    cols = {
        "post": [r[0], r[3], r[2], r[2], r[1], r[0]],
        "pilaster": [r[0], r[1], r[2], r[2], r[1], r[0]],
        "board": [r[0], r[3], r[2], r[2], r[1], r[0]],
    }[style]
    if trim and style == "pilaster":
        g = palette.ramp(t, trim)
        cols = [r[0], r[1], g[1], g[2], g[1], r[1]] if side == "l" else [r[1], g[1], g[2], g[1], r[1], r[0]]
    for i, c in enumerate(cols):
        rect(a, x0 + i, 0, x0 + i + 1, h, c)
    if style == "post":
        for y in range(3, h, 8):
            rect(a, x0 + 2, y, x0 + 4, y + 1, r[3])
            rect(a, x0 + 2, y + 1, x0 + 4, y + 2, r[0])
    elif style == "board":
        for y in range(3, h, 8):
            put(a, x0 + 2, y, r[0])
            put(a, x0 + 3, y + 4, r[0])


def masonry(r, rng, w, h, course=8, length=16, style="brick"):
    """Running bond. 'brick': pillow bricks with wide mortar, lit top-left, the odd half
    brick (the object-set look); 'block': flat cast blocks; 'panel': precast panels, two per
    tile. Courses start on the tile edge so the corner quoins line up with them; the mortar
    row meets itself across the seam."""
    a = canvas(w, h)
    rect(a, 0, 0, w, h, r[0])
    if style == "panel":
        course, length = 16, 32
    for c in range(h // course):
        y0 = c * course
        x, left = (0 if c % 2 == 0 else length // 2), w
        while left > 0:
            ln = length // 2 if style == "brick" and left >= length and rng.random() < 0.15 else min(length, left)
            draw_brick(a, rng, r, x, y0, ln, course, style)
            x, left = x + ln, left - ln
    return a if style == "brick" else np.roll(a, (3, 4), axis=(0, 1))


def corrugated(r, rng, w, h):
    """Rusty corrugated sheet: 8 px ribs lit on their left flank, a lapped seam every 16
    rows, bolts on the crests, rust runs down the troughs."""
    a = canvas(w, h)
    ribs = [r[1], r[2], r[3], r[3], r[2], r[2], r[1], r[1]]
    dim = [r[1], r[1], r[2], r[2], r[2], r[1], r[1], r[1]]
    for s in range(h // 16):
        pattern = dim if rng.random() < 0.3 else ribs
        for x in range(w):
            rect(a, x, s * 16, x + 1, s * 16 + 16, pattern[x % 8])
        rect(a, 0, s * 16, w, s * 16 + 1, r[0])
        rect(a, 0, s * 16 + 1, w, s * 16 + 2, r[1])
        for x in range(2, w, 16):
            put(a, x, s * 16 + 3, r[0])
            put(a, x + 1, s * 16 + 3, r[1])
        for _ in range(rng.integers(2, 4)):
            x = rng.integers(0, w // 8) * 8 + 7
            y = s * 16 + 2 + rng.integers(0, 6)
            for k in range(rng.integers(3, 9)):
                put(a, x, y + k, r[0])
        for _ in range(rng.integers(1, 4)):  # dents catching the light
            put(a, rng.integers(0, w // 8) * 8 + 5, s * 16 + rng.integers(4, 15), r[3])
    return np.roll(a, 5, axis=0)  # keep the lap seam off the tile edge


def wall(t, rng, w, h, side=None):
    m = t["materials"]["wall"]
    r = palette.ramp(t, m["ramp"])
    a = corrugated(r, rng, w, h) if m["style"] == "corrugated" else masonry(r, rng, w, h, style=m["style"])
    if side:
        edge_trim(a, t, side, "wall")
    return a


def concrete(t, rng, w=32, h=32, side=None, detail=None):
    """A precast panel per cell: lit bevel top-left, a joint bottom-right (kept a hair
    inside the tile), two form-tie holes, the odd stain; `detail` adds a vent grille."""
    r = palette.ramp(t, t["materials"]["concrete"]["ramp"])
    a = canvas(w, h)
    rect(a, 0, 0, w, h, r[2])
    speckle(a, rng, np.ones((h, w), bool), [(r[1], 0.07), (r[3], 0.03)])
    rect(a, 0, 0, w, 1, r[3])
    rect(a, 0, 0, 1, h, r[3])
    rect(a, 1, h - 2, w, h - 1, r[1])
    rect(a, w - 2, 1, w - 1, h, r[1])
    rect(a, 0, h - 1, w, h, r[0])
    rect(a, w - 1, 0, w, h, r[0])
    for hx in (9, 21):  # form-tie holes
        rect(a, hx, 7, hx + 2, 9, r[0])
        put(a, hx + 2, 8, r[3])
        put(a, hx + 1, 9, r[3])
        if rng.random() < 0.5:  # water stain under the tie
            for k in range(rng.integers(2, 7)):
                put(a, hx + rng.integers(0, 2), 10 + k, r[1])
    if detail == "vent":  # a louvred grille
        rect(a, 10, 15, 22, 25, r[1])
        rect(a, 10, 15, 22, 16, r[0])
        rect(a, 10, 15, 11, 25, r[0])
        for y in range(17, 25, 2):
            rect(a, 11, y, 21, y + 1, r[0])
            rect(a, 11, y + 1, 21, y + 2, r[3])
    a = np.roll(a, (3, 3), axis=(0, 1))
    if side:
        edge_trim(a, t, side, "concrete")
    return a


def wood_planks(r, rng, a, y0, y1, w, joints=1, outline_rows=True):
    """Horizontal boards between rows y0..y1 (8 rows each): seam, lit top, grain, joints."""
    for py in range(y0, y1, 8):
        rect(a, 0, py, w, py + 8, r[2])
        if outline_rows:
            rect(a, 0, py, w, py + 1, r[0])
        rect(a, 0, py + 1, w, py + 2, r[3])
        rect(a, 0, py + 7, w, py + 8, r[1])
        for _ in range(rng.integers(3, 6)):  # grain
            gx, gy = rng.integers(0, w), py + rng.integers(2, 7)
            for k in range(rng.integers(3, 11)):
                put(a, gx + k, gy, r[1])
        if rng.random() < 0.3:  # knot
            kx = rng.integers(0, w)
            put(a, kx, py + 4, r[0])
            put(a, kx + 1, py + 4, r[1])
            put(a, kx, py + 3, r[1])
        for _ in range(joints):
            jx = rng.integers(0, w)
            for k in range(1, 8):
                put(a, jx, py + k, r[0])
            put(a, jx + 1, py + 2, r[3])
            put(a, jx - 2, py + 3, r[0])
            put(a, jx + 3, py + 3, r[0])
            put(a, jx - 2, py + 5, r[0])
            put(a, jx + 3, py + 5, r[0])


def wood(t, rng, w=32, h=32, side=None):
    r = palette.ramp(t, t["materials"]["wood"]["ramp"])
    a = canvas(w, h)
    wood_planks(r, rng, a, 0, h, w)
    a = np.roll(a, 4, axis=0)  # board seams off the tile edge
    if side:
        edge_trim(a, t, side, "wood")
    return a


# --- openings --------------------------------------------------------------------------

def glass(t, rng, w=32, h=64, variant=0):
    """A window two cells tall: lintel and a sill that drips, a 2x2 pane grid with a
    diagonal reflection. Variants: blinds half down (1), boarded up (2)."""
    m = t["materials"]["glass"]
    f, p = palette.ramp(t, m["frame"]), palette.ramp(t, m["pane"])
    hl = [palette.ramp(t, n)[i] for n, i in m["highlights"]]
    mask = np.zeros((h, w), bool)
    mask[0:5, :] = True           # lintel
    mask[h - 5:, :] = True        # sill
    mask[:, 8:24] = True          # pane + jambs (the collider's middle half)
    a = shade(mask, f)
    rect(a, 0, h - 5, w, h - 4, f[3])  # the sill's lit top, its drip shadow under
    rect(a, 1, h - 1, w - 1, h, f[0])
    rect(a, 12, 2, 20, 4, f[1])        # a keystone in the lintel
    rect(a, 13, 2, 19, 3, f[3])
    py0, py1 = 5, h - 5
    rect(a, 10, py0, 22, py1, p[1])
    rect(a, 10, py0, 22, py0 + 1, p[0])
    rect(a, 21, py0, 22, py1, p[0])
    rect(a, 10, py1 - 10, 21, py1, p[0])
    for y in range(py0 + 1, py1):
        for x in range(10, 21):
            d = (x + y) % 28
            if d in (0, 1, 2):
                a[y, x] = (*hl[0], 255)
            elif d == 3 or d == 6:
                a[y, x] = (*step(p, 2), 255)
            elif d == 7 and len(hl) > 1:
                a[y, x] = (*hl[1], 255)
    # frame: jamb bevels, a transom bar and a mullion
    rect(a, 8, py0, 9, py1, f[0])
    rect(a, 9, py0, 10, py1, f[3])
    rect(a, 22, py0, 23, py1, f[1])
    rect(a, 23, py0, 24, py1, f[0])
    ty = h // 2 - 2
    rect(a, 8, ty, 24, ty + 4, f[2])
    rect(a, 8, ty, 24, ty + 1, f[0])
    rect(a, 9, ty + 1, 23, ty + 2, f[3])
    rect(a, 8, ty + 3, 24, ty + 4, f[0])
    rect(a, 15, py0, 17, py1, f[2])
    rect(a, 15, py0, 16, py1, f[3])
    rect(a, 17, py0, 18, py1, f[0])
    if variant == 1:  # blinds, drawn to a random height in each pane
        for x0, x1 in ((10, 15), (18, 22)):
            for yy in (py0 + 1, ty + 4):
                drop = int(rng.integers(6, 18))
                for y in range(yy, yy + drop, 2):
                    rect(a, x0, y, x1, y + 1, f[2])
                rect(a, x0, yy + drop, x1, yy + drop + 1, f[0])
    elif variant == 2:  # boarded up: two planks nailed across each half
        wd = palette.ramp(t, t["materials"]["wood"]["ramp"])
        for yy in (py0 + 6, py0 + 16, ty + 10, ty + 20):
            tilt = int(rng.integers(-2, 3))
            for k in range(16):
                y = yy + (k * tilt) // 16
                rect(a, 8 + k, y, 9 + k, y + 5, wd[2])
                put(a, 8 + k, y, wd[3], wrap=False)
                put(a, 8 + k, y + 4, wd[0], wrap=False)
            put(a, 10, yy + 2, wd[0], wrap=False)
            put(a, 21, yy + 2 + tilt, wd[0], wrap=False)
    return a


def door(t, rng, w=32, h=64):
    m = t["materials"]["door"]
    f, leaf = palette.ramp(t, m["frame"]), palette.ramp(t, m["leaf"])
    mask = np.zeros((h, w), bool)
    mask[:, :4] = mask[:, w - 4:] = True
    mask[:6, :] = True
    a = shade(mask, f, edges=(True, False, True, True))
    x0, x1 = 4, w - 4
    if m["style"] == "shutter":
        for y in range(6, h):
            k = (y - 6) % 3
            rect(a, x0, y, x1, y + 1, [leaf[0], leaf[3], leaf[2]][k] if k else leaf[1])
        rect(a, x0, 6, x1, 10, leaf[1])
        rect(a, x0, 10, x1, 11, leaf[0])
        st = palette.ramp(t, m["stripe"])
        dk = palette.ramp(t, m["stripe_dark"])
        rect(a, x0, h - 9, x1, h - 8, leaf[0])
        hazard_stripes(a, x0, h - 8, x1, h - 3, st[3], dk[1])
        rect(a, x0, h - 3, x1, h, leaf[1])
        rect(a, x0, h - 3, x1, h - 2, leaf[0])
        rect(a, w // 2 - 3, h - 2, w // 2 + 3, h - 1, leaf[3])
        for _ in range(rng.integers(3, 7)):  # dents/scuffs
            put(a, rng.integers(x0, x1), rng.integers(12, h - 10), leaf[1], wrap=False)
    else:
        lit = palette.ramp(t, m["window"])
        rect(a, x0, 6, x1, h, leaf[1])
        rect(a, x0, 6, x1, 7, leaf[0])
        rect(a, x0, 7, x0 + 1, h, step(leaf, 2))
        for px0, py0, py1 in ((7, 10, 34), (7, 38, h - 4)):  # two inset panels
            rect(a, px0, py0, x1 - 3, py1, leaf[0])
            rect(a, px0 + 1, py0 + 1, x1 - 3, py1, step(leaf, 2))
            rect(a, px0 + 1, py0 + 1, x1 - 4, py1 - 1, leaf[1])
        rect(a, 12, 13, 20, 31, lit[0])          # lit glass in the top panel
        rect(a, 13, 14, 19, 31, lit[2])
        rect(a, 13, 14, 19, 18, lit[3])
        rect(a, 13, 22, 19, 23, lit[1])
        k = palette.ramp(t, m["handle"])
        rect(a, x1 - 6, 40, x1 - 4, 44, k[1])
        put(a, x1 - 6, 40, k[2], wrap=False)
        if m.get("sign"):
            neon_tube(a, 1, 1, w - 1, 4, palette.ramp(t, m["sign"]))
    return a


def interior(t, rng, w=32, h=32, top=False, variant=0):
    """A building's back wall, behind open rooms (BuildingInterior): background, so no
    outline, low contrast and darker than the walls in front. Styles: 'wallpaper' (stripes
    with a small motif), 'blocks' (dark cast blocks), 'boards' (vertical panelling),
    'plaster' (plain over a wainscot), 'tiles' (a small tile grid). `top` adds the shadow a
    ceiling casts on the wall right under it; each storey draws one `variant`."""
    m = interior_spec(t, variant)
    r = palette.ramp(t, m["ramp"])
    base, line = r[m.get("base", 1)], r[m.get("pattern", 0)]
    a = canvas(w, h)
    rect(a, 0, 0, w, h, base)
    style = m["style"]
    if style == "wallpaper":
        for x in range(3, w, 8):
            rect(a, x, 0, x + 1, h, line)
        for y in range(4, h, 16):  # a small diamond between the stripes, offset each row
            for x in range(7 if (y // 16) % 2 == 0 else 3, w, 8):
                if (x // 8) % 2 == (y // 16) % 2:
                    for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
                        put(a, x + dx, y + dy, line)
    elif style == "blocks":
        for y in range(0, h, 8):
            rect(a, 0, y, w, y + 1, line)
            off = 0 if (y // 8) % 2 == 0 else 8
            for x in range(off, w + off, 16):
                rect(a, x % w, y, x % w + 1, y + 8, line)
        speckle(a, rng, np.ones((h, w), bool), [(line, 0.04)])
        a = np.roll(a, (3, 5), axis=(0, 1))
    elif style == "boards":
        for x in range(0, w, 8):
            rect(a, x, 0, x + 1, h, line)
        for _ in range(rng.integers(3, 6)):
            gx, gy = rng.integers(0, w // 8) * 8 + rng.integers(2, 7), rng.integers(0, h)
            for k in range(rng.integers(3, 8)):
                put(a, gx, gy + k, line)
        a = np.roll(a, 4, axis=1)
    elif style == "plaster":
        speckle(a, rng, np.ones((h, w), bool), [(line, 0.015)])
        rect(a, 0, h - 9, w, h, line)           # wainscot
        rect(a, 0, h - 10, w, h - 9, r[min(m.get("base", 1) + 1, len(r) - 1)])
        for x in range(2, w, 8):
            rect(a, x, h - 8, x + 1, h, base)
    else:  # tiles
        for y in range(0, h, 8):
            rect(a, 0, y, w, y + 1, line)
        for x in range(0, w, 8):
            rect(a, x, 0, x + 1, h, line)
        a = np.roll(a, (4, 4), axis=(0, 1))
    if top:
        shadow = r[0] if m.get("pattern", 0) != 0 else line
        rect(a, 0, 0, w, 3, shadow)
        for x in range(0, w, 2):  # dithered edge of the shadow
            put(a, x, 3, shadow, wrap=False)
    return a


# --- structure -------------------------------------------------------------------------

def plank(t, rng, w=32, h=32):
    m = t["materials"]["plank"]
    r = palette.ramp(t, m["ramp"])
    a = canvas(w, h)
    if m["style"] == "boards":
        wood_planks(r, rng, a, 0, 16, w)
        rect(a, 0, 15, w, 16, r[0])
    else:
        rect(a, 0, 0, w, 16, r[2])
        speckle(a, rng, np.ones((16, w), bool), [(r[1], 0.1), (r[3], 0.03)])
        rect(a, 0, 0, w, 1, r[0])
        rect(a, 0, 1, w, 2, r[3])
        rect(a, 0, 14, w, 15, r[1])
        rect(a, 0, 15, w, 16, r[0])
        if m.get("trim"):
            rect(a, 0, 9, w, 10, r[0])
            neon_tube(a, 0, 10, w, 13, palette.ramp(t, m["trim"]))
            rect(a, 0, 13, w, 14, r[0])
    return a


def metal_truss(t, rng, w=64, h=32):
    m = t["materials"]["truss"]
    r = palette.ramp(t, m["ramp"])
    mask = np.zeros((h, w), bool)
    mask[0:5, :] = mask[h - 5:, :] = True
    for px in (0, w // 2 - 2, w - 5):
        mask[:, px:px + 5] = True
    yy, xx = np.mgrid[0:h, 0:w]
    for bx0, bx1 in ((4, w // 2 - 2), (w // 2 + 2, w - 4)):
        slope = (h - 8) / (bx1 - bx0)
        for sgn in (1, -1):
            ycen = 4 + slope * (xx - bx0) if sgn > 0 else h - 4 - slope * (xx - bx0)
            mask |= (np.abs(yy - ycen) <= 2.6) & (xx >= bx0) & (xx < bx1)
    a = shade(mask, r)
    rv = palette.ramp(t, m["rivet"])
    for bx in (w // 4, 3 * w // 4):  # rivets at the X crossings
        for dx, dy in ((-2, -2), (1, -2), (-2, 1), (1, 1)):
            rect(a, bx + dx, h // 2 + dy, bx + dx + 1, h // 2 + dy + 1, rv[-1])
    for x in range(8, w - 6, 8):
        put(a, x, 2, rv[-1], wrap=False)
        put(a, x, h - 3, rv[-1], wrap=False)
    return a


def beam(t, rng, w=64, h=32):
    m = t["materials"]["beam"]
    r = palette.ramp(t, m["ramp"])
    a = canvas(w, h)
    rect(a, 0, 0, w, h, r[1])
    for y0 in (0, h - 6):  # flanges
        rect(a, 0, y0, w, y0 + 6, r[2])
        rect(a, 0, y0, w, y0 + 1, r[0])
        rect(a, 0, y0 + 1, w, y0 + 2, r[3])
        rect(a, 0, y0 + 4, w, y0 + 5, r[1])
        rect(a, 0, y0 + 5, w, y0 + 6, r[0])
    rect(a, 0, 6, w, 7, r[0])  # web in the flange's shadow
    for x in range(0, w, 16):  # stiffeners
        rect(a, x, 7, x + 3, h - 6, r[2])
        rect(a, x, 7, x + 1, h - 6, r[3])
        rect(a, x + 3, 7, x + 4, h - 6, r[0])
    for x in range(6, w, 8):  # rivet rows
        if x % 16 in (6, 14):
            put(a, x, 9, r[3])
            put(a, x, 10, r[0])
            put(a, x, h - 9, r[3])
            put(a, x, h - 8, r[0])
    if m.get("trim"):
        neon_tube(a, 0, h // 2 - 1, w, h // 2 + 2, palette.ramp(t, m["trim"]))
    elif m.get("stain"):
        speckle(a, rng, np.pad(np.ones((h - 18, w), bool), ((9, 9), (0, 0))), [(r[0], 0.04)])
    return np.roll(a, 8, axis=1)  # stiffeners off the tile edge


def brick_cap(t, rng, w=64, h=32):
    m = t["materials"]["cap"]
    c = palette.ramp(t, m["ramp"])
    wr = palette.ramp(t, t["materials"]["wall"]["ramp"])
    a = wall(t, rng, w, h)
    # coping: lit stone slabs along the roofline
    rect(a, 0, 0, w, 8, c[2])
    rect(a, 0, 0, w, 1, c[0])
    rect(a, 0, 1, w, 2, c[3])
    rect(a, 0, 6, w, 7, c[1])
    rect(a, 0, 7, w, 8, c[0])
    for x in range(0, w, 16):
        rect(a, x, 1, x + 1, 7, c[0])
        rect(a, x + 1, 2, x + 2, 6, c[3])
    speckle(a, rng, np.pad(np.ones((4, w), bool), ((2, h - 6), (0, 0))), [(c[1], 0.08)])
    # trim band
    rect(a, 0, 8, w, 9, wr[0])
    if not m.get("trim"):  # a plain cornice band
        rect(a, 0, 9, w, 13, c[1])
        rect(a, 0, 9, w, 10, c[3])
        rect(a, 0, 12, w, 13, wr[0])
    elif m["trim"] == "hazard":
        st, dk = palette.ramp(t, m["stripe"]), palette.ramp(t, m["stripe_dark"])
        hazard_stripes(a, 0, 9, w, 13, st[3], dk[1], width=4)
    else:
        neon_tube(a, 0, 9, w, 12, palette.ramp(t, m["trim"]))
        rect(a, 0, 12, w, 13, wr[0])
    rect(a, 0, 13, w, 14, wr[0])
    # dentils under the band
    for x in range(0, w, 8):
        rect(a, x + 1, 14, x + 6, 17, c[2])
        rect(a, x + 1, 14, x + 2, 17, c[3])
        rect(a, x + 5, 14, x + 6, 17, c[1])
        rect(a, x + 1, 17, x + 6, 18, c[0])
    rect(a, 0, 14, w, 15, wr[0])
    return a


def support(t, rng, w=32, h=32):
    m = t["materials"]["support"]
    r = palette.ramp(t, m["ramp"])
    a = canvas(w, h)
    x0, x1 = 9, 23
    if m["style"] == "ibeam":
        rect(a, x0, 0, x1, h, r[1])
        for fx in (x0, x1 - 3):  # flanges
            rect(a, fx, 0, fx + 3, h, r[2])
            rect(a, fx, 0, fx + 1, h, r[0])
            rect(a, fx + 1, 0, fx + 2, h, r[3])
            rect(a, fx + 2, 0, fx + 3, h, r[1])
        rect(a, x1 - 1, 0, x1, h, r[0])
        rect(a, x0 + 3, 0, x0 + 4, h, r[0])
        for y in range(4, h, 16):  # bolt pairs
            for bx in (x0 + 5, x1 - 6):
                put(a, bx, y, r[3])
                put(a, bx, y + 1, r[0])
        for y in range(12, h, 16):  # stiffener plate
            rect(a, x0 + 4, y, x1 - 3, y + 2, r[2])
            rect(a, x0 + 4, y, x1 - 3, y + 1, r[3])
            rect(a, x0 + 4, y + 2, x1 - 3, y + 3, r[0])
    else:
        rect(a, x0, 0, x1, h, r[2])
        speckle(a, rng, np.pad(np.ones((h, x1 - x0 - 3), bool), ((0, 0), (x0 + 2, w - x1 + 1))),
                [(r[1], 0.1), (r[3], 0.03)])
        rect(a, x0, 0, x0 + 1, h, r[0])
        rect(a, x0 + 1, 0, x0 + 2, h, r[3])
        rect(a, x1 - 2, 0, x1 - 1, h, r[1])
        rect(a, x1 - 1, 0, x1, h, r[0])
        if m.get("trim"):
            rect(a, 13, 0, 14, h, r[0])
            neon_tube(a, 14, 0, 17, h, palette.ramp(t, m["trim"]))
            rect(a, 17, 0, 18, h, r[0])
    return a


def steel(t, rng, w=32, h=32):
    """A column of plated steel wall, one cell wide: two heavy plates per cell, darker than
    any masonry, lap seams, rivet rows, a sheen across each plate; `stripe` paints a hazard
    band along the foot of every other plate. Tougher than brick or concrete at a glance."""
    m = t["materials"]["steel"]
    r = palette.ramp(t, m["ramp"])
    a = canvas(w, h)
    rect(a, 0, 0, w, h, r[1])
    speckle(a, rng, np.ones((h, w), bool), [(r[0], 0.015), (r[2], 0.02)])
    for i, y0 in enumerate(range(0, h, 16)):
        rect(a, 0, y0, w, y0 + 1, r[0])             # lap seam, the plate's lit edge under it
        rect(a, 0, y0 + 1, w, y0 + 2, r[2])
        rect(a, 0, y0 + 15, w, y0 + 16, r[0])
        for k in range(9):                          # sheen
            put(a, 9 + k + i * 9, y0 + 12 - k, r[2], wrap=False)
            put(a, 10 + k + i * 9, y0 + 12 - k, r[2], wrap=False)
        if m.get("stripe") and i % 2:
            hazard_stripes(a, 0, y0 + 11, w, y0 + 15, step(palette.ramp(t, m["stripe"]), 3),
                           palette.ramp(t, m["stripe_dark"])[1], width=4)
            rect(a, 0, y0 + 10, w, y0 + 11, r[0])
        for x in range(4, w, 7):                    # rivets
            for y in (y0 + 4,) + (() if m.get("stripe") and i % 2 else (y0 + 11,)):
                rect(a, x, y, x + 2, y + 2, r[2])
                put(a, x, y, r[3], wrap=False)
                rect(a, x, y + 2, x + 3, y + 3, r[0])
                rect(a, x + 2, y + 1, x + 3, y + 3, r[0])
    rect(a, 1, 0, 2, h, r[2])                       # the column's lit and shaded flanks
    rect(a, w - 2, 0, w - 1, h, r[0])
    rect(a, 0, 0, 1, h, r[0])
    rect(a, w - 1, 0, w, h, r[0])
    return np.roll(a, 8, axis=0)                    # seams, rivets and stripes off the tile edge


def pole(t, rng, w=32, h=32):
    """A scaffold pole up the middle of the cell (6 px; the rest stays open): a steel tube
    with a coupler, or a wooden pole with a rope lashing (`style`: 'clamp', 'rope'), and
    the stubs of a ledger either side of it, so a stack reads as scaffolding."""
    m = t["materials"]["pole"]
    r, c = palette.ramp(t, m["ramp"]), palette.ramp(t, m["clamp"])
    a = canvas(w, h)
    for x, i in zip(range(13, 19), (0, 3, 2, 2, 1, 0)):
        rect(a, x, 0, x + 1, h, r[i])
    if m["style"] == "rope":
        for _ in range(int(rng.integers(3, 6))):    # grain
            y = int(rng.integers(0, h))
            rect(a, 15 + int(rng.integers(0, 2)), y, 16, y + int(rng.integers(2, 5)), r[1])
    for x0, x1 in ((6, 13), (19, 26)):              # ledger stubs
        rect(a, x0, 14, x1, 18, r[0])
        rect(a, max(x0, 7), 15, min(x1, 25), 16, r[3])
        rect(a, max(x0, 7), 16, min(x1, 25), 17, r[2])
    rect(a, 11, 11, 21, 21, c[0])                   # the coupler or lashing over the joint
    rect(a, 12, 12, 20, 20, c[-2])
    if m["style"] == "rope":
        for y in range(12, 20):                     # turns of rope
            rect(a, 12, y, 20, y + 1, (c[-1], c[-2], c[-3])[y % 3])
            put(a, 12 + (y * 3) % 8, y, c[0], wrap=False)
    else:
        rect(a, 12, 12, 20, 13, c[-1])
        rect(a, 12, 12, 13, 20, c[-1])
        rect(a, 19, 13, 20, 20, c[-3])
        rect(a, 13, 19, 20, 20, c[-3])
        rect(a, 15, 15, 17, 17, c[0])               # its bolt
        put(a, 15, 15, c[-1], wrap=False)
    return a


def ladder(t, rng, w=32, h=32):
    """A ladder, 20 px wide: two rails and a rung every 8 px. Drawn behind the blocks, so no
    outline; each rail and rung has a lit and a dark side, so it reads on the dark back wall
    as well as against the sky or a wall outside."""
    r = palette.ramp(t, t["materials"]["ladder"]["ramp"])
    a = canvas(w, h)
    for y in range(2, h, 8):
        rect(a, 9, y, 23, y + 1, step(r, 3))
        rect(a, 9, y + 1, 23, y + 2, step(r, 2))
        rect(a, 9, y + 2, 23, y + 3, r[0])
    for x0 in (6, 22):
        rect(a, x0, 0, x0 + 1, h, step(r, 3))
        rect(a, x0 + 1, 0, x0 + 3, h, step(r, 2))
        rect(a, x0 + 3, 0, x0 + 4, h, r[0])
    return a


def chimney(t, rng, w=32, h=32, variant=0):
    """A roof block: the theme's chimney (a brick stack or a pipe), or one of its roof
    props (water tank, AC unit, vent cowl, sign box); all fill most of the cell."""
    m = variant_spec(t["materials"]["chimney"], variant)
    style = m["style"]
    r = palette.ramp(t, m["ramp"])
    if style == "tank":
        tr = r
        mask = np.zeros((h, w), bool)
        mask[9:25, 6:26] = True
        for i in range(6):  # conical lid
            mask[3 + i, 16 - i * 2 - 1:16 + i * 2 + 1] = True
        mask[25:h, 8:11] = mask[25:h, 21:24] = True
        a = shade(mask, tr, edges=(True, False, True, True))
        for y in (12, 20):
            rect(a, 7, y, 25, y + 1, tr[0])
        rect(a, 7, 9, 25, 10, tr[0])
        rect(a, 8, 10, 11, 23, step(tr, 3))     # the lit flank
        rect(a, 6, 24, 26, 25, tr[0])
        return a
    if style == "ac":
        mask = np.zeros((h, w), bool)
        mask[10:h, 3:29] = True
        a = shade(mask, r, edges=(True, False, True, True))
        fan = disc(np.zeros((h, w), bool), 13, 21, 6)
        a[fan] = (*r[0], 255)
        rim = fan & ~disc(np.zeros((h, w), bool), 13, 21, 5)
        a[rim] = (*r[1], 255)
        for k in range(-4, 5):  # blades
            put(a, 13 + k, 21, r[2], wrap=False)
            put(a, 13, 21 + k, r[2], wrap=False)
        for y in range(13, 30, 3):  # louvres
            rect(a, 21, y, 27, y + 1, r[0])
        rect(a, 3, 10, 29, 11, r[3])
        return a
    if style == "vent":
        mask = np.zeros((h, w), bool)
        mask[5:11, 6:26] = True
        mask[11:h, 12:20] = True
        a = shade(mask, r, edges=(True, False, True, True))
        rect(a, 7, 10, 25, 11, r[0])
        rect(a, 10, 7, 22, 8, r[0])
        for y in range(14, h, 6):
            rect(a, 13, y, 19, y + 1, r[0])
        return a
    if style == "sign":
        neon = palette.ramp(t, m.get("beacon", m.get("band", "magenta")))
        mask = np.zeros((h, w), bool)
        mask[3:23, 2:30] = True
        mask[23:h, 7:10] = mask[23:h, 22:25] = True
        a = shade(mask, r, edges=(True, False, True, True))
        rect(a, 4, 5, 28, 21, r[0])
        neon_tube(a, 5, 5, 27, 8, neon)
        neon_tube(a, 5, 18, 27, 21, neon)
        for x in range(7, 25, 5):
            rect(a, x, 10, x + 3, 16, neon[-1])
            rect(a, x + 1, 11, x + 2, 15, neon[1])
        return a
    mask = np.zeros((h, w), bool)
    mask[3:9, 7:25] = True
    mask[9:, 9:23] = True
    a = shade(mask, r, edges=(True, False, True, True))
    if style == "pipe":
        for x, i in zip(range(10, 22), (2, 3, 3, 2, 2, 2, 2, 2, 1, 2, 1, 1)):
            rect(a, x, 9, x + 1, h, r[i])
        rect(a, 10, 9, 22, 10, r[0])  # shadow under the lip
        for _ in range(rng.integers(3, 6)):  # rust drips
            x, y = rng.integers(11, 21), rng.integers(10, 20)
            for k in range(rng.integers(2, 6)):
                put(a, x, y + k, r[0], wrap=False)
    else:
        speckle(a, rng, np.pad(np.ones((h - 11, 10), bool), ((10, 1), (11, 11))), [(r[1], 0.12), (r[3], 0.04)])
        rect(a, 10, 9, 22, 10, r[0])
    band = m.get("band")
    if not band:
        rect(a, 9, 9, 23, 10, r[0])  # just the lip's shadow
    elif band == "hazard":
        st, dk = palette.ramp(t, m["stripe"]), palette.ramp(t, m["stripe_dark"])
        rect(a, 9, 15, 23, 16, r[0])
        hazard_stripes(a, 10, 16, 22, 21, st[3], dk[1])
        rect(a, 9, 21, 23, 22, r[0])
    else:
        neon_tube(a, 10, 15, 22, 18, palette.ramp(t, band))
        rect(a, 10, 14, 22, 15, r[0])
        rect(a, 10, 18, 22, 19, r[0])
    if m.get("beacon"):
        b = palette.ramp(t, m["beacon"])
        rect(a, 14, 0, 18, 3, b[0])
        rect(a, 15, 0, 17, 2, b[-1])
        rect(a, 15, 2, 17, 3, b[1])
    return a


# --- street props ----------------------------------------------------------------------

def disc(mask, cx, cy, r):
    yy, xx = np.mgrid[0:mask.shape[0], 0:mask.shape[1]]
    mask |= (xx - cx + 0.5) ** 2 + (yy - cy + 0.5) ** 2 <= r * r
    return mask


def crate(t, rng, w=32, h=32):
    """A wooden crate: framed planks with a diagonal brace."""
    r = palette.ramp(t, t["materials"]["crate"]["ramp"])
    a = canvas(w, h)
    rect(a, 1, 2, w - 1, h, r[0])
    wood_planks(r, rng, a, 4, h - 4, w, joints=0, outline_rows=False)  # whole planks only: put() wraps
    for y in range(4, h - 4, 8):
        rect(a, 2, y, w - 2, y + 1, r[1])
    for k in range(w - 8):  # brace from bottom-left to top-right
        x, y = 4 + k, h - 5 - k * (h - 9) // (w - 8)
        rect(a, x, y - 1, x + 2, y + 2, r[2])
        put(a, x, y - 1, r[3], wrap=False)
        put(a, x + 1, y + 2, r[1], wrap=False)
    for x0, y0, x1, y1 in ((1, 2, w - 1, 6), (1, h - 4, w - 1, h), (1, 2, 5, h), (w - 5, 2, w - 1, h)):  # frame
        rect(a, x0, y0, x1, y1, r[2])
    rect(a, 2, 3, w - 2, 4, r[3])
    rect(a, 2, 3, 3, h - 1, r[3])
    for x, y in ((3, 4), (w - 4, 4), (3, h - 3), (w - 4, h - 3)):  # nails
        put(a, x, y, r[0], wrap=False)
    outline(a, r[0])
    return a


def outline(a, c):
    """1 px outline around the opaque shape (on its outermost opaque pixels)."""
    m = a[:, :, 3] > 0
    pad = np.pad(m, 1)
    edge = m & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    a[edge] = (*c, 255)


def car(t, rng, w=64, h=32):
    """A parked car, facing right: lit body, cabin windows, two wheels."""
    m = t["materials"]["car"]
    body, glass_r, trim = palette.ramp(t, m["body"]), palette.ramp(t, m["glass"]), palette.ramp(t, m["trim"])
    tyre = palette.ramp(t, m["tyre"])
    mask = np.zeros((h, w), bool)
    mask[15:27, 2:62] = True                       # body
    mask[13:15, 6:58] = True
    for y in range(4, 13):                         # cabin, sloped front and back
        inset = (13 - y) // 2
        mask[y, 16 + inset:46 - inset * 2] = True
    a = shade(mask, body)
    for x0, x1 in ((19, 30), (32, 42)):            # side windows
        for y in range(6, 13):
            inset = (13 - y) // 2
            lo, hi = max(x0, 17 + inset), min(x1, 45 - inset * 2)
            rect(a, lo, y, hi, y + 1, glass_r[1])
        rect(a, max(x0, 19), 7, min(x1, 40), 8, step(glass_r, 3))
    rect(a, 2, 19, 62, 20, body[1])                # waist line
    rect(a, 3, 24, 62, 26, trim[1])                # bumper line
    rect(a, 3, 24, 62, 25, step(trim, 3))
    rect(a, 58, 16, 61, 19, palette.ramp(t, m["light"])[-1])   # headlight
    rect(a, 2, 16, 4, 19, palette.ramp(t, m["light"])[1])      # tail light
    for cx in (15, 49):                            # wheels
        wheel = disc(np.zeros((h, w), bool), cx, 26, 6)
        a[wheel] = (*tyre[0], 255)
        hub = disc(np.zeros((h, w), bool), cx, 26, 3)
        a[hub] = (*step(trim, 2), 255)
        put(a, cx - 1, 25, step(trim, 3), wrap=False)
    outline(a, body[0])
    return a


def barrel(t, rng, w=32, h=32):
    """A gas tank: a drum lit from the left with two bands and a warning flame."""
    m = t["materials"]["barrel"]
    r, band = palette.ramp(t, m["ramp"]), palette.ramp(t, m["band"])
    a = canvas(w, h)
    hi = step(r, 3)
    shades = [r[0], r[1], hi, hi, r[2], r[2], r[2], r[2], r[2], r[2], r[1], r[2], r[1], r[1], r[1], r[0]]
    x0 = 8
    for i, c in enumerate(shades):                 # cylinder columns
        rect(a, x0 + i, 3, x0 + i + 1, h, c)
    rect(a, x0, 2, x0 + 16, 4, r[1])              # rim
    rect(a, x0 + 1, 2, x0 + 15, 3, step(r, 3))
    for y in (8, 24):                              # bands
        rect(a, x0, y, x0 + 16, y + 2, band[1])
        rect(a, x0 + 1, y, x0 + 4, y + 1, step(band, 3))
    for dy, (lo, hi) in enumerate(((15, 17), (14, 17), (13, 18), (13, 19), (14, 19), (14, 18))):  # flame
        rect(a, lo, 13 + dy, hi, 14 + dy, step(band, 3))
    rect(a, 15, 16, 17, 18, band[1])
    outline(a, r[0])
    return a


def container(t, rng, w=96, h=64):
    """A shipping container's long side: ribbed steel between corner posts and rails."""
    r = palette.ramp(t, t["materials"]["container"]["ramp"])
    a = canvas(w, h)
    rect(a, 0, 0, w, h, r[2])
    for x in range(6, w - 6, 6):                   # ribs, lit on their left flank
        rect(a, x, 6, x + 1, h - 6, r[3])
        rect(a, x + 3, 6, x + 5, h - 6, r[1])
    for y0 in (0, h - 6):                          # top and bottom rails
        rect(a, 0, y0, w, y0 + 6, r[1])
        rect(a, 0, y0 + 1, w, y0 + 2, step(r, 3))
    for x0 in (0, w - 6):                          # corner posts and castings
        rect(a, x0, 0, x0 + 6, h, r[1])
        rect(a, x0 + 1, 0, x0 + 2, h, step(r, 3))
        for y0 in (1, h - 5):
            rect(a, x0 + 1, y0, x0 + 5, y0 + 4, r[0])
            put(a, x0 + 2, y0 + 1, r[2], wrap=False)
    speckle(a, rng, np.pad(np.ones((h - 14, w - 14), bool), ((7, 7), (7, 7))), [(r[1], 0.03), (r[0], 0.01)])
    for _ in range(rng.integers(2, 5)):            # rust runs from the top rail
        x, y = rng.integers(8, w - 8), 6
        for k in range(rng.integers(4, 14)):
            put(a, x, y + k, r[0] if k % 3 else r[1], wrap=False)
    outline(a, r[0])
    return a


# --- street dressing (cosmetic) ---------------------------------------------------------

def lamp(t, rng, w=32, h=96):
    """A street lamp: base, pole, an arm reaching right and a lit head."""
    m = t["materials"]["lamp"]
    pole, light = palette.ramp(t, m["pole"]), palette.ramp(t, m["light"])
    mask = np.zeros((h, w), bool)
    mask[h - 6:h, 11:21] = True                    # base
    mask[12:h - 6, 14:18] = True                   # pole
    mask[10:14, 14:28] = True                      # arm
    mask[13:17, 22:30] = True                      # head
    a = shade(mask, pole)
    rect(a, 23, 16, 29, 18, step(light, 3))        # the light under the head
    rect(a, 24, 18, 28, 19, light[1])
    if m.get("tube"):                              # a neon ring on the pole
        neon_tube(a, 14, 40, 18, 64, light)
    outline(a, pole[0])
    return a


def fence(t, rng, w=32, h=32):
    """One tileable section of fence: picket, chain-link or rail."""
    m = t["materials"]["fence"]
    r = palette.ramp(t, m["ramp"])
    a = canvas(w, h)
    style = m["style"]
    if style == "picket":
        mask = np.zeros((h, w), bool)
        for x in range(2, w, 8):                   # pickets with pointed tops
            mask[12:h, x:x + 5] = True
            mask[10:12, x + 1:x + 4] = True
            mask[9, x + 2] = True
        mask[16:19, :] = True                      # rails
        mask[25:28, :] = True
        a = shade(mask, r, edges=(True, True, False, False))
    elif style == "chain":
        rect(a, 0, 8, w, 10, r[1])                 # top rail
        rect(a, 0, 8, w, 9, step(r, 3))
        for y in range(10, h):                     # diamond mesh
            for x in range(w):
                if (x + y) % 6 == 0 or (x - y) % 6 == 0:
                    a[y, x] = (*r[2], 255)
        rect(a, 14, 6, 18, h, r[1])                # post
        rect(a, 14, 6, 15, h, step(r, 3))
    else:  # rail
        for y0 in (12, 22):
            rect(a, 0, y0, w, y0 + 3, r[2])
            rect(a, 0, y0, w, y0 + 1, step(r, 3))
            rect(a, 0, y0 + 2, w, y0 + 3, r[0])
        rect(a, 14, 10, 18, h, r[1])
        rect(a, 14, 10, 15, h, step(r, 3))
        rect(a, 17, 10, 18, h, r[0])
        if m.get("trim"):
            neon_tube(a, 0, 17, w, 20, palette.ramp(t, m["trim"]))
    return a


def bush(t, rng, w=32, h=32, variant=0):
    """Ground clutter: a hedge, a stack of tyres, bin bags, pallets; `variant` picks one
    of the theme's `variants` (0 = its base look)."""
    m = variant_spec(t["materials"]["bush"], variant)
    r = palette.ramp(t, m["ramp"])
    style = m["style"]
    mask = np.zeros((h, w), bool)
    if style == "hedge":
        for cx, cy, rad in ((10, 20, 8), (20, 17, 9), (26, 23, 6), (6, 25, 6)):
            disc(mask, cx, cy, rad)
        mask[25:, 2:30] = True
        a = shade(mask, r, edges=(True, False, True, True))
        speckle(a, rng, mask & (a[:, :, :3] == step(r, 2)).all(-1), [(step(r, 3), 0.12), (r[1], 0.12)])
    elif style == "tires":
        for y0 in (h - 8, h - 15, h - 22):
            mask[y0:y0 + 7, 6:26] = True
        a = shade(mask, r, edges=(True, False, True, True))
        for y0 in (h - 8, h - 15, h - 22):
            rect(a, 6, y0, 26, y0 + 1, r[0])
            rect(a, 12, y0 + 2, 20, y0 + 4, r[0])  # the hole
    elif style == "bags":
        for cx, cy, rad in ((10, 24, 8), (22, 23, 8), (16, 15, 7)):
            disc(mask, cx, cy, rad)
        mask[h - 4:, 3:29] = True
        a = shade(mask, r, edges=(True, False, True, True))
        for x, y in ((15, 8), (16, 8), (21, 15), (9, 16)):  # tied tops
            put(a, x, y, step(r, 3), wrap=False)
    else:  # pallets: a short stack of wooden pallets
        for y0 in (h - 6, h - 12, h - 18):
            mask[y0:y0 + 5, 3:29] = True
        a = shade(mask, r, edges=(True, False, True, True))
        for y0 in (h - 6, h - 12, h - 18):
            for x in range(6, 27, 7):
                rect(a, x, y0 + 1, x + 2, y0 + 4, r[0])
    outline(a, r[0])
    return a


def bin_(t, rng, w=32, h=32, variant=0):
    """A small kerbside thing: a dustbin, a bollard, a hydrant, a post box, a cone."""
    m = variant_spec(t["materials"]["bin"], variant)
    r = palette.ramp(t, m["ramp"])
    style = m["style"]
    mask = np.zeros((h, w), bool)
    if style == "can":
        mask[12:h, 9:23] = True
        mask[9:12, 8:24] = True
        mask[7:9, 14:18] = True
    elif style == "bollard":
        mask[14:h, 11:21] = True
        mask[11:14, 10:22] = True
    elif style == "hydrant":
        mask[12:h, 11:21] = True
        mask[9:12, 12:20] = True
        mask[15:19, 7:25] = True
        mask[h - 3:h, 9:23] = True
    elif style == "postbox":
        mask[6:h, 10:22] = True
        mask[4:6, 11:21] = True
    else:  # cone
        for i in range(20):
            mask[10 + i, 15 - i * 5 // 20 - 1:17 + i * 5 // 20 + 1] = True
        mask[h - 3:h, 7:25] = True
    a = shade(mask, r, edges=(True, False, True, True))
    if style == "can":
        for y in (16, 22, 28):
            rect(a, 10, y, 22, y + 1, r[1])
    elif style == "bollard":
        band = palette.ramp(t, m["band"])
        rect(a, 11, 18, 21, 21, step(band, 3))
    elif style == "postbox":
        rect(a, 12, 10, 20, 12, r[0])
        rect(a, 11, 19, 21, 20, r[0])
    elif style == "cone":
        band = palette.ramp(t, m.get("band", m["ramp"]))
        rect(a, 12, 17, 20, 20, step(band, 3))
    outline(a, r[0])
    return a


def sign(t, rng, w=32, h=64, variant=0):
    """A sign on a post: a plain board (faces: symbol, arrow, stop, p) or a neon board."""
    m = t["materials"]["sign"]
    post, face = palette.ramp(t, m["post"]), palette.ramp(t, m["face"])
    faces = m.get("faces", ["symbol"])
    which = faces[variant % len(faces)]
    mask = np.zeros((h, w), bool)
    mask[20:h, 14:18] = True
    mask[2:22, 4:28] = True
    a = shade(mask, post)
    rect(a, 6, 4, 26, 20, face[1])
    rect(a, 6, 4, 26, 5, step(face, 3))
    if m.get("style") == "neon":
        neon_tube(a, 6, 4, 26, 7, face)
        neon_tube(a, 6, 17, 26, 20, face)
        if which == "arrow":
            for k in range(6):
                put(a, 10 + k, 12 - k, step(face, 2), wrap=False)
                put(a, 10 + k, 12 + k, step(face, 2), wrap=False)
            rect(a, 10, 12, 22, 13, step(face, 2))
        else:
            for x in range(9, 24, 4):
                rect(a, x, 10, x + 2, 14, step(face, 2))
    elif which == "arrow":
        rect(a, 8, 11, 20, 14, step(face, 3))
        for k in range(5):
            rect(a, 19 + k, 8 + k, 20 + k, 17 - k, step(face, 3))
    elif which == "stop":
        rect(a, 9, 7, 23, 17, step(face, 3))
        rect(a, 11, 11, 21, 13, face[0])
    elif which == "p":
        rect(a, 12, 7, 15, 17, step(face, 3))
        rect(a, 15, 7, 20, 13, step(face, 3))
        rect(a, 15, 9, 18, 11, face[1])
    else:
        rect(a, 13, 8, 19, 16, step(face, 3))     # a symbol
        rect(a, 15, 10, 17, 14, face[0])
    outline(a, post[0])
    return a


# --- ground ----------------------------------------------------------------------------

def ground_mass(r, rng, a, y0, y1, row_offset, style, stone=None):
    """Ground below the surface. 'blocks': 16-row courses whose absolute course index keeps
    the bond continuous from ground_top into ground_fill. 'asphalt': aggregate speckle with
    a few hairline cracks. 'soil': earth with the odd stone."""
    w = a.shape[1]
    rect(a, 0, y0, w, y1, r[1])
    region = np.pad(np.ones((y1 - y0, w), bool), ((y0, a.shape[0] - y1), (0, 0)))
    if style == "soil":
        speckle(a, rng, region, [(r[0], 0.12), (r[2], 0.08)])
        for _ in range(rng.integers(2, 5)):
            x, y = rng.integers(2, w - 5), rng.integers(y0 + 1, max(y0 + 2, y1 - 4))
            rect(a, x, y, x + 3, y + 2, stone[1] if stone else r[2])
            put(a, x, y, stone[2] if stone else r[2], wrap=False)
            put(a, x + 2, y + 1, stone[0] if stone else r[0], wrap=False)
        return
    speckle(a, rng, region, [(r[0], 0.06), (r[2], 0.07), (step(r, 3), 0.01 if style != "blocks" else 0)])
    if style != "blocks":
        for _ in range(rng.integers(1, 3)):
            x, y = rng.integers(4, w - 12), rng.integers(y0 + 3, y1 - 8)
            for _ in range(rng.integers(4, 8)):
                put(a, x, y, r[0], wrap=False)
                x, y = x + 1, y + rng.integers(0, 2)
        return
    for y in range(y0, y1):
        ay = y + row_offset
        if ay % 16 == 0:
            rect(a, 0, y, w, y + 1, r[0])
        elif ay % 16 == 1:
            rect(a, 0, y, w, y + 1, r[2])
        jx = 8 if (ay // 16) % 2 == 0 else 24
        if ay % 16 != 0:
            put(a, jx, y, r[0])
            put(a, jx + 1, y, r[2])


def ground_top(t, rng, w=32, h=32):
    m = t["materials"]["ground"]
    fill = palette.ramp(t, m["fill"])
    stone = palette.ramp(t, m["stone"]) if m.get("stone") else None
    a = canvas(w, h)
    ground_mass(fill, rng, a, 8, h, 8, m.get("fill_style", "blocks"), stone)
    top = palette.ramp(t, m["top"])
    if m["style"] == "dock":
        rect(a, 0, 0, w, 8, top[2])
        rect(a, 0, 0, w, 1, top[3])
        rect(a, 0, 1, w, 2, top[3])
        rect(a, 0, 6, w, 7, top[1])
        rect(a, 0, 7, w, 8, top[0])
        for _ in range(rng.integers(3, 6)):  # grain
            gx, gy = rng.integers(0, w), rng.integers(2, 6)
            for k in range(rng.integers(3, 9)):
                put(a, gx + k, gy, top[1])
        for jx in (5, 21):  # board ends with nails
            rect(a, jx, 1, jx + 1, 7, top[0])
            put(a, jx + 1, 2, top[3])
            put(a, jx - 2, 3, top[0])
            put(a, jx + 3, 3, top[0])
        rect(a, 0, 8, w, 9, fill[0])   # shadow under the deck
        bolt = palette.ramp(t, m["bolt"])
        for bx in (12, 28):             # deck bolts into the quay
            put(a, bx, 10, bolt[-1])
            put(a, bx, 11, bolt[0])
    elif m["style"] == "pavement":   # paving slabs on a kerb
        rect(a, 0, 0, w, 7, top[2])
        speckle(a, rng, np.pad(np.ones((4, w), bool), ((2, h - 6), (0, 0))), [(top[1], 0.08), (top[3], 0.05)])
        rect(a, 0, 0, w, 1, top[3])
        rect(a, 0, 5, w, 6, top[1])
        rect(a, 0, 6, w, 7, top[0])
        for jx in (3, 19):              # slab joints
            rect(a, jx, 0, jx + 1, 6, top[0])
            rect(a, jx + 1, 1, jx + 2, 5, top[3])
        rect(a, 0, 7, w, 8, fill[0])
        if rng.random() < 0.5:          # a weed in a joint
            leaf = palette.ramp(t, m.get("weed", m["fill"]))
            put(a, 19, 0, leaf[2], wrap=False)
            put(a, 20, -1 + 1, leaf[1], wrap=False)
    else:  # kerb
        rect(a, 0, 0, w, 7, top[2])
        speckle(a, rng, np.pad(np.ones((4, w), bool), ((2, h - 6), (0, 0))), [(top[1], 0.12), (top[3], 0.05)])
        rect(a, 0, 0, w, 1, top[3])
        rect(a, 0, 1, w, 2, top[3])
        rect(a, 0, 5, w, 6, top[1])
        rect(a, 0, 6, w, 7, top[0])
        for jx in (7, 23):
            rect(a, jx, 1, jx + 1, 6, top[0])
            rect(a, jx + 1, 2, jx + 2, 5, top[3])
        rect(a, 0, 7, w, 8, fill[0])
        if m.get("stud"):
            s = palette.ramp(t, m["stud"])
            for sx in (14, 30):         # reflective kerb studs
                rect(a, sx, 3, sx + 2, 4, s[-1])
                rect(a, sx, 4, sx + 2, 5, s[1])
    return a


def ground_fill(t, rng, w=32, h=32):
    m = t["materials"]["ground"]
    fill = palette.ramp(t, m["fill"])
    stone = palette.ramp(t, m["stone"]) if m.get("stone") else None
    a = canvas(w, h)
    ground_mass(fill, rng, a, 0, h, 8, m.get("fill_style", "blocks"), stone)
    return a


def variant_spec(m, variant):
    """A material block for one of its variants: the base look (0), or the base with the
    n-th entry of its `variants` list laid over it."""
    variants = m.get("variants", [])
    if variant == 0 or not variants:
        return m
    return {**m, **variants[(variant - 1) % len(variants)]}


def interior_spec(t, variant):
    """The theme's back-wall look for a storey variant (0 = the base look)."""
    return variant_spec(t["materials"]["interior"], variant)


def wall_detail(t, kind, w=32, h=32):
    """Something on the back wall of an open room (drawn over the wallpaper of one cell):
    a picture, a shelf with boxes, a poster, a pipe, a clock, a notice board. Background:
    no outline, in the wall's line colour plus the theme's accent."""
    m = t["materials"]["interior"]
    r = palette.ramp(t, m["ramp"])
    line, dark = r[m.get("pattern", 0)], r[0]
    acc = palette.ramp(t, m.get("accent", "warm"))
    a = canvas(w, h)
    if kind == "picture":
        rect(a, 9, 7, 23, 19, line)
        rect(a, 10, 8, 22, 18, acc[0])
        rect(a, 11, 12, 21, 18, acc[1])        # a landscape: ground, a hill, the sun
        rect(a, 13, 10, 19, 13, acc[1])
        rect(a, 17, 9, 19, 11, step(acc, 3))
    elif kind == "shelf":
        rect(a, 5, 18, 27, 20, line)
        rect(a, 5, 20, 27, 21, dark)
        rect(a, 8, 12, 14, 18, dark)
        rect(a, 9, 13, 13, 17, line)
        rect(a, 17, 14, 21, 18, acc[1])
        rect(a, 22, 11, 25, 18, acc[0])
    elif kind == "poster":
        rect(a, 10, 4, 22, 20, acc[0])
        rect(a, 11, 5, 21, 10, step(acc, 3))
        rect(a, 11, 11, 21, 19, acc[1])
        for y in (13, 15, 17):
            rect(a, 12, y, 18 if y != 15 else 20, y + 1, acc[0])
        put(a, 10, 4, dark, wrap=False)
        put(a, 21, 19, dark, wrap=False)
    elif kind == "pipe":
        rect(a, 6, 0, 9, h, dark)
        rect(a, 7, 0, 8, h, line)
        for y in (6, 22):
            rect(a, 5, y, 10, y + 2, line)
        rect(a, 9, 14, 20, 16, dark)           # a branch with a valve
        rect(a, 9, 14, 20, 15, line)
        rect(a, 18, 11, 22, 14, acc[1])
    elif kind == "clock":
        for dy, (lo, hi) in enumerate(((13, 19), (11, 21), (10, 22), (10, 22), (9, 23), (9, 23), (9, 23), (9, 23), (10, 22), (10, 22), (11, 21), (13, 19))):
            rect(a, lo, 6 + dy, hi, 7 + dy, line)
        for dy, (lo, hi) in enumerate(((13, 19), (12, 20), (11, 21), (11, 21), (11, 21), (11, 21), (12, 20), (13, 19))):
            rect(a, lo, 8 + dy, hi, 9 + dy, step(acc, 3))
        rect(a, 15, 9, 16, 12, dark)
        rect(a, 15, 11, 19, 12, dark)
    else:  # board
        rect(a, 7, 6, 25, 20, line)
        rect(a, 8, 7, 24, 19, acc[0])
        for x, y, c in ((10, 9, step(acc, 3)), (16, 10, acc[2]), (12, 14, acc[2]), (19, 15, step(acc, 3))):
            rect(a, x, y, x + 4, y + 3, c)
            put(a, x + 1, y, dark, wrap=False)
    return a


# --- lit overlays of blocks --------------------------------------------------------------

def keep(a, colours):
    """What glows on a block: only its pixels in the given colours."""
    glow = np.zeros(a.shape[:2], bool)
    for c in colours:
        glow |= (a[..., :3] == c).all(-1)
    a[~glow] = 0
    return a


def glass_lit(t, rng, variant=0):
    """A window with the light on behind it: its panes in the props' `light` tones, keeping
    their reflection and the shade along the bottom. The frame stays out, and so do the
    slats of the blinds, which show as dark gaps."""
    m = t["materials"]["glass"]
    a = glass(t, rng, variant=variant)
    frame, pane, light = palette.ramp(t, m["frame"]), palette.ramp(t, m["pane"]), prop_roles(t).light
    out = canvas(a.shape[1], a.shape[0])
    for y in range(5, a.shape[0] - 5):
        for x in range(10, 22):
            c = tuple(int(v) for v in a[y, x, :3])
            if c not in frame:
                out[y, x] = (*light[1 if c == pane[0] else 2 if c == pane[1] else 3], 255)
    return out


def door_lit(t, rng):
    """The door's lit window and neon sign; nothing for a door with neither (a shutter)."""
    m = t["materials"]["door"]
    return keep(door(t, rng), [c for key in ("window", "sign") if m.get(key) for c in palette.ramp(t, m[key])[1:]])


def chimney_lit(t, rng, variant=0):
    """A sign box's tubes and letters, or the beacon on a stack or pipe; else nothing."""
    m = variant_spec(t["materials"]["chimney"], variant)
    a = chimney(t, rng, variant=variant)
    if m["style"] == "sign":
        return keep(a, palette.ramp(t, m.get("beacon", m.get("band", "magenta")))[1:])
    if m["style"] in ("tank", "ac", "vent") or not m.get("beacon"):
        return canvas(a.shape[1], a.shape[0])
    a[3:] = 0  # the beacon sits on top of the stack
    return keep(a, palette.ramp(t, m["beacon"])[1:])


# --- furnishing: the props of a building's rooms and its roof sign ----------------------

def prop_roles(t):
    """What the theme's props are made of (`materials.props`): role -> four tones, dark to
    light (ink, shade, body, highlight). A role is a ramp name (its top four steps; a
    three-step ramp doubles its darkest) or four [ramp, step] pairs."""
    def tones(v):
        if isinstance(v, str):
            r = palette.ramp(t, v)
            return tuple(r[max(0, len(r) - 4 + i)] for i in range(4))
        return tuple(palette.ramp(t, name)[i] for name, i in v)
    return SimpleNamespace(**{role: tones(v) for role, v in t["materials"]["props"].items()})


def glows(a, g, x0, y0, x1, y1, off, on):
    """A light: `off` on the base sprite, `on` on its lit overlay."""
    rect(a, x0, y0, x1, y1, off)
    rect(g, x0, y0, x1, y1, on)


def dot(a, x, y, c):
    put(a, x, y, c, wrap=False)


def fill(a, mask, c):
    a[mask] = (*c, 255)


def ball(a, cx, cy, rad, c):
    fill(a, disc(np.zeros(a.shape[:2], bool), cx, cy, rad), c)


def box(a, x0, y0, x1, y1, r, face=2):
    """A solid lit from the top-left, no outline: lighter top and left edge, darker bottom
    and right edge."""
    rect(a, x0, y0, x1, y1, r[face])
    rect(a, x0, y0, x1, y0 + 1, r[min(face + 1, 3)])
    rect(a, x0, y0, x0 + 1, y1, r[min(face + 1, 3)])
    rect(a, x1 - 1, y0 + 1, x1, y1, r[face - 1])
    rect(a, x0 + 1, y1 - 1, x1, y1, r[face - 1])


def cyl(a, x0, y0, x1, y1, r, domes=()):
    """A standing cylinder lit from the left; `domes` rounds its ends (the inset per row)."""
    for x in range(x0, x1):
        f = (x - x0) / (x1 - x0 - 1)
        rect(a, x, y0, x + 1, y1, r[2 if f < 0.08 else 3 if f < 0.3 else 2 if f < 0.7 else 1 if f < 0.93 else 0])
    for i, inset in enumerate(domes):
        for y in (y0 + i, y1 - 1 - i):
            a[y, x0:x0 + inset] = 0
            a[y, x1 - inset:x1] = 0


def goods(rng, *kinds):
    return kinds[int(rng.integers(len(kinds)))]


def draw_bar(P, rng, a, g):
    """A bar with a tap, glasses and two stools; the strip under its top lights up."""
    W, M, F, A = P.wood, P.white, P.fabric, P.accent
    rect(a, 4, 11, 60, 32, W[2])
    rect(a, 4, 11, 5, 32, W[3])
    rect(a, 59, 11, 60, 32, W[1])
    for x in range(11, 59, 8):                      # panel joints
        rect(a, x, 16, x + 1, 29, W[1])
    rect(a, 4, 29, 60, 32, W[1])                    # kick board
    rect(a, 4, 29, 60, 30, W[0])
    rect(a, 2, 8, 62, 11, W[3])                     # the top
    rect(a, 2, 10, 62, 11, W[2])
    rect(a, 4, 11, 60, 12, W[0])
    glows(a, g, 5, 12, 59, 14, A[0], A[3])            # the light strip
    rect(g, 5, 13, 59, 14, A[2])
    for c in (18, 46):                              # stools
        rect(a, c - 5, 16, c + 5, 19, F[2])
        rect(a, c - 5, 16, c + 5, 17, F[3])
        rect(a, c - 4, 19, c + 4, 20, F[1])
        rect(a, c - 1, 20, c + 1, 31, M[2])
        rect(a, c - 3, 26, c + 3, 27, M[1])
        rect(a, c - 4, 31, c + 4, 32, M[1])
    rect(a, 30, 2, 32, 8, M[2])                     # tap
    rect(a, 27, 2, 32, 4, M[3])
    rect(a, 27, 4, 28, 5, M[1])
    rect(a, 30, 0, 32, 2, A[2])
    for x in (37, 41):                              # glasses and a bottle
        rect(a, x, 5, x + 2, 8, M[3])
    rect(a, 50, 3, 52, 8, P.accent2[2])
    rect(a, 50, 1, 51, 3, P.accent2[2])


def draw_djdesk(P, rng, a, g):
    """A DJ booth: two turntables and a mixer on a dark desk; the equaliser across its
    front, the mixer's lamps and the corner strips light up."""
    S, M, A, B = P.screen, P.white, P.accent, P.accent2
    rect(a, 5, 12, 59, 32, S[0])
    rect(a, 5, 12, 6, 32, S[3])
    rect(a, 8, 15, 56, 30, S[1])
    rect(a, 3, 9, 61, 12, M[2])                     # the deck
    rect(a, 3, 9, 61, 10, M[3])
    rect(a, 3, 11, 61, 12, M[1])
    for x0 in (7, 41):                              # turntables
        rect(a, x0, 6, x0 + 16, 9, M[1])
        rect(a, x0 + 2, 4, x0 + 14, 6, S[0])
        rect(a, x0 + 3, 4, x0 + 6, 5, S[3])
        rect(a, x0 + 7, 4, x0 + 9, 5, A[2])
        rect(a, x0 + 13, 2, x0 + 15, 4, M[3])
    rect(a, 26, 4, 38, 9, M[1])                     # mixer
    rect(a, 26, 4, 38, 5, M[2])
    for x, r in ((28, A), (31, B), (34, P.light)):
        dot(a, x, 6, M[3])
        glows(a, g, x, 7, x + 2, 8, r[0], r[3])
    for x in range(13, 51, 4):                      # equaliser: a column of lamps per band
        level = int(rng.integers(1, 5))
        for k in range(4):
            rect(a, x, 27 - k * 3, x + 3, 29 - k * 3, S[2])
            if k < level:
                rect(g, x, 27 - k * 3, x + 3, 29 - k * 3, (B[2], B[3], A[2], A[3])[k])
    for x in (9, 53):                               # light strips up the corners
        glows(a, g, x, 16, x + 2, 29, A[0], A[2])
        rect(g, x, 16, x + 1, 29, A[3])


def draw_counter(P, rng, a, g):
    """A shop counter: a glass case of goods in its front; the till, a jar of sweets and
    papers on top."""
    W, M, S = P.wood, P.white, P.screen
    kinds = (P.accent, P.accent2, P.light, P.plant)
    rect(a, 3, 11, 61, 32, W[2])
    rect(a, 3, 11, 4, 32, W[3])
    rect(a, 60, 11, 61, 32, W[1])
    rect(a, 3, 29, 61, 32, W[1])
    rect(a, 3, 29, 61, 30, W[0])
    rect(a, 2, 8, 62, 11, W[3])
    rect(a, 2, 10, 62, 11, W[2])
    rect(a, 3, 11, 61, 12, W[0])
    rect(a, 6, 14, 37, 27, M[2])                    # the glass case
    rect(a, 6, 14, 37, 15, M[3])
    rect(a, 7, 15, 36, 26, S[1])
    rect(a, 7, 20, 36, 21, M[1])
    for sy in (20, 26):
        for x in range(8, 35, 4):
            r = goods(rng, *kinds)
            rect(a, x, sy - 3, x + 3, sy, r[2])
            rect(a, x, sy - 3, x + 3, sy - 2, r[3])
    for x in (41, 51):                              # panels
        rect(a, x, 15, x + 7, 27, W[1])
        rect(a, x + 1, 16, x + 7, 27, W[2])
    box(a, 43, 3, 57, 8, M)                         # the till
    rect(a, 45, 0, 53, 3, M[1])
    rect(a, 46, 1, 52, 2, S[1])
    for x in (46, 49, 52):
        dot(a, x, 5, S[0])
    rect(a, 9, 3, 15, 8, M[2])                      # the jar
    rect(a, 9, 3, 10, 8, M[3])
    for x, y, r in ((11, 5, kinds[0]), (13, 6, kinds[2]), (12, 4, kinds[1]), (11, 7, kinds[1]), (13, 4, kinds[0])):
        dot(a, x, y, r[2])
    rect(a, 10, 1, 14, 3, P.accent[2])
    rect(a, 22, 6, 32, 8, M[3])                     # papers
    rect(a, 22, 7, 32, 8, M[1])
    rect(a, 22, 5, 32, 6, P.accent2[2])


def draw_desk(P, rng, a, g):
    """An office desk: drawers, a monitor whose screen lights up, keyboard, mug, papers."""
    W, M, S = P.wood, P.white, P.screen
    rect(a, 5, 18, 8, 32, W[2])                     # leg
    rect(a, 5, 18, 6, 32, W[3])
    rect(a, 8, 18, 43, 23, W[1])                    # modesty panel
    rect(a, 43, 18, 59, 32, W[2])                   # drawers
    rect(a, 43, 18, 44, 32, W[3])
    rect(a, 58, 18, 59, 32, W[1])
    for y in (18, 23, 28):
        rect(a, 44, y + 4, 58, y + 5, W[0])
        rect(a, 48, y + 2, 54, y + 3, M[3])
    rect(a, 3, 15, 61, 18, W[2])                    # top
    rect(a, 3, 15, 61, 16, W[3])
    rect(a, 4, 17, 60, 18, W[0])
    rect(a, 12, 0, 34, 12, S[0])                    # monitor
    glows(a, g, 13, 1, 33, 11, S[1], P.glow[2])
    for k in range(4):
        rect(a, 16 + k, 6 - k, 18 + k, 7 - k, S[2])
    rect(g, 13, 1, 33, 3, P.glow[3])                # what's on: a title bar, lines, a chart
    for y in (5, 7, 9):
        rect(g, 15, y, 20 + int(rng.integers(0, 6)), y + 1, P.glow[3])
    rect(g, 27, 5, 32, 10, P.accent[2])
    rect(a, 21, 12, 25, 14, M[1])
    rect(a, 18, 14, 28, 15, M[2])
    rect(a, 30, 14, 40, 15, M[1])                   # keyboard
    rect(a, 41, 11, 44, 15, P.accent[2])            # mug
    dot(a, 41, 11, P.accent[3])
    rect(a, 47, 12, 57, 15, M[3])                   # papers
    rect(a, 47, 13, 57, 14, M[2])


def draw_sofa(P, rng, a, g):
    """A two-seater: back, arms, two seat cushions, wooden feet."""
    F, W = P.fabric, P.wood
    rect(a, 7, 9, 57, 10, F[3])                     # the back
    rect(a, 6, 10, 58, 24, F[2])
    rect(a, 6, 10, 7, 24, F[3])
    rect(a, 31, 11, 33, 20, F[1])
    for x0 in (2, 54):                              # arms
        rect(a, x0 + 1, 14, x0 + 7, 15, F[3])
        rect(a, x0, 15, x0 + 8, 29, F[2])
        rect(a, x0, 15, x0 + 1, 29, F[3])
        rect(a, x0 + 7, 16, x0 + 8, 29, F[1])
    rect(a, 10, 20, 54, 21, F[3])                   # seat cushions
    rect(a, 10, 21, 54, 26, F[2])
    rect(a, 31, 21, 33, 26, F[1])
    rect(a, 10, 26, 54, 29, F[1])
    rect(a, 2, 29, 62, 30, F[0])
    for x in (4, 57):
        rect(a, x, 30, x + 3, 32, W[1])


def draw_bed(P, rng, a, g):
    """A bed from the side: headboard, pillow, a blanket turned down over the mattress."""
    W, M, F = P.wood, P.white, P.fabric
    box(a, 2, 6, 6, 30, W)                          # headboard, footboard
    box(a, 58, 14, 62, 30, W)
    rect(a, 2, 30, 5, 32, W[1])
    rect(a, 59, 30, 62, 32, W[1])
    rect(a, 6, 25, 58, 29, W[2])                    # frame
    rect(a, 6, 28, 58, 29, W[1])
    rect(a, 6, 19, 58, 25, M[2])                    # mattress
    rect(a, 6, 19, 58, 20, M[3])
    rect(a, 9, 13, 19, 14, M[3])                    # pillow
    rect(a, 8, 14, 20, 19, M[3])
    rect(a, 8, 18, 20, 19, M[2])
    rect(a, 24, 17, 58, 27, F[2])                   # blanket
    rect(a, 24, 17, 58, 18, F[3])
    rect(a, 24, 17, 29, 27, F[3])
    rect(a, 29, 18, 30, 27, F[1])
    rect(a, 24, 26, 58, 27, F[1])


def draw_table(P, rng, a, g):
    """A table laid for two, a chair at each end."""
    W, M = P.wood, P.white
    for post, seat, leg in ((3, 3, 12), (58, 49, 49)):     # chairs: back post, seat, front leg
        rect(a, post, 3, post + 3, 32, W[2])
        rect(a, post, 3, post + 1, 32, W[3])
        rect(a, seat, 19, seat + 12, 22, W[2])
        rect(a, seat, 19, seat + 12, 20, W[3])
        rect(a, leg, 22, leg + 3, 32, W[2])
        rect(a, leg + 2, 22, leg + 3, 32, W[1])
        rect(a, min(post, leg) + 3, 27, max(post, leg), 28, W[1])
    rect(a, 16, 16, 19, 32, W[2])                   # table legs, apron, top
    rect(a, 16, 16, 17, 32, W[3])
    rect(a, 45, 16, 48, 32, W[2])
    rect(a, 47, 16, 48, 32, W[1])
    rect(a, 15, 16, 49, 18, W[1])
    rect(a, 13, 13, 51, 16, W[2])
    rect(a, 13, 13, 51, 14, W[3])
    rect(a, 30, 8, 34, 13, P.accent2[2])            # a vase of flowers
    rect(a, 30, 8, 31, 13, P.accent2[3])
    rect(a, 31, 6, 33, 8, P.accent2[2])
    for x, y in ((32, 5), (31, 4), (33, 3)):
        dot(a, x, y, P.plant[2])
    for x, y in ((30, 3), (33, 1), (34, 4), (31, 1)):
        rect(a, x, y, x + 2, y + 2, P.accent[3])
    rect(a, 20, 12, 27, 13, M[3])                   # plates and cups
    rect(a, 37, 12, 44, 13, M[3])
    rect(a, 22, 10, 25, 12, M[2])
    rect(a, 39, 10, 42, 12, M[2])


def draw_plant(P, rng, a, g):
    """A pot plant: long leaves fanning out of a clay pot."""
    W, L = P.wood, P.plant
    h, w = a.shape[:2]
    for i, (ang, ln) in enumerate(((-158, 10), (-22, 10), (-135, 13), (-45, 13), (-112, 15), (-68, 15), (-90, 17))):
        leaf = np.zeros((h, w), bool)
        ca, sa = np.cos(np.radians(ang)), np.sin(np.radians(ang))
        for s in np.arange(1, ln, 0.4):
            half = 0.5 + 1.6 * np.sin(np.pi * s / ln)
            for d in np.arange(-half, half + 0.01, 0.4):
                x, y = int(round(15.5 + ca * s - sa * d)), int(round(22 + sa * s + ca * d))
                if 0 <= x < w and 0 <= y < 23:
                    leaf[y, x] = True
        pad = np.pad(leaf, 1)                       # lit along its upper left, dark along its lower right
        fill(a, leaf, L[2])
        fill(a, leaf & ~(pad[2:, 1:-1] & pad[1:-1, 2:]), L[1])
        fill(a, leaf & ~(pad[:-2, 1:-1] & pad[1:-1, :-2]), L[3])
    rect(a, 10, 22, 22, 24, W[2])                   # pot
    rect(a, 10, 22, 22, 23, W[3])
    for y in range(24, 32):
        inset = (y - 24) // 3
        rect(a, 11 + inset, y, 21 - inset, y + 1, W[2])
        dot(a, 11 + inset, y, W[3])
        dot(a, 20 - inset, y, W[1])


def draw_workbench(P, rng, a, g):
    """A workbench: a vise, a hammer and a paint tin on its top, a toolbox and a bucket
    on the shelf under it."""
    W, M, A = P.wood, P.white, P.accent
    for x in (4, 55):                               # legs
        rect(a, x, 17, x + 5, 32, W[2])
        rect(a, x, 17, x + 1, 32, W[3])
        rect(a, x + 4, 17, x + 5, 32, W[1])
    rect(a, 9, 26, 55, 28, W[2])                    # shelf
    rect(a, 9, 26, 55, 27, W[3])
    box(a, 14, 19, 29, 26, A)                       # toolbox
    rect(a, 14, 21, 29, 22, A[1])
    rect(a, 19, 17, 24, 18, M[2])
    dot(a, 19, 18, M[2])
    dot(a, 23, 18, M[2])
    for y in range(19, 26):                         # bucket
        rect(a, 39 + (y - 19) // 4, y, 49 - (y - 19) // 4, y + 1, M[2])
    rect(a, 39, 19, 49, 20, M[3])
    rect(a, 2, 13, 62, 17, W[2])                    # the top
    rect(a, 2, 13, 62, 14, W[3])
    rect(a, 2, 16, 62, 17, W[0])
    rect(a, 7, 7, 11, 13, M[2])                     # vise
    rect(a, 7, 7, 8, 13, M[3])
    rect(a, 13, 8, 17, 13, M[2])
    rect(a, 17, 10, 23, 11, M[1])
    rect(a, 23, 7, 24, 14, M[3])
    rect(a, 28, 11, 40, 13, W[3])                   # hammer
    rect(a, 38, 9, 42, 13, M[1])
    rect(a, 38, 9, 42, 10, M[2])
    rect(a, 48, 6, 55, 13, M[2])                    # paint tin
    rect(a, 48, 6, 49, 13, M[3])
    rect(a, 48, 8, 55, 11, P.accent2[2])
    rect(a, 48, 5, 55, 6, M[1])


def carton(a, x0, y0, x1, y1, P):
    """A cardboard box: taped down the middle, a label."""
    box(a, x0, y0, x1, y1, P.wood)
    rect(a, (x0 + x1) // 2, y0, (x0 + x1) // 2 + 1, y0 + 4, P.wood[1])
    rect(a, x0 + 2, y1 - 5, x0 + 6, y1 - 2, P.white[3])


def draw_pallet(P, rng, a, g):
    """A pallet stacked with cartons."""
    W = P.wood
    for x0, x1 in ((4, 23), (23, 42), (42, 60)):
        carton(a, x0, 12, x1, 26, P)
    carton(a, 8, 1, 29, 12, P)
    carton(a, 31, 3, 52, 12, P)
    rect(a, 31, 6, 52, 8, P.accent[2])              # a strapped one
    rect(a, 2, 26, 62, 28, W[2])                    # the pallet: deck, blocks, bottom boards
    rect(a, 2, 26, 62, 27, W[3])
    for x in (3, 29, 55):
        rect(a, x, 28, x + 6, 31, W[1])
    rect(a, 2, 31, 62, 32, W[2])


def draw_speaker(P, rng, a, g):
    """A PA speaker: a black cabinet with a handle, the tweeter above the woofer."""
    S, M = P.screen, P.white
    rect(a, 5, 8, 27, 64, S[0])
    rect(a, 5, 8, 27, 9, S[3])
    rect(a, 5, 8, 6, 64, S[3])
    rect(a, 7, 11, 25, 62, S[1])
    rect(a, 12, 6, 20, 8, M[1])
    for cy, rad in ((21, 6), (43, 9.5)):
        yy, xx = np.mgrid[0:64, 0:32]
        d = np.hypot(xx - 15.5, yy - cy + 0.5)
        fill(a, d <= rad, M[1])
        fill(a, (d <= rad) & (xx + yy < 15 + cy), M[2])
        fill(a, d <= rad - 1.5, S[0])
        fill(a, d <= rad - 3, S[2])
        fill(a, d <= rad / 3.2, M[2])
        dot(a, 15, cy - 1, M[3])
    rect(a, 10, 56, 22, 59, S[0])                   # bass port
    for x, y in ((7, 11), (24, 11), (7, 61), (24, 61)):
        dot(a, x, y, M[2])


def draw_fridge(P, rng, a, g):
    """A drinks fridge: rows of bottles behind a glass door; inside and the sign on its
    head light up."""
    M, S, A = P.white, P.screen, P.accent
    box(a, 5, 3, 27, 62, M)
    rect(a, 6, 62, 9, 64, S[0])
    rect(a, 23, 62, 26, 64, S[0])
    glows(a, g, 7, 5, 25, 11, A[0], A[2])             # the sign on its head
    for x in range(9, 23, 4):
        glows(a, g, x, 7, x + 2, 9, A[1], A[3])
    glows(a, g, 7, 13, 23, 57, S[1], P.glow[3])       # the door
    rect(a, 24, 27, 25, 43, M[3])                   # handle
    for sy in (23, 34, 45, 56):                     # a shelf of bottles
        for x in range(8, 22, 3):
            r = goods(rng, P.accent, P.accent2, P.light, P.plant)
            glows(a, g, x, sy - 6, x + 2, sy, r[1], r[2])
            glows(a, g, x, sy - 8, x + 1, sy - 6, r[1], r[2])
            dot(g, x, sy - 5, r[3])
        rect(a, 7, sy, 23, sy + 1, M[1])
        g[sy, 7:23] = 0
    rect(a, 7, 58, 25, 61, M[1])                    # grille
    for x in range(8, 25, 2):
        rect(a, x, 59, x + 1, 60, S[0])


def draw_rack(P, rng, a, g):
    """Shop shelves: boxes, tins, magazines cover out, more boxes."""
    W, M = P.wood, P.white
    kinds = (P.accent, P.accent2, P.light, P.plant, P.fabric)
    rect(a, 3, 5, 29, 64, W[1])
    for x0 in (3, 27):
        rect(a, x0, 5, x0 + 2, 64, W[2])
    rect(a, 3, 5, 4, 64, W[3])
    rect(a, 3, 5, 29, 7, W[3])
    for row, sy in enumerate((19, 33, 47, 61)):     # shelf tops
        x = 6
        while x < 25:
            r = goods(rng, *kinds)
            if row == 1:                            # tins
                wd, ht = 3, 6
                rect(a, x, sy - ht, x + wd, sy, r[2])
                rect(a, x, sy - ht, x + wd, sy - ht + 1, M[3])
                rect(a, x, sy - 3, x + wd, sy - 2, r[3])
            elif row == 2:                          # magazines
                wd, ht = 5, 10
                rect(a, x, sy - ht, x + wd, sy, r[2])
                rect(a, x + 1, sy - ht + 1, x + wd - 1, sy - ht + 3, M[3])
                rect(a, x + 1, sy - 5, x + 3, sy - 2, r[3])
            else:                                   # boxes
                wd, ht = min(int(rng.integers(4, 7)), 27 - x), int(rng.integers(6, 11))
                rect(a, x, sy - ht, x + wd, sy, r[2])
                rect(a, x, sy - ht, x + wd, sy - ht + 1, r[3])
                rect(a, x + wd - 1, sy - ht + 1, x + wd, sy, r[1])
            x += wd + 1
        rect(a, 5, sy, 27, sy + 2, W[2])
        rect(a, 5, sy, 27, sy + 1, W[3])
    rect(a, 3, 63, 29, 64, W[0])


def draw_cabinet(P, rng, a, g):
    """An arcade cabinet from the front: marquee, screen, stick and buttons, coin door.
    The marquee, the game on the screen, the buttons and the coin slots light up."""
    B, S, M, A, C = P.metal, P.screen, P.white, P.accent, P.accent2
    rect(a, 6, 4, 26, 62, B[1])
    rect(a, 6, 4, 7, 62, B[3])
    rect(a, 25, 4, 26, 62, B[0])
    rect(a, 5, 3, 27, 5, B[3])
    glows(a, g, 8, 6, 24, 12, A[0], A[2])             # marquee
    for x in range(10, 22, 3):
        glows(a, g, x, 8, x + 2, 10, A[1], A[3])
    rect(a, 8, 13, 24, 30, S[0])                    # bezel and screen
    glows(a, g, 10, 15, 22, 28, S[1], S[2])
    for k in range(3):
        dot(a, 12 + k, 19 - k, S[2])
    for x, y in ((11, 17), (15, 17), (19, 17), (13, 20), (17, 20)):     # the game
        rect(g, x, y, x + 2, y + 2, A[3])
    rect(g, 15, 25, 18, 27, P.light[3])
    dot(g, 16, 24, P.light[3])
    dot(g, 16, 22, M[3])
    rect(a, 4, 33, 28, 38, M[2])                    # control panel
    rect(a, 4, 33, 28, 34, M[3])
    rect(a, 4, 37, 28, 38, M[1])
    rect(a, 10, 31, 11, 34, M[3])                   # stick
    rect(a, 9, 29, 12, 32, A[2])
    dot(a, 9, 29, A[3])
    for x, r in ((16, C), (19, P.light), (22, C)):
        glows(a, g, x, 35, x + 2, 36, r[0], r[3])
    rect(a, 12, 43, 20, 54, S[0])                   # coin door
    rect(a, 12, 43, 20, 44, B[2])
    glows(a, g, 15, 46, 17, 49, P.light[0], P.light[3])
    for y in (51, 53):
        rect(a, 14, y, 18, y + 1, B[1])
    for y in range(40, 60, 4):                      # side stripes
        rect(a, 8, y, 10, y + 2, C[2])
        rect(a, 22, y, 24, y + 2, C[2])
    rect(a, 5, 62, 27, 64, S[0])


def draw_slot(P, rng, a, g):
    """A slot machine: three reels behind glass, a lever, buttons, the payout tray. The
    reels, the lamps round them, the buttons and the lamp on top light up."""
    B, S, M, A, C, L = P.metal, P.screen, P.white, P.accent, P.accent2, P.light
    rect(a, 8, 9, 24, 11, B[3])                     # rounded head
    rect(a, 7, 11, 25, 14, B[2])
    rect(a, 6, 14, 26, 60, B[2])
    rect(a, 6, 14, 7, 60, B[3])
    rect(a, 25, 14, 26, 60, B[1])
    rect(a, 14, 7, 18, 9, B[1])                     # the lamp on top
    ball(a, 16, 5, 3.2, L[0])
    ball(g, 16, 5, 3.2, L[3])
    rect(a, 8, 17, 24, 31, S[0])                    # the window and its reels
    for x in (9, 14, 19):
        glows(a, g, x, 19, x + 4, 29, M[1], M[3])
    for x, y in ((9, 22), (10, 22), (11, 22), (11, 23), (10, 24), (10, 25)):     # 7
        glows(a, g, x, y, x + 1, y + 1, A[0], A[2])
    for x, y in ((14, 25), (16, 25)):                                            # cherries
        glows(a, g, x, y, x + 2, y + 2, A[0], A[2])
    for x, y in ((15, 24), (16, 23), (16, 22)):
        glows(a, g, x, y, x + 1, y + 1, P.plant[1], P.plant[2])
    rect(a, 19, 23, 23, 26, S[0])                                                # BAR
    g[23:26, 19:23] = 0
    for x in range(9, 24, 3):                       # lamps over and under the window
        glows(a, g, x, 15, x + 1, 16, L[0], L[3])
        glows(a, g, x, 32, x + 1, 33, A[0], A[3])
    rect(a, 27, 22, 29, 40, M[2])                   # lever
    rect(a, 26, 38, 29, 41, B[1])
    ball(a, 28, 20, 2.6, A[2])
    dot(a, 27, 18, A[3])
    rect(a, 8, 35, 24, 39, M[2])                    # buttons
    rect(a, 8, 35, 24, 36, M[3])
    for x, r in ((10, A), (15, C), (20, A)):
        glows(a, g, x, 36, x + 2, 38, r[0], r[3])
    rect(a, 9, 43, 23, 48, S[0])                    # payout tray
    rect(a, 8, 48, 24, 50, M[3])
    for x in (12, 15, 19):
        dot(a, x, 47, L[2])
    rect(a, 9, 52, 23, 58, C[1])                    # a diamond on its belly
    for i in range(3):
        rect(a, 15 - i * 2, 53 + i, 17 + i * 2, 57 - i, C[2])
    rect(a, 5, 60, 27, 64, B[1])
    rect(a, 5, 60, 27, 61, B[3])


def draw_filing(P, rng, a, g):
    """A filing cabinet: four drawers with label and handle; a box and a binder on top."""
    B, M = P.metal, P.white
    rect(a, 6, 12, 26, 63, B[1])
    rect(a, 6, 12, 26, 13, B[3])
    rect(a, 6, 12, 7, 63, B[3])
    rect(a, 25, 13, 26, 63, B[0])
    for y in range(14, 62, 12):
        rect(a, 8, y, 24, y + 11, B[2])
        rect(a, 8, y, 24, y + 1, B[3])
        rect(a, 8, y + 10, 24, y + 11, B[0])
        rect(a, 13, y + 2, 19, y + 5, M[3])         # label
        rect(a, 14, y + 3, 17, y + 4, M[1])
        rect(a, 12, y + 7, 20, y + 8, M[2])         # handle
        rect(a, 12, y + 8, 20, y + 9, B[0])
    rect(a, 7, 63, 25, 64, B[0])
    carton(a, 8, 5, 19, 12, P)
    rect(a, 20, 3, 23, 12, P.accent[2])
    rect(a, 20, 3, 21, 12, P.accent[3])
    dot(a, 21, 9, M[3])


def draw_racking(P, rng, a, g):
    """Warehouse racking: cartons, a crate and drums on beams between two uprights."""
    B, W, A, M, C = P.metal, P.wood, P.accent, P.white, P.accent2
    carton(a, 6, 8, 16, 20, P)
    carton(a, 17, 4, 26, 20, P)
    box(a, 7, 27, 25, 40, W)                        # a crate on a pallet
    for x in (12, 18):
        rect(a, x, 28, x + 1, 39, W[1])
    rect(a, 6, 40, 26, 42, W[1])
    for x0, r in ((6, C), (17, M)):                 # drums
        cyl(a, x0, 50, x0 + 9, 64, r)
        rect(a, x0, 54, x0 + 9, 55, r[1])
        rect(a, x0, 59, x0 + 9, 60, r[1])
    for x0 in (2, 27):                              # uprights
        rect(a, x0, 2, x0 + 3, 64, B[2])
        rect(a, x0, 2, x0 + 1, 64, B[3])
        rect(a, x0 + 2, 2, x0 + 3, 64, B[1])
        for y in range(5, 62, 4):
            dot(a, x0 + 1, y, B[0])
    for y in (20, 42):                              # beams
        rect(a, 2, y, 30, y + 3, A[2])
        rect(a, 2, y, 30, y + 1, A[3])
        rect(a, 2, y + 2, 30, y + 3, A[1])
    rect(a, 1, 62, 6, 64, B[1])
    rect(a, 26, 62, 31, 64, B[1])


def gauge(a, cx, cy, P):
    ball(a, cx, cy, 4.2, P.white[1])
    ball(a, cx, cy, 3.2, P.white[3])
    dot(a, cx - 1, cy, P.screen[0])
    dot(a, cx, cy - 1, P.screen[0])
    dot(a, cx + 1, cy - 2, P.screen[0])


def draw_tank(P, rng, a, g):
    """A pressure tank on legs: domed ends, riveted bands, a gauge, a warning label, a
    valve wheel on top."""
    B, M, A, S = P.metal, P.white, P.accent, P.screen
    rect(a, 8, 56, 11, 64, B[1])
    rect(a, 21, 56, 24, 64, B[1])
    cyl(a, 7, 6, 25, 58, B, domes=(5, 3, 1))
    rect(a, 12, 6, 20, 7, B[3])
    for y in (13, 50):                              # bands
        rect(a, 7, y, 25, y + 1, B[1])
        for x in range(9, 24, 4):
            dot(a, x, y + 1, B[3])
    gauge(a, 16, 26, P)
    rect(a, 12, 36, 20, 44, A[2])                   # warning label
    rect(a, 12, 36, 20, 37, A[3])
    rect(a, 15, 38, 17, 41, S[0])
    rect(a, 15, 42, 17, 43, S[0])
    rect(a, 15, 2, 17, 6, M[2])                     # valve
    rect(a, 11, 1, 21, 3, A[2])
    rect(a, 11, 1, 21, 2, A[3])


def draw_machine(P, rng, a, g):
    """A vat on legs piped to its control cabinet: porthole, gauges, switches, a hand
    wheel, and three indicator lamps that light up."""
    B, M, A, S = P.metal, P.white, P.accent, P.screen
    rect(a, 6, 54, 9, 64, B[1])                     # the vat
    rect(a, 31, 54, 34, 64, B[1])
    cyl(a, 4, 14, 36, 56, B, domes=(9, 6, 3, 1))
    for y in (22, 47):
        rect(a, 4, y, 36, y + 1, B[1])
        for x in range(6, 35, 4):
            dot(a, x, y + 1, B[3])
    ball(a, 20, 35, 6.2, M[2])                      # porthole
    ball(a, 20, 35, 4.6, S[1])
    dot(a, 18, 33, S[3])
    dot(a, 19, 32, S[3])
    rect(a, 14, 10, 26, 14, B[1])                   # hatch
    rect(a, 14, 10, 26, 11, B[3])
    rect(a, 19, 4, 22, 10, M[2])                    # the pipe over to the cabinet
    rect(a, 19, 4, 50, 7, M[2])
    rect(a, 19, 4, 50, 5, M[3])
    rect(a, 47, 7, 50, 22, M[2])
    rect(a, 47, 7, 48, 22, M[3])
    rect(a, 19, 6, 47, 7, M[1])
    rect(a, 49, 7, 50, 22, M[1])
    rect(a, 31, 3, 33, 8, A[2])
    rect(a, 46, 14, 51, 16, A[2])
    rect(a, 18, 56, 22, 61, M[2])                   # drain valve
    rect(a, 15, 60, 25, 62, A[2])
    box(a, 40, 22, 62, 64, B)                       # the cabinet
    gauge(a, 46, 29, P)
    gauge(a, 56, 29, P)
    for x, r in ((43, A), (50, P.glow), (57, P.light)):
        glows(a, g, x, 36, x + 3, 39, r[0], r[3])
        rect(g, x, 38, x + 3, 39, r[2])
    rect(a, 42, 42, 60, 43, B[1])
    for x in (44, 48, 52, 56):                      # switches
        rect(a, x, 45, x + 2, 48, M[2])
        dot(a, x, 45, M[3])
    yy, xx = np.mgrid[0:64, 0:64]
    d = np.hypot(xx - 50.5, yy - 55.5)
    fill(a, (d <= 5.2) & (d > 3.6), A[2])           # hand wheel
    rect(a, 46, 55, 56, 56, A[2])
    rect(a, 50, 51, 51, 60, A[2])
    rect(a, 50, 55, 52, 57, M[3])


def draw_vault(P, rng, a, g):
    """A bank vault: the round door in its steel frame, bolts round its rim, a spoked
    brass wheel, two hinges; the keypad lights up."""
    B, M, L, S = P.metal, P.white, P.light, P.screen
    rect(a, 1, 1, 63, 64, B[1])
    rect(a, 1, 1, 63, 2, B[3])
    rect(a, 1, 1, 2, 64, B[3])
    rect(a, 62, 2, 63, 64, B[0])
    for k in range(4, 62, 8):                       # rivets round the frame
        for x, y in ((k, 3), (3, k), (60, k)):
            dot(a, x, y, M[2])
    yy, xx = np.mgrid[0:64, 0:64]
    dx, dy = xx - 31.5, yy - 32.5
    d = np.hypot(dx, dy)
    fill(a, d <= 27.5, B[0])                        # the gap round the door
    fill(a, d <= 26.5, B[2])
    fill(a, (d <= 26.5) & (d > 23.5), B[1])
    fill(a, (d <= 26.5) & (d > 23.5) & (dx + dy < 0), B[3])
    fill(a, (d <= 18.5) & (d > 17), B[1])
    for k in range(8):                              # bolts
        x = int(round(31.5 + 21 * np.cos(k * np.pi / 4 + np.pi / 8)))
        y = int(round(32.5 + 21 * np.sin(k * np.pi / 4 + np.pi / 8)))
        rect(a, x - 1, y - 1, x + 2, y + 2, M[2])
        dot(a, x - 1, y - 1, M[3])
    for k in range(6):                              # the wheel's spokes, rim and hub
        for s in range(3, 13):
            x = int(round(31.5 + s * np.cos(k * np.pi / 3)))
            y = int(round(32.5 + s * np.sin(k * np.pi / 3)))
            rect(a, x, y, x + 1, y + 1, L[1])
        rect(a, x - 1, y - 1, x + 2, y + 2, L[2])
    fill(a, (d <= 9.5) & (d > 8), L[1])
    fill(a, (d <= 9.5) & (d > 8) & (dx + dy < 0), L[2])
    fill(a, d <= 3.5, L[1])
    fill(a, d <= 2, L[2])
    for y in (14, 42):                              # hinges
        rect(a, 0, y, 7, y + 9, M[2])
        rect(a, 0, y, 7, y + 1, M[3])
        rect(a, 0, y + 8, 7, y + 9, M[1])
    rect(a, 52, 50, 61, 62, S[0])                   # keypad
    glows(a, g, 53, 51, 60, 54, S[1], P.glow[3])
    for ky in (56, 58, 60):
        for kx in (54, 56, 58):
            glows(a, g, kx, ky, kx + 1, ky + 1, S[2], P.glow[2])


def draw_tv(P, rng, a, g):
    """A flat TV on the wall; its picture lights up."""
    S = P.screen
    rect(a, 3, 7, 29, 25, S[0])
    glows(a, g, 4, 8, 28, 24, S[1], P.glow[2])
    for k in range(5):
        rect(a, 8 + k, 14 - k, 10 + k, 15 - k, S[2])
    rect(g, 4, 8, 28, 14, P.glow[3])                # what's on: sky, a sun, hills, a ticker
    rect(g, 19, 10, 23, 14, P.light[3])
    rect(g, 4, 16, 12, 18, P.glow[1])
    rect(g, 10, 17, 22, 19, P.glow[1])
    rect(g, 4, 19, 28, 21, P.glow[1])
    rect(g, 4, 21, 28, 24, P.accent[2])
    for x in range(6, 26, 4):
        rect(g, x, 22, x + 2, 23, P.accent[3])
    glows(a, g, 26, 24, 27, 25, P.accent[0], P.accent[3])


def tube_path(a, g, pts, off, on):
    """A bent neon tube through the points: thin and grey when off; lit, a bright core with
    its glow below and right of it."""
    path = []
    for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
        n = max(abs(xb - xa), abs(yb - ya), 1)
        path += [(int(round(xa + (xb - xa) * k / n)), int(round(ya + (yb - ya) * k / n))) for k in range(n + 1)]
    for x, y in path:
        rect(a, x, y, x + 2, y + 2, off[1])
        rect(g, x, y, x + 2, y + 2, on[2])
    for x, y in path:
        dot(a, x, y, off[2])
        dot(g, x, y, on[3])


def draw_neon(P, rng, a, g):
    """A neon figure on the wall: a cocktail glass with an olive, three chevrons."""
    off = P.white
    tube_path(a, g, [(6, 5), (26, 5), (16, 15), (6, 5)], off, P.accent)
    tube_path(a, g, [(16, 15), (16, 25)], off, P.accent)
    tube_path(a, g, [(10, 25), (22, 25)], off, P.accent)
    tube_path(a, g, [(18, 9), (24, 2)], off, P.light)
    for x in (33, 42, 51):
        tube_path(a, g, [(x, 7), (x + 7, 15), (x, 23)], off, P.accent2)


def draw_bottles(P, rng, a, g):
    """The back-bar: two shelves of bottles against a dark panel; lit, the panel glows
    behind them and each bottle shows a rim of light."""
    S, W, L = P.screen, P.wood, P.light
    rect(a, 3, 2, 61, 30, S[1])
    for sy in (15, 29):                             # the glow is brightest along each shelf
        for y0, y1, tone in ((sy - 9, sy - 5, 1), (sy - 5, sy - 2, 2), (sy - 2, sy, 3)):
            rect(g, 3, y0, 61, y1, L[tone])
    for sy in (15, 29):
        x = 5
        while x < 57:
            wd, ht = int(rng.integers(3, 5)), int(rng.integers(7, 12))
            r = goods(rng, P.accent, P.accent2, P.light, P.glow, P.plant)
            rect(a, x, sy - ht + 3, x + wd, sy, r[1])
            rect(a, x + 1, sy - ht, x + wd - 1, sy - ht + 3, r[1])
            dot(a, x + 1, sy - ht, P.white[2])
            g[sy - ht + 3:sy, x:x + wd] = 0         # a silhouette against the glow
            g[sy - ht:sy - ht + 3, x + 1:x + wd - 1] = 0
            rect(g, x, sy - ht + 4, x + 1, sy - 1, r[3])
            x += wd + int(rng.integers(1, 3))
        rect(a, 2, sy, 62, sy + 2, W[2])
        rect(a, 2, sy, 62, sy + 1, W[3])
        g[sy:sy + 2] = 0


def draw_lamp(P, rng, a, g):
    """A pendant lamp: flex, a metal shade, the bulb under it lights up."""
    B, M, L = P.metal, P.white, P.light
    rect(a, 15, 0, 17, 10, P.screen[0])
    rect(a, 14, 9, 18, 11, B[1])
    for y in range(11, 18):                         # the shade widens downwards
        half = 3 + (y - 11) * 7 // 6
        rect(a, 16 - half, y, 16 + half, y + 1, B[2])
        rect(a, 16 - half, y, 16 - half + 2, y + 1, B[3])
        rect(a, 16 + half - 2, y, 16 + half, y + 1, B[1])
    glows(a, g, 6, 18, 26, 19, B[1], L[2])            # its lit inside
    glows(a, g, 13, 19, 19, 22, M[2], L[3])           # the bulb
    glows(a, g, 14, 22, 18, 23, M[1], L[2])


def draw_tube(P, rng, a, g):
    """A fluorescent tube in its fitting, hung on two rods; the tube lights up."""
    B, M, G = P.metal, P.white, P.glow
    for x in (13, 50):
        rect(a, x, 0, x + 1, 5, P.screen[0])
    rect(a, 5, 5, 59, 9, B[2])                      # fitting
    rect(a, 5, 5, 59, 6, B[3])
    rect(a, 5, 8, 59, 9, B[1])
    rect(a, 6, 9, 8, 12, B[1])                      # end caps
    rect(a, 56, 9, 58, 12, B[1])
    glows(a, g, 8, 9, 56, 12, M[2], G[3])
    rect(a, 8, 11, 56, 12, M[1])
    rect(g, 8, 11, 56, 12, G[2])


def draw_discoball(P, rng, a, g):
    """A mirror ball on its wire: a grid of facets lit from the top-left; lit, some facets
    throw back the room's colours and glints stand round it."""
    M = P.white
    rect(a, 15, 0, 17, 8, P.screen[0])
    rect(a, 14, 8, 18, 10, M[1])
    colours = (P.accent, P.accent2, P.light, P.glow)
    for y in range(10, 28):
        for x in range(7, 25):
            dx, dy = (x - 15.5) / 9, (y - 18.5) / 9
            if dx * dx + dy * dy > 1:
                continue
            toward = -(dx + dy)                     # how far the facet faces the light
            tone = 3 if toward > 0.55 else 2 if toward > -0.1 else 1 if toward > -0.8 else 0
            if x % 3 == 1 or y % 3 == 1:            # the joints between facets
                tone = max(tone - 1, 0)
            dot(a, x, y, M[tone])
    for _ in range(9):                              # facets that flash
        x, y = 8 + int(rng.integers(0, 6)) * 3, 11 + int(rng.integers(0, 6)) * 3
        r = goods(rng, *colours)
        for px in (x, x + 1):
            for py in (y, y + 1):
                if a[py, px, 3]:
                    dot(g, px, py, r[3])
    for (x, y), r in zip(((4, 12), (27, 15), (8, 27), (25, 26)), colours):   # glints
        rect(g, x - 1, y, x + 2, y + 1, r[2])
        rect(g, x, y - 1, x + 1, y + 2, r[2])
        dot(g, x, y, r[3])


def draw_spot(P, rng, a, g):
    """A rig of three spotlights on a bar; their lenses light up, each in its colour."""
    S, M = P.screen, P.white
    for x in (7, 23):
        rect(a, x, 0, x + 2, 3, S[0])
    rect(a, 2, 3, 30, 6, M[2])                      # the bar
    rect(a, 2, 3, 30, 4, M[3])
    rect(a, 2, 5, 30, 6, M[1])
    for c, r in ((7, P.accent), (16, P.light), (25, P.accent2)):
        rect(a, c - 1, 6, c + 1, 8, S[0])           # yoke and can
        rect(a, c - 3, 8, c + 3, 14, S[0])
        rect(a, c - 4, 12, c + 4, 16, S[0])
        rect(a, c - 3, 8, c - 2, 14, S[3])
        glows(a, g, c - 3, 16, c + 3, 18, r[0], r[3])
        rect(g, c - 2, 18, c + 2, 19, r[2])


FONT = {  # 5x7 capitals, rows top to bottom
    "A": ".###. #...# #...# ##### #...# #...# #...#",
    "B": "####. #...# #...# ####. #...# #...# ####.",
    "C": ".###. #...# #.... #.... #.... #...# .###.",
    "D": "####. #...# #...# #...# #...# #...# ####.",
    "E": "##### #.... #.... ####. #.... #.... #####",
    "F": "##### #.... #.... ####. #.... #.... #....",
    "G": ".###. #...# #.... #.### #...# #...# .###.",
    "H": "#...# #...# #...# ##### #...# #...# #...#",
    "I": "##### ..#.. ..#.. ..#.. ..#.. ..#.. #####",
    "J": "..### ...#. ...#. ...#. ...#. #..#. .##..",
    "K": "#...# #..#. #.#.. ##... #.#.. #..#. #...#",
    "L": "#.... #.... #.... #.... #.... #.... #####",
    "M": "#...# ##.## #.#.# #.#.# #...# #...# #...#",
    "N": "#...# ##..# #.#.# #.#.# #..## #...# #...#",
    "O": ".###. #...# #...# #...# #...# #...# .###.",
    "P": "####. #...# #...# ####. #.... #.... #....",
    "Q": ".###. #...# #...# #...# #.#.# #..#. .##.#",
    "R": "####. #...# #...# ####. #.#.. #..#. #...#",
    "S": ".#### #.... #.... .###. ....# ....# ####.",
    "T": "##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#..",
    "U": "#...# #...# #...# #...# #...# #...# .###.",
    "V": "#...# #...# #...# #...# #...# .#.#. ..#..",
    "W": "#...# #...# #...# #.#.# #.#.# ##.## #...#",
    "X": "#...# #...# .#.#. ..#.. .#.#. #...# #...#",
    "Y": "#...# #...# .#.#. ..#.. ..#.. ..#.. ..#..",
    "Z": "##### ....# ...#. ..#.. .#... #.... #####",
}


def sign_text(word):
    """The word as a mask 14 px tall: each 5x7 glyph twice as high and 8 px wide (its
    outer and middle columns doubled, so stems are 2 px), letters 2 px apart, 1 px when
    there are six."""
    gap = 1 if len(word) > 5 else 2
    rows = [[] for _ in range(7)]
    for i, ch in enumerate(word.upper()):
        glyph = FONT[ch].split()
        for y in range(7):
            rows[y] += [False] * (gap if i else 0) + [glyph[y][c] == "#" for c in (0, 0, 1, 2, 2, 3, 4, 4)]
    return np.repeat(np.array(rows, bool), 2, axis=0)


def roof_sign(P, a, g, word):
    """A roof sign: the word on a dark board in a steel frame on two legs, as wide as the
    word needs. The letters light up."""
    text = sign_text(word)
    th, tw = text.shape
    assert tw <= 54, f"sign '{word}' does not fit two cells"
    x0 = (64 - tw - 8) // 2
    x1 = x0 + tw + 8
    mask = np.zeros(a.shape[:2], bool)
    mask[3:27, x0:x1] = True
    mask[27:, x0 + 5:x0 + 8] = mask[27:, x1 - 8:x1 - 5] = True
    a[:] = shade(mask, P.metal, edges=(True, False, True, True))
    rect(a, x0 + 2, 5, x1 - 2, 25, P.screen[0])
    below_right = np.pad(text, ((0, 1), (0, 1)))
    core = text & below_right[1:, :-1] & below_right[:-1, 1:]      # the tube's bright core
    fill(a[8:8 + th, x0 + 4:x0 + 4 + tw], text, P.accent[1])
    fill(g[8:8 + th, x0 + 4:x0 + 4 + tw], text, P.accent[2])
    fill(g[8:8 + th, x0 + 4:x0 + 4 + tw], core, P.accent[3])


PROP_ART = {
    "bar": draw_bar, "speaker": draw_speaker, "djdesk": draw_djdesk, "counter": draw_counter,
    "fridge": draw_fridge, "rack": draw_rack, "cabinet": draw_cabinet, "slot": draw_slot,
    "desk": draw_desk, "sofa": draw_sofa, "bed": draw_bed, "table": draw_table,
    "plant": draw_plant, "filing": draw_filing, "workbench": draw_workbench, "pallet": draw_pallet,
    "racking": draw_racking, "machine": draw_machine, "tank": draw_tank, "vault": draw_vault,
    "tv": draw_tv, "neon": draw_neon, "bottles": draw_bottles,
    "lamp": draw_lamp, "tube": draw_tube, "discoball": draw_discoball, "spot": draw_spot,
}
WALL_DETAILS = ("poster", "picture", "clock", "shelf", "pipe", "board")


def prop(t, rng, name, lit=False):
    """One prop of the catalog (PROPS) or, with `lit`, only its pixels that glow: the base
    sprite looks switched off, the overlay on top of it switched on. Props are background,
    behind the blocks and the players: no outline."""
    w, h, _ = PROPS[name]
    a, g = canvas(w * 32, h * 32), canvas(w * 32, h * 32)
    if name in WALL_DETAILS:
        a = wall_detail(t, name)
    elif name.startswith("sign_"):
        roof_sign(prop_roles(t), a, g, name[5:])
    else:
        PROP_ART[name](prop_roles(t), rng, a, g)
    return g if lit else a


GENERATORS = {
    "brick": lambda t, g: wall(t, g, 32, 32),
    "brick_1": lambda t, g: wall(t, g, 32, 32),
    "brick_2": lambda t, g: wall(t, g, 32, 32),
    "brick_tall": lambda t, g: wall(t, g, 32, 64),
    "concrete": concrete,
    "concrete_1": lambda t, g: concrete(t, g, detail="vent"),
    "wood": wood,
    "brick_l": lambda t, g: wall(t, g, 32, 32, "l"),
    "brick_l_1": lambda t, g: wall(t, g, 32, 32, "l"),
    "brick_r": lambda t, g: wall(t, g, 32, 32, "r"),
    "brick_r_1": lambda t, g: wall(t, g, 32, 32, "r"),
    "brick_tall_l": lambda t, g: wall(t, g, 32, 64, "l"),
    "brick_tall_r": lambda t, g: wall(t, g, 32, 64, "r"),
    "concrete_l": lambda t, g: concrete(t, g, side="l"),
    "concrete_r": lambda t, g: concrete(t, g, side="r"),
    "wood_l": lambda t, g: wood(t, g, side="l"),
    "wood_r": lambda t, g: wood(t, g, side="r"),
    "glass": glass,
    "glass_1": lambda t, g: glass(t, g, variant=1),
    "glass_2": lambda t, g: glass(t, g, variant=2),
    "door": door,
    "plank": plank,
    "metal_truss": metal_truss,
    "beam": beam,
    "brick_cap": brick_cap,
    "support": support,
    "steel": steel,
    "pole": pole,
    "ladder": ladder,
    "chimney": chimney,
    "chimney_1": lambda t, g: chimney(t, g, variant=1),
    "chimney_2": lambda t, g: chimney(t, g, variant=2),
    "glass_lit": glass_lit,
    "glass_1_lit": lambda t, g: glass_lit(t, g, variant=1),
    "door_lit": door_lit,
    "chimney_lit": chimney_lit,
    "chimney_1_lit": lambda t, g: chimney_lit(t, g, variant=1),
    "chimney_2_lit": lambda t, g: chimney_lit(t, g, variant=2),
    "interior": interior,
    "interior_1": lambda t, g: interior(t, g, variant=1),
    "interior_2": lambda t, g: interior(t, g, variant=2),
    "interior_top": lambda t, g: interior(t, g, top=True),
    "interior_top_1": lambda t, g: interior(t, g, top=True, variant=1),
    "interior_top_2": lambda t, g: interior(t, g, top=True, variant=2),
    "crate": crate,
    "car": car,
    "barrel": barrel,
    "container": container,
    "lamp": lamp,
    "fence": fence,
    "bush": bush,
    "bush_1": lambda t, g: bush(t, g, variant=1),
    "bin": bin_,
    "bin_1": lambda t, g: bin_(t, g, variant=1),
    "sign": sign,
    "sign_1": lambda t, g: sign(t, g, variant=1),
    "sign_2": lambda t, g: sign(t, g, variant=2),
    "ground_top": ground_top,
    "ground_fill": ground_fill,
    "backdrop_sky": lambda t, g: backdrop(t, g, "sky"),
    "backdrop_sun": lambda t, g: backdrop(t, g, "sun"),
    "backdrop_far": lambda t, g: backdrop(t, g, "far"),
    "backdrop_mid": lambda t, g: backdrop(t, g, "mid"),
    "backdrop_near": lambda t, g: backdrop(t, g, "near"),
}
for _name, (_w, _h, _glows) in PROPS.items():
    GENERATORS["prop_" + _name] = lambda t, g, n=_name: prop(t, g, n)
    if _glows:
        GENERATORS["prop_" + _name + "_lit"] = lambda t, g, n=_name: prop(t, g, n, lit=True)
assert GENERATORS.keys() == SPRITES.keys()


def furnish_props(theme):
    """The props the theme's archetypes furnish their buildings with (`furnish` in
    Assets/Content/<Theme>/Archetypes/*.json), sorted."""
    names = set()
    for path in glob.glob(os.path.join(CONTENT, theme["name"], "Archetypes", "*.json")):
        with open(path, encoding="utf-8") as f:
            plan = json.load(f).get("furnish") or {}
        names.update(n for key in ("any", "ground", "upper") for n in plan.get(key) or [])
        names.update([plan["roof"]] if plan.get("roof") else [])
    unknown = sorted(names - PROPS.keys())
    if unknown:
        raise KeyError(f"{theme['name']}: an archetype names props tiles.py has no drawing for: {unknown}")
    return sorted(names)


def theme_sprites(theme):
    """Sprites a theme builds: its "sprites" list, or every block, tile and backdrop; on top
    the props its archetypes name; and the lit overlay of each of those that has one (a
    block's only where the theme's look glows: a door with a window, a chimney with a beacon)."""
    names = list(theme.get("sprites") or (n for n in SPRITES if not n.startswith("prop_") and not n.endswith("_lit")))
    names += ["prop_" + n for n in furnish_props(theme)]
    return names + [n + "_lit" for n in names if n + "_lit" in SPRITES and generate(theme, n + "_lit")[..., 3].any()]


def generate(theme, name):
    """One sprite of a theme as an RGBA array; same theme seed + name, same pixels."""
    base = name[:-4] if name.endswith("_lit") else name     # an overlay rolls what its base rolled
    seed = theme.get("seeds", {}).get(base, theme["seed"])  # a per-sprite override re-rolls one sprite only
    rng = np.random.default_rng([seed, zlib.crc32(base.encode())])
    a = GENERATORS[name](theme, rng)
    (w, h), _, _ = SPRITES[name]
    assert a.shape == (h, w, 4), (name, a.shape)
    return a


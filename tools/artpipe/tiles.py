"""Procedural material generators, deterministic from a seed.

Each generator draws with palette ramps only (step 0 = darkest = outline/mortar, light from
the top-left) and returns an RGBA numpy array with hard alpha. Which ramp and look each
material uses comes from the theme's "materials" block (themes/*.json).
"""
import zlib

import numpy as np

import palette
from backdrops import backdrop

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
    "chimney":     ((32, 32), "", True),
    "chimney_1":   ((32, 32), "", True),
    "chimney_2":   ((32, 32), "", True),
    # back walls: one wallpaper per storey, details scattered over open cells
    "interior":    ((32, 32), "xy", False),
    "interior_1":  ((32, 32), "xy", False),
    "interior_2":  ((32, 32), "xy", False),
    "interior_top": ((32, 32), "x", False),
    "interior_top_1": ((32, 32), "x", False),
    "interior_top_2": ((32, 32), "x", False),
    "interior_detail": ((32, 32), "", False),
    "interior_detail_1": ((32, 32), "", False),
    "interior_detail_2": ((32, 32), "", False),
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


def interior_detail(t, rng, which, w=32, h=32):
    """Something on the back wall of an open room (drawn over the wallpaper of one cell):
    a picture, a shelf with boxes, a poster, a pipe, a clock, a notice board. Background:
    no outline, in the wall's line colour plus the theme's accent."""
    m = t["materials"]["interior"]
    kinds = m.get("details", ["picture", "shelf", "poster"])
    kind = kinds[which % len(kinds)]
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
    "chimney": chimney,
    "chimney_1": lambda t, g: chimney(t, g, variant=1),
    "chimney_2": lambda t, g: chimney(t, g, variant=2),
    "interior": interior,
    "interior_1": lambda t, g: interior(t, g, variant=1),
    "interior_2": lambda t, g: interior(t, g, variant=2),
    "interior_top": lambda t, g: interior(t, g, top=True),
    "interior_top_1": lambda t, g: interior(t, g, top=True, variant=1),
    "interior_top_2": lambda t, g: interior(t, g, top=True, variant=2),
    "interior_detail": lambda t, g: interior_detail(t, g, 0),
    "interior_detail_1": lambda t, g: interior_detail(t, g, 1),
    "interior_detail_2": lambda t, g: interior_detail(t, g, 2),
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
assert GENERATORS.keys() == SPRITES.keys()


def theme_sprites(theme):
    """Sprites a theme builds: its "sprites" list, or all of them."""
    return theme.get("sprites", list(SPRITES))


def generate(theme, name):
    """One sprite of a theme as an RGBA array; same theme seed + name, same pixels."""
    seed = theme.get("seeds", {}).get(name, theme["seed"])  # a per-sprite override re-rolls one sprite only
    rng = np.random.default_rng([seed, zlib.crc32(name.encode())])
    a = GENERATORS[name](theme, rng)
    (w, h), _, _ = SPRITES[name]
    assert a.shape == (h, w, 4), (name, a.shape)
    return a


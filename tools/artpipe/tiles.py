"""Procedural material generators, deterministic from a seed.

Each generator draws with palette ramps only (step 0 = darkest = outline/mortar, light from
the top-left) and returns an RGBA numpy array with hard alpha. Which ramp and look each
material uses comes from the theme's "materials" block (themes/*.json).
"""
import zlib

import numpy as np

import palette

# name: (size w x h, tileable axes, foreground). Shared with qa.py.
SPRITES = {
    "brick":       ((32, 32), "xy", True),
    "brick_1":     ((32, 32), "xy", True),
    "brick_2":     ((32, 32), "xy", True),
    "brick_tall":  ((32, 64), "xy", True),
    "concrete":    ((32, 32), "xy", True),
    "wood":        ((32, 32), "xy", True),
    "glass":       ((32, 64), "", True),
    "door":        ((32, 64), "", True),
    "plank":       ((32, 32), "x", True),
    "metal_truss": ((64, 32), "", True),
    "beam":        ((64, 32), "x", True),
    "brick_cap":   ((64, 32), "x", True),
    "support":     ((32, 32), "y", True),
    "chimney":     ((32, 32), "", True),
    "window_back": ((32, 64), "x", False),
    "ground_top":  ((32, 32), "x", True),
    "ground_fill": ((32, 32), "xy", True),
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

def masonry(r, rng, w, h, course=8, length=16, style="brick"):
    """Running bond. 'brick': rounded lit bricks (object-set look); 'block': flat cast
    blocks with speckle. Joints sit on the tile edge so tiles repeat seamlessly."""
    a = canvas(w, h)
    rect(a, 0, 0, w, h, r[0])
    for c in range(h // course):
        y0 = c * course
        off = 0 if c % 2 == 0 else length // 2
        for b in range(w // length):
            xs = off + b * length
            dark = rng.random() < 0.22
            body, hi = (r[1], r[2]) if dark else (r[2], r[3])
            for i in range(1, length):
                for j in range(1, course):
                    col = body
                    if j == 1 or (i == 1 and j < course - 1):
                        col = hi
                    if j == course - 1 or i == length - 1:
                        col = r[1]
                    put(a, xs + i, y0 + j, col)
            if style == "brick":
                # chipped corners + bottom speckles, like the hand-drawn set
                for _ in range(rng.integers(1, 4)):
                    put(a, xs + rng.integers(3, length - 2), y0 + rng.integers(course - 3, course - 1), r[1] if not dark else r[0])
                if rng.random() < 0.4:
                    put(a, xs + length - 2, y0 + 1, r[0])
            else:
                for _ in range(rng.integers(2, 6)):
                    put(a, xs + rng.integers(2, length - 1), y0 + rng.integers(2, course - 1), r[1] if not dark else r[0])
                for _ in range(rng.integers(0, 2)):
                    put(a, xs + rng.integers(2, length - 1), y0 + rng.integers(2, course - 2), r[3])
    return np.roll(a, (3, 4), axis=(0, 1))  # keep joints off the tile edge


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


def wall(t, rng, w, h):
    m = t["materials"]["wall"]
    r = palette.ramp(t, m["ramp"])
    if m["style"] == "corrugated":
        return corrugated(r, rng, w, h)
    return masonry(r, rng, w, h, style=m["style"])


def concrete(t, rng, w=32, h=32):
    r = palette.ramp(t, t["materials"]["concrete"]["ramp"])
    a = canvas(w, h)
    rect(a, 0, 0, w, h, r[2])
    speckle(a, rng, np.ones((h, w), bool), [(r[1], 0.08), (r[3], 0.03)])
    rect(a, 0, h - 1, w, h, r[1])
    rect(a, w - 1, 0, w, h, r[1])
    rect(a, 1, 1, w, 2, r[3])
    rect(a, 1, 1, 2, h, r[3])
    rect(a, 0, 0, w, 1, r[0])
    rect(a, 0, 0, 1, h, r[0])
    for hx, hy in ((7, 7), (23, 7), (7, 23), (23, 23)):  # form-tie holes
        rect(a, hx, hy, hx + 2, hy + 2, r[0])
        put(a, hx + 2, hy + 1, r[3])
        put(a, hx + 1, hy + 2, r[3])
        if rng.random() < 0.5:  # water stain under the tie
            for k in range(rng.integers(2, 6)):
                put(a, hx + rng.integers(0, 2), hy + 3 + k, r[1])
    return np.roll(a, (16, 16), axis=(0, 1))  # panel joints inside the tile, edges mid-panel


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


def wood(t, rng, w=32, h=32):
    r = palette.ramp(t, t["materials"]["wood"]["ramp"])
    a = canvas(w, h)
    wood_planks(r, rng, a, 0, h, w)
    return a


# --- openings --------------------------------------------------------------------------

def glass(t, rng, w=32, h=64):
    m = t["materials"]["glass"]
    f, p = palette.ramp(t, m["frame"]), palette.ramp(t, m["pane"])
    hl = [palette.ramp(t, n)[i] for n, i in m["highlights"]]
    mask = np.zeros((h, w), bool)
    mask[0:5, :] = True           # lintel
    mask[h - 5:, :] = True        # sill
    mask[:, 8:24] = True          # pane + jambs (the collider's middle half)
    a = shade(mask, f)
    # pane
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
    # frame: jamb bevel and a transom bar
    rect(a, 8, py0, 9, py1, f[0])
    rect(a, 9, py0, 10, py1, f[3])
    rect(a, 22, py0, 23, py1, f[1])
    rect(a, 23, py0, 24, py1, f[0])
    ty = h // 2 - 2
    rect(a, 8, ty, 24, ty + 4, f[2])
    rect(a, 8, ty, 24, ty + 1, f[0])
    rect(a, 9, ty + 1, 23, ty + 2, f[3])
    rect(a, 8, ty + 3, 24, ty + 4, f[0])
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
        neon_tube(a, 1, 1, w - 1, 4, palette.ramp(t, m["sign"]))
    return a


def window_back(t, rng, w=32, h=64):
    """Room seen through the glass (behind the pane, visible beside it). Background: no
    outline. Only x 0..7 and 24..31 show, so detail sits at the sides."""
    m = t["materials"]["window_back"]
    r = palette.ramp(t, m["ramp"])
    a = canvas(w, h)
    if m["style"] == "lit":
        rect(a, 0, 0, w, h, r[2])
        rect(a, 0, 5, w, 14, r[3])
        for y in range(16, 50, 4):  # blinds
            rect(a, 0, y, w, y + 1, r[1])
        rect(a, 0, 50, w, h, r[1])
        rect(a, 0, 52, w, h, r[0])
        # silhouettes: a plant on the left, a lamp on the right
        rect(a, 3, 44, 6, 52, r[0])
        for dx, dy in ((1, 40), (2, 38), (4, 39), (6, 41), (0, 42), (5, 43), (3, 41)):
            rect(a, dx, dy, dx + 2, dy + 2, r[0])
        rect(a, 27, 30, 28, 52, r[0])
        rect(a, 25, 27, 31, 30, r[0])
    else:
        rect(a, 0, 0, w, h, r[1])
        rect(a, 0, 0, w, 9, r[0])
        dither = np.zeros((h, w), bool)
        dither[10:30, :] = (np.add.outer(np.arange(h), np.arange(w)) % 2 == 0)[10:30, :]
        dither[10:30, 6:26] = False
        a[dither] = (*r[2], 255)
        rect(a, 1, 21, 8, 22, r[2])      # shelf left
        rect(a, 1, 22, 8, 23, r[0])
        rect(a, 1, 35, 8, 36, r[2])
        rect(a, 1, 36, 8, 37, r[0])
        rect(a, 1, 31, 4, 35, r[0])      # boxes on the shelf
        rect(a, 25, 42, 31, 52, r[0])    # crate on the right
        rect(a, 25, 42, 31, 43, r[2])
        rect(a, 25, 42, 26, 52, r[2])
        rect(a, 0, 52, w, 53, r[2])      # floor line
        rect(a, 0, 53, w, h, r[0])
        acc = palette.ramp(t, m["accent"])
        rect(a, 2, 13, 5, 15, acc[3])    # distant work lamp
        rect(a, 2, 15, 5, 16, acc[1])
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
    if m["trim"] == "hazard":
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


def chimney(t, rng, w=32, h=32):
    m = t["materials"]["chimney"]
    r = palette.ramp(t, m["ramp"])
    mask = np.zeros((h, w), bool)
    mask[3:9, 7:25] = True
    mask[9:, 9:23] = True
    a = shade(mask, r, edges=(True, False, True, True))
    if m["style"] == "pipe":
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
    band = m["band"]
    if band == "hazard":
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


# --- ground ----------------------------------------------------------------------------

def ground_mass(r, rng, a, y0, y1, row_offset, blocks):
    """Ground below the surface. 'blocks': 16-row courses whose absolute course index keeps
    the bond continuous from ground_top into ground_fill. Otherwise asphalt: aggregate
    speckle with a few hairline cracks kept off the tile edges."""
    w = a.shape[1]
    rect(a, 0, y0, w, y1, r[1])
    speckle(a, rng, np.pad(np.ones((y1 - y0, w), bool), ((y0, a.shape[0] - y1), (0, 0))),
            [(r[0], 0.06), (r[2], 0.07), (step(r, 3), 0.01 if not blocks else 0)])
    if not blocks:
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
    a = canvas(w, h)
    ground_mass(fill, rng, a, 8, h, 8, m.get("fill_style", "blocks") == "blocks")
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
    a = canvas(w, h)
    ground_mass(fill, rng, a, 0, h, 8, m.get("fill_style", "blocks") == "blocks")
    return a


GENERATORS = {
    "brick": lambda t, g: wall(t, g, 32, 32),
    "brick_1": lambda t, g: wall(t, g, 32, 32),
    "brick_2": lambda t, g: wall(t, g, 32, 32),
    "brick_tall": lambda t, g: wall(t, g, 32, 64),
    "concrete": concrete,
    "wood": wood,
    "glass": glass,
    "door": door,
    "plank": plank,
    "metal_truss": metal_truss,
    "beam": beam,
    "brick_cap": brick_cap,
    "support": support,
    "chimney": chimney,
    "window_back": window_back,
    "ground_top": ground_top,
    "ground_fill": ground_fill,
}
assert GENERATORS.keys() == SPRITES.keys()


def generate(theme, name):
    """One sprite of a theme as an RGBA array; same theme seed + name, same pixels."""
    rng = np.random.default_rng([theme["seed"], zlib.crc32(name.encode())])
    a = GENERATORS[name](theme, rng)
    (w, h), _, _ = SPRITES[name]
    assert a.shape == (h, w, 4), (name, a.shape)
    return a


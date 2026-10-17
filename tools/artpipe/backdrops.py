"""Parallax backdrop layers: far, mid and near silhouettes, 512x192, seamless left/right.

Backgrounds follow the style bible: no outline, haze-ramp colours only (plus a few lights),
and the farther the layer the higher its haze steps, so it sits closer to the sky with less
contrast. Everything is drawn with x wrapping round, so the layers tile horizontally.
"""
import numpy as np

import palette

W, H = 512, 192


def canvas():
    return np.zeros((H, W, 4), np.uint8)


def wrect(a, x0, y0, x1, y1, c, on=None):
    """Fill columns x0..x1 (wrapping) between rows y0..y1. With `on`, only repaint pixels
    that currently hold that colour (highlights that stay inside a shape)."""
    y0, y1 = max(int(y0), 0), min(int(y1), H)
    if y1 <= y0:
        return
    for x in range(int(x0), int(x1)):
        col = a[y0:y1, x % W]
        if on is None:
            col[:] = (*c, 255)
        else:
            m = (col[:, 3] > 0) & np.all(col[:, :3] == on, axis=1)
            col[m] = (*c, 255)


def wput(a, x, y, c):
    if 0 <= y < H:
        a[int(y), int(x) % W] = (*c, 255)


def disc(a, cx, cy, r, c, on=None):
    for dy in range(-r, r + 1):
        half = int((r * r - dy * dy) ** 0.5 + 0.3)
        wrect(a, cx - half, cy + dy, cx + half + 1, cy + dy + 1, c, on)


def periodic(rng, amps):
    """Seamless 1D noise: sines with a whole number of periods across the width."""
    x = np.arange(W)
    out = np.zeros(W)
    for k, amp in enumerate(amps, 1):
        out += amp * np.sin(2 * np.pi * k * x / W + rng.uniform(0, 2 * np.pi))
    return out


def fill_profile(a, top, c):
    for x in range(W):
        a[max(int(top[x]), 0):, x] = (*c, 255)


def gable(a, x0, wd, yb, rh, lit, dark):
    """Pitched roof over x0..x0+wd with eaves at row yb: lit left slope, dark right."""
    cx = x0 + wd / 2
    for i in range(rh):
        half = (i + 1) * (wd / 2 + 2) / rh
        y = yb - rh + i
        wrect(a, round(cx - half), y, round(cx), y + 1, lit)
        wrect(a, round(cx), y, round(cx + half), y + 1, dark)


def tree(a, cx, base, r, trunk_h, body, lit):
    wrect(a, cx - 1, base - trunk_h, cx + 2, base, body)
    cy = base - trunk_h - r + 3
    disc(a, cx, cy, r, body)
    disc(a, cx - r // 2, cy + r // 3, max(r * 2 // 3, 2), body)
    disc(a, cx + r // 2, cy + r // 3, max(r * 2 // 3, 2), body)
    disc(a, cx, cy, r, lit, on=body)          # rim light: crown lit, then shaded
    disc(a, cx + 2, cy + 2, r, body, on=lit)  # back except a top-left crescent


def lamp_post(a, x, base, h, body, light):
    wrect(a, x, base - h, x + 2, base, body)
    wrect(a, x - 3, base - h, x + 5, base - h + 2, body)
    wrect(a, x - 2, base - h + 2, x + 4, base - h + 3, light)


def skyline(rng, a, base, wmin, wmax, hmin, hmax, body, lit, gap=(0, 3)):
    """Row of rectangular blocks across the full width; returns their boxes."""
    boxes, x = [], int(rng.integers(0, 8))
    while x < W - wmin:
        wd = int(min(rng.integers(wmin, wmax + 1), W - x - 1))
        ht = int(rng.integers(hmin, hmax + 1))
        wrect(a, x, base - ht, x + wd, base, body)
        wrect(a, x, base - ht, x + 1, base, lit)
        boxes.append((x, wd, ht))
        x += wd + int(rng.integers(*gap))
    return boxes


# --- Town: hills, suburban roofs, trees, a water tower ---------------------------------

def town_far(t, rng, h):
    a = canvas()
    back = H - 54 + periodic(rng, [11, 5, 3, 1.5])
    fill_profile(a, back, h[4])
    for _ in range(45):  # distant tree line on the ridge
        x = int(rng.integers(W))
        r = int(rng.integers(2, 5))
        disc(a, x, int(back[x]) + r - 2, r, h[4])
    front = H - 30 + periodic(rng, [6, 4, 2])
    fill_profile(a, front, h[3])
    for _ in range(7):  # hamlets on the near ridge
        x = int(rng.integers(W))
        wd, ht = int(rng.integers(7, 14)), int(rng.integers(4, 8))
        y = int(front[x % W]) + 1
        wrect(a, x, y - ht, x + wd, y + 2, h[3])
        gable(a, x, wd, y - ht, wd // 2 + 1, h[3], h[3])
    return a


def town_mid(t, rng, h):
    a = canvas()
    body, lit, dark = h[2], h[3], h[1]
    base = H - 6
    wrect(a, 0, base, W, H, body)
    tx = int(rng.integers(0, W))  # water tower
    wrect(a, tx + 3, 98, tx + 5, base, body)
    wrect(a, tx + 25, 98, tx + 27, base, body)
    for y in range(104, base - 10, 16):  # cross bracing
        for k in range(16):
            wput(a, tx + 5 + k * 20 // 16, y + k, body)
            wput(a, tx + 25 - k * 20 // 16, y + k, body)
    wrect(a, tx - 1, 80, tx + 31, 100, body)
    wrect(a, tx - 1, 80, tx + 1, 100, lit)
    wrect(a, tx - 1, 89, tx + 31, 90, dark)
    gable(a, tx - 1, 32, 80, 8, lit, body)
    x = int(rng.integers(0, 10))
    while x < W - 30:
        if rng.random() < 0.3:  # garden tree
            r = int(rng.integers(8, 13))
            tree(a, x + r, base, r, int(rng.integers(8, 16)), body, lit)
            x += 2 * r + int(rng.integers(0, 4))
            continue
        wd = int(min(rng.integers(30, 52), W - x - 4))
        ht = int(rng.integers(22, 38))
        yb = base - ht
        wrect(a, x, yb, x + wd, base, body)
        rh = int(min(wd // 2, rng.integers(10, 18)))
        gable(a, x, wd, yb, rh, lit, body)
        cxh = x + int(rng.integers(wd // 2 + 3, wd - 5))  # chimney on the right slope
        wrect(a, cxh, yb - rh // 2 - 6, cxh + 4, yb, body)
        wrect(a, cxh - 1, yb - rh // 2 - 7, cxh + 5, yb - rh // 2 - 5, lit)
        for wx in range(x + 5, x + wd - 6, 10):  # windows, two floors
            for wy in (yb + 5, yb + 17):
                if wy + 6 < base - 4 and rng.random() < 0.7:
                    wrect(a, wx, wy, wx + 4, wy + 6, dark)
        dx = x + int(rng.integers(3, wd - 8))
        wrect(a, dx, base - 10, dx + 5, base, dark)
        x += wd + int(rng.integers(3, 12))
    return a


def town_near(t, rng, h):
    a = canvas()
    body, lit = h[0], h[1]
    base = H
    hedge = H - 12 + periodic(rng, [2, 1.5, 1, 1])
    fill_profile(a, hedge, body)
    for x in range(0, W, 4):  # picket fence
        wrect(a, x, H - 26, x + 2, H - 12, body)
        wput(a, x, H - 27, body)
    wrect(a, 0, H - 22, W, H - 20, body)
    wrect(a, 0, H - 16, W, H - 14, body)
    for _ in range(int(rng.integers(5, 8))):
        x = int(rng.integers(W))
        r = int(rng.integers(13, 21))
        tree(a, x, base - 8, r, int(rng.integers(10, 20)), body, lit)
    for _ in range(3):
        lamp_post(a, int(rng.integers(W)), base - 8, int(rng.integers(44, 54)), body, lit)
    return a


# --- Port: coast, warehouses, cranes, lighthouse, ship, containers ---------------------

def crane(a, x, base, ht, boom, body, lit, light):
    """Ship-to-shore container crane: A-frame legs, boom, machinery house, warning light."""
    leg = 30
    for lx in (x, x + leg):
        wrect(a, lx, base - ht + 14, lx + 3, base, body)
        wrect(a, lx, base - ht + 14, lx + 1, base, lit)
    for y in range(base - ht + 30, base - 8, 20):
        wrect(a, x, y, x + leg + 3, y + 2, body)
    wrect(a, x - 12, base - ht + 14, x + leg + boom, base - ht + 18, body)   # boom
    wrect(a, x - 12, base - ht + 14, x + leg + boom, base - ht + 15, lit)
    wrect(a, x + 2, base - ht + 18, x + 20, base - ht + 28, body)            # trolley house
    wrect(a, x + 10, base - ht, x + 13, base - ht + 14, body)                # A-frame mast
    for k in range(14):
        wput(a, x + 13 + k * (leg + boom - 16) // 14, base - ht + k, body)   # stays
        wput(a, x + 10 - k, base - ht + k, body)
    wrect(a, x + 10, base - ht - 2, x + 13, base - ht, light)


def port_far(t, rng, h):
    a = canvas()
    hills = H - 26 + periodic(rng, [6, 3, 2])
    fill_profile(a, hills, h[4])
    for (x, wd, ht) in skyline(rng, a, H - 6, 14, 46, 10, 34, h[3], h[3], gap=(4, 30)):
        if rng.random() < 0.5:  # sawtooth roof
            for sx in range(x, x + wd - 4, 6):
                for k in range(4):
                    wrect(a, sx + k, H - 6 - ht - 4 + k, sx + k + 1, H - 6 - ht, h[3])
    wrect(a, 0, H - 6, W, H, h[3])
    for _ in range(3):  # distant cranes
        x = int(rng.integers(W))
        crane(a, x, H - 6, int(rng.integers(58, 72)), int(rng.integers(20, 34)), h[3], h[3], h[3])
    return a


def port_mid(t, rng, h, light):
    a = canvas()
    body, lit, dark = h[2], h[3], h[1]
    base = H - 6
    wrect(a, 0, base, W, H, body)
    x = int(rng.integers(0, 10))  # warehouses with sawtooth roofs
    while x < W - 40:
        wd = int(min(rng.integers(44, 90), W - x - 6))
        ht = int(rng.integers(24, 44))
        yb = base - ht
        wrect(a, x, yb, x + wd, base, body)
        for tx in range(x, x + wd - 7, 8):
            for k in range(7):
                wrect(a, tx + k, yb - 7 + k, tx + k + 1, yb, lit if k < 2 else body)
        for wx in range(x + 4, x + wd - 8, 12):
            wrect(a, wx, yb + 6, wx + 8, yb + 9, dark)
        if rng.random() < 0.6:
            wrect(a, x + wd // 3, base - 18, x + wd // 3 + 14, base, dark)  # loading door
        x += wd + int(rng.integers(18, 60))
    anchor = int(rng.integers(W))  # landmarks spread round the loop so they never pile up
    sx = anchor  # moored ship
    for i in range(16):
        wrect(a, sx + i // 3, base - 16 + i, sx + 150 - i // 2, base - 15 + i, dark if i < 2 else body)
    wrect(a, sx + 96, base - 34, sx + 128, base - 16, body)
    wrect(a, sx + 96, base - 34, sx + 128, base - 33, lit)
    wrect(a, sx + 100, base - 46, sx + 122, base - 34, body)
    for wx in range(sx + 102, sx + 120, 4):
        wrect(a, wx, base - 42, wx + 2, base - 40, dark)
    wrect(a, sx + 108, base - 60, sx + 116, base - 46, body)
    wrect(a, sx + 108, base - 56, sx + 116, base - 53, light)
    lx = anchor + 360  # lighthouse
    for i, y in enumerate(range(base - 92, base)):
        half = 5 + i * 4 // 92
        band = (y - (base - 92)) // 12 % 2 == 1
        wrect(a, lx - half, y, lx, y + 1, lit if not band else body)
        wrect(a, lx, y, lx + half + 1, y + 1, body if not band else dark)
    wrect(a, lx - 6, base - 104, lx + 7, base - 92, body)
    wrect(a, lx - 4, base - 102, lx + 5, base - 96, light)
    wrect(a, lx - 7, base - 106, lx + 8, base - 104, body)
    wrect(a, lx - 2, base - 110, lx + 3, base - 106, body)
    crane(a, anchor + 200, base, 108, 46, body, lit, light)
    return a


def port_near(t, rng, h, light):
    a = canvas()
    body, lit = h[0], h[1]
    base = H - 4
    wrect(a, 0, base, W, H, body)
    x = int(rng.integers(0, 12))
    while x < W - 50:
        if rng.random() < 0.35:  # chain-link fence run
            run = int(min(rng.integers(40, 90), W - x - 4))
            for px in range(x, x + run, 16):
                wrect(a, px, base - 28, px + 2, base, body)
            wrect(a, x, base - 28, x + run, base - 26, body)
            for fx in range(x, x + run):
                for fy in range(base - 26, base):
                    if (fx + fy) % 6 == 0 or (fx - fy) % 6 == 0:
                        wput(a, fx, fy, body)
            x += run + int(rng.integers(4, 16))
            continue
        stack = int(rng.integers(1, 4))
        for s in range(stack):  # 40 ft containers, ribbed, door end on the right
            cw = 48
            cy = base - 13 * (s + 1)
            ox = x + int(rng.integers(-4, 5)) * (s > 0)
            wrect(a, ox, cy, ox + cw, cy + 13, body)
            wrect(a, ox, cy, ox + cw, cy + 1, lit)
            for rx in range(ox + 2, ox + cw - 6, 3):
                wrect(a, rx, cy + 2, rx + 1, cy + 12, lit)
            wrect(a, ox + cw - 5, cy + 2, ox + cw - 4, cy + 12, lit)
            wrect(a, ox + cw - 2, cy + 2, ox + cw - 1, cy + 12, lit)
        x += 50 + int(rng.integers(2, 20))
    for _ in range(3):
        lamp_post(a, int(rng.integers(W)), base, int(rng.integers(50, 60)), body, light)
    for _ in range(6):  # bollards
        bx = int(rng.integers(W))
        wrect(a, bx, base - 6, bx + 5, base, body)
        wrect(a, bx - 1, base - 8, bx + 6, base - 6, body)
    return a


# --- Neon: night towers, lit windows, signs, antenna lights ----------------------------

def windows(a, rng, x, wd, top, base, lit_ramp, p_lit, unlit=None):
    """Window grid on a tower face; lit ones come in runs along a floor."""
    for wy in range(top + 4, base - 4, 5):
        floor_on = rng.random() < 0.45
        for wx in range(x + 3, x + wd - 3, 4):
            if floor_on and rng.random() < p_lit:
                c = lit_ramp[int(rng.choice([1, 2, 2, 3]))]
                wrect(a, wx, wy, wx + 2, wy + 3, c)
            elif unlit is not None:
                wrect(a, wx, wy, wx + 2, wy + 3, unlit)


def sign(a, rng, x, y, ramp, vertical=True):
    """A neon sign: dark box, glowing border, letter-ish core segments."""
    w, ht = (6, int(rng.integers(16, 26))) if vertical else (int(rng.integers(16, 28)), 6)
    wrect(a, x, y, x + w, y + ht, ramp[0])
    wrect(a, x, y, x + w, y + 1, ramp[1])
    wrect(a, x, y + ht - 1, x + w, y + ht, ramp[1])
    wrect(a, x, y, x + 1, y + ht, ramp[1])
    wrect(a, x + w - 1, y, x + w, y + ht, ramp[1])
    if vertical:
        for ly in range(y + 2, y + ht - 3, 4):
            wrect(a, x + 2, ly, x + 4, ly + 3, ramp[2])
    else:
        for lx in range(x + 2, x + w - 3, 4):
            wrect(a, lx, y + 2, lx + 3, y + 4, ramp[2])


def antenna(a, x, top, ht, body, light):
    wrect(a, x, top - ht, x + 1, top, body)
    wput(a, x, top - ht - 1, light[2])
    wput(a, x - 1, top - ht, light[1])
    wput(a, x + 1, top - ht, light[1])


def neon_far(t, rng, h, win):
    a = canvas()
    wrect(a, 0, H - 8, W, H, h[3])
    for (x, wd, ht) in skyline(rng, a, H - 8, 10, 34, 20, 64, h[3], h[4], gap=(0, 6)):
        top = H - 8 - ht
        if rng.random() < 0.4:  # stepped crown
            wrect(a, x + wd // 4, top - 8, x + wd - wd // 4, top, h[3])
            wrect(a, x + wd // 4, top - 8, x + wd // 4 + 1, top, h[4])
        for _ in range(int(wd * ht / 90)):  # sparse dim windows
            wput(a, x + 2 + int(rng.integers(0, max(wd - 4, 1))) // 3 * 3,
                 top + 3 + int(rng.integers(0, max(ht - 6, 1))) // 4 * 4, win[0])
    return a


def neon_mid(t, rng, h, win, signs):
    a = canvas()
    body, lit, dark = h[2], h[3], h[1]
    base = H - 6
    wrect(a, 0, base, W, H, body)
    for (x, wd, ht) in skyline(rng, a, base, 22, 50, 44, 100, body, lit, gap=(2, 16)):
        top = base - ht
        if rng.random() < 0.5:  # setback crown
            inset = wd // 4
            wrect(a, x + inset, top - 10, x + wd - inset, top, body)
            wrect(a, x + inset, top - 10, x + inset + 1, top, lit)
            top -= 10
        windows(a, rng, x, wd, top, base, win, 0.35, unlit=dark)
        if rng.random() < 0.5:
            antenna(a, x + wd // 2, top, int(rng.integers(8, 22)), body, signs[0])
        if rng.random() < 0.5:
            sign(a, rng, x + wd - 8, top + int(rng.integers(8, 30)), signs[x % len(signs)])
    return a


def neon_near(t, rng, h, win, signs):
    a = canvas()
    body, lit = h[0], h[1]
    base = H - 4
    wrect(a, 0, base, W, H, body)
    x = int(rng.integers(0, 10))
    while x < W - 30:
        wd = int(min(rng.integers(34, 70), W - x - 4))
        ht = int(rng.integers(28, 60))
        top = base - ht
        wrect(a, x, top, x + wd, base, body)
        wrect(a, x, top, x + wd, top + 1, lit)
        sy = base - 22  # shop window, lit
        for wx in range(x + 4, x + wd - 10, 14):
            if rng.random() < 0.6:
                wrect(a, wx, sy, wx + 10, base - 4, win[1])
                wrect(a, wx + 1, sy + 1, wx + 9, sy + 4, win[2])
            else:
                wrect(a, wx, sy, wx + 10, base - 4, lit)
            wrect(a, wx + 5, sy, wx + 6, base - 4, body)
        for ax in range(x, x + wd, 4):  # awning
            wrect(a, ax, sy - 5, ax + 2, sy - 2, lit)
        ramp = signs[(x // 7) % len(signs)]
        if ht > 40:
            sign(a, rng, x + int(rng.integers(2, max(wd - 28, 3))), top + 5, ramp, vertical=False)
        x += wd + int(rng.integers(6, 30))
    for _ in range(4):  # street lamps and a railing
        lamp_post(a, int(rng.integers(W)), base, int(rng.integers(46, 58)), body, signs[-1][2])
    for rx in range(0, W, 6):
        wrect(a, rx, base - 10, rx + 1, base, body)
    wrect(a, 0, base - 10, W, base - 9, body)
    return a


def backdrop(t, rng, layer):
    m = t["materials"]["backdrop"]
    h = palette.ramp(t, m["ramp"])
    style = m["style"]
    if style == "town":
        return {"far": town_far, "mid": town_mid, "near": town_near}[layer](t, rng, h)
    if style == "port":
        light = palette.ramp(t, m["light"])[-1]
        return {"far": lambda: port_far(t, rng, h), "mid": lambda: port_mid(t, rng, h, light),
                "near": lambda: port_near(t, rng, h, light)}[layer]()
    win = palette.ramp(t, m["window"])
    signs = [palette.ramp(t, s) for s in m["signs"]]
    return {"far": lambda: neon_far(t, rng, h, win), "mid": lambda: neon_mid(t, rng, h, win, signs),
            "near": lambda: neon_near(t, rng, h, win, signs)}[layer]()

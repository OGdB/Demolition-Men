"""Shoulders that run up to the sign: for signs narrower than the body's flat top.

    python tools/artpipe/shoulders.py   # after action_frames.py, before accessories.py and heads.py

The bodies were drawn under Player 1's warning sign, whose wide base hides their flat top. Under
a round sign that top looks sawn off, so each side gets a slope from the shoulder up to the sign's
lower third, in the skin's colours. heads.py puts the slope in the atlas pieces of the signs in
heads.SLOPED. This script draws it into Player 2's own frames (the sheet and Resources/Actions/P2)
in colours one step from the skin's (RISE): accessories.py keys those as the sign, so a look with
another sign takes them off with the stop sign. Run again, it redraws them.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from accessories import ACTIONS, P2_SKIN, RISE, SHEETS, frames, full_sign, match  # noqa: E402

REACH = 16  # columns from the sign's middle a shoulder is looked for
DROP = 12  # rows under the slope's top the body may start
STEP = 3  # a bigger jump in the body's top ends the shoulder (an arm's edge)...
GAP = 3  # ...unless it's back within this many columns: a dip between the neck and the shoulder
POCKET = 3, 6  # columns and rows to a body on every side that make a clear pixel a hole in it


def slope(skin, sign):
    """The slope's pixels on this frame: (light, shade) masks, under the sign's lower third on
    both sides. A side with no shoulder there (an arm raised, a body bent over) gets none."""
    light, shade = np.zeros_like(skin), np.zeros_like(skin)
    ys, xs = np.nonzero(sign)
    if not len(ys):
        return light, shade
    bottom = ys.max()
    y1 = bottom - (bottom - ys.min() + 1) // 3
    row = np.nonzero(sign[y1])[0]
    if not len(row):
        return light, shade
    middle = (xs.min() + xs.max()) // 2
    for side in (-1, 1):
        x1 = int(row.min() if side < 0 else row.max())

        def top(x):
            if not 0 <= x < skin.shape[1]:
                return None
            below = np.nonzero(skin[y1:, x])[0]
            return int(below[0]) + y1 if len(below) and below[0] <= DROP else None

        tops, last = {}, None
        for x in range(middle, middle + side * (REACH + 1), side):
            t = top(x)
            if (x - x1) * side <= 0:  # under the sign: a dip is only a gap to fill
                if t is not None:
                    tops[x] = t
            elif last is None or (t is not None and abs(t - last) <= STEP):
                if t is None:
                    break
                tops[x], last = t, t
            elif any(top(x + side * k) is not None and abs(top(x + side * k) - last) <= STEP for k in range(1, GAP + 1)):
                if t is not None:  # a narrow dip: the shoulder goes on past it
                    tops[x] = t
            else:
                break
        outer = [x for x in tops if (x - x1) * side > 0]
        if not outer:
            continue
        x0 = max(outer, key=lambda x: (x - middle) * side)
        for x, t in tops.items():
            run = (x - x1) * side / ((x0 - x1) * side)
            y = y1 if run <= 0 else int(round(y1 + (tops[x0] - y1) * run))
            light[y:t, x] = True
        for k in range(1, 4):  # a crease from the sign down the muscle
            x, y = x1 - side * k, bottom + k
            if 0 <= y < skin.shape[0] and 0 <= x < skin.shape[1]:
                shade[y, x] = light[y, x] or skin[y, x]
    light |= pockets(skin | light | sign, y1, bottom + POCKET[1] + 2, middle)
    return light & ~sign, shade & ~sign & light


def pockets(body, y0, y1, middle):
    """Clear pixels around the sign with the body close on all four sides: what's left of a gap."""
    out = np.zeros_like(body)
    across, down = POCKET
    h, w = body.shape
    for y in range(max(0, y0), min(h, y1 + 1)):
        for x in range(max(0, middle - REACH), min(w, middle + REACH + 1)):
            if not body[y, x] and (body[y, max(0, x - across):x].any() and body[y, x + 1:x + across + 1].any()
                                   and body[max(0, y - down):y, x].any() and body[y + 1:y + down + 1, x].any()):
                out[y, x] = True
    return out


def main():
    sheet = np.array(Image.open(SHEETS["P2"]).convert("RGBA"))
    sheet[match(sheet, RISE)] = 0
    done = {}
    for name, _, a, texture, (x0, y0) in frames():
        action = None if texture == "sheet" else name.split("_")[0]
        a = a.copy()
        a[match(a, RISE)] = 0
        light, shade = slope(match(a, P2_SKIN), full_sign(a, "P2", action))
        clear = a[..., 3] == 0
        a[light & clear] = RISE[0] + (255,)
        a[shade & clear] = RISE[1] + (255,)
        if texture == "sheet":
            sheet[y0:y0 + a.shape[0], x0:x0 + a.shape[1]] = a
        else:
            Image.fromarray(a).save(os.path.join(ACTIONS, "P2", name + ".png"))
        done[name] = int((light & clear).sum())
    Image.fromarray(sheet).save(SHEETS["P2"])
    print(f"{len(done)} frames, {sum(1 for v in done.values() if v)} with a slope")


if __name__ == "__main__":
    main()

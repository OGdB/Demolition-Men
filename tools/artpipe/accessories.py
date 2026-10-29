"""Chains, bracelets and tattoos for the crew's looks: one overlay per player sheet and action frame.

    python tools/artpipe/accessories.py            # overlays, and the sheet to review them on
    python tools/artpipe/accessories.py --detect   # first guesses for the frames anchors.json lacks

Where each goes is content-work/looks/anchors.json, per frame of Player 1 ("sheet c,r" for the
sheet's 128x64 cells, the action frames by name), in the frame's pixels from its top left:
  wrists: [[wx, wy, hx, hy], ...]  a bracelet across the arm at w, the hand toward h
  arms:   [[x, y], ...]            an arm tattoo's centre
  chest:  [x, y]                   the chest piece's top centre (default: under the chain)
  chain:  false, "back" or [x0, x1] no chain (the neck is out of sight), seen from behind, or only
                                   between those columns (an arm in front of the rest)
  chestTattoo: false               no chest piece (the chest is out of sight)
  keep:   [[x0, y0, x1, y1], ...]  no look recolours the trousers' colour in here (a prop drawn in it)
The chain hangs under the lower edge of the player's own sign wherever skin is below it. Player 2
has the same body under a taller head, so its frames take Player 1's anchors moved by the offset
that lines the two players' skin up.

Everything is drawn in key colours (KEYS, LookPalette.Extras in Looks.cs), and only on skin: a look
says which parts it wears in which colour, and LookSkin lays them over the recoloured frame. One more
key marks what no look recolours though it's in a colour looks change (keep_mask): the radio, the
light spots on the skin in the sign's yellow.
Writes Demolition Men/Assets/Resources/LookOverlays/P1|P2/<texture>.png with their .metas, and
content-work/looks/anchors.png, every frame wearing everything, to review.
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from action_frames import P2_COLOURS, PROP_BELOW, SIGN_YELLOW, TROUSERS, grow, largest, prop_columns, sign_mask, write_sprite  # noqa: E402
from build_theme import FOLDER_META, write_meta  # noqa: E402

ROOT = os.path.join(HERE, "..", "..")
ASSETS = os.path.join(ROOT, "Demolition Men", "Assets")
ACTIONS = os.path.join(ASSETS, "Resources", "Actions")
OUT = os.path.join(ASSETS, "Resources", "LookOverlays")
ANCHORS = os.path.join(ROOT, "content-work", "looks", "anchors.json")
REVIEW = os.path.join(ROOT, "content-work", "looks", "anchors.png")
SHEETS = {"P1": os.path.join(ASSETS, "Animations", "Player_1", "+Demolition_man.png"),
          "P2": os.path.join(ASSETS, "Animations", "Player_2", "+Demolition_man_Stop_V2.png")}
CELL = (128, 64)
PROPS = {"pallet", "radio", "note"}

# Key colours, mirrored in LookPalette (Looks.cs): what each overlay pixel is part of.
KEYS = {"chain": (255, 0, 0), "chainShade": (128, 0, 0),
        "bracelets": (0, 255, 0), "braceletsShade": (0, 128, 0),
        "chestTattoo": (0, 0, 255), "armTattoo": (255, 0, 255),
        "keep": (1, 1, 1)}  # no look recolours this pixel (keep_mask)

P1_SKIN = {(238, 195, 154), (217, 160, 102), (205, 166, 129)}
P2_SKIN = {P2_COLOURS[c] for c in P1_SKIN}
P2_SIGN = {(246, 73, 73), (191, 0, 0)}

# Ink, one pixel per "x": a heart on the arm, an eagle across the chest.
HEART = [".x.x.",
         "xxxxx",
         ".xxx.",
         "..x.."]
WINGS = ["x.........x",
         "xx.......xx",
         ".xxx.x.xxx.",
         "..xxxxxxx..",
         "....xxx....",
         "....x.x...."]
CHAIN_DROP = 3  # px the chain hangs below the sign's edge at its middle
SHOES = (102, 57, 49)  # both players'
FEET = 6  # rows above the lowest shoe pixel an action frame's feet reach


def match(a, colours):
    rgb = a[..., :3]
    m = np.zeros(a.shape[:2], bool)
    for c in colours:
        m |= np.all(rgb == c, axis=-1)
    return m & (a[..., 3] > 0)


def p2_sign(a):
    red = largest(match(a, P2_SIGN))
    if not red.any():
        return red
    ys, xs = np.nonzero(red)
    box = np.zeros_like(red)
    box[ys.min():ys.max() + 1, xs.min():xs.max() + 1] = True
    white = match(a, [(255, 255, 255)])
    return red | (white & box) | (white & grow(grow(red)))


RIM = [(255, 255, 255), (241, 241, 241), (223, 221, 219)]


def sign_of(a, player):
    sign = sign_mask(a)[0] if player == "P1" else p2_sign(a)
    rim = match(a, RIM)
    if not sign.any():
        return largest(rim)  # seen from behind: all white
    for _ in range(4):  # the action frames' off-white rim, a few pixels deep
        sign |= grow(sign) & rim
    return sign


def chain(sign, skin, pendant=True, span=None):
    """The chain's pixels: under the sign's lower edge from one side of the neck to the other."""
    h, w = sign.shape
    bottom = {}
    for x in range(span[0], span[1] + 1) if span else range(w):
        ys = np.nonzero(sign[:, x])[0]
        # the first skin under the sign, past a row or two of outline or a grey rim
        below = [y for y in range(ys[-1] + 1, min(h, ys[-1] + 4)) if skin[y, x]] if len(ys) else []
        if below:
            bottom[x] = below[0] - 1
    if len(bottom) < 5:
        return []
    xl, xr = min(bottom), max(bottom)
    out, last = [], None
    for x in range(xl, xr + 1):
        edge = bottom.get(x)
        if edge is None:  # a gap in the edge: the neighbours' height
            edge = bottom[max(k for k in bottom if k < x)]
        t = (x - xl) / (xr - xl)
        y = edge + 1 + round(CHAIN_DROP * 4 * t * (1 - t))
        key = "chain" if x % 2 == 0 else "chainShade"
        out.append((x, y, key))
        if last is not None:  # keep the links joined where it drops more than a pixel
            for fill in range(min(last, y) + 1, max(last, y)):
                out.append((x, fill, key))
        last = y
    mid = (xl + xr) // 2
    low = max(y for x, y, _ in out if x == mid)
    if pendant:
        out += [(mid, low + 1, "chain"), (mid + 1, low + 1, "chainShade"), (mid, low + 2, "chainShade")]
    return out


def chain_bottom(pixels):
    xs = [x for x, _, _ in pixels]
    mid = (min(xs) + max(xs)) // 2
    return mid, max(y for x, y, _ in pixels if abs(x - mid) <= 1)


def bracelet(w, h):
    """Two rows across the arm at w, the light one toward the elbow."""
    d = np.array([h[2] - w[0], h[3] - w[1]], float) if h is not None else np.array([0.0, 1.0])
    d /= np.linalg.norm(d) or 1.0
    n = np.array([-d[1], d[0]])
    out = []
    for y in range(w[1] - 4, w[1] + 5):
        for x in range(w[0] - 4, w[0] + 5):
            p = np.array([x - w[0], y - w[1]], float)
            u, v = p @ d, p @ n
            if abs(v) <= 3.5 and -1.0 <= u < 1.0:
                out.append((x, y, "bracelets" if u < 0 else "braceletsShade"))
    return out


def stamp(rows, cx, top, key, centred=True):
    w = len(rows[0])
    x0 = cx - w // 2
    y0 = top - (len(rows) // 2 if centred else 0)
    return [(x0 + i, y0 + j, key) for j, row in enumerate(rows) for i, ch in enumerate(row) if ch == "x"]


def keep_mask(a, player, action, rects=(), offset=(0, 0)):
    """What no look recolours though it's drawn in a colour looks change: the sign's colours away
    from the sign (the radio, light spots on the skin), the trousers' colour in the sign's mark, in
    the action frames the shoes' colour above the feet (outlines, the belt, the clipboard, the
    shovel's handle), Player 1's radio in its trousers' colour, and the trousers' colour in the
    frame's keep rectangles (the detonator's outline and plunger)."""
    colours = list(SIGN_YELLOW) if player == "P1" else list(P2_SIGN)
    sign, through = sign_of(a, player), match(a, colours + [(0, 0, 0)] + RIM)
    while True:  # all of the sign, a piece cut off by its mark included
        more = sign | (grow(sign) & through)
        if (more == sign).all():
            break
        sign = more
    keep = match(a, colours) & ~sign
    trousers = TROUSERS if player == "P1" else P2_COLOURS[TROUSERS]
    keep |= match(a, [trousers]) & grow(grow(sign))
    for x0, y0, x1, y1 in rects:
        box = np.zeros_like(keep)
        box[max(0, y0 + offset[1]):y1 + offset[1] + 1, max(0, x0 + offset[0]):x1 + offset[0] + 1] = True
        keep |= match(a, [trousers]) & box
    if action is not None:
        shoes = match(a, [SHOES])
        ys = np.nonzero(shoes)[0]
        if len(ys):
            keep |= shoes & (np.arange(a.shape[0])[:, None] < ys.max() - FEET)
    if action in PROP_BELOW and player == "P1":
        trousers = match(a, [TROUSERS])
        for x in prop_columns(a):
            if 0 <= x < a.shape[1]:
                keep[int(a.shape[0] * 0.6) + 1:, x] |= trousers[int(a.shape[0] * 0.6) + 1:, x]
    return keep


def paint(a, player, anchor, offset=(0, 0), keep=None):
    """The overlay for one frame: every part, keyed, on this player's skin only."""
    skin = match(a, P1_SKIN if player == "P1" else P2_SKIN)
    h, w = skin.shape
    ox, oy = offset
    worn = anchor.get("chain", True)
    span = [v + ox for v in worn] if isinstance(worn, list) else None
    links = chain(sign_of(a, player), skin, pendant=worn != "back", span=span) if worn else []
    parts = list(links)
    for wx, wy, hx, hy in anchor.get("wrists", []):
        parts += bracelet((wx + ox, wy + oy), (wx + ox, wy + oy, hx + ox, hy + oy))
    if "chest" in anchor:
        cx, top = anchor["chest"][0] + ox, anchor["chest"][1] + oy
    elif links:
        cx, top = chain_bottom(links)
        top += 3
    else:
        cx = top = None
    if cx is not None and anchor.get("chestTattoo", True):
        parts += stamp(WINGS, cx, top, "chestTattoo", centred=False)
    for x, y in anchor.get("arms", []):
        parts += stamp(HEART, x + ox, y + oy, "armTattoo")
    out = np.zeros((h, w, 4), np.uint8)
    taken = np.zeros((h, w), bool)
    for x, y, key in parts:  # the first part on a pixel keeps it: chain, bracelets, tattoos
        if 0 <= x < w and 0 <= y < h and skin[y, x] and not taken[y, x]:
            out[y, x] = KEYS[key] + (255,)
            taken[y, x] = True
    if keep is not None:
        out[keep] = KEYS["keep"] + (255,)
    return out


def offset(p1, p2):
    """How far Player 2's body sits from Player 1's in a frame (its head is taller)."""
    a, b = match(p1, P1_SKIN), match(p2, P2_SKIN)
    best, score = (0, 0), -1
    for dy in range(-2, 10):
        for dx in range(-3, 4):
            shifted = np.zeros_like(b)
            ys, xs = np.nonzero(a)
            ys, xs = ys + dy, xs + dx
            keep = (ys >= 0) & (ys < b.shape[0]) & (xs >= 0) & (xs < b.shape[1])
            shifted[ys[keep], xs[keep]] = True
            s = int((shifted & b).sum())
            if s > score:
                best, score = (dx, dy), s
    return best


def frames():
    """(name, P1 frame, P2 frame, texture, where in it) for every frame the players wear a look in."""
    sheets = {p: np.array(Image.open(path).convert("RGBA")) for p, path in SHEETS.items()}
    cw, ch = CELL
    for r in range(sheets["P1"].shape[0] // ch):
        for c in range(sheets["P1"].shape[1] // cw):
            crop = lambda s: s[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw]
            if crop(sheets["P1"])[..., 3].any():
                yield f"sheet {c},{r}", crop(sheets["P1"]), crop(sheets["P2"]), "sheet", (c * cw, r * ch)
    for name in sorted(os.listdir(os.path.join(ACTIONS, "P1"))):
        if name.endswith(".png") and name[:-4] not in PROPS:
            p1 = np.array(Image.open(os.path.join(ACTIONS, "P1", name)).convert("RGBA"))
            p2 = np.array(Image.open(os.path.join(ACTIONS, "P2", name)).convert("RGBA"))
            yield name[:-4], p1, p2, name[:-4], (0, 0)


def guess(a):
    """First guesses for one frame: each arm off the torso, its hand the far end."""
    skin = match(a, P1_SKIN)
    links = chain(sign_mask(a)[0], skin)
    if not links:
        return {}
    xs = [x for x, _, _ in links]
    cx, chest_y = chain_bottom(links)
    half = (max(xs) - min(xs)) / 2 + 1
    h, w = skin.shape
    arm = skin.copy()
    arm[:, max(0, int(cx - half)):min(w, int(cx + half) + 1)] = False
    arm[:chest_y - 4] = False
    found = {"wrists": [], "arms": []}
    seen = np.zeros_like(arm)
    for y0, x0 in zip(*np.nonzero(arm)):
        if seen[y0, x0]:
            continue
        blob, stack = [], [(y0, x0)]
        seen[y0, x0] = True
        while stack:
            y, x = stack.pop()
            blob.append((int(x), int(y)))
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and arm[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(blob) < 12:
            continue
        # Along the arm from where it leaves the torso (else its point nearest the chest): the
        # hand is the far end, so a flexed arm's elbow isn't taken for it.
        inside = {(x, y) for x, y in blob}
        edge = int(cx - half) - 1, int(cx + half) + 1
        roots = [(x, y) for x, y in blob if x in edge]
        if not roots:
            roots = [min(blob, key=lambda p: (p[0] - cx) ** 2 + (p[1] - chest_y) ** 2)]
        dist = {r: 0 for r in roots}
        queue = list(roots)
        for x, y in queue:
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if n in inside and n not in dist:
                    dist[n] = dist[(x, y)] + 1
                    queue.append(n)
        far = max(dist.values())
        if far < 6:
            continue

        def centre(lo, hi):
            band = [p for p, d in dist.items() if lo <= d <= hi]
            mx, my = np.mean([p[0] for p in band]), np.mean([p[1] for p in band])
            return list(min(band, key=lambda p: (p[0] - mx) ** 2 + (p[1] - my) ** 2))
        hand = max(dist, key=dist.get)
        found["wrists"].append(centre(far - 4, far - 3) + list(hand))
        found["arms"].append(centre(round(far * 0.25), round(far * 0.4)))
    return found


def main():
    anchors = json.load(open(ANCHORS)) if os.path.exists(ANCHORS) else {}
    all_frames = list(frames())
    if "--detect" in sys.argv:
        for name, p1, _, _, _ in all_frames:
            anchors.setdefault(name, guess(p1))
        os.makedirs(os.path.dirname(ANCHORS), exist_ok=True)
        with open(ANCHORS, "w", encoding="utf-8", newline="\n") as f:
            f.write("{\n" + ",\n".join(f'  "{k}": {json.dumps(v)}' for k, v in anchors.items()) + "\n}\n")
        print(f"{ANCHORS}: {len(anchors)} frames")

    with open(os.path.join(HERE, "sprite.meta.template"), encoding="utf-8") as f:
        template = f.read()
    for folder in (OUT, os.path.join(OUT, "P1"), os.path.join(OUT, "P2")):
        os.makedirs(folder, exist_ok=True)
        write_meta(folder.rstrip("/\\") + ".meta", FOLDER_META)
    sheet_out = {p: np.zeros(np.array(Image.open(path)).shape[:2] + (4,), np.uint8) for p, path in SHEETS.items()}
    review = []
    for name, p1, p2, texture, (x0, y0) in all_frames:
        anchor = anchors.get(name, {})
        action = None if texture == "sheet" else name.split("_")[0]  # the sheet draws only shoes in the shoes' colour
        moved = offset(p1, p2)
        rects = anchor.get("keep", [])
        o1 = paint(p1, "P1", anchor, keep=keep_mask(p1, "P1", action, rects))
        o2 = paint(p2, "P2", anchor, moved, keep=keep_mask(p2, "P2", action, rects, moved))
        review.append((name, p1, o1, p2, o2))
        if texture == "sheet":
            for p, o in (("P1", o1), ("P2", o2)):
                sheet_out[p][y0:y0 + o.shape[0], x0:x0 + o.shape[1]] = o
        else:
            for p, a, o in (("P1", p1, o1), ("P2", p2, o2)):
                write_sprite(Image.fromarray(o, "RGBA"), os.path.join(OUT, p, texture + ".png"), a.shape[1] / 2, template)
    for p, path in SHEETS.items():
        write_sprite(Image.fromarray(sheet_out[p], "RGBA"), os.path.join(OUT, p, os.path.basename(path)), 0, template)
    draw_review(review)
    print(f"{len(review)} frames: {OUT}, {REVIEW}")


SHOW = {"chain": (255, 205, 60), "chainShade": (190, 140, 30), "bracelets": (230, 236, 245),
        "braceletsShade": (150, 160, 175), "chestTattoo": (40, 52, 110), "armTattoo": (196, 30, 50)}


def wear(a, o):
    out = a.copy()
    for key, colour in KEYS.items():
        if key not in SHOW:
            continue
        m = np.all(o[..., :3] == colour, axis=-1) & (o[..., 3] > 0)
        out[m, :3] = SHOW[key]
    return out


def draw_review(review, scale=4, cols=9):
    cw, ch = 74, 78
    rows = (len(review) * 2 + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cw * scale, rows * (ch * scale + 12)), (88, 96, 116, 255))
    d = ImageDraw.Draw(sheet)
    tiles = []
    for name, p1, o1, p2, o2 in review:
        tiles += [(name, wear(p1, o1)), (name + " P2", wear(p2, o2))]
    for i, (name, a) in enumerate(tiles):
        im = Image.fromarray(a, "RGBA")
        bbox = im.getbbox()
        im = im.crop(bbox) if bbox else im
        x, y = (i % cols) * cw * scale, (i // cols) * (ch * scale + 12)
        sheet.alpha_composite(im.resize((im.width * scale, im.height * scale), Image.NEAREST), (x + 4, y + 12))
        d.text((x + 4, y), name, fill=(255, 255, 255, 255))
    os.makedirs(os.path.dirname(REVIEW), exist_ok=True)
    sheet.convert("RGB").save(REVIEW)


if __name__ == "__main__":
    main()

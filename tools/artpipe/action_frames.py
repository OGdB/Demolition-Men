"""Turn the Actions' poses in content-work/actions/ into the sprites the game plays, plus their props.

    python tools/artpipe/action_frames.py

Writes Demolition Men/Assets/Resources/Actions/P1/<action>_<n>.png (the poses as drawn) and
P2/<action>_<n>.png (Player 2's colours, and its stop sign instead of the warning sign, turned
as far as the warning sign leans), each with a .meta: single sprite, 32 PPU, point filter, the
pivot on the belt at the bottom edge, the way the player sheets are cut. An existing .meta keeps
its GUID. The order of each action's frames is ACTIONS below; the game plays them in that order.
"""
import os
import sys
import uuid

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build_theme import FOLDER_META, write_meta  # noqa: E402

ROOT = os.path.join(HERE, "..", "..")
SRC = os.path.join(ROOT, "content-work", "actions")
ASSETS = os.path.join(ROOT, "Demolition Men", "Assets")
OUT = os.path.join(ASSETS, "Resources", "Actions")
P2_SHEET = os.path.join(ASSETS, "Animations", "Player_2", "+Demolition_man_Stop_V2.png")

# Frames per action in play order; the ids are ActionCatalog's sprite folders.
ACTIONS = {
    "boer": ["drink", "tween-drink-windup", "windup", "tween-windup-release", "release"],
    "kontplof": ["air", "tween-air-impact", "impact"],
    "bakkie": ["sip", "tween-sip-rush", "rush"],
    "kees": ["whistle", "tween-whistle-point", "point"],
    "sloop": ["arm", "tween-arm-fire", "fire"],
    "radio": ["set-down", "tween-set-down-groove", "groove"],
    # One pose breathing: rest, half, in, half (breathe-00, -01, -02 and -10 are its only looks).
    "schep": ["breathe-00", "breathe-01", "breathe-02", "breathe-10"],
    "stagiair": ["breathe-00", "breathe-01", "breathe-02", "breathe-10"],
    "hesje": ["breathe-00", "breathe-01", "breathe-02", "breathe-10"],
}

TROUSERS = (34, 32, 52)
SIGN_YELLOW = {(255, 224, 139), (255, 192, 85), (255, 236, 164)}
# Player 2's sheet is Player 1's with these colours swapped (and another head).
P2_COLOURS = {
    (238, 195, 154): (179, 100, 66),
    (217, 160, 102): (125, 69, 12),
    (205, 166, 129): (152, 85, 39),
    TROUSERS: (225, 187, 122),
}
P2_HEAD = {(246, 73, 73), (191, 0, 0), (255, 255, 255)}
P2_SIGN = {(246, 73, 73), (191, 0, 0)}
SPECK = 5  # pixels in a clump on the sign that are stray ones (specks)
# What sits on top of the sign (the hard hat, the slam's sparks) moves up with the taller head.
KEEP_ABOVE = {"hesje", "kontplof"}
# A prop on the ground drawn in the trousers' colour (the radio) keeps it.
PROP_BELOW = {"radio"}


def colours(a):
    return [[tuple(int(v) for v in p[:3]) if p[3] else None for p in row] for row in a]


def grow(mask):
    g = mask.copy()
    g[1:] |= mask[:-1]
    g[:-1] |= mask[1:]
    g[:, 1:] |= mask[:, :-1]
    g[:, :-1] |= mask[:, 1:]
    return g


def largest(mask):
    """The largest 8-connected blob of a mask."""
    seen = np.zeros_like(mask)
    best = np.zeros_like(mask)
    h, w = mask.shape
    for y0, x0 in zip(*np.nonzero(mask)):
        if seen[y0, x0]:
            continue
        blob, stack = [], [(y0, x0)]
        seen[y0, x0] = True
        while stack:
            y, x = stack.pop()
            blob.append((y, x))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
        if len(blob) > best.sum():
            best[:] = False
            for y, x in blob:
                best[y, x] = True
    return best


def sign_mask(a):
    """The warning sign: its yellow, the mark inside, the white ring around it."""
    c = colours(a)
    yellow = largest(np.array([[v in SIGN_YELLOW for v in row] for row in c], bool))
    if not yellow.any():
        return yellow, yellow
    ys, xs = np.nonzero(yellow)
    box = np.zeros_like(yellow)
    box[ys.min():ys.max() + 1, xs.min():xs.max() + 1] = True
    black = np.array([[v == (0, 0, 0) for v in row] for row in c], bool) & box
    core = yellow | black
    white = np.array([[v == (255, 255, 255) for v in row] for row in c], bool) & grow(grow(core))
    return core | white, black


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


RIM = [(255, 255, 255), (241, 241, 241), (223, 221, 219), (146, 146, 146)]  # the last: Player 2's back, dithered


def sign_of(a, player):
    sign = sign_mask(a)[0] if player == "P1" else p2_sign(a)
    rim = match(a, RIM)
    if not sign.any():
        return largest(rim)  # seen from behind: all white
    for _ in range(4):  # the action frames' off-white rim, a few pixels deep
        sign |= grow(sign) & rim
    return sign


# The marks the action frames draw on the sign in browns and the trousers' navy instead of black.
MARK = [(164, 111, 58), (102, 57, 49), (64, 36, 8), (34, 32, 52), (109, 67, 25), (196, 114, 31),
        (255, 204, 86), (210, 126, 48), (122, 46, 16), (70, 30, 22), P2_COLOURS[TROUSERS]]
HAT = [(196, 114, 31), (224, 138, 74), (246, 142, 40)]  # the hard hat's, on the vest's frames


def full_sign(a, player, action=None):
    """All of the player's sign: its colours, mark and rim, a piece cut off by the mark included,
    and the mark colours inside its outline (not the hard hat over it)."""
    colours = list(SIGN_YELLOW) if player == "P1" else list(P2_SIGN)
    sign, through = sign_of(a, player), match(a, colours + [(0, 0, 0)] + RIM)
    while True:
        more = sign | (grow(sign) & through)
        if (more == sign).all():
            break
        sign = more
    outline = np.zeros_like(sign)
    for y in np.nonzero(sign.any(1))[0]:
        xs = np.nonzero(sign[y])[0]
        outline[y, xs.min():xs.max() + 1] = True
    for x in np.nonzero(sign.any(0))[0]:
        ys = np.nonzero(sign[:, x])[0]
        outline[ys.min():ys.max() + 1, x] = True
    marks = [c for c in MARK if not (action in KEEP_ABOVE and c in HAT)]
    return sign | (outline & (match(a, marks) | match(a, colours) | specks(a, sign)))


def specks(a, sign, most=None):
    """Opaque pixels other than the sign in clumps of at most SPECK (or most): stray pixels drawn
    on it, not something in front of it. The caller says where they count (not the slam's sparks)."""
    most = SPECK if most is None else most
    rest = (a[..., 3] > 0) & ~sign
    out, seen = np.zeros_like(sign), np.zeros_like(sign)
    h, w = rest.shape
    for y0, x0 in zip(*np.nonzero(rest)):
        if seen[y0, x0]:
            continue
        clump, stack = [(y0, x0)], [(y0, x0)]
        seen[y0, x0] = True
        while stack:
            y, x = stack.pop()
            for ny in range(max(0, y - 1), min(h, y + 2)):
                for nx in range(max(0, x - 1), min(w, x + 2)):
                    if rest[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        clump.append((ny, nx))
                        stack.append((ny, nx))
        if len(clump) <= most:
            for y, x in clump:
                out[y, x] = True
    return out


def lean(mark):
    """Degrees the sign leans, from its mark's long axis: 0 upright, + top to the right."""
    ys, xs = np.nonzero(mark)
    if len(xs) < 4:
        return 0.0
    w, v = np.linalg.eigh(np.cov(np.vstack([xs, ys])))
    axis = v[:, 1]
    if axis[1] > 0:
        axis = -axis
    return float(np.degrees(np.arctan2(axis[0], -axis[1])))


def scale2x(a):
    p = np.pad(a, ((1, 1), (1, 1), (0, 0)), mode="edge")
    B, D, E, F, H = p[:-2, 1:-1], p[1:-1, :-2], p[1:-1, 1:-1], p[1:-1, 2:], p[2:, 1:-1]
    eq = lambda x, y: (x == y).all(2)  # noqa: E731
    out = np.zeros((a.shape[0] * 2, a.shape[1] * 2, a.shape[2]), a.dtype)
    out[0::2, 0::2] = np.where((eq(D, B) & ~eq(B, F) & ~eq(D, H))[..., None], D, E)
    out[0::2, 1::2] = np.where((eq(B, F) & ~eq(B, D) & ~eq(F, H))[..., None], F, E)
    out[1::2, 0::2] = np.where((eq(D, H) & ~eq(D, B) & ~eq(H, F))[..., None], D, E)
    out[1::2, 1::2] = np.where((eq(H, F) & ~eq(D, H) & ~eq(B, F))[..., None], F, E)
    return out


def rotsprite(im, degrees):
    """Turn pixel art without blurring it: Scale2x three times, turn, take every 8th pixel."""
    if abs(degrees) < 3:
        return im
    a = np.array(im)
    for _ in range(3):
        a = scale2x(a)
    big = Image.fromarray(a).rotate(degrees, resample=Image.NEAREST, expand=True)
    b = np.array(big)[4::8, 4::8]
    b[:, :, 3] = np.where(b[:, :, 3] >= 128, 255, 0)
    out = Image.fromarray(b)
    return out.crop(out.getbbox())


def p2_head():
    sheet = Image.open(P2_SHEET).convert("RGBA")
    idle = np.array(sheet.crop((0, 0, 128, 64)))
    head = largest(np.array([[v in P2_HEAD for v in row] for row in colours(idle)], bool))
    out = idle.copy()
    out[~head] = 0
    im = Image.fromarray(out)
    return im.crop(im.getbbox())


def prop_columns(a):
    """Columns of the orange prop standing on the ground (the radio)."""
    h = a.shape[0]
    orange = [x for y in range(int(h * 0.6), h) for x in range(a.shape[1])
              if a[y, x, 3] and a[y, x, 0] > 200 and 60 < a[y, x, 1] < 200 and a[y, x, 2] < 100]
    return range(min(orange) - 1, max(orange) + 2) if orange else range(0)


def to_p2(a, head, action):
    out = a.copy()
    c = colours(a)
    keep = prop_columns(a) if action in PROP_BELOW else range(0)
    for y, row in enumerate(c):
        for x, v in enumerate(row):
            if v in P2_COLOURS and not (v == TROUSERS and x in keep and y > a.shape[0] * 0.6):
                out[y, x, :3] = P2_COLOURS[v]
    sign, mark = sign_mask(a)
    if not sign.any():
        return Image.fromarray(out)
    ys, xs = np.nonzero(sign)
    bottom, centre = ys.max(), (xs.min() + xs.max()) / 2
    above = None
    if action in KEEP_ABOVE:
        above = np.zeros_like(out)
        above[:ys.min(), xs.min():xs.max() + 1] = out[:ys.min(), xs.min():xs.max() + 1]
        out[:ys.min(), xs.min():xs.max() + 1] = 0
    out[full_sign(a, "P1", action)] = 0  # with its rim and stray pixels; the head still goes where the core was
    turned = rotsprite(head, -lean(mark))
    pad = max(0, turned.height - bottom - 1)
    canvas = Image.new("RGBA", (out.shape[1], out.shape[0] + pad))
    canvas.alpha_composite(Image.fromarray(out), (0, pad))
    x = int(round(centre - turned.width / 2))
    left = max(0, -x)
    if left:  # a head over the left edge: widen the frame
        wider = Image.new("RGBA", (canvas.width + left, canvas.height))
        wider.alpha_composite(canvas, (left, 0))
        canvas, x = wider, 0
    if x + turned.width > canvas.width:
        wider = Image.new("RGBA", (x + turned.width, canvas.height))
        wider.alpha_composite(canvas, (0, 0))
        canvas = wider
    top = pad + bottom + 1 - turned.height
    canvas.alpha_composite(turned, (x, top))
    if above is not None:
        # The frame sits at (left, pad) in the canvas; the old sign's top row moves to the new
        # head's, three rows into it so the hat rests on the head.
        y = pad + (top - (pad + ys.min()) + 3)
        if y < 0:
            taller = Image.new("RGBA", (canvas.width, canvas.height - y))
            taller.alpha_composite(canvas, (0, -y))
            canvas, y = taller, 0
        canvas.alpha_composite(Image.fromarray(above), (left, y))
    return canvas.crop(canvas.getbbox())


def pivot_x(a, trousers):
    """The belt's middle: the trousers' top rows. The player sheets pivot on the body, so a
    lunge doesn't slide the player under their own feet."""
    navy = np.array([[v == trousers for v in row] for row in colours(a)], bool)
    rows = np.nonzero(navy.any(1))[0]
    if len(rows) == 0:
        return a.shape[1] / 2
    top = navy[rows[0]:rows[0] + 3]
    return float(np.nonzero(top)[1].mean()) + 0.5


def write_sprite(im, path, px, template):
    im.save(path)
    meta = path + ".meta"
    guid = uuid.uuid4().hex
    if os.path.exists(meta):
        for line in open(meta, encoding="utf-8"):
            if line.startswith("guid: "):
                guid = line.split()[1]
    text = template.replace("{GUID}", guid)
    text = text.replace("  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}",
                        f"  alignment: 9\n  spritePivot: {{x: {px / im.width:.4f}, y: 0}}")
    assert "alignment: 9" in text, "sprite.meta.template changed: pivot line not found"
    # Readable: a look recolours them while the game runs (Meta/View/LookSkin).
    text = text.replace("  isReadable: 0\n", "  isReadable: 1\n")
    assert "isReadable: 1" in text, "sprite.meta.template changed: isReadable line not found"
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def pallet():
    """A pallet of bricks for the crane to drop: running-bond bricks on wooden slats, strapped."""
    from palette import RAMPS
    brick, wood, steel = RAMPS["brick"], RAMPS["wood"], RAMPS["steel"]
    w, h = 40, 24
    a = np.zeros((h, w, 4), np.uint8)

    def put(x, y, c):
        a[y, x] = (*c, 255)
    for y in range(18, 24):  # the pallet: two boards over three blocks
        for x in range(w):
            if y in (18, 19) or (y in (22, 23) and x % 18 < 5) or (y in (20, 21) and x % 18 < 5):
                put(x, y, wood[0] if y in (19, 23) or x in (0, w - 1) else wood[2] if y == 18 else wood[1])
    for y in range(2, 18):  # bricks, 8 x 4 in running bond, mortar in the darkest brick
        row = (y - 2) // 4
        for x in range(2, w - 2):
            joint = (y - 2) % 4 == 3 or (x - 2 + (4 if row % 2 else 0)) % 8 == 7
            shade = brick[3] if (y - 2) % 4 == 0 and not joint else brick[2] if (x * 7 + row * 3) % 5 else brick[1]
            put(x, y, brick[0] if joint else shade)
        put(1, y, brick[0])
        put(w - 2, y, brick[0])
    for x in range(1, w - 1):  # top and the straps
        put(x, 1, brick[0])
    for y in range(1, 18):
        for x in (12, 27):
            put(x, y, steel[2] if y % 4 else steel[3])
    return Image.fromarray(a, "RGBA")


def radio_prop():
    """The site radio as it stands on the ground in the groove pose."""
    a = np.array(Image.open(os.path.join(SRC, "radio", "groove.png")).convert("RGBA"))
    cols = prop_columns(a)
    h = a.shape[0]
    crop = a[int(h * 0.6):, cols.start:cols.stop].copy()
    keep = np.zeros(crop.shape[:2], bool)
    for y in range(crop.shape[0]):
        for x in range(crop.shape[1]):
            r, g, b, al = crop[y, x]
            keep[y, x] = al and (r > 150 or (r < 70 and g < 70))  # orange body, black speakers and frame
    keep = largest(keep)
    crop[~keep] = 0
    im = Image.fromarray(crop)
    return im.crop(im.getbbox())


def note():
    """An eighth note, white (tinted in the game)."""
    rows = ["...##..", "...###.", "...#.##", "...#..#", "...#...", "...#...", ".###...", "####...", "####...", ".##...."]
    a = np.zeros((len(rows), len(rows[0]), 4), np.uint8)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == "#":
                a[y, x] = (255, 255, 255, 255)
    return Image.fromarray(a, "RGBA")


def main():
    with open(os.path.join(HERE, "sprite.meta.template"), encoding="utf-8") as f:
        template = f.read()
    for folder in (OUT, os.path.join(OUT, "P1"), os.path.join(OUT, "P2")):
        os.makedirs(folder, exist_ok=True)
        write_meta(folder.rstrip("/\\") + ".meta", FOLDER_META)
    head = p2_head()
    for action, poses in ACTIONS.items():
        for n, pose in enumerate(poses):
            src = Image.open(os.path.join(SRC, action, pose + ".png")).convert("RGBA")
            a = np.array(src)
            name = f"{action}_{n}.png"
            write_sprite(src, os.path.join(OUT, "P1", name), pivot_x(a, TROUSERS), template)
            p2 = to_p2(a, head, action)
            write_sprite(p2, os.path.join(OUT, "P2", name), pivot_x(np.array(p2), P2_COLOURS[TROUSERS]), template)
        print(f"{action}: {len(poses)} frames")
    for name, im, px in (("pallet", pallet(), None), ("radio", radio_prop(), None), ("note", note(), None)):
        write_sprite(im, os.path.join(OUT, name + ".png"), im.width / 2, template)
    print("props: pallet, radio, note")


if __name__ == "__main__":
    main()

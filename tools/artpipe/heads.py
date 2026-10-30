"""Other traffic signs for the crew's heads: drawn here, turned and placed on every frame.

    python tools/artpipe/heads.py

A look with a sign (Looks.json "head") clears the player's own sign (the overlays' "sign" key,
accessories.py) and puts this one in its place: on the same spot, its lower edge on the old
sign's, turned as far as the old one leans (rotsprite, as action_frames.py turns Player 2's stop
sign), seen from behind as a white blank like the warning sign. It goes on behind whatever was
in front of the old sign (an arm, the wrecking ball, the mug, the hard hat).

Writes, per sign, Demolition Men/Assets/Resources/LookHeads/<id>.png (every turn of it the frames
need, packed) and Resources/LookHeads.json (per sign, player and texture: which piece of the
atlas goes where, in pixels from the top left), read by Meta/View/LookSkin; and
content-work/looks/heads.png to review them on a few frames of both players.
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from accessories import ASSETS, KEYS, OUT as OVERLAYS, SHEETS, frames, full_sign, keep_mask, paint  # noqa: E402
from action_frames import grow, lean, p2_head, rotsprite, sign_mask, write_sprite  # noqa: E402
from build_theme import FOLDER_META, write_meta  # noqa: E402

ROOT = os.path.join(HERE, "..", "..")
OUT = os.path.join(ASSETS, "Resources", "LookHeads")
INDEX = os.path.join(ASSETS, "Resources", "LookHeads.json")
REVIEW = os.path.join(ROOT, "content-work", "looks", "heads.png")
ATLAS_WIDTH = 256

WHITE, BLACK, DARK = (255, 255, 255), (0, 0, 0), (40, 40, 46)
RED, RED_SHADE = (246, 73, 73), (191, 0, 0)  # Player 2's stop sign
BLUE, BLUE_SHADE = (52, 104, 214), (30, 70, 160)
YELLOW, YELLOW_SHADE = (255, 204, 51), (230, 160, 20)

N = 17  # the drawn signs fit 17x17, as tall as the warning sign, so they fit where it was
YY, XX = np.mgrid[:N, :N]
C = (N - 1) / 2
R = np.hypot(XX - C, YY - C)
RIGHT = XX - C > 2  # the shaded side, as on the warning and stop signs

DIGITS = {"3": ["xxx", "..x", ".xx", "..x", "xxx"],
          "0": ["xxx", "x.x", "x.x", "x.x", "xxx"]}
P = ["xxxx.", "x...x", "x...x", "xxxx.", "x....", "x....", "x...."]


def image(colour_of):
    """A 17x17 sign from a function of (x, y) to a colour or None."""
    a = np.zeros((N, N, 4), np.uint8)
    for y in range(N):
        for x in range(N):
            c = colour_of(x, y)
            if c is not None:
                a[y, x] = c + (255,)
    return Image.fromarray(a)


def stamp(rows, x0, y0):
    return {(x0 + i, y0 + j) for j, row in enumerate(rows) for i, ch in enumerate(row) if ch == "x"}


def no_entry():
    def at(x, y):
        r = R[y, x]
        if r > 8.4:
            return None
        if r > 7.2:
            return WHITE
        if 7 <= y <= 9 and 3 <= x <= 13:
            return WHITE
        return RED_SHADE if RIGHT[y, x] else RED
    return image(at)


def speed_30():
    digits = stamp(DIGITS["3"], 5, 6) | stamp(DIGITS["0"], 9, 6)

    def at(x, y):
        r = R[y, x]
        if r > 8.4:
            return None
        if r > 7.6:
            return WHITE
        if r > 5.6:
            return RED_SHADE if RIGHT[y, x] else RED
        return BLACK if (x, y) in digits else WHITE
    return image(at)


def no_parking():
    def at(x, y):
        r = R[y, x]
        if r > 8.4:
            return None
        if r > 7.6:
            return WHITE
        if r > 5.6 or abs((x - C) - (y - C)) <= 0.8:
            return RED_SHADE if RIGHT[y, x] else RED
        return BLUE_SHADE if RIGHT[y, x] else BLUE
    return image(at)


def give_way():
    # Point down: 17 wide at the top, 15 rows to the point.
    def at(x, y):
        if y > 14:
            return None
        half = 8 * (14 - y) / 14 + 0.5
        side = half - abs(x - C)
        if side < 0:
            return None
        inset = min(y + 0.5, side * 0.868)
        if inset < 1:
            return WHITE
        if inset < 3.2:
            return RED_SHADE if RIGHT[y, x] else RED
        return WHITE
    return image(at)


def priority():
    def at(x, y):
        d = abs(x - C) + abs(y - C)
        if d > 8:
            return None
        if d > 7:
            return DARK
        if d > 5:
            return WHITE
        return YELLOW_SHADE if (x - C) + (y - C) > 1 else YELLOW
    return image(at)


def parking():
    glyph = stamp(P, 6, 5)

    def at(x, y):
        if not (1 <= x <= 15 and 1 <= y <= 15):
            return None
        if x in (1, 15) or y in (1, 15):
            return WHITE
        if (x, y) in glyph:
            return WHITE
        return BLUE_SHADE if x >= 12 else BLUE
    return image(at)


def railway():
    a = (XX - C) - (YY - C)
    b = (XX - C) + (YY - C)
    boards = ((np.abs(a) <= 1.6) | (np.abs(b) <= 1.6)) & (R <= 8.6)
    edge = grow(boards) & ~boards

    def at(x, y):
        if edge[y, x]:
            return DARK
        if not boards[y, x]:
            return None
        along = b[y, x] if abs(a[y, x]) <= 1.6 else a[y, x]
        return RED if int((along + 20) // 4) % 2 == 0 else WHITE
    return image(at)


def warning():
    """Player 1's warning sign, for Player 2 to wear."""
    idle = np.array(Image.open(SHEETS["P1"]).convert("RGBA").crop((0, 0, 128, 64)))
    out = idle.copy()
    out[~full_sign(idle, "P1")] = 0
    im = Image.fromarray(out)
    return im.crop(im.getbbox())


# Looks.json's "head" ids.
HEADS = {"warning": warning, "stop": p2_head, "noentry": no_entry, "giveway": give_way,
         "priority": priority, "speed30": speed_30, "parking": parking, "noparking": no_parking,
         "railway": railway}


def blank(im):
    """The back of a sign: all white, as the warning sign's."""
    a = np.array(im)
    a[a[..., 3] > 0, :3] = 255
    return Image.fromarray(a)


def placements(head):
    """Every frame's turn of the sign and where it goes: (player, texture, image, x, y), top left."""
    out = []
    for name, p1, p2, texture, (x0, y0) in frames():
        yellow, mark = sign_mask(p1)
        back = not yellow.any()  # seen from behind
        angle = 0.0 if back else lean(mark)  # Player 2's frames lean as Player 1's (action_frames.to_p2)
        for player, a in (("P1", p1), ("P2", p2)):
            sign = full_sign(a, player, None if texture == "sheet" else name.split("_")[0])
            if not sign.any():
                continue
            ys, xs = np.nonzero(sign)
            turned = rotsprite(blank(head) if back else head, -angle)
            x = int(round((xs.min() + xs.max()) / 2 - turned.width / 2))
            y = int(ys.max()) + 1 - turned.height
            # Keep it inside the frame: a sign at the frame's edge loses what sticks out.
            left, top = max(0, -x), max(0, -y)
            right, bottom = min(turned.width, a.shape[1] - x), min(turned.height, a.shape[0] - y)
            piece = turned.crop((left, top, right, bottom))
            tex = os.path.basename(SHEETS[player])[:-4] if texture == "sheet" else texture
            out.append((player, tex, piece, x0 + x + left, y0 + y + top, name))
    return out


def pack(pieces):
    """Shelves of pieces, the same piece once: the atlas and each piece's place in it."""
    seen, spots, x, y, shelf = {}, [], 0, 0, 0
    for im in pieces:
        key = (im.size, im.tobytes())
        if key not in seen:
            if x + im.width > ATLAS_WIDTH:
                x, y, shelf = 0, y + shelf + 1, 0
            seen[key] = (x, y)
            x, shelf = x + im.width + 1, max(shelf, im.height)
        spots.append(seen[key])
    atlas = Image.new("RGBA", (ATLAS_WIDTH, y + shelf + 1))
    done = set()
    for im, spot in zip(pieces, spots):
        if spot not in done:
            atlas.alpha_composite(im, spot)
            done.add(spot)
    return atlas.crop((0, 0, ATLAS_WIDTH, y + shelf + 1)), spots


def wear(frame, overlay, pieces):
    """As LookSkin does: clear the player's own sign, then the new one on whatever is clear."""
    a = frame.copy()
    for key in ("sign", "signKeep"):
        a[np.all(overlay[..., :3] == KEYS[key], axis=-1) & (overlay[..., 3] > 0)] = 0
    for im, x, y in pieces:
        b = np.array(im)
        for j in range(b.shape[0]):
            for i in range(b.shape[1]):
                if b[j, i, 3] and a[y + j, x + i, 3] == 0:
                    a[y + j, x + i] = b[j, i]
    return a


def main():
    with open(os.path.join(HERE, "sprite.meta.template"), encoding="utf-8") as f:
        template = f.read()
    os.makedirs(OUT, exist_ok=True)
    write_meta(OUT + ".meta", FOLDER_META)
    index, review = {"heads": []}, {}
    for hid, draw in HEADS.items():
        head = draw()
        places = placements(head.crop(head.getbbox()))
        atlas, spots = pack([p[2] for p in places])
        write_sprite(atlas, os.path.join(OUT, hid + ".png"), atlas.width / 2, template)
        index["heads"].append({"id": hid, "places": [
            {"p2": player == "P2", "tex": tex, "sx": sx, "sy": sy, "w": im.width, "h": im.height, "dx": x, "dy": y}
            for (player, tex, im, x, y, _), (sx, sy) in zip(places, spots)]})
        review[hid] = places
        print(f"{hid}: {len(places)} frames, atlas {atlas.size}")
    with open(INDEX, "w", encoding="utf-8", newline="\n") as f:
        json.dump(index, f, separators=(",", ":"))
    write_meta(INDEX + ".meta", "fileFormatVersion: 2\nguid: {GUID}\nTextScriptImporter:\n  externalObjects: {}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    draw_review(review)


SHOWN = ["sheet 0,0", "sheet 2,1", "sheet 1,3", "sheet 3,3", "sheet 1,4", "boer_4", "hesje_0", "kees_1", "bakkie_0"]


def draw_review(review, scale=3):
    tiles = {(name, p): (a, texture, xy) for name, p1, p2, texture, xy in frames() if name in SHOWN
             for p, a in (("P1", p1), ("P2", p2))}
    overlays = {}
    for (name, player), (a, texture, xy) in tiles.items():
        action = None if texture == "sheet" else name.split("_")[0]
        overlays[(name, player)] = paint(a, player, {}, keep=keep_mask(a, player, action), action=action)
    cols = len(SHOWN) * 2
    sheet = Image.new("RGBA", (cols * 80 * scale + 120, len(review) * 80 * scale), (88, 96, 116, 255))
    d = ImageDraw.Draw(sheet)
    for row, (hid, places) in enumerate(review.items()):
        d.text((4, row * 80 * scale + 4), hid, fill=(255, 255, 255, 255))
        for col, (name, player) in enumerate((n, p) for n in SHOWN for p in ("P1", "P2")):
            a, texture, (x0, y0) = tiles[(name, player)]
            mine = [(im, x - x0, y - y0) for p, tex, im, x, y, n in places if p == player and n == name]
            im = Image.fromarray(wear(a, overlays[(name, player)], mine))
            bbox = im.getbbox()
            im = im.crop(bbox)
            sheet.alpha_composite(im.resize((im.width * scale, im.height * scale), Image.NEAREST),
                                  (120 + col * 80 * scale, row * 80 * scale + 4))
    os.makedirs(os.path.dirname(REVIEW), exist_ok=True)
    sheet.convert("RGB").save(REVIEW)
    print(REVIEW, sheet.size)


if __name__ == "__main__":
    main()

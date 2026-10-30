"""The crew's look items as the game draws them, on one contact sheet to review.

    python tools/artpipe/looks.py [out.png]

A look item (Demolition Men/Assets/Resources/Looks.json, the game's LookCatalog) dresses one slot:
the outfit recolours the trousers and shoes, the sign recolours the player's own or puts another
on (heads.py), the chain, bracelets and tattoos come from the overlays accessories.py draws.
This dresses the players the way Meta/View/LookSkin does and writes, to
content-work/looks/looks.png, a few sets of items worn together and then every item on its own,
by slot (Player 1 and Player 2: idle, a run frame, a punch, the jump, an action pose).
"""
import json
import os
import sys

from PIL import Image

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from accessories import KEYS, OUT as OVERLAYS  # noqa: E402
from heads import INDEX as HEAD_INDEX, OUT as HEAD_ATLASES  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..", "..")
ASSETS = os.path.join(ROOT, "Demolition Men", "Assets")
LOOKS = os.path.join(ASSETS, "Resources", "Looks.json")

# What a look may recolour, per player: the colours as the sheets draw them (LookPalette in Looks.cs).
P1 = {"trousers": [(34, 32, 52)], "shoes": [(102, 57, 49)],
      "sign": [(255, 224, 139), (255, 236, 164)], "signShade": [(255, 192, 85)]}
P2 = {"trousers": [(225, 187, 122)], "shoes": [(102, 57, 49)],
      "sign": [(246, 73, 73)], "signShade": [(191, 0, 0)]}

SHEETS = {1: os.path.join(ASSETS, "Animations", "Player_1", "+Demolition_man.png"),
          2: os.path.join(ASSETS, "Animations", "Player_2", "+Demolition_man_Stop_V2.png")}
# Frames on the sheet (128x64 cells): idle, a run frame, a punch, the jump.
CELLS = [(0, 0), (2, 1), (1, 3), (0, 2)]
POSE = "radio_2.png"  # the radio's groove, from Resources/Actions/P<n>


def rgb(hex_):
    return tuple(int(hex_[i:i + 2], 16) for i in (0, 2, 4))


def lighten(c, t):
    return tuple(round(v + (255 - v) * t) for v in c)


def darken(c, t):
    return tuple(round(v * (1 - t)) for v in c)


def extras(look):
    # Mirrors LookPalette.Extras: overlay key to the look's colour; metal gets a darker shade.
    out = {}
    for part in ("chain", "bracelets", "chestTattoo", "armTattoo"):
        if look and look.get(part):
            out[KEYS[part]] = rgb(look[part])
            if part + "Shade" in KEYS:
                out[KEYS[part + "Shade"]] = darken(rgb(look[part]), 0.3)
    return out


def dress(im, layer, palette, extra, head=()):
    """As LookSkin does: the palette and extras on all but what the overlay keeps, the player's own
    sign cleared for another, which then goes on every clear pixel of its pieces (image, x, y)."""
    im = im.convert("RGBA")
    px, keys = im.load(), layer.convert("RGBA").load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            key = keys[x, y][:3] if keys[x, y][3] else None
            if head and key in (KEYS["sign"], KEYS["signKeep"]):
                px[x, y] = (0, 0, 0, 0)
                continue
            if not a or key in (KEYS["keep"], KEYS["signKeep"]):
                continue
            if key in extra:
                px[x, y] = extra[key] + (a,)
            elif (r, g, b) in palette:
                px[x, y] = palette[(r, g, b)] + (a,)
    for piece, x0, y0 in head:
        for j in range(piece.height):
            for i in range(piece.width):
                c = piece.getpixel((i, j))
                x, y = x0 + i, y0 + j
                if c[3] and 0 <= x < im.width and 0 <= y < im.height and px[x, y][3] == 0:
                    px[x, y] = c
    return im


_HEADS = None


def head_pieces(head, player, texture, box):
    """The sign's pieces for a texture (LookHeads.json), placed relative to box's top left."""
    global _HEADS
    if not head:
        return []
    if _HEADS is None:
        _HEADS = {h["id"]: h["places"] for h in json.load(open(HEAD_INDEX))["heads"]}
    atlas = Image.open(os.path.join(HEAD_ATLASES, head + ".png")).convert("RGBA")
    return [(atlas.crop((p["sx"], p["sy"], p["sx"] + p["w"], p["sy"] + p["h"])), p["dx"] - box[0], p["dy"] - box[1])
            for p in _HEADS[head] if p["p2"] == (player == 2) and p["tex"] == texture]


def combine(items):
    """A player's items as one look, as LookCatalog.Combine puts them together."""
    look = {}
    for item in items:
        for key, value in item.items():
            if value and key not in ("id", "name", "slot", "source", "price", "sheets"):
                look[key] = value
    return look


def mapping(look, player):
    # Mirrors LookPalette.Map: each part's colours to the look's; the sign's highlight a little lighter.
    sources = P1 if player == 1 else P2
    out = {}
    for part, colours in sources.items():
        if not look.get(part):
            continue
        to = rgb(look[part])
        for i, c in enumerate(colours):
            out[c] = lighten(to, 0.35) if part == "sign" and i > 0 else to
    return out


def row(look, scale=3):
    tiles = []
    for player in (1, 2):
        table = mapping(look, player) if look else {}
        extra = extras(look)
        head = (look or {}).get("head")
        sheet = Image.open(SHEETS[player])
        name = os.path.basename(SHEETS[player])
        layer = Image.open(os.path.join(OVERLAYS, f"P{player}", name))
        for cx, cy in CELLS:
            box = (cx * 128, cy * 64, cx * 128 + 128, cy * 64 + 64)
            tiles.append(dress(sheet.crop(box), layer.crop(box), table, extra, head_pieces(head, player, name[:-4], box)))
        pose = os.path.join(ASSETS, "Resources", "Actions", f"P{player}", POSE)
        if os.path.exists(pose):
            p = dress(Image.open(pose), Image.open(os.path.join(OVERLAYS, f"P{player}", POSE)), table, extra,
                      head_pieces(head, player, POSE[:-4], (0, 0)))
            canvas = Image.new("RGBA", (128, 64))
            p.thumbnail((128, 64))
            canvas.alpha_composite(p, ((128 - p.width) // 2, 64 - p.height))
            tiles.append(canvas)
    strip = Image.new("RGBA", (128 * len(tiles), 64), (88, 90, 100, 255))
    for i, t in enumerate(tiles):
        strip.alpha_composite(t, (i * 128, 0))
    return strip.resize((strip.width * scale // 2, strip.height * scale // 2), Image.NEAREST)


# Sets worn together, to see that the slots go with each other.
SETS = [["denim", "head.noentry", "chain.gold", "bracelets.gold", "tattoo.full"],
        ["blackout", "head.railway", "chain.silver", "tattoo.hearts"],
        ["whitesuit", "sign.gold", "chain.ice", "bracelets.silver"],
        ["camo", "head.priority", "bracelets.leather", "tattoo.toxic"]]


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "content-work", "looks", "looks.png")
    items = json.load(open(LOOKS))["looks"]
    by_id = {i["id"]: i for i in items}
    from PIL import ImageDraw
    rows = [("CREW (own look)", row(None))]
    rows += [(" + ".join(by_id[i]["name"] for i in s), row(combine(by_id[i] for i in s))) for s in SETS]
    for slot in ("Outfit", "Sign", "Chain", "Bracelets", "Tattoo"):
        rows += [(f"{slot}: {i['name']}  ({i['source']} {i['price'] or i['sheets'] or ''})", row(combine([i])))
                 for i in items if i["slot"] == slot]
    label = 420
    sheet = Image.new("RGBA", (label + rows[0][1].width, sum(r.height for _, r in rows)), (40, 42, 48, 255))
    draw = ImageDraw.Draw(sheet)
    y = 0
    for name, r in rows:
        draw.text((8, y + r.height // 2 - 6), name, fill=(240, 230, 200, 255))
        sheet.alpha_composite(r, (label, y))
        y += r.height
    os.makedirs(os.path.dirname(out), exist_ok=True)
    sheet.convert("RGB").save(out)
    print(out, sheet.size)


if __name__ == "__main__":
    main()

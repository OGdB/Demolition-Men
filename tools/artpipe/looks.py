"""The crew's looks as the game draws them, on one contact sheet to review.

    python tools/artpipe/looks.py [out.png]

A look (Demolition Men/Assets/Resources/Looks.json, the game's LookCatalog) recolours the
trousers, the shoes and the sign of the player sheets and the action frames, the skin staying
the player's, and adds the chain, bracelets and tattoos it wears from the overlays
accessories.py draws. This applies each look the way Meta/View/LookSkin does and writes a row
per look (Player 1 and Player 2: idle, a run frame, a punch, the jump, an action pose) to
content-work/looks/looks.png, the crew's own look first.
"""
import json
import os
import sys

from PIL import Image

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from accessories import KEYS, OUT as OVERLAYS  # noqa: E402

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


def dress(im, layer, palette, extra):
    """As LookSkin does: the palette on all but what the overlay keeps, then the look's extras."""
    im = im.convert("RGBA")
    px, keys = im.load(), layer.convert("RGBA").load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            key = keys[x, y]
            if key[3] and key[:3] == KEYS["keep"]:
                continue
            if a and (r, g, b) in palette:
                px[x, y] = palette[(r, g, b)] + (a,)
            if key[3] and key[:3] in extra:
                px[x, y] = extra[key[:3]] + (px[x, y][3],)
    return im


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
        sheet = Image.open(SHEETS[player])
        layer = Image.open(os.path.join(OVERLAYS, f"P{player}", os.path.basename(SHEETS[player])))
        for cx, cy in CELLS:
            box = (cx * 128, cy * 64, cx * 128 + 128, cy * 64 + 64)
            tiles.append(dress(sheet.crop(box), layer.crop(box), table, extra))
        pose = os.path.join(ASSETS, "Resources", "Actions", f"P{player}", POSE)
        if os.path.exists(pose):
            p = dress(Image.open(pose), Image.open(os.path.join(OVERLAYS, f"P{player}", POSE)), table, extra)
            canvas = Image.new("RGBA", (128, 64))
            p.thumbnail((128, 64))
            canvas.alpha_composite(p, ((128 - p.width) // 2, 64 - p.height))
            tiles.append(canvas)
    strip = Image.new("RGBA", (128 * len(tiles), 64), (88, 90, 100, 255))
    for i, t in enumerate(tiles):
        strip.alpha_composite(t, (i * 128, 0))
    return strip.resize((strip.width * scale // 2, strip.height * scale // 2), Image.NEAREST)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "content-work", "looks", "looks.png")
    looks = json.load(open(LOOKS))["looks"]
    from PIL import ImageDraw
    rows = [("CREW (own look)", row(None))] + [(f"{l['name']}  ({l['source']} {l['price'] or l['sheets'] or ''})", row(l)) for l in looks]
    label = 260
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

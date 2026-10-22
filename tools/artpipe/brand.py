"""Store and brand art from the game's own sprites and title font.

    python tools/artpipe/brand.py

Writes:
  brand/icon-512.png            Play listing icon (full square: Play rounds the corners)
  brand/feature-graphic.png     Play feature graphic, 1024x500, no alpha
  brand/logo.png                the title on one line, transparent (press kit, trailer)
  brand/logo-stacked.png        the title on two lines, transparent (square spaces, splash)
  Demolition Men/Assets/Brand/Icon.png                 default/legacy/round app icon, 512
  Demolition Men/Assets/Brand/IconBackground.png       adaptive icon layers, 432 (108 dp at
  Demolition Men/Assets/Brand/IconForeground.png       xxxhdpi); launchers show the middle 2/3
  content-work/brand.png        contact sheet: everything, the icon at launcher sizes and masks

Pixel art like the game: each scene is drawn on a small grid and scaled up by a whole
number. The title is Black Ops One in the in-game title's look (gold, ink outline, drop
shadow, hazard tape). An existing .meta is kept, so GUIDs survive regeneration.
"""
import math
import os
import re
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFont

sys.dont_write_bytecode = True  # no __pycache__ next to the tools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_theme import META_TEMPLATE, ensure_folder, write_meta  # noqa: E402
from palette import RAMPS  # noqa: E402

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
ASSETS = os.path.join(ROOT, "Demolition Men", "Assets")
OUT = os.path.join(ROOT, "brand")
UNITY_OUT = os.path.join(ASSETS, "Brand")
PREVIEW = os.path.join(ROOT, "content-work", "brand.png")
FONT = os.path.join(ASSETS, "Resources", "Fonts", "BlackOpsOne.ttf")
PLAYER_1 = os.path.join(ASSETS, "Animations", "Player_1", "+Demolition_man.png")
PLAYER_2 = os.path.join(ASSETS, "Animations", "Player_2", "+Demolition_man_Stop_V2.png")
OBJECTS = os.path.join(ASSETS, "Sprites", "Demolition men object set.png")
TOWN = os.path.join(ASSETS, "Content", "Town", "Sprites")

INK = RAMPS["asphalt"][0] + (255,)
GOLD_LIGHT, GOLD = (255, 228, 120, 255), (255, 200, 60, 255)  # around UiFactory.Gold
SEA, BRICK, CONCRETE, WARM = RAMPS["sea"], RAMPS["brick"], RAMPS["concrete"], RAMPS["warm"]
TOWN_SKY = (135, 191, 235, 255)  # themes/town.json sky


# ---- sprites ---------------------------------------------------------------------------

def slices(png):
    """Every sprite of a sliced sheet by name, cropped by the rects in its .meta."""
    with open(png + ".meta", encoding="utf-8") as f:
        meta = f.read().replace("\r", "")
    sheet = Image.open(png).convert("RGBA")
    out = {}
    pattern = r"name: ([^\n]+)\n\s+rect:\n\s+serializedVersion: 2\n\s+x: ([\d.]+)\n\s+y: ([\d.]+)\n\s+width: ([\d.]+)\n\s+height: ([\d.]+)"
    for m in re.finditer(pattern, meta):
        x, y, w, h = (int(float(v)) for v in m.groups()[1:])
        out[m.group(1)] = sheet.crop((x, sheet.height - y - h, x + w, sheet.height - y))  # Unity rects start bottom-left
    return out


def trim(im):
    return im.crop(im.getbbox())


def hard(im):
    """Alpha to 0 or 255, as pixel art should be."""
    r, g, b, a = im.split()
    return Image.merge("RGBA", (r, g, b, a.point(lambda v: 255 if v >= 128 else 0)))


def outlined(im, colour=INK):
    """One pixel of outline around the silhouette (4-neighbour), so a sprite reads on any background."""
    im = hard(im)
    padded = Image.new("RGBA", (im.width + 2, im.height + 2))
    padded.alpha_composite(im, (1, 1))
    a = padded.split()[3]
    grown = a
    for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        grown = ImageChops.lighter(grown, ImageChops.offset(a, dx, dy))
    out = Image.new("RGBA", padded.size, colour)
    out.putalpha(grown)
    out.alpha_composite(padded)
    return out


def up(im, k):
    return im.resize((im.width * k, im.height * k), Image.NEAREST)


def brick(w=7, h=4):
    """A loose brick in the object set's colours: outline, face, lit top, shaded bottom."""
    im = Image.new("RGBA", (w, h), BRICK[2] + (255,))
    d = ImageDraw.Draw(im)
    d.line([1, 1, w - 2, 1], fill=BRICK[3] + (255,))
    d.line([1, h - 2, w - 2, h - 2], fill=BRICK[1] + (255,))
    d.rectangle([0, 0, w - 1, h - 1], outline=BRICK[0] + (255,))
    return im


def puff(r):
    """A dust cloud: three overlapping blobs, lit top-left, outlined."""
    size = r * 4
    im = Image.new("RGBA", (size, size))
    d = ImageDraw.Draw(im)
    blobs = [(r * 1.2, r * 2.2, r), (r * 2.1, r * 1.5, r * 1.1), (r * 2.9, r * 2.3, r * 0.9)]
    for cx, cy, br in blobs:
        d.ellipse([cx - br, cy - br, cx + br, cy + br], fill=CONCRETE[2] + (255,))
    for cx, cy, br in blobs:
        d.ellipse([cx - br + 1, cy - br, cx + br - 2, cy + br - 3], fill=CONCRETE[3] + (255,))
    return outlined(trim(hard(im)), CONCRETE[1] + (255,))


def spark(r):
    """A hit flash: a four-point star, white core, warm rim."""
    size = r * 2 + 1
    im = Image.new("RGBA", (size, size))
    d = ImageDraw.Draw(im)
    c = r
    for rim, colour in [(r, WARM[2]), (r * 0.65, WARM[3]), (r * 0.35, (255, 255, 255))]:
        t = max(1, rim * 0.28)
        d.polygon([(c, c - rim), (c + t, c - t), (c + rim, c), (c + t, c + t), (c, c + rim), (c - t, c + t), (c - rim, c), (c - t, c - t)], fill=colour + (255,))
    return hard(im)


def rotated(im, degrees):
    """Pixel-art rotation: nearest neighbour at 1x, so it stays on the grid."""
    return hard(im.rotate(degrees, Image.NEAREST, expand=True))


def place(canvas, im, x, y, k=1):
    """Paste a grid-scale sprite scaled by k at canvas pixel (x, y)."""
    canvas.alpha_composite(up(im, k), (int(x), int(y)))


# ---- icon ------------------------------------------------------------------------------

def burst(g, rays=14):
    """Teal sunburst like the menu sky: light core, rays, darker towards the edge."""
    im = Image.new("RGBA", (g, g))
    px = im.load()
    c = (g - 1) / 2
    for y in range(g):
        for x in range(g):
            dx, dy = x - c, y - c
            r = math.hypot(dx, dy) / (g / 2)
            ray = int((math.atan2(dy, dx) + math.pi) / (2 * math.pi) * rays + 0.5) % 2
            if r < 0.34:
                colour = SEA[3]
            elif r < 0.8:
                colour = SEA[2] if ray else SEA[1]
            else:
                colour = SEA[1] if ray else SEA[0]
            px[x, y] = colour + (255,)
    return im


def hero():
    """Player 1 flexing, outlined: the icon's subject."""
    return outlined(trim(slices(PLAYER_1)["Demolition_man_8"]))


def debris(g):
    """Bricks flying out from behind the hero, placed relative to the grid centre."""
    im = Image.new("RGBA", (g, g))
    c = g / 2
    for dx, dy, w, h, deg in [(-27, -17, 7, 4, 20), (22, -21, 7, 4, -15), (-30, 6, 5, 4, -30),
                               (27, 4, 7, 4, 35), (-18, -28, 4, 3, 0), (13, -30, 5, 3, 10)]:
        b = rotated(brick(w, h), deg)
        im.alpha_composite(b, (int(c + dx - b.width / 2), int(c + dy - b.height / 2)))
    return im


def icon_layers(g):
    """Background (burst + debris) and foreground (hero) on a g x g grid."""
    bg = burst(g)
    bg.alpha_composite(debris(g))
    fg = Image.new("RGBA", (g, g))
    h = hero()
    fg.alpha_composite(h, ((g - h.width) // 2, (g - h.height) // 2 + 2))
    return bg, fg


def icon(g, k):
    bg, fg = icon_layers(g)
    bg.alpha_composite(fg)
    return up(bg, k)


# ---- title -----------------------------------------------------------------------------

def hazard_tape(w, h):
    """The UI hazard stripe (ui.py), at any size: stripes 45 degrees, ink edges."""
    im = Image.new("RGBA", (w, h))
    px = im.load()
    stripe = h  # one stripe as wide as the tape is tall
    yellow, black = RAMPS["hazard"][3] + (255,), RAMPS["asphalt"][1] + (255,)
    for y in range(h):
        for x in range(w):
            px[x, y] = yellow if (x + y) % (2 * stripe) < stripe else black
    d = ImageDraw.Draw(im)
    edge = max(2, h // 8)
    d.rectangle([0, 0, w - 1, edge - 1], fill=INK)
    d.rectangle([0, h - edge, w - 1, h - 1], fill=INK)
    return im


def wordmark(lines, size, tape=True):
    """The title: gold two-tone fill, ink outline, drop shadow, hazard tape under it."""
    font = ImageFont.truetype(FONT, size)
    stroke = max(2, size // 10)
    shadow = (size // 14, size // 11)
    gap = size // 8
    boxes = [font.getbbox(line, stroke_width=stroke) for line in lines]
    widths = [b[2] - b[0] for b in boxes]
    heights = [b[3] - b[1] for b in boxes]
    w = max(widths)
    tape_h = size // 4 if tape else 0
    h = sum(heights) + gap * (len(lines) - 1) + (tape_h + gap if tape else 0)
    pad = stroke + max(shadow)
    width, height = w + 2 * pad, h + 2 * pad
    fill = Image.new("L", (width, height))
    edge = Image.new("L", (width, height))
    split = Image.new("L", (width, height))  # where the darker lower half of each line starts
    df, de, ds = ImageDraw.Draw(fill), ImageDraw.Draw(edge), ImageDraw.Draw(split)
    y = pad
    for line, box, lw, lh in zip(lines, boxes, widths, heights):
        x = pad + (w - lw) // 2 - box[0]
        df.text((x, y - box[1]), line, font=font, fill=255)
        de.text((x, y - box[1]), line, font=font, fill=255, stroke_width=stroke, stroke_fill=255)
        ds.rectangle([0, y + lh * 0.52, width, y + lh], fill=255)
        y += lh + gap
    out = Image.new("RGBA", (width, height))
    if tape:
        band = hazard_tape(int(w * 0.92), tape_h)
        out.alpha_composite(band, ((width - band.width) // 2, y))
    shade = Image.new("RGBA", (width, height), (0, 0, 0, 150))
    shade.putalpha(ImageChops.multiply(ImageChops.offset(edge, *shadow), Image.new("L", edge.size, 150)))
    out.alpha_composite(shade)
    ink = Image.new("RGBA", (width, height), INK)
    ink.putalpha(edge)
    out.alpha_composite(ink)
    top = Image.new("RGBA", (width, height), GOLD_LIGHT)
    top.putalpha(ImageChops.subtract(fill, split))
    low = Image.new("RGBA", (width, height), GOLD)
    low.putalpha(ImageChops.multiply(fill, split))
    out.alpha_composite(top)
    out.alpha_composite(low)
    return out.crop(out.getbbox())


# ---- feature graphic -------------------------------------------------------------------

def street(w, h):
    """Curb and asphalt under the scene, at grid scale."""
    im = Image.new("RGBA", (w, h), RAMPS["asphalt"][2] + (255,))
    d = ImageDraw.Draw(im)
    d.line([0, 0, w, 0], fill=CONCRETE[3] + (255,))
    d.line([0, 1, w, 1], fill=CONCRETE[2] + (255,))
    d.line([0, 2, w, 2], fill=CONCRETE[0] + (255,))
    for x in range(3, w, 11):  # grit
        d.point([(x, 5 + x % 4)], fill=RAMPS["asphalt"][3] + (255,))
        d.point([(x + 5, 8 - x % 3)], fill=RAMPS["asphalt"][1] + (255,))
    return im


def feature():
    """1024x500: the crew knocking down a Town block, the title top-left."""
    W, H, K = 1024, 500, 4
    ground = H - 12 * K
    canvas = Image.new("RGBA", (W, H), TOWN_SKY)
    for layer in ["backdrop_far", "backdrop_mid", "backdrop_near"]:
        bd = Image.open(os.path.join(TOWN, layer + ".png")).convert("RGBA")
        place(canvas, bd, 0, ground - bd.height * 2 + 4, 2)
    place(canvas, street(W // K, (H - ground) // K), 0, ground, K)

    obj = slices(OBJECTS)
    wall, truss, slab = (obj[f"Demolition men object set_{i}"] for i in (9, 18, 26))
    x0 = 690  # the building's left face, where the punch lands

    def cell(col, row, im=wall, dx=0, dy=0, deg=0):
        sprite = rotated(im, deg) if deg else im
        place(canvas, sprite, x0 + col * 32 * K + dx - (sprite.width - im.width) * K // 2,
              ground - row * 32 * K - im.height * K + dy - (sprite.height - im.height) * K // 2, K)

    ImageDraw.Draw(canvas).rectangle([x0 + 32 * K, ground - 3 * 32 * K, W, ground], fill=BRICK[0] + (255,))  # mortar between blocks
    for col in (1, 2):
        for row in range(3):
            cell(col, row)
    cell(1, 3, slab)
    cell(2, 3, slab)
    cell(0, 0, truss)
    cell(0, 2, dx=-14 * K, dy=9 * K, deg=-12)  # lost its support: on its way down
    cell(0, 1, dx=4 * K, dy=-2 * K, deg=7)     # the block being punched

    dust = puff(9)
    for x, rise in [(600, 0), (770, 6), (890, 2)]:
        place(canvas, dust, x, ground - (dust.height - 3 + rise) * K, K)

    p1 = outlined(trim(slices(PLAYER_1)["Demolition_man_12"]))
    p2 = outlined(trim(slices(PLAYER_2)["Demolition_man_Stop_V2_17"]))
    p1x, p1y = x0 - p1.width * K + 2 * K, ground - p1.height * K + K
    place(canvas, p2, 96, ground - p2.height * K + K, K)
    place(canvas, p1, p1x, p1y, K)
    fist = (x0, p1y + 19 * K)
    for dx, dy, deg, w in [(-20, -70, 25, 7), (10, -120, -20, 7), (-60, -110, 50, 5), (-80, -40, -40, 7),
                           (-30, -170, 10, 5), (-110, -150, 65, 7), (-140, -80, -15, 5), (20, -60, 30, 5),
                           (-50, 40, 15, 7), (-100, 10, -60, 5), (-170, -130, 35, 4)]:
        b = rotated(brick(w, 4), deg)
        place(canvas, b, fist[0] + dx - b.width * K // 2, fist[1] + dy - b.height * K // 2, K)
    s = spark(9)
    place(canvas, s, fist[0] - s.width * K // 2, fist[1] - s.height * K // 2, K)

    logo = wordmark(["DEMOLITION MEN"], 82)
    canvas.alpha_composite(logo, (34, 18))
    return canvas.convert("RGB")


# ---- output ----------------------------------------------------------------------------

def masked(im, shape):
    """What a launcher shows: the middle 2/3 of an adaptive layer, cut to a shape."""
    s = im.width // 6
    im = im.crop((s, s, im.width - s, im.height - s))
    mask = Image.new("L", im.size)
    d = ImageDraw.Draw(mask)
    if shape == "circle":
        d.ellipse([0, 0, im.width - 1, im.height - 1], fill=255)
    else:
        d.rounded_rectangle([0, 0, im.width - 1, im.height - 1], radius=im.width // 4, fill=255)
    out = Image.new("RGBA", im.size)
    out.paste(im, (0, 0), mask)
    return out


def contact(play_icon, adaptive, feature_graphic, logo, stacked):
    sheet = Image.new("RGBA", (1600, 1500), (36, 36, 44, 255))
    d = ImageDraw.Draw(sheet)
    d.text((20, 10), "Demolition Men brand art (tools/artpipe/brand.py)", fill=(230, 230, 230))
    sheet.alpha_composite(feature_graphic.convert("RGBA"), (20, 40))
    d.text((20, 544), "feature-graphic.png 1024x500", fill=(200, 200, 200))
    sheet.alpha_composite(play_icon, (1060, 40))
    d.text((1060, 556), "icon-512.png", fill=(200, 200, 200))
    x = 20
    for shape in ["circle", "squircle"]:
        m = masked(adaptive, shape)
        for size in [192, 96, 48]:
            sheet.alpha_composite(m.resize((size, size), Image.LANCZOS), (x, 600))
            x += size + 16
    d.text((20, 800), "adaptive icon: circle and squircle masks at 192 / 96 / 48 px", fill=(200, 200, 200))
    light = Image.new("RGBA", (1560, 300), (230, 232, 236, 255))
    light.alpha_composite(logo.resize((1520, logo.height * 1520 // logo.width), Image.LANCZOS), (20, 20))
    sheet.alpha_composite(light, (20, 830))
    sheet.alpha_composite(stacked.resize((stacked.width * 330 // stacked.height, 330), Image.LANCZOS), (20, 1150))
    d.text((20, 1140), "logo.png / logo-stacked.png (transparent)", fill=(200, 200, 200))
    return sheet


def save_unity(im, name):
    path = os.path.join(UNITY_OUT, name + ".png")
    im.save(path)
    with open(META_TEMPLATE, encoding="utf-8") as f:
        write_meta(path + ".meta", f.read())
    print(f"  Assets/Brand/{name}.png {im.width}x{im.height}")


def main():
    os.makedirs(OUT, exist_ok=True)
    ensure_folder(UNITY_OUT)

    play_icon = icon(64, 8)
    play_icon.save(os.path.join(OUT, "icon-512.png"))
    save_unity(play_icon, "Icon")
    bg, fg = icon_layers(108)
    save_unity(up(bg, 4), "IconBackground")
    save_unity(up(fg, 4), "IconForeground")
    adaptive = up(bg, 4)
    adaptive.alpha_composite(up(fg, 4))

    graphic = feature()
    graphic.save(os.path.join(OUT, "feature-graphic.png"))
    logo = wordmark(["DEMOLITION MEN"], 220)
    logo.save(os.path.join(OUT, "logo.png"))
    stacked = wordmark(["DEMOLITION", "MEN"], 220)
    stacked.save(os.path.join(OUT, "logo-stacked.png"))
    for name in ["icon-512.png", "feature-graphic.png", "logo.png", "logo-stacked.png"]:
        with Image.open(os.path.join(OUT, name)) as im:
            print(f"  brand/{name} {im.width}x{im.height} {im.mode}")

    contact(play_icon, adaptive, graphic, logo, stacked).save(PREVIEW)
    print(f"  {os.path.relpath(PREVIEW, ROOT)}")


if __name__ == "__main__":
    main()

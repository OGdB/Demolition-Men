"""Generate the UI sprites every code-built screen shares (UiFactory).

    python tools/artpipe/ui.py

Writes Demolition Men/Assets/Resources/UI/*.png. They are pixel art, like the game: each
sprite pixel shows as 4 UI units (25 pixels per unit against the canvas's 100). Plate and
Star are drawn light grey so the Image colour tints them (steel buttons, gold when
selected, gold or dark stars). An existing .meta is kept, so GUIDs survive regeneration.
"""
import os
import sys

from PIL import Image, ImageDraw

sys.dont_write_bytecode = True  # no __pycache__ next to the tools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_theme import META_TEMPLATE, ensure_folder, write_meta  # noqa: E402
from palette import RAMPS  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Demolition Men", "Assets", "Resources", "UI")
PPU = 25
OUTLINE = RAMPS["asphalt"][0] + (255,)
WHITE, LIGHT, FACE, SHADE, DEEP = (255, 255, 255, 255), (238, 238, 238, 255), (214, 214, 214, 255), (170, 170, 170, 255), (128, 128, 128, 255)


def plate():
    """24x24 riveted steel plate, 7 px borders for 9-slicing: outline, bevel, a rivet per corner."""
    size, border = 24, 7
    im = Image.new("RGBA", (size, size), FACE)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, size - 1, size - 1], outline=OUTLINE)
    d.line([1, 1, size - 2, 1], fill=WHITE)               # light from the top-left
    d.line([1, 1, 1, size - 2], fill=LIGHT)
    d.line([1, size - 2, size - 2, size - 2], fill=DEEP)  # shadow bottom-right
    d.line([size - 2, 2, size - 2, size - 2], fill=SHADE)
    d.line([2, size - 3, size - 3, size - 3], fill=SHADE)
    for x, y in [(3, 3), (size - 5, 3), (3, size - 5), (size - 5, size - 5)]:
        d.point([(x, y)], fill=WHITE)
        d.point([(x + 1, y)], fill=LIGHT)
        d.point([(x, y + 1)], fill=LIGHT)
        d.point([(x + 1, y + 1)], fill=DEEP)
    return im, (border, border, border, border)


def hazard():
    """16x8 yellow/black diagonal stripes, seamless left/right, dark edge top and bottom."""
    w, h = 16, 8
    im = Image.new("RGBA", (w, h))
    yellow, black = RAMPS["hazard"][3] + (255,), RAMPS["asphalt"][1] + (255,)
    for y in range(h):
        for x in range(w):
            im.putpixel((x, y), yellow if (x + y) % 8 < 4 else black)
    d = ImageDraw.Draw(im)
    d.line([0, 0, w - 1, 0], fill=OUTLINE)
    d.line([0, h - 1, w - 1, h - 1], fill=OUTLINE)
    return im, (0, 0, 0, 0)


def star():
    """32x32 five-point star: outline, light body, a highlight on the upper-left arms."""
    import math
    size = 32
    big = 8  # draw large, then take every 8th pixel: crisp pixel edges, no blur
    canvas = Image.new("RGBA", (size * big, size * big), (0, 0, 0, 0))
    d = ImageDraw.Draw(canvas)
    c = size * big / 2
    def points(r_out, r_in, cy):
        pts = []
        for i in range(10):
            r = r_out if i % 2 == 0 else r_in
            a = -math.pi / 2 + i * math.pi / 5
            pts.append((c + r * math.cos(a), cy + r * math.sin(a)))
        return pts
    cy = c + 1.2 * big
    d.polygon(points(15.5 * big, 6.6 * big, cy), fill=OUTLINE)
    d.polygon(points(13 * big, 5.2 * big, cy), fill=SHADE)
    d.polygon(points(11.5 * big, 4.4 * big, cy - 0.8 * big), fill=FACE)
    d.polygon(points(6 * big, 2.4 * big, cy - 2.2 * big), fill=LIGHT)
    im = canvas.resize((size, size), Image.NEAREST)
    return im, (0, 0, 0, 0)


def fist():
    """22x22 clenched fist, knuckles up, in the players' skin tones: the HUD's bare-handed weapon slot."""
    out = OUTLINE
    light, mid, dark, hi = (238, 195, 154, 255), (217, 160, 102, 255), (170, 104, 60, 255), (252, 228, 200, 255)
    im = Image.new("RGBA", (22, 22), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    def part(x0, y0, x1, y1, fill, rounded=True):
        d.rectangle([x0, y0, x1, y1], fill=out)
        d.rectangle([x0 + 1, y0 + 1, x1 - 1, y1 - 1], fill=fill)
        if rounded:  # the top corners cut in by a pixel
            for x, step in ((x0, 1), (x1, -1)):
                im.putpixel((x, y0), (0, 0, 0, 0))
                im.putpixel((x + step, y0 + 1), out)

    part(4, 9, 19, 20, mid, rounded=False)  # back of the hand
    d.rectangle([5, 10, 18, 12], fill=light)
    d.rectangle([5, 17, 18, 19], fill=dark)
    for i in range(4):  # fingers
        x0 = 3 + i * 4
        part(x0, 2, x0 + 4, 11, light)
        d.rectangle([x0 + 1, 8, x0 + 3, 10], fill=mid)
        im.putpixel((x0 + 1, 3), hi)
    part(1, 10, 15, 14, light)  # thumb across the front
    d.rectangle([2, 13, 14, 13], fill=mid)
    im.putpixel((2, 11), hi)
    im.putpixel((15, 10), out)  # where the thumb meets the little finger
    im.putpixel((14, 11), out)
    return im, (0, 0, 0, 0)


SPRITES = {"Plate": plate, "Hazard": hazard, "Star": star, "Fist": fist}
REPEAT = {"Hazard"}


def main():
    ensure_folder(OUT)
    with open(META_TEMPLATE, encoding="utf-8") as f:
        template = f.read()
    for name, make in SPRITES.items():
        im, (left, bottom, right, top) = make()
        path = os.path.join(OUT, name + ".png")
        im.save(path)
        meta = template.replace("spritePixelsToUnits: 32", f"spritePixelsToUnits: {PPU}")
        meta = meta.replace("spriteBorder: {x: 0, y: 0, z: 0, w: 0}", f"spriteBorder: {{x: {left}, y: {bottom}, z: {right}, w: {top}}}")
        if name in REPEAT:  # tiled Images repeat the texture both ways
            meta = meta.replace("wrapU: 1", "wrapU: 0").replace("wrapV: 1", "wrapV: 0")
        write_meta(path + ".meta", meta)
        print(f"  {name}.png {im.size[0]}x{im.size[1]}")


if __name__ == "__main__":
    main()

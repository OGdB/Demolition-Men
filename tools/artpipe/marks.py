"""Paint the marks the game sprays on blocks: the weak point's X (docs/hit-plan.md F1).

    python tools/artpipe/marks.py

Writes Demolition Men/Assets/Resources/Marks/WeakPoint.png (32x32, one cell, imported like the
block sprites it sits on: 32 PPU, point filter, centred pivot) and the review sheet
content-work/marks.png: the X on real blocks of every theme, at 4x and at about the size a
phone shows it. An existing .meta is kept, so the GUID survives regeneration.

The X is hazard yellow inside the outline colour, so it reads on brick, wood, steel and the
dark Neon walls alike; wavering strokes and a few drips make it paint.
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw

sys.dont_write_bytecode = True  # no __pycache__ next to the tools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import level_preview  # noqa: E402
from build_theme import META_TEMPLATE, ensure_folder, write_meta  # noqa: E402
from palette import RAMPS  # noqa: E402
from qa import ASSETS, ROOT, font, scaled  # noqa: E402

OUT = os.path.join(ASSETS, "Resources", "Marks")
SHEET = os.path.join(ROOT, "content-work", "marks.png")
SIZE = 32
SEED = 1
PAINT, SHADE, OUTLINE = RAMPS["hazard"][3] + (255,), RAMPS["hazard"][2] + (255,), RAMPS["asphalt"][0] + (255,)
# Two strokes, drawn by hand: not quite the same length, not quite through the middle.
STROKES = [((7.5, 7.0), (24.5, 25.0)), ((25.0, 7.5), (7.0, 24.0))]
DRIPS = [(8, 24, 5), (23, 25, 3), (21, 12, 4)]  # x, top, length: paint running down from a stroke


def distance(px, py, a, b):
    """From a pixel centre to the segment a-b."""
    (ax, ay), (bx, by) = a, b
    t = ((px - ax) * (bx - ax) + (py - ay) * (by - ay)) / ((bx - ax) ** 2 + (by - ay) ** 2)
    t = max(0.0, min(1.0, t))
    return math.hypot(px - (ax + t * (bx - ax)), py - (ay + t * (by - ay)))


def weak_point():
    rng = random.Random(SEED)
    paint = set()
    for y in range(SIZE):
        for x in range(SIZE):
            # A can's stroke: round ends, an edge that wavers by a pixel here and there.
            if any(distance(x + 0.5, y + 0.5, a, b) < 1.75 + rng.uniform(-0.3, 0.3) for a, b in STROKES):
                paint.add((x, y))
    for x, top, length in DRIPS:
        paint.update((x, top + i) for i in range(length))
    assert all(0 < x < SIZE - 1 and 0 < y < SIZE - 1 for x, y in paint), "the X needs a pixel of room for its outline"

    im = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    for x, y in paint:
        for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            if (x + dx, y + dy) not in paint:
                im.putpixel((x + dx, y + dy), OUTLINE)
    for x, y in paint:  # light from the top-left, as on every sprite: the paint's lower edge is a step darker
        im.putpixel((x, y), SHADE if (x, y + 1) not in paint else PAINT)
    return im


def review(mark):
    """The X on the blocks it can land on, per theme: 4x to judge the pixels, 2x about as a phone shows them."""
    kinds = ["Support", "Door", "Brick", "Wood", "Concrete", "MetalTruss", "Beam"]
    cell, pad, label = SIZE * 2, 12, 18

    def strip(theme, k):
        row = Image.new("RGBA", (len(kinds) * (cell + pad), cell + pad), theme["sky"] + (255,))
        back = theme["interior"]
        for i, kind in enumerate(kinds):
            entry = theme["tiles"].get(kind)
            if not entry or not entry["sprites"] or not entry["sprites"][0]:
                continue
            spr = level_preview.tinted(entry["sprites"][0][0], entry["tint"])
            left, top = i * (cell + pad) + pad // 2 + (cell - spr.width) // 2, pad // 2 + cell - spr.height
            if back:  # the back wall shows through supports and trusses, as in a building
                for y in range(top, top + spr.height, SIZE):
                    for x in range(left, left + spr.width, SIZE):
                        row.alpha_composite(back[0], (x, y))
            row.alpha_composite(spr, (left, top))
            row.alpha_composite(mark, (left + (spr.width - SIZE) // 2, top + (spr.height - SIZE) // 2))
        return scaled(row, k)

    rows = []
    for name in ("Town", "Port", "Neon"):
        theme = level_preview.load_theme(name)
        rows += [(name + ": " + ", ".join(kinds), strip(theme, 4)), ("", strip(theme, 2))]
    sheet = Image.new("RGBA", (rows[0][1].width, sum(img.height + (label if text else 0) for text, img in rows)), (30, 30, 36, 255))
    d = ImageDraw.Draw(sheet)
    y = 0
    for text, img in rows:
        if text:
            d.text((6, y + 2), text, fill="white", font=font(12))
            y += label
        sheet.paste(img, (0, y))
        y += img.height
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    sheet.convert("RGB").save(SHEET)
    print(SHEET)


def main():
    ensure_folder(OUT)
    mark = weak_point()
    path = os.path.join(OUT, "WeakPoint.png")
    mark.save(path)
    with open(META_TEMPLATE, encoding="utf-8") as f:
        write_meta(path + ".meta", f.read())
    print(f"  WeakPoint.png {mark.size[0]}x{mark.size[1]}")
    review(mark)


if __name__ == "__main__":
    main()

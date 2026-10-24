"""Draw generated levels the way the game lays them out, without Unity.

    tools/unity-check/check.sh levels [level-id...]     # layouts -> content-work/levels/*.json
    python tools/artpipe/level_preview.py [level-id...] # -> content-work/levels/<id>.png

Reads the theme's real assets (ground tiles, building tileset, backdrop layers, sky)
through their .meta GUIDs, sliced sheets included, and draws every building block where
BuildingBuilder puts it, and the crew's caravan past the finish. Pickups, spawn, finish and
segment names are marked.
The level is cut into strips stacked top to bottom so a long level fits one image.
Parallax and camera framing are not simulated: backdrops sit where they spawn.
"""
import glob
import json
import os
import re
import sys

from PIL import Image, ImageDraw

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from qa import ASSETS, ROOT, font  # noqa: E402

OUT = os.path.join(ROOT, "content-work", "levels")
PPU = 16           # preview pixels per world unit (the art is 32)
STRIP = 70         # world units per strip
GROUND_DEPTH = 12  # LevelLayout.GroundDepth
CARAVAN_WIDTH = 6  # LevelLayout.CaravanWidth
KINDS = ["Brick", "BrickTall", "Concrete", "Wood", "Glass", "Door", "Plank", "MetalTruss", "Beam", "BrickCap",
         "Support", "Chimney", "Crate", "Car", "Barrel", "Container"]
DECOR = ["Lamp", "Fence", "Bush", "Bin", "Sign"]
PICKUPS = {"Dynamite": ((220, 60, 40), "D"), "Axe": ((90, 160, 230), "A"), "WreckingBall": ((120, 120, 140), "W"),
           "Resurrect": ((90, 210, 110), "R")}

_guids = None
_images = {}


def guid_index():
    global _guids
    if _guids is None:
        _guids = {}
        for meta in glob.glob(os.path.join(ASSETS, "**", "*.meta"), recursive=True):
            with open(meta, encoding="utf-8", errors="ignore") as f:
                m = re.search(r"^guid: ([0-9a-f]{32})", f.read(400), re.M)
            if m:
                _guids[m.group(1)] = meta[:-5]
    return _guids


def ref(text):
    """{fileID, guid} pairs in a YAML snippet."""
    return [(int(a), b) for a, b in re.findall(r"fileID: (-?\d+), guid: ([0-9a-f]{32})", text)]


def sprite(file_id, guid):
    """(RGBA image, pixels per unit, pivot 0..1) of a Unity sprite reference, or None."""
    key = (file_id, guid)
    if key in _images:
        return _images[key]
    path = guid_index().get(guid)
    result = None
    if path and os.path.isfile(path) and path.lower().endswith(".png"):
        meta = open(path + ".meta", encoding="utf-8").read()
        ppu = float(re.search(r"spritePixelsToUnits: ([\d.]+)", meta).group(1))
        img = Image.open(path).convert("RGBA")
        if file_id == 21300000:  # a single sprite: the whole texture
            pv = re.search(r"spritePivot: \{x: ([\d.]+), y: ([\d.]+)\}", meta)
            align = int(re.search(r"alignment: (\d+)", meta).group(1))
            result = (img, ppu, pivot(align, pv))
        else:
            for block in meta.split("- serializedVersion: 2\n")[1:]:
                if f"internalID: {file_id}\n" not in block:
                    continue
                r = {k: float(re.search(rf"\b{k}: (-?[\d.]+)", block).group(1)) for k in ("x", "y", "width", "height")}
                top = img.height - r["y"] - r["height"]
                crop = img.crop((int(r["x"]), int(top), int(r["x"] + r["width"]), int(top + r["height"])))
                pv = re.search(r"pivot: \{x: ([\d.]+), y: ([\d.]+)\}", block)
                result = (crop, ppu, pivot(int(re.search(r"alignment: (\d+)", block).group(1)), pv))
                break
    _images[key] = result
    return result


def pivot(alignment, custom):
    table = {0: (0.5, 0.5), 1: (0, 1), 2: (0.5, 1), 3: (1, 1), 4: (0, 0.5), 5: (1, 0.5), 6: (0, 0), 7: (0.5, 0), 8: (1, 0)}
    if alignment == 9 and custom:
        return float(custom.group(1)), float(custom.group(2))
    return table.get(alignment, (0.5, 0.5))


def caravan_sprite():
    """GameConfig's caravan, parked past every level's finish; None if it has none."""
    m = re.search(r"\n  caravan: (\{[^}]*\})", open(os.path.join(ASSETS, "Resources", "GameConfig.asset"), encoding="utf-8").read())
    refs = ref(m.group(1)) if m else []
    return sprite(*refs[0]) if refs else None


def tile_sprite(file_id, guid):
    """The sprite a Tile asset shows."""
    path = guid_index().get(guid)
    if not path:
        return None
    refs = ref(re.search(r"m_Sprite: \{[^}]*\}", open(path, encoding="utf-8").read()).group(0))
    return sprite(*refs[0]) if refs else None


def color(text, name):
    m = re.search(name + r": \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)", text)
    return tuple(float(v) for v in m.groups()) if m else (1.0, 1.0, 1.0)


def load_theme(name):
    asset = open(os.path.join(ASSETS, "Content", name, f"{name}Theme.asset"), encoding="utf-8").read()
    theme = {
        "sky": tuple(int(c * 255) for c in color(asset, "sky")),
        "top": tile_sprite(*ref(re.search(r"groundTop: .*", asset).group(0))[0]),
        "fill": tile_sprite(*ref(re.search(r"groundFill: .*", asset).group(0))[0]),
        "backdrop": [],
        "tiles": {},
    }
    theme["decor"] = {}
    m = re.search(r"\n  decor:\n((?:  - kind:.*\n(?:    .*\n)*)*)", asset)
    for entry in re.split(r"  - kind: ", m.group(1))[1:] if m else []:
        theme["decor"][DECOR[int(entry.split("\n", 1)[0])]] = [sprite(*r) for r in ref(entry)]
    for layer in re.finditer(r"- sprite: (\{[^}]*\})\s+parallax: [\d.]+\s+bottom: (-?[\d.]+)(?:\s+single: (\d)\s+offset: (-?[\d.]+))?", asset):
        theme["backdrop"].append((sprite(*ref(layer.group(1))[0]), float(layer.group(2)), layer.group(3) == "1",
                                  float(layer.group(4) or 0)))
    tileset = open(guid_index()[ref(re.search(r"tileset: .*", asset).group(0))[0][1]], encoding="utf-8").read()
    for entry in re.split(r"\n  - kind: ", tileset)[1:]:
        kind = int(entry.split("\n", 1)[0])
        theme["tiles"][KINDS[kind]] = {
            "sprites": [sprite(*r) for r in ref(yaml_list(entry, "sprites", 4))],
            "left": [sprite(*r) for r in ref(yaml_list(entry, "left", 4))],
            "right": [sprite(*r) for r in ref(yaml_list(entry, "right", 4))],
            "tint": color(entry, "tint"),
            "scale": tuple(float(v) for v in re.search(r"scale: \{x: (-?[\d.]+), y: (-?[\d.]+)", entry).groups()),
            "offset": tuple(float(v) for v in re.search(r"offset: \{x: (-?[\d.]+), y: (-?[\d.]+)", entry).groups()),
            "order": int(re.search(r"sortingOrder: (-?\d+)", entry).group(1)),
            "rotation": float(re.search(r"rotation: (-?[\d.]+)", entry).group(1)),
        }
    for key, field in (("interior", "interior"), ("interiorTop", "interiorTop")):
        m = re.search(field + r": (\{[^}]*\})", tileset)
        theme[key] = sprite(*ref(m.group(1))[0]) if m and ref(m.group(1)) else None
    theme["interiorTop"] = theme["interiorTop"] or theme["interior"]
    for key in ("interiorVariants", "interiorTopVariants", "interiorDetails"):
        theme[key] = [sprite(*r) for r in ref(yaml_list(tileset, key, 2))]
    m = re.search(r"interiorDetailChance: ([\d.]+)", tileset)
    theme["interiorDetailChance"] = float(m.group(1)) if m else 0.0
    return theme


def yaml_list(text, name, indent):
    """The lines of the list `name:` at the given indent, up to the next key ('' if empty)."""
    m = re.search(rf"^{' ' * indent}{name}:(?: \[\]\n|\n((?:{' ' * indent}- .*\n)*))", text, re.M)
    return m.group(1) or "" if m else ""


def cell_hash(x, y, seed):
    """BuildingTileset.CellHash: which open room cells get a detail, and which one."""
    u = lambda v: v & 0xFFFFFFFF
    h = u(u(seed * 73856093) ^ u(x * 19349663) ^ u(y * 83492791))
    h ^= h >> 13
    h = u(h * 0x5bd1e995)
    h ^= h >> 15
    return h


def interior_sprite(theme, storey, under, seed):
    """BuildingTileset.InteriorFor: the storey's wallpaper, the base look and the variants in turn."""
    count = 1 + len(theme["interiorVariants"])
    i = ((seed & 0xFFFFFFFF) + storey) % count
    if i == 0:
        return theme["interiorTop"] if under else theme["interior"]
    tops = theme["interiorTopVariants"]
    return tops[i - 1] if under and i - 1 < len(tops) and tops[i - 1] else theme["interiorVariants"][i - 1]


def tinted(img, tint):
    if tint == (1.0, 1.0, 1.0):
        return img
    r, g, b, a = img.split()
    r, g, b = (ch.point(lambda v, k=k: int(v * k)) for ch, k in zip((r, g, b), tint))
    return Image.merge("RGBA", (r, g, b, a))


class Canvas:
    def __init__(self, x0, x1, y0, y1, sky):
        self.x0, self.y1 = x0, y1
        self.img = Image.new("RGBA", (int((x1 - x0) * PPU), int((y1 - y0) * PPU)), sky + (255,))

    def px(self, x, y):
        return int(round((x - self.x0) * PPU)), int(round((self.y1 - y) * PPU))

    def draw(self, spr, cx, cy, scale=(1.0, 1.0), tint=(1.0, 1.0, 1.0), size=None, rotation=0.0):
        """Sprite with its pivot at world (cx, cy); `size` in units overrides its own.
        Rotation: multiples of 90 degrees, about a centred pivot."""
        if spr is None:
            return
        img, ppu, (pvx, pvy) = spr
        turns = int(round(rotation / 90.0)) % 4
        if turns:
            img = img.rotate(90 * turns, expand=True)
        w = size[0] if size else img.width / ppu * abs(scale[0])
        h = size[1] if size else img.height / ppu * abs(scale[1])
        out = img.resize((max(1, round(w * PPU)), max(1, round(h * PPU))), Image.NEAREST)
        out = tinted(out, tint)
        if scale[0] < 0:
            out = out.transpose(Image.FLIP_LEFT_RIGHT)
        left, top = self.px(cx - pvx * w, cy + (1 - pvy) * h)
        self.img.alpha_composite(out, (left, top)) if left >= 0 and top >= 0 else self.img.paste(out, (left, top), out)


def render(level):
    theme = load_theme(level["theme"])
    ground = level["ground"]
    width = len(ground)
    solid = [g for g in ground if g is not None]
    street = ground[level["spawnX"]]
    bottom = min(solid) - GROUND_DEPTH
    top = max([b["y"] + b["h"] for b in level["buildings"]] + solid) + 4
    y0 = min(solid) - 5
    c = Canvas(-2, width + 2, y0, top, theme["sky"])

    # Backdrops where LevelBootstrap spawns them (no parallax), repeated along the level;
    # a single layer (the sun) once, where it starts.
    for spr, b, single, offset in theme["backdrop"]:
        if spr is None:
            continue
        w = spr[0].width / spr[1]
        h = spr[0].height / spr[1]
        if single:
            c.draw(spr, level["spawnX"] + 4 + offset, street + b + h * 0.5 - (0.5 - spr[2][1]) * h)
            continue
        x = -2.0
        while x < width + 2:
            c.draw(spr, x + w * spr[2][0], street + b + h * 0.5 - (0.5 - spr[2][1]) * h)
            x += w

    # Back wall in trenches, abyss below pit lips (LevelBuilder.BuildBackWall).
    def lip(x):
        lft = x
        while lft >= 0 and ground[lft] is None:
            lft -= 1
        rgt = x
        while rgt < width and ground[rgt] is None:
            rgt += 1
        return min(ground[lft] if lft >= 0 else 10 ** 6, ground[rgt] if rgt < width else 10 ** 6)

    for x, g in enumerate(ground):
        if g is not None:
            for y in range(g, street):
                c.draw(theme["fill"], x + 0.5, y + 0.5, tint=(0.55, 0.55, 0.6), size=(1, 1))
            for y in range(bottom, g):
                c.draw(theme["top"] if y == g - 1 else theme["fill"], x + 0.5, y + 0.5, size=(1, 1))
        else:
            lp = lip(x)
            for y in range(bottom, max(lp, street)):
                c.draw(theme["fill"], x + 0.5, y + 0.5, tint=(0.06, 0.05, 0.08) if y < lp else (0.55, 0.55, 0.6), size=(1, 1))

    # Street dressing, standing on the ground behind the buildings (LevelBuilder.Spawn).
    for kind, x, y, variant in level.get("decor", []):
        sprites = theme["decor"].get(kind)
        if sprites:
            spr = sprites[variant % len(sprites)]
            c.draw(spr, x + 0.5, y + spr[2][1] * spr[0].height / spr[1])  # pivot up from the ground
    caravan = caravan_sprite()
    if caravan and "caravanX" in level:
        x = level["caravanX"]
        h = CARAVAN_WIDTH * caravan[0].height / caravan[0].width  # scaled to its columns (LevelBuilder.SpawnCaravan)
        c.draw(caravan, x + caravan[2][0] * CARAVAN_WIDTH, ground[x] + caravan[2][1] * h, size=(CARAVAN_WIDTH, h))

    # Buildings: the back wall (BuildingInterior: one look per storey, details on some room
    # cells), then each block's visual centred on its rect (BuildingBuilder +
    # BuildingTileset.Dress: corner variants on the building's edges).
    for b in level["buildings"]:
        seed = b.get("seed", 0)
        for x, y, under, storey, see_through in b.get("interior", []) if theme["interior"] else []:
            c.draw(interior_sprite(theme, storey, under, seed), b["x"] + x + 0.5, b["y"] + y + 0.5)
            details = theme["interiorDetails"]
            if details and not see_through:
                h = cell_hash(x, y, seed)
                if (h & 0xFFFF) / 65536 < theme["interiorDetailChance"]:
                    c.draw(details[(h >> 16) % len(details)], b["x"] + x + 0.5, b["y"] + y + 0.5)
        blocks = sorted(b["blocks"], key=lambda k: theme["tiles"].get(k[0], {}).get("order", 0))
        for kind, bx, by, bw, bh, variant, _, edge in blocks:
            entry = theme["tiles"].get(kind)
            if not entry or not entry["sprites"]:
                continue
            pool = entry["left"] if edge & 1 and entry["left"] else entry["right"] if edge & 2 and entry["right"] else entry["sprites"]
            spr = pool[variant % len(pool)]
            c.draw(spr, b["x"] + bx + bw / 2 + entry["offset"][0], b["y"] + by + bh / 2 + entry["offset"][1],
                   scale=entry["scale"], tint=entry["tint"], rotation=entry["rotation"])

    d = ImageDraw.Draw(c.img)
    f = font(12)
    for p in level["pickups"]:
        rgb, letter = PICKUPS.get(p["kind"], ((255, 255, 255), "?"))
        x, y = c.px(p["x"], p["y"] + 0.3)
        d.ellipse((x - 7, y - 7, x + 7, y + 7), fill=rgb, outline=(0, 0, 0))
        d.text((x - 4, y - 7), letter, fill="white", font=f)
    for x, label, rgb in ((level["spawnX"] + 0.5, "SPAWN", (255, 220, 60)), (level["finishX"], "FINISH", (255, 255, 255))):
        px, py = c.px(x, ground[int(x)] if ground[int(x)] is not None else street)
        d.line((px, py, px, py - 4 * PPU), fill=rgb, width=3)
        d.text((px + 4, py - 4 * PPU), label, fill=rgb, font=f)
    for s in level["segments"]:
        px, py = c.px(s["x0"], y0 + 0.2)
        d.line((px, py, px, py - 12), fill=(255, 255, 255), width=1)
        d.text((px + 3, py - 14), s["kind"].upper(), fill=(255, 255, 255), font=f)
    return c.img


def strips(level, img):
    """Cut a long level into strips stacked under a header."""
    width = img.width
    step = STRIP * PPU
    parts = [img.crop((x, 0, min(width, x + step), img.height)) for x in range(0, width, step)]
    header = 34
    sheet = Image.new("RGBA", (step, header + len(parts) * (img.height + 8)), (30, 30, 36, 255))
    d = ImageDraw.Draw(sheet)
    counts = {}
    for p in level["pickups"]:
        counts[p["kind"]] = counts.get(p["kind"], 0) + 1
    d.text((8, 8), f"{level['id']}  {level['name']}  ({level['theme']})   {len(level['ground'])} columns, "
                   f"{len(level['buildings'])} buildings, {level['cells']} cells, par {level['par']:.0f}s, "
                   f"scroll {level['scrollSpeed']}   pickups {counts}", fill="white", font=font(16))
    for i, part in enumerate(parts):
        sheet.paste(part, (0, header + i * (img.height + 8)))
    return sheet


def main(ids):
    os.makedirs(OUT, exist_ok=True)
    paths = sorted(glob.glob(os.path.join(OUT, "*.json")))
    if ids:
        paths = [p for p in paths if os.path.splitext(os.path.basename(p))[0] in ids]
    if not paths:
        sys.exit("no layouts: run tools/unity-check/check.sh levels first")
    for path in paths:
        level = json.load(open(path))
        out = os.path.splitext(path)[0] + ".png"
        strips(level, render(level)).convert("RGB").save(out)
        print(out)


if __name__ == "__main__":
    main(sys.argv[1:])

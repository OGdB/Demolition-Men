"""Generate every sprite of a theme, write Unity .meta files, then run QA.

    python tools/artpipe/build_theme.py <Theme>
    python tools/artpipe/build_theme.py <Theme> --props

Reads tools/artpipe/themes/<theme>.json and writes PNGs to
Demolition Men/Assets/Content/<Theme>/Sprites/. An existing .meta is kept, so GUIDs (and
every reference to the sprite) survive regeneration; a PNG whose pixels are already the
generated ones is left alone, so another machine's PNG encoder changes no file.

--props writes nothing to Assets: it draws content-work/<Theme>/props.png, the review sheet
of the whole furnishing catalog in the theme's colours (every prop and roof sign, whether
the theme's archetypes use it or not), each switched off beside switched on.
"""
import os
import re
import sys
import uuid

import numpy as np
from PIL import Image, ImageDraw

sys.dont_write_bytecode = True  # no __pycache__ next to the tools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import qa  # noqa: E402
from normalize import load_theme  # noqa: E402
from tiles import PROPS, SPRITES, generate, theme_sprites  # noqa: E402

META_TEMPLATE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sprite.meta.template")
FURNISHING = os.path.join(qa.ASSETS, "Scripts", "Demolition", "Generation", "Model", "Furnishing.cs")
CEILING = ("lamp", "tube", "discoball", "spot")
FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_meta(path, text):
    """Write a .meta only if none exists. Returns the new GUID, or None if kept."""
    if os.path.exists(path):
        return None
    guid = uuid.uuid4().hex
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text.replace("{GUID}", guid) if "{GUID}" in text else text.format(guid=guid))
    return guid


def ensure_folder(path):
    os.makedirs(path, exist_ok=True)
    guid = write_meta(path.rstrip("/\\") + ".meta", FOLDER_META)
    if guid:
        print(f"  new folder {os.path.relpath(path, qa.ASSETS)} {guid}")


def check_catalog():
    """The game places props by name and size (Furnishing.cs): tiles.PROPS must say the same."""
    with open(FURNISHING, encoding="utf-8") as f:
        entries = re.findall(r'\{ "(\w+)", (?:Floor|Wall|Ceiling)\((\d+)(?:, (\d+))?\) \}', f.read())
    game = {name: (int(w), int(h or 1)) for name, w, h in entries}
    ours = {name: (w, h) for name, (w, h, _) in PROPS.items() if not name.startswith("sign_")}
    if game != ours:
        differ = sorted(n for n in game.keys() | ours.keys() if game.get(n) != ours.get(n))
        sys.exit(f"tiles.PROPS and Furnishing.cs disagree on: {', '.join(differ)}")


def build(theme_name):
    check_catalog()
    theme = load_theme(theme_name)
    name = theme["name"]
    ensure_folder(os.path.join(qa.ASSETS, "Content", name))
    out = qa.sprite_dir(name)
    ensure_folder(out)
    with open(META_TEMPLATE, encoding="utf-8") as f:
        template = f.read()
    for sprite in theme_sprites(theme):
        png = os.path.join(out, sprite + ".png")
        art = generate(theme, sprite)
        if not (os.path.exists(png) and np.array_equal(np.array(Image.open(png).convert("RGBA")), art)):
            Image.fromarray(art, "RGBA").save(png)
        guid = write_meta(png + ".meta", template)
        print(f"  {name}/{sprite}.png" + (f" new guid {guid}" if guid else ""))
    return qa.run(name)


def props_sheet(theme_name):
    """content-work/<Theme>/props.png: the furnishing catalog at 4x on the theme's back wall
    (roof signs and blocks on its sky), each off and, where it has a lit overlay, on beside
    it; then everything switched on at 2x on each of the theme's three back walls."""
    check_catalog()
    theme = load_theme(theme_name)
    built = set(theme_sprites(theme))
    sky = qa.sky_rgb(theme) + (255,)
    f, fb = qa.font(14), qa.font(22)

    def art(sprite):
        return Image.fromarray(generate(theme, sprite), "RGBA")

    def shown(sprite, wall, on, ceiling=False):
        """The sprite on a back wall (None: the sky), with its lit overlay when `on`."""
        s = art(sprite)
        img = Image.new("RGBA", s.size, sky)
        for x in range(0, s.width, 32):
            for y in range(0, s.height if wall else 0, 32):
                img.alpha_composite(art(wall.replace("interior", "interior_top") if ceiling and y == 0 else wall), (x, y))
        img.alpha_composite(s)
        if on and sprite + "_lit" in SPRITES:
            img.alpha_composite(art(sprite + "_lit"))
        return img

    def pair(sprite, wall, ceiling=False):
        """Off beside on (or just the sprite when nothing on it glows)."""
        shots = [shown(sprite, wall, False, ceiling)]
        if sprite + "_lit" in SPRITES and generate(theme, sprite + "_lit")[..., 3].any():
            shots.append(shown(sprite, wall, True, ceiling))
        img = Image.new("RGBA", (sum(s.width for s in shots) + 4 * (len(shots) - 1), shots[0].height), (40, 40, 48, 255))
        for i, s in enumerate(shots):
            img.alpha_composite(s, (i * (s.width + 4), 0))
        return img, len(shots) > 1

    cells = []
    for prop, (w, h, _) in PROPS.items():
        img, glows = pair("prop_" + prop, None if prop.startswith("sign_") else "interior", prop in CEILING)
        note = "" if "prop_" + prop in built else "  (not built)"
        cells.append((prop + (" / lit" if glows else "") + note, qa.scaled(img, 4)))
    for block in ("glass", "glass_1", "door", "chimney", "chimney_1", "chimney_2"):
        if block + "_lit" in built:
            cells.append((block + " / lit", qa.scaled(pair(block, "interior" if block.startswith("glass") else None)[0], 4)))

    pad, label_h, width = 16, 20, 1800
    pos, bottom = qa.flow([img.size for _, img in cells], pad, 60, width - pad, pad, label_h)
    rooms = []  # the catalog switched on at 2x, on each back wall: (wall, images, positions)
    for wall in ("interior", "interior_1", "interior_2"):
        shots = []
        for prop, (w, h, _) in PROPS.items():
            if prop.startswith("sign_"):
                continue
            s = shown("prop_" + prop, wall, True, prop in CEILING)
            if h == 1:  # a cell of wall above it (below it, under the ceiling)
                room = Image.new("RGBA", (s.width, 64), sky)
                for x in range(0, s.width, 32):
                    room.alpha_composite(art(wall), (x, 32 if prop in CEILING else 0))
                room.alpha_composite(s, (0, 0 if prop in CEILING else 32))
                s = room
            shots.append(qa.scaled(s, 2))
        at, bottom = qa.flow([s.size for s in shots], pad, bottom + pad + label_h, width - pad, 4, 4)
        rooms.append((wall, shots, at))
    sheet = Image.new("RGBA", (width, bottom + label_h + pad), (40, 40, 48, 255))
    d = ImageDraw.Draw(sheet)
    d.text((pad, 14), f"{theme['name']} props: off / lit, 4x  ({len(cells)} entries)", fill="white", font=fb)
    for (label, img), (x, y) in zip(cells, pos):
        sheet.alpha_composite(img, (x, y))
        d.text((x, y + img.height + 2), label, fill=(160, 160, 170) if "(not built)" in label else "white", font=f)
    for wall, shots, at in rooms:
        d.text((pad, at[0][1] - label_h), f"switched on, 2x, on {wall}", fill=(200, 200, 200), font=f)
        for s, xy in zip(shots, at):
            sheet.alpha_composite(s, xy)
    out = os.path.join(qa.ROOT, "content-work", theme["name"], "props.png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    sheet.convert("RGB").save(out)
    print(f"{theme['name']}: props sheet {os.path.relpath(out, qa.ROOT)}")
    return True


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if a != "--props"]
    if len(args) != 1:
        sys.exit(__doc__)
    sys.exit(0 if (props_sheet if "--props" in sys.argv else build)(args[0]) else 1)

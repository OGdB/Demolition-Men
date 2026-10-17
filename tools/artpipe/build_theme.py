"""Generate every sprite of a theme, write Unity .meta files, then run QA.

    python tools/artpipe/build_theme.py <Theme>

Reads tools/artpipe/themes/<theme>.json and writes PNGs to
Demolition Men/Assets/Content/<Theme>/Sprites/. An existing .meta is kept, so GUIDs (and
every reference to the sprite) survive regeneration.
"""
import os
import sys
import uuid

from PIL import Image

sys.dont_write_bytecode = True  # no __pycache__ next to the tools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import qa  # noqa: E402
from normalize import load_theme  # noqa: E402
from tiles import generate, theme_sprites  # noqa: E402

META_TEMPLATE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sprite.meta.template")
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


def build(theme_name):
    theme = load_theme(theme_name)
    name = theme["name"]
    ensure_folder(os.path.join(qa.ASSETS, "Content", name))
    out = qa.sprite_dir(name)
    ensure_folder(out)
    with open(META_TEMPLATE, encoding="utf-8") as f:
        template = f.read()
    for sprite in theme_sprites(theme):
        png = os.path.join(out, sprite + ".png")
        Image.fromarray(generate(theme, sprite), "RGBA").save(png)
        guid = write_meta(png + ".meta", template)
        print(f"  {name}/{sprite}.png" + (f" new guid {guid}" if guid else ""))
    return qa.run(name)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    sys.exit(0 if build(sys.argv[1]) else 1)

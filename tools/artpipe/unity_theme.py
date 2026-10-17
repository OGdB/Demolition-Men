#!/usr/bin/env python3
"""Write a theme's Unity assets from its sprites and JSON (the integrator step, roadmap §7).

    python tools/artpipe/unity_theme.py Port --music "Assets/Sounds/Game Music.mp3"

Reads   Demolition Men/Assets/Content/<Theme>/Sprites/*.png (+ .meta, from build_theme.py),
        Content/<Theme>/Archetypes/*.json and Content/<Theme>/Levels/*.json (sorted),
        tools/artpipe/themes/<theme>.json (sky / backdrop tint suggestions).
Writes  Content/<Theme>/Tiles/GroundTop.asset, GroundFill.asset (Tile, grid collider),
        Content/<Theme>/<Theme>Tileset.asset (BuildingTileset), Content/<Theme>/<Theme>Theme.asset
        (LevelTheme), and .meta files for anything new. Existing GUIDs are kept, so
        re-running only refreshes the asset contents. Add the theme to
        Assets/Resources/GameConfig.asset `themes` yourself (campaign order is a design call).
"""
import argparse
import glob
import json
import os
import re
import sys
import uuid

sys.dont_write_bytecode = True
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(REPO, "Demolition Men", "Assets")
SCRIPT_GUIDS = {}

# TileKind enum order (Scripts/Demolition/Generation/Model/TileKind.cs) -> sprite files.
KINDS = [
    ("Brick", ["brick", "brick_1", "brick_2"]),
    ("BrickTall", ["brick_tall"]),
    ("Concrete", ["concrete"]),
    ("Wood", ["wood"]),
    ("Glass", ["glass"]),
    ("Door", ["door"]),
    ("Plank", ["plank"]),
    ("MetalTruss", ["metal_truss"]),
    ("Beam", ["beam"]),
    ("BrickCap", ["brick_cap"]),
    ("Support", ["support"]),
    ("Chimney", ["chimney"]),
]
SORTING = {"Glass": 1}  # the window back draws one below the pane
# Parallax backdrop layers, far to near: (file, parallax, bottom above the street in units).
BACKDROP = [("backdrop_far", 0.9, -0.5), ("backdrop_mid", 0.75, -0.5), ("backdrop_near", 0.55, -0.5)]
# All start at the street line: a far layer raised above a nearer one would show sky under it.

HEADER = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" \
         "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n" \
         "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"


def guid_of(path):
    with open(path + ".meta") as f:
        return re.search(r"^guid: (\w+)", f.read(), re.M).group(1)


def ensure_meta(path, kind):
    """Create a .meta for a new asset/folder; return its GUID."""
    meta = path + ".meta"
    if os.path.exists(meta):
        return guid_of(path)
    g = uuid.uuid4().hex
    if kind == "folder":
        body = f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif kind == "text":
        body = f"fileFormatVersion: 2\nguid: {g}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    else:
        body = f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    with open(meta, "w") as f:
        f.write(body)
    return g


def script_guid(relative):
    return guid_of(os.path.join(ASSETS, "Scripts", "Demolition", relative))


def sprite_ref(theme_dir, name):
    path = os.path.join(theme_dir, "Sprites", name + ".png")
    if not os.path.exists(path):
        sys.exit(f"missing sprite {path} (run build_theme.py first)")
    return f"{{fileID: 21300000, guid: {guid_of(path)}, type: 3}}"


def colour(rgb):
    r, g, b = rgb
    return f"{{r: {r}, g: {g}, b: {b}, a: 1}}"


def write(path, text):
    with open(path, "w", newline="\n") as f:
        f.write(text)


def tile(theme_dir, name, sprite):
    path = os.path.join(theme_dir, "Tiles", name + ".asset")
    write(path, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
          "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
          "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
          "  m_Script: {fileID: 13312, guid: 0000000000000000e000000000000000, type: 0}\n"
          f"  m_Name: {name}\n  m_EditorClassIdentifier: \n  m_Sprite: {sprite_ref(theme_dir, sprite)}\n"
          "  m_Color: {r: 1, g: 1, b: 1, a: 1}\n  m_Transform:\n"
          + "".join(f"    e{r}{c}: {1 if r == c else 0}\n" for r in range(4) for c in range(4))
          + "  m_InstancedGameObject: {fileID: 0}\n  m_Flags: 1\n  m_ColliderType: 2\n")
    return f"{{fileID: 11400000, guid: {ensure_meta(path, 'asset')}, type: 2}}"


def tileset(theme, theme_dir):
    lines = [HEADER, f"  m_Script: {{fileID: 11500000, guid: {script_guid('Generation/View/BuildingTileset.cs')}, type: 3}}\n",
             f"  m_Name: {theme}Tileset\n  m_EditorClassIdentifier: Demolition::Demolition.Generation.BuildingTileset\n  entries:\n"]
    for kind, (name, sprites) in enumerate(KINDS):
        lines.append(f"  - kind: {kind}\n    sprites:\n")
        lines += [f"    - {sprite_ref(theme_dir, s)}\n" for s in sprites]
        lines.append("    tint: {r: 1, g: 1, b: 1, a: 1}\n    rotation: 0\n    scale: {x: 1, y: 1}\n"
                     f"    offset: {{x: 0, y: 0}}\n    sortingOrder: {SORTING.get(name, 0)}\n")
    lines.append(f"  windowBack: {sprite_ref(theme_dir, 'window_back')}\n  windowBackTint: {{r: 1, g: 1, b: 1, a: 1}}\n")
    path = os.path.join(theme_dir, f"{theme}Tileset.asset")
    write(path, "".join(lines))
    return f"{{fileID: 11400000, guid: {ensure_meta(path, 'asset')}, type: 2}}"


def text_refs(folder):
    refs = []
    for path in sorted(glob.glob(os.path.join(folder, "*.json"))):
        refs.append(f"  - {{fileID: 4900000, guid: {ensure_meta(path, 'text')}, type: 3}}\n")
    return refs


def backdrop_block(theme_dir):
    """The LevelTheme `backdrop` list for whichever backdrop sprites exist (empty if none)."""
    layers = [b for b in BACKDROP if os.path.exists(os.path.join(theme_dir, "Sprites", b[0] + ".png"))]
    if not layers:
        return "  backdrop: []\n"
    out = ["  backdrop:\n"]
    for name, parallax, bottom in layers:
        out.append(f"  - sprite: {sprite_ref(theme_dir, name)}\n    parallax: {parallax}\n    bottom: {bottom}\n")
    return "".join(out)


def update_backdrop(theme, theme_dir):
    """Only refresh the backdrop list of an existing theme asset (hand-made themes like Town)."""
    path = os.path.join(theme_dir, f"{theme}Theme.asset")
    text = open(path).read()
    text = re.sub(r"  backdrop:(?: \[\]\n|\n(?:  - sprite:.*\n(?:    .*\n)*)*)", "", text)
    text = text.replace("  music:", backdrop_block(theme_dir) + "  music:", 1)
    write(path, text)
    print(f"{theme}: backdrop refreshed")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("theme")
    ap.add_argument("--display", help="name shown in the level select (default: THEME)")
    ap.add_argument("--music", default="Assets/Sounds/Game Music.mp3", help="project-relative AudioClip path")
    ap.add_argument("--backdrop-only", action="store_true", help="only refresh the backdrop list of an existing theme asset")
    args = ap.parse_args()
    theme = args.theme
    theme_dir = os.path.join(ASSETS, "Content", theme)
    if args.backdrop_only:
        update_backdrop(theme, theme_dir)
        return
    style_path = os.path.join(os.path.dirname(__file__), "themes", theme.lower() + ".json")
    style = json.load(open(style_path)) if os.path.exists(style_path) else {}
    sky = style.get("sky", [0.55, 0.75, 0.9])
    tint = style.get("backdrop_tint", style.get("backdropTint", [1, 1, 1]))

    os.makedirs(os.path.join(theme_dir, "Tiles"), exist_ok=True)
    ensure_meta(os.path.join(theme_dir, "Tiles"), "folder")
    top = tile(theme_dir, "GroundTop", "ground_top")
    fill = tile(theme_dir, "GroundFill", "ground_fill")
    tiles = tileset(theme, theme_dir)
    archetypes = text_refs(os.path.join(theme_dir, "Archetypes"))
    levels = text_refs(os.path.join(theme_dir, "Levels"))
    music = guid_of(os.path.join(REPO, "Demolition Men", args.music))

    body = [HEADER, f"  m_Script: {{fileID: 11500000, guid: {script_guid('Level/View/LevelTheme.cs')}, type: 3}}\n",
            f"  m_Name: {theme}Theme\n  m_EditorClassIdentifier: Demolition::Demolition.Levels.LevelTheme\n",
            f"  displayName: {args.display or theme.upper()}\n  groundTop: {top}\n  groundFill: {fill}\n  tileset: {tiles}\n",
            "  archetypes:\n", *archetypes, "  levels:\n", *levels,
            f"  sky: {colour(sky)}\n  backdropTint: {colour(tint)}\n", backdrop_block(theme_dir),
            f"  music: {{fileID: 8300000, guid: {music}, type: 3}}\n"]
    path = os.path.join(theme_dir, f"{theme}Theme.asset")
    write(path, "".join(body))
    print(f"{theme}: theme {ensure_meta(path, 'asset')}, {len(archetypes)} archetypes, {len(levels)} levels")


if __name__ == "__main__":
    main()

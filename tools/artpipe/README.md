# artpipe

Procedural pixel-art pipeline (roadmap §6, milestone M4). Python 3, Pillow, numpy.

```
python tools/artpipe/build_theme.py Port     # generate sprites + .meta, then QA
python tools/artpipe/qa.py Port              # QA only; exit 1 on failure
python tools/artpipe/normalize.py in.png out.png --theme Port --size 32x32 --outline --trim --pivot bottom
python tools/artpipe/palette.py              # print the master palette
```

- `palette.py`: master palette (64 colours, 16 ramps, including one backdrop haze ramp per sky, dark to light). Step 0 is the outline/mortar colour. Light comes from the top-left.
- `themes/<theme>.json`: the style. It lists the ramps the theme may use (`ramps` + `accents`), the seed, the sky and backdrop tint suggestions, and per-material ramp and style choices.
- `backdrops.py`: three parallax layers per theme (`backdrop_far/mid/near.png`, 512x192). They are seamless left/right silhouettes in the theme's haze ramp, with no outline; farther layers sit lighter and closer to the sky. A theme JSON can limit what it builds with `sprites` (Town builds the backdrops and the pieces the hand-drawn object set lacks; its red brick, trusses, glass and supports stay hand-drawn). `seeds` overrides the seed of single sprites (`{"wood": 6304}`) to re-roll one without changing the rest.
- `tiles.py`: one generator per sprite, plus `SPRITES` (size, tile axes, foreground). The same seed always gives the same pixels. Tileable sprites draw with wrap-around, and their joints sit away from the tile edge.
- `build_theme.py`: writes `Demolition Men/Assets/Content/<Theme>/Sprites/*.png`. It adds a `.meta` (from `sprite.meta.template`: single sprite, 32 PPU, point filter, no compression) and folder metas only where none exist, so GUIDs survive regeneration.
- `interior` / `interior_top`: a building's back wall behind open rooms (`BuildingInterior` in the game), and the same under a ceiling with its shadow. Background: no outline, darker than the walls, so trusses and supports (the weak points) stand out. Styles `wallpaper`, `blocks`, `boards`.
- Street props (breakable in the game: `crate`, `car`, `barrel` (a gas tank), `container`) and dressing (cosmetic: `lamp`, `fence` (tiles along x), `bush`, `bin`, `sign`). Each theme picks their look in its JSON (`fence` style `picket`/`chain`/`rail`, `bush` style `hedge`/`tires`/`bags`, `bin` style `can`/`bollard`/`hydrant`, `sign` plain or `neon`). `unity_theme.py` writes the props into the tileset and the dressing into the theme's `decor`; for a hand-made theme (Town) use `--street-only`.
- `level_preview.py`: draws generated levels with the theme's real assets, headless. `tools/unity-check/check.sh levels [id...]` writes the layouts to `content-work/levels/*.json`; `python3 tools/artpipe/level_preview.py [id...]` draws `content-work/levels/<id>.png` (the level cut into stacked strips; backdrops without parallax).
- `ui.py`: the shared UI sprites in `Demolition Men/Assets/Resources/UI/` (riveted plate for 9-sliced buttons and panels, hazard stripe, star). They are light grey so the Image colour tints them, and are imported at 25 pixels per unit (4 UI units per pixel).
- `qa.py` checks:
  - every colour is in the theme palette;
  - alpha is hard;
  - each sprite has its exact expected size, a multiple of 32;
  - each sprite has a `.meta`;
  - seams are continuous on each tile axis.

  It writes `content-work/<Theme>/contact.png`. The sheet shows the sprites at 4x, labelled, beside the reference object set. It also has tiled previews and a mock street on the theme sky.

To add a theme, copy a theme JSON, change its ramps, materials and seed, run `build_theme.py` and look at the contact sheet.

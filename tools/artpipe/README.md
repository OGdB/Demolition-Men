# artpipe

Procedural pixel-art pipeline (roadmap §6, milestone M4). Python 3, Pillow, numpy.

```
python tools/artpipe/build_theme.py Port     # generate sprites + .meta, then QA
python tools/artpipe/qa.py Port              # QA only; exit 1 on failure
python tools/artpipe/normalize.py in.png out.png --theme Port --size 32x32 --outline --trim --pivot bottom
python tools/artpipe/palette.py              # print the master palette
```

- `palette.py`: master palette (83 colours, 20 ramps: materials, accents, one backdrop haze ramp and one sky ramp per sky, dark to light). Step 0 is the outline/mortar colour; a sky ramp's step 0 is the camera's clear colour. Light comes from the top-left. The shared palette is what makes the three themes read as one game.
- `themes/<theme>.json`: the style. It lists the ramps the theme may use (`ramps` + `accents`), the seed, the sky and backdrop tint suggestions, and per-material ramp and style choices. A material with a `variants` list draws its extra sprites (`chimney_1`, `bush_1`, `interior_1`...) with that entry laid over the base look.
- `backdrops.py`: the sky and three parallax layers per theme. `backdrop_sky` (512x384) is an ordered-dithered gradient whose top is the exact clear colour, so the camera can zoom out past it, with cumulus (Town), stratus bands and gulls (Port) or stars and wisps (Neon); `backdrop_sun` (64x64) is the sun or moon, one sprite glued to the camera so it never repeats. `backdrop_far/mid/near.png` (512x192) are seamless left/right silhouettes in the theme's haze ramp, with no outline; farther layers sit lighter and closer to the sky. A theme JSON can limit what it builds with `sprites` (Town keeps its hand-drawn trusses, glass columns and supports). `seeds` overrides the seed of single sprites (`{"wood": 6304}`) to re-roll one without changing the rest.
- `tiles.py`: one generator per sprite, plus `SPRITES` (size, tile axes, foreground). The same seed always gives the same pixels. Tileable sprites draw with wrap-around, and their joints sit away from the tile edge (brick courses start on it, so the corners line up).
- Building corners: every wall material also comes as `<name>_l` / `<name>_r` (`brick_l`, `brick_l_1`, `brick_tall_r`, `concrete_l`, `wood_r`...), the same tile with the theme's corner trim on its outer 6 px (`edge` in the JSON: `quoin` stones, a steel `post`, a neon `pilaster`; wood always gets a corner board, concrete a plain pilaster). `BuildingTileset.Dress` picks them for blocks on the building's silhouette (`BuildingSpec.EdgeOf`). Walls: `brick` (pillow bricks, the object-set look), `block`, `panel` (precast, two per tile), `corrugated`; `concrete_1` has a vent grille; `glass_1` has blinds and `glass_2` is boarded up; `chimney_1`, `chimney_2` are the theme's roof props (water tank, AC unit, vent cowl, sign box).
- `build_theme.py`: writes `Demolition Men/Assets/Content/<Theme>/Sprites/*.png`. It adds a `.meta` (from `sprite.meta.template`: single sprite, 32 PPU, point filter, no compression) and folder metas only where none exist, so GUIDs survive regeneration.
- `interior` / `interior_top`: a building's back wall behind open rooms and windows (`BuildingInterior` in the game), and the same under a ceiling with its shadow. Background: no outline, darker than the walls, so trusses and supports (the weak points) stand out. Styles `wallpaper`, `blocks`, `boards`, `plaster` (over a wainscot), `tiles`; `interior_1`, `interior_2` (+ `_top`) are the looks the other storeys get, `interior_detail`, `_1`, `_2` the things on the wall of some room cells (`details`: `picture`, `shelf`, `poster`, `pipe`, `clock`, `board`; `accent` colours them).
- Street props (breakable in the game: `crate`, `car`, `barrel` (a gas tank), `container`) and dressing (cosmetic: `lamp`, `fence` (tiles along x), `bush`, `bin`, `sign`, plus variants `bush_1`, `bin_1`, `sign_1`, `sign_2`). Each theme picks their look in its JSON (`fence` style `picket`/`chain`/`rail`, `bush` style `hedge`/`tires`/`bags`/`pallets`, `bin` style `can`/`bollard`/`hydrant`/`postbox`/`cone`, `sign` plain or `neon` with `faces` `symbol`/`arrow`/`stop`/`p`). `unity_theme.py` writes the props into the tileset and the dressing into the theme's `decor`.
- Ground: `ground_top` (`dock` planks, a `kerb`, or `pavement` slabs) over `ground_fill` (`blocks`, `asphalt` or `soil` with stones).
- `unity_theme.py <Theme>` writes the tiles, tileset and theme assets from the sprite files (naming: `<kind>`, `<kind>_1`... variants; `<kind>_l`, `<kind>_r` corners; `interior_1`, `interior_detail`...; decor variants). An existing theme asset keeps the fields the script doesn't own (lights, music); a tileset entry the theme doesn't generate (Town's hand-drawn glass, trusses, supports) is kept as it is.
- `level_preview.py`: draws generated levels with the theme's real assets, headless. `tools/unity-check/check.sh levels [id...]` writes the layouts to `content-work/levels/*.json`; `python3 tools/artpipe/level_preview.py [id...]` draws `content-work/levels/<id>.png` (the level cut into stacked strips; backdrops without parallax).
- `ui.py`: the shared UI sprites in `Demolition Men/Assets/Resources/UI/` (riveted plate for 9-sliced buttons and panels, hazard stripe, star). They are light grey so the Image colour tints them, and are imported at 25 pixels per unit (4 UI units per pixel).
- `brand.py`: store and brand art from the players' sprites, the object set, the Town backdrops and the title font. Play icon (`brand/icon-512.png`), feature graphic (`brand/feature-graphic.png`, 1024x500, no alpha), the logo on one and two lines (`brand/logo*.png`, transparent), and the app icon in `Demolition Men/Assets/Brand/` (`Icon` for default/legacy/round, `IconBackground` + `IconForeground` for the adaptive icon). Preview with launcher masks: `content-work/brand.png`.
- `qa.py` checks:
  - every colour is in the theme palette;
  - alpha is hard;
  - each sprite has its exact expected size, a multiple of 32;
  - each sprite has a `.meta`;
  - seams are continuous on each tile axis.

  It writes `content-work/<Theme>/contact.png`. The sheet shows the sprites at 4x, labelled, beside the reference object set. It also has tiled previews and a mock street on the theme sky.

To add a theme, copy a theme JSON, change its ramps, materials and seed, run `build_theme.py` and look at the contact sheet.

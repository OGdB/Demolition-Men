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
- `backdrops.py`: three parallax layers per theme (`backdrop_far/mid/near.png`, 512x192). They are seamless left/right silhouettes in the theme's haze ramp, with no outline; farther layers sit lighter and closer to the sky. A theme JSON can limit what it builds with `sprites` (Town only builds the backdrops; its tiles stay hand-drawn).
- `tiles.py`: one generator per sprite, plus `SPRITES` (size, tile axes, foreground). The same seed always gives the same pixels. Tileable sprites draw with wrap-around, and their joints sit away from the tile edge.
- `build_theme.py`: writes `Demolition Men/Assets/Content/<Theme>/Sprites/*.png`. It adds a `.meta` (from `sprite.meta.template`: single sprite, 32 PPU, point filter, no compression) and folder metas only where none exist, so GUIDs survive regeneration.
- `qa.py` checks:
  - every colour is in the theme palette;
  - alpha is hard;
  - each sprite has its exact expected size, a multiple of 32;
  - each sprite has a `.meta`;
  - seams are continuous on each tile axis.

  It writes `content-work/<Theme>/contact.png`. The sheet shows the sprites at 4x, labelled, beside the reference object set. It also has tiled previews and a mock street on the theme sky.

To add a theme, copy a theme JSON, change its ramps, materials and seed, run `build_theme.py` and look at the contact sheet.

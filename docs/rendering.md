# Rendering: URP 2D, lights and post-processing

**Status:** code in, editor steps open (2026-10-21). Roadmap D26: the game moves from the
built-in render pipeline to URP with the 2D Renderer, before the R1 phone profiling, so the
profile measures the renderer that ships.

## What the project had (inventory, 2026-10-21)

- No custom shaders, no `.mat` assets, no Post Processing v2, no `OnRenderImage`,
  replacement shaders or command buffers. Everything is sprites, tilemaps, uGUI and TMP.
- Sprites and tilemaps use Unity's default sprite material; most are created in code
  (buildings, ground, decor), which picks up the pipeline's default 2D material by itself.
- One code-made material: `Particles` uses `Sprites/Default`. It works in URP, unlit, so dust and
  sparks stay bright in dark levels (intended).
- Cameras: `Main Camera 1` (prefab, full screen; past 2.4:1 it adds a black `Bars` camera behind
  it), a scene camera in MainMenu, plus the test bench camera made in code.
- Six Quality levels (Very Low … Ultra), none with a pipeline asset.

## What the code does (`Demolition.Lighting`)

`Scripts/Demolition/Lighting/View/` is its own assembly. Its asmdef only compiles once the
URP package is installed (a `versionDefines` entry sets `DEMOLITION_URP`), so nothing breaks
before the editor steps below. `SceneLighting` adds itself to every scene:

- **Ambient:** one global `Light2D`. In a run it uses the level theme's `ambient` and
  `ambientIntensity`; menus, the tutorial and daylight themes stay at white 1.0 (looks unlit).
- **Point lights:** lamp posts (`lampLight`), signs (`signLight`, `signColor`) and hard-hat
  headlamps on both players (`headlamps`, reach `headlampRange`), from the theme. Only Neon
  uses them so far: ambient 0.55 bluish, lamps 1.0, magenta signs 1.2, headlamps on (range 5).
- **Blasts:** every dynamite or gas-tank explosion flashes an orange light that fades in 0.4 s.
- **Post-processing:** a global `Volume` with a profile built in code: Bloom (threshold
  0.95, intensity 0.6), Vignette (0.22), Color Adjustments (contrast +8, saturation +10). No
  tonemapping, because it shifts the pixel-art palette. The main camera's post-processing is
  switched on by the code.
- **Settings ▸ EFFECTS:** OFF turns off the point lights, the flashes and the post-processing,
  and puts the ambient back to daylight, for slow phones.
- **Settings ▸ QUALITY (render scale):** `RenderScaleView` sets the URP asset's render scale
  from `Meta.Quality`: the world renders at most 720 pixels tall on LOW, 1080 on HIGH phones and
  at full resolution on HIGH elsewhere, then scales up (URP's automatic filter); canvases stay
  sharp. AUTO picks LOW for phones under ~4.5 GB of memory or 6 cores and for phone browsers
  (`QualityPolicy`). The asset is changed in memory only; in the editor the authored scale
  comes back when play mode ends. The six stock quality levels in Project Settings are unused:
  they all share `URP-2D.asset` and only differ in 3D settings.

The values are first guesses; tune them on `NeonTheme.asset` (Light header) and in
`SceneLighting.PostProfile`. unity-check compiles the assembly against `UrpStubs.cs` (URP 17.4
signatures; URP isn't on the package registry since Unity 6). That proves the code calls what
exists, not how it looks.

## Editor steps (once, about 15 minutes)

Rollback point: the commit you start from. If anything goes wrong, discard the working copy.

1. **Install URP.** Window ▸ Package Manager ▸ Unity Registry ▸ *Universal RP* ▸ Install.
   Wait for the compile. The Console should stay free of errors; `Demolition.Lighting` now
   compiles.
2. **Create the pipeline asset.** In `Assets/Settings/` (create the folder): right-click ▸
   Create ▸ Rendering ▸ *URP Asset (with 2D Renderer)*, name it `URP-2D`. Unity also makes
   `URP-2D_Renderer`.
   - `URP-2D`: HDR **on** (lights push past white, bloom picks that up), Anti Aliasing (MSAA)
     **disabled** (pixel art), Render Scale 1.
   - `URP-2D_Renderer`: Default Material Type **Lit**, Light Render Texture Scale **0.5**
     (saves fill rate on phones), Transparency Sort Mode left at Default.
3. **Assign it.** Edit ▸ Project Settings ▸ Graphics ▸ Default Render Pipeline = `URP-2D`.
   Then Quality: select each of the six levels in turn and set its Render Pipeline Asset to
   `URP-2D`.
4. **Convert the sprite materials.** Window ▸ Rendering ▸ Render Pipeline Converter ▸
   *Convert Built-in to 2D (URP)* ▸ tick *Material and Material Reference Upgrade* ▸
   Initialize Converters ▸ check the list ▸ Convert Assets. This moves the scenes' and
   prefabs' sprite renderers from Sprites-Default to Sprite-Lit-Default.
5. **Check.** Open MainMenu, Tutorial, `Sandbox`, and a Neon level (Level Lab ▸
   Play). Look for:
   - nothing magenta; sprites, tilemaps, UI and TMP text look as before in daylight;
   - Neon at night: lit lamp posts, glowing signs, headlamps;
   - blasts flash;
   - Settings ▸ EFFECTS OFF gives the plain look back;
   - the Console stays clean.

   If `Prefabs/Particle System.prefab` shows magenta, give it the *Sprite-Unlit-Default*
   material.
6. **Commit and push:**
   - `Packages/manifest.json` and `packages-lock.json`;
   - `Assets/Settings/` with its `.meta` files;
   - `ProjectSettings/GraphicsSettings.asset` and `QualitySettings.asset`;
   - the converted scenes and prefabs.

   Screenshots of a Neon level help with tuning.

For a local Claude agent with the Unity MCP: "Follow the editor steps in docs/rendering.md,
using the Unity MCP. Stop and report if any step shows Console errors or magenta materials.
Report each step's result and attach screenshots of step 5."

## After the steps

- R1 profiling measures the worst collapse on a phone with EFFECTS on and off. The result
  decides the phone default, and whether the light texture scale or bloom need to come down.
- Possible next lights (not built):
  - window glow from the back walls at night;
  - fire;
  - a darker Port at dusk;
  - normal maps for the sprites.

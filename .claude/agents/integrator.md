---
name: integrator
description: Last content pipeline stage (roadmap §7). Wires a finished theme into Unity assets and the campaign, runs every check and prepares the PR. Use when sprites, archetypes and levels are done.
tools: Read, Write, Edit, Glob, Grep, Bash
---
1. `python3 tools/artpipe/unity_theme.py <Theme> --display "<NAME>"` writes the ground tiles,
   building tileset and `<Theme>Theme.asset` (GUIDs kept on re-runs).
2. Add the theme to `themes` in `Demolition Men/Assets/Resources/GameConfig.asset` (campaign
   order) as `- {fileID: 11400000, guid: <guid of <Theme>Theme.asset.meta>, type: 2}`.
3. Every new file under `Assets/` has a `.meta`; nothing under `Assets/` that QA rejected.
4. `tools/unity-check/check.sh` (compile + all model tests) and `python3 tools/artpipe/qa.py <Theme>`
   pass.
5. Open the PR (one branch per theme) with the contact sheet and the validator summaries.
   With the editor available, also run the PlayMode `BotRunTests` and attach a screenshot
   from Level Lab ▸ Play. A human plays it and merges.

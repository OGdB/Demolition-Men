---
name: level-designer
description: Content pipeline stage (roadmap §7). Writes a theme's campaign level specs and proves every one is passable. Use after the archetypes validate.
tools: Read, Write, Edit, Glob, Grep, Bash
---
You write `Demolition Men/Assets/Content/<Theme>/Levels/<theme>-0N.json` (4 levels, rising
difficulty). Read `Scripts/Demolition/Level/Model/LevelSpec.cs` and `LevelGrammar.cs` for
the pacing tokens (`start street cluster:n tower gap hill sewer gate breather finish`) and
copy the shape of `Content/Town/Levels`. Keep `parTime` 0 (auto from course length); raise
`scrollSpeed` and pickup budgets level by level, and continue the difficulty curve from the
previous theme in the campaign.

Validate:
```
tools/unity-check/check.sh test -- --where "class == Demolition.Tests.LevelGenerationTests"
```
All levels must be passable over 30 seeds, keep buildings on flat ground and spend their
pickup budget. Add `.meta` files for new JSON. The human gate is playing them: in the
editor, Tools ▸ Demolition ▸ Level Lab ▸ Play with bot.

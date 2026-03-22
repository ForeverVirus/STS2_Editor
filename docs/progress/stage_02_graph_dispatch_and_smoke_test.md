# Stage 02 - Graph Dispatch And Smoke Test

## Date
- 2026-03-23

## Developed
- Graph runtime execution is now wired into real gameplay entry points.
  - Card main effect override now runs through a graph-aware replacement for `CardModel.OnPlayWrapper`.
  - Potion main effect override now runs through a graph-aware replacement for `PotionModel.OnUseWrapper`.
  - Relic and enchantment hook dispatch now runs through graph-aware replacements for:
    - `Hook.BeforeCardPlayed`
    - `Hook.AfterCardPlayed`
    - `Hook.BeforePotionUsed`
    - `Hook.AfterPotionUsed`
- Added a reusable graph executor and built-in gameplay node executors.
  - New flow/value runtime: entry, exit, sequence, branch, random choice, set, add, compare, log.
  - New combat runtime nodes:
    - `combat.damage`
    - `combat.gain_block`
    - `combat.heal`
    - `combat.draw_cards`
    - `combat.apply_power`
- Added trigger-aware graph routing.
  - Card main effect uses trigger id `card.on_play`.
  - Potion main effect uses trigger id `potion.on_use`.
  - Enchantment main effect uses trigger id `enchantment.on_play`.
  - Hook-driven entities can now map specific triggers through graph metadata keys such as `trigger.relic.after_card_played`.
  - `trigger.default` is supported as a default graph entry for direct effect entities.
- Added minimal in-editor graph authoring support.
  - `Project Mode` now exposes a `Graph JSON` editor panel.
  - `Save Graph` persists the current graph JSON into the project file.
  - `Save Override` also persists graph JSON when graph mode is enabled.
  - Default graph scaffold now includes `trigger.default`.
- Bootstrap now registers built-in graph executors during mod initialization.

## Not Developed Yet
- There is still no full visual node editor UI.
  - Graph editing is currently JSON-based inside the game UI.
- Hook-style graph replacement is only wired for the first useful combat slice.
  - Predicate hooks such as `ShouldPlay`, damage modifiers, energy modifiers, and reward modifiers are still native-only.
- Event runtime behavior override is not yet connected to actual event option execution.
- New content injection into `ModelDb`, pools, and event routing is still not implemented.
- External asset import UI, asset preview workflow, and package asset extraction/runtime remap are still incomplete.
- Monster wiki, monster skills, monster editing, and Spine replacement are still not started.

## Issues Encountered
- Decompiled source signatures did not fully match the real referenced game DLL signatures.
  - `CardPileCmd.RemoveFromCombat(...)` is one confirmed example.
  - Resolution: prefer the actual compile-time DLL contract and runtime behavior, not decompiled parameter lists.
- Replacing `CardModel.OnPlayWrapper` required preserving the original wrapper lifecycle instead of only replacing `OnPlay`.
  - The wrapper also handles pile movement, hook notifications, play history, enchantment/affliction follow-up, cleanup, and VFX timing.
  - Resolution: implement a graph-aware wrapper replacement that mirrors the original flow closely.
- Hook-driven relic behavior cannot safely use the same default fallback rule as direct card/potion effect graphs.
  - A single default entry would otherwise fire on every supported hook trigger.
  - Resolution: hook-driven graph execution only falls back when a specific trigger mapping exists, or when `trigger.default` is intentionally set.
- Real smoke-test evidence was not easy to obtain through log files because the game log location was not obvious.
  - Resolution: verify the loaded module list of the live game process and verify bootstrap-created runtime directories on disk.

## Validation
- `dotnet build STS2_Editor.csproj` passed with `0 warning / 0 error`.
- Real game smoke test completed:
  - `SlayTheSpire2.exe` stayed alive for at least 15-18 seconds after launch.
  - The live game process loaded `STS2_Editor.dll` from the game `mods` folder.
  - Bootstrap-created runtime directories were created under:
    - `C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\sts2_editor`
  - Created directories include:
    - `projects`
    - `packages`
    - `packages\\installed`
    - `imports`
    - `exports`
    - `cache`
- This confirms:
  - the mod assembly was actually loaded by the game process,
  - initialization ran far enough to create editor storage roots,
  - the new Harmony patch set does not crash the game during early startup.

## Next Step
- Stage 03 should focus on making graph behavior usable without hand-editing JSON structure.
  - Add graph template actions for common card/relic/potion patterns.
  - Add in-UI validation feedback and graph trigger mapping helpers.
  - Add simple asset selector workflow for card/relic/potion/event images.
- After that, the highest-value backend step is new content injection.
  - New cards
  - New relics
  - New potions
  - New events
- Once those are in place, move to multiplayer conflict/session verification with actual modified packages.

# Stage 11 - Real Game External Asset Render Validation

## Date
- 2026-03-23

## Developed
- Extended managed-asset runtime loading in [RuntimeAssetLoader.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeAssetLoader.cs).
  - Runtime lookup now recognizes normalized managed paths under `user://sts2_editor/...` in addition to `modstudio://asset/...`.
  - Successful texture loads now emit explicit size-bearing proof logs.
- Added once-per-entity texture-override application logs in [RuntimeOverridePatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeOverridePatches.cs).
  - Card portrait overrides now log when the real runtime actually swaps in the imported texture.
  - Relic and potion overrides do the same.
- Closed the Stage 10 proof gap:
  - Stage 10 had already proven `external file -> managed asset import -> package export -> install`.
  - Stage 11 proves `installed managed asset -> real game runtime load -> real texture override application`.

## Validation
- Main mod build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Real-game evidence captured from [godot.log](C:/Users/Administrator/AppData/Roaming/SlayTheSpire2/logs/godot.log):
  - `[ModStudio.Asset] Loaded managed texture ... stage10_potion.png ... size=128x128`
  - `[ModStudio.Asset] Applied texture override Potion:ED_STAGE10_POTION001:image_path ... size=128x128`
  - `[ModStudio.Asset] Loaded managed texture ... stage10_relic.png ... size=128x128`
  - `[ModStudio.Asset] Applied texture override Relic:ED_STAGE10_RELIC001:icon_path ... size=128x128`
  - `[ModStudio.Asset] Loaded managed texture ... stage10_card.png ... size=320x240`
  - `[ModStudio.Asset] Applied texture override Card:ED_STAGE10_CARD001:portrait_path ... size=320x240`
- Conclusion:
  - imported external PNG assets are not only packaged and installed
  - they are actually loaded by the shipped game and bound to the real card / relic / potion runtime models

## Not Developed Yet
- This stage still does not remove the atlas warning path for custom card portraits.
- No screenshot-based visual proof was collected; current proof is log-based.
- Event portrait override still relies on the default event portrait path being attempted first, which can produce noisy asset warnings before the override lands.

## Issues Encountered
- The first runtime logs only showed normalized `user://...` paths, which made it look as if `modstudio://asset/...` resolution was bypassing the managed-asset branch.
  - Resolution:
    - broaden managed-path detection in [RuntimeAssetLoader.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeAssetLoader.cs) and add explicit success logging.
- Asset application could previously be inferred only indirectly from gameplay.
  - Resolution:
    - add explicit once-per-entity apply logs in [RuntimeOverridePatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeOverridePatches.cs).

## Next Step
- Move to the next remaining proof gap: brand-new template event runtime validation in the shipped game.
- Recommended target:
  - inject a deterministic proof event into a real run,
  - verify event page initialization,
  - verify event-triggered combat,
  - verify event resume after combat.

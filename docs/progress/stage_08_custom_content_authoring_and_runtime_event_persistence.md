# Stage 08 - Custom Content Authoring And Runtime Event Persistence

## Date
- 2026-03-23

## Developed
- Built project-local custom entry authoring in [ModelMetadataService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Services/ModelMetadataService.cs) and [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - `Project Mode` can create new `Card` / `Relic` / `Potion` / `Event` / `Enchantment` entries.
  - New entries appear in the browser immediately even before they exist in the runtime `ModelDb`.
  - IDs are generated with stable project-prefixed editor IDs such as `ed_<project>__card_001`.
- Added bilingual strings for the new-entry flow in [ModStudioLocalization.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Utilities/ModStudioLocalization.cs).
  - Includes create-entry actions, placeholder text, default titles/descriptions, and project-only entry messaging.
- Expanded runtime metadata coverage for dynamic content in [RuntimeOverridePatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeOverridePatches.cs).
  - Card metadata now covers `target_type`, energy cost, star cost, pool, generation flag, and portrait path.
  - Potion metadata now covers `pool`, `rarity`, `usage`, `target_type`, generation flag, and image path.
  - Event metadata now covers portrait override and canonical encounter override.
  - Character starter deck / starter relic / starter potion resolution now supports brand-new custom runtime entries.
- Extended event asset sourcing in [ProjectAssetBindingService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Services/ProjectAssetBindingService.cs).
  - Events can now choose original in-game runtime assets instead of only imported external assets.
- Added event-template resume persistence in [RuntimeEventTemplateSupport.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeEventTemplateSupport.cs) and [RuntimeEventTemplatePersistencePatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeEventTemplatePersistencePatches.cs).
  - Event combat resume page IDs now survive room serialization and restore correctly after save/load boundaries.
- Added a real-game custom-content proof tool in [Stage09CustomContentProof.csproj](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage09CustomContentProof/Stage09CustomContentProof.csproj) and [Program.template](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage09CustomContentProof/Program.template).
  - The tool builds and installs a package containing a brand-new card, potion, and relic plus character starter overrides to force them into a real run.

## Validation
- Main build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: passed
- Stage 09 proof tool build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\tools\Stage09CustomContentProof\Stage09CustomContentProof.csproj`
  - Result: passed
- Stage 09 proof tool generation:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage09CustomContentProof\Stage09CustomContentProof.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage09-proof-run`
  - Result:
    - sample project saved into real editor project storage
    - `.sts2pack` exported
    - package installed into real game package storage
    - session state updated for runtime loading

## Not Developed Yet
- This stage does not yet prove that the new content survives a real combat run in the shipping game.
- External imported art for custom card/relic/potion content has not been validated in a real run yet.
- Custom event templates are persisted, but a real-game event proof package is not part of this stage.
- Monster cataloging and monster editing remain out of scope.

## Issues Encountered
- The standalone gameplay proof tools originally compiled but did not run because `sts2.dll` and `0Harmony.dll` were not copied to the tool output directory.
  - Resolution:
    - update the tool projects so those game assemblies are copied locally.
- The Stage 09 proof tool originally assumed `ModelDb` was initialized in a standalone process.
  - Resolution:
    - switch the proof package generator to source-backed static sample IDs and paths instead of depending on live `ModelDb` boot order.

## Next Step
- Run the real game with the installed Stage 09 proof package and use autoslay plus `godot.log` markers to confirm that:
  - brand-new custom card content executes
  - brand-new custom potion content executes
  - brand-new custom relic content executes
- If startup or combat fails, fix the runtime registration path until the custom-content proof reaches real combat successfully.

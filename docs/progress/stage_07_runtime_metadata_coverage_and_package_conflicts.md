# Stage 07 - Runtime Metadata Coverage And Package Conflicts

## Date
- 2026-03-23

## Developed
- Expanded runtime metadata getter coverage in [RuntimeOverridePatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeOverridePatches.cs).
  - Card overrides now affect:
    - `type`
    - `rarity`
    - `pool_id`
  - Relic overrides now affect:
    - `rarity`
    - `pool_id`
  - Potion overrides now affect:
    - `rarity`
    - `usage`
    - `target_type`
  - Event overrides now affect:
    - `layout_type`
    - `is_shared`
  - Enchantment overrides now affect:
    - `show_amount`
    - `has_extra_card_text`
- Added enum parsing support in [RuntimeOverrideMetadata.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeOverrideMetadata.cs).
  - Runtime metadata can now resolve strongly typed enum values instead of only strings, ints, and bools.
- Added explicit Phase 1 monster extension slot in [ModStudioEntityKind.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Models/ModStudioEntityKind.cs).
  - `Monster` is now part of the entity-kind contract.
  - The browser still hides it, so Phase 1 UI scope remains unchanged.
- Added package conflict modeling in [RuntimePackageModels.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimePackageModels.cs) and [RuntimeOverrideResolver.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeOverrideResolver.cs).
  - The resolver now records object-level conflicts whenever multiple enabled packages override the same entity.
  - Conflict entries include participant order and the final winning package.
- Exposed conflict inspection in `Package Mode` via [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - Selecting an installed package now shows:
    - conflict count
    - conflicting entity kind/id
    - winner package
    - package order chain
- Added bilingual conflict UI text in [ModStudioLocalization.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Utilities/ModStudioLocalization.cs).
- Extended smoke coverage in [Stage03SmokeTest/Program.template](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage03SmokeTest/Program.template).
  - The smoke test now asserts package conflict detection and winner selection.
- Improved the real-game proof sample in [Stage06GameplayProof/Program.template](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage06GameplayProof/Program.template).
  - The proof card now deals more damage so later autoslay verification runs can complete faster.

## Validation
- Main build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Stage 03 smoke build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj`
  - Result: `0 warning / 0 error`
- Stage 03 smoke run:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - Result: `PASS`
  - New proof lines include:
    - `Conflict winner: package-conflict-b@1.0.0`
    - `Conflict chain: Package Conflict A -> Package Conflict B`
- Stage 06 gameplay-proof build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\tools\Stage06GameplayProof\Stage06GameplayProof.csproj`
  - Result: `0 warning / 0 error`

## Not Developed Yet
- These runtime metadata patches do not yet prove every field through a real in-game visual or gameplay assertion.
- Event graph execution is still not implemented.
- New-content registration for cards/relics/potions/enchantments/events is still not implemented.
- Conflict inspection is object-level only; there is still no field-level diff or merge UI.

## Issues Encountered
- `System.Environment` and `Godot.Environment` collided in the new conflict-rendering helper.
  - Resolution:
    - fully qualify `System.Environment.NewLine`
- Running smoke build and smoke run in parallel caused file locks on the generated smoke assembly.
  - Resolution:
    - rerun build and run sequentially

## Next Step
- Move from metadata/inspection completeness into the remaining structural gaps.
- Recommended next targets:
  - implement minimal event runtime templates with graph triggers
  - then implement startup-time registration for newly created content

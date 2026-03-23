# Stage 14 - Editor Browser Stability And UI Rework

## Date
- 2026-03-23

## Developed
- Fixed the card-browser canonical model crash in [ModelMetadataService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Services/ModelMetadataService.cs).
  - `BuildCardItem` and `BuildCardMetadata` no longer call `CardEnergyCost.GetAmountToSpend()` while browsing canonical runtime models.
  - Card browser metadata now reads canonical-safe energy cost information from `EnergyCost.Canonical` and `EnergyCost.CostsX`.
- Reworked the main project UX in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - `Project Mode` is now split into two explicit layers:
    - `Project Home`
    - `Editor Workspace`
  - `Project Home` focuses on:
    - new/open/duplicate/delete/export project actions
    - project list
    - current project details
    - usage guidance
  - `Editor Workspace` focuses on editing only:
    - left side searchable entity browser
    - right side tabbed editing surface
- Replaced the old mixed browser/editor stack with a clearer workspace layout.
  - Left pane:
    - content type selector
    - search box
    - `New Entry`
    - dedicated scrollable entity list
  - Right pane:
    - `Overview`
    - `Metadata`
    - `Assets`
    - `Graph`
- Separated previously mixed controls into clearer tabs.
  - `Overview`: selected runtime object details
  - `Metadata`: runtime capture, event scaffold, structured metadata fields, advanced JSON, notes, save/remove override
  - `Assets`: asset binding and preview workflow
  - `Graph`: graph toggle, graph id, presets, graph JSON, validation, node catalog
- Added a first structured metadata editor layer in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - common metadata keys are now exposed as direct form fields
  - boolean values render as toggles
  - the advanced JSON editor remains available for power users and unsupported fields
- Reworked `Package Mode` into a split layout in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - left side package list
  - right side package details and conflict details
- Added the new bilingual UI strings required by the reworked flow in [ModStudioLocalization.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Utilities/ModStudioLocalization.cs).
  - default language remains Chinese
  - English remains switchable
  - persisted local settings behavior is unchanged

## Validation
- Main mod build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Existing smoke test:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - Result: `PASS`
- Shipped-game startup smoke test:
  - launched `F:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe`
  - let the game reach main menu
  - terminated the process intentionally after smoke capture
  - checked [godot.log](C:/Users/Administrator/AppData/Roaming/SlayTheSpire2/logs/godot.log)
  - observed:
    - `Mod Studio bootstrap initialized.`
    - `Mod Studio main menu entry attached.`
    - no startup-time `CanonicalModelException` after a clean-log relaunch

## Not Developed Yet
- This stage did not add form-based field editors for each entity type.
  - metadata is still primarily JSON-driven inside the editor tabs.
- This stage did not complete a fully automated in-game click-through of:
  - opening Mod Studio
  - switching to card / relic / potion tabs
  - visually confirming every list entry on-screen
- This stage did not redesign the underlying data model or graph authoring model.
  - it improves discoverability and layout first.

## Issues Encountered
- The original card tab crash was a real Stage 03 bug, not a “not yet implemented” placeholder.
  - root cause: canonical card browser code called `GetAmountToSpend()`, which reaches mutable owner state.
  - resolution: switch the browser to canonical-safe cost reads only.
- The first startup log review looked like the old crash still existed.
  - root cause: the previous log content had not been isolated from the fresh run.
  - resolution: rerun a clean startup smoke and inspect the new log content only.
- The old project page mixed browser, metadata, assets, notes, graph presets, graph JSON, validation, and node catalog into one long surface.
  - result: users could not tell whether they were browsing, editing metadata, binding assets, or editing behavior.
  - resolution: split the workflow into explicit project and workspace layers, then split editing by tabs.

## Next Step
- Run a focused in-game UX pass for the reworked workspace:
  - open `Project Home`
  - enter `Editor Workspace`
  - verify `Card / Relic / Potion` lists are browseable end-to-end
  - confirm bottom-area controls are reachable on the target resolution
- After that, continue the next usability stage:
  - replace high-friction JSON-only editing with structured field editors for the highest-value entity types first
  - start with cards, relics, potions, and character base stats

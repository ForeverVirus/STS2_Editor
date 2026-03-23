# Stage 16 - Workspace Filters, Asset Library, And Graph Inspector

## Date
- 2026-03-23

## Developed
- Upgraded the left-side entity browser in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - Replaced the generic `ItemList` with a custom scrollable browser list made of explicit entry rows.
  - Each row now exposes clearer state directly in the list:
    - project-only entry
    - override exists
    - graph behavior
    - native behavior
    - asset override present
  - Added a browser scope filter so the author can switch between:
    - all entries
    - modified only
    - project-new only
- Reworked the asset workflow in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - Split the asset editor into three explicit sub-tabs:
    - `Current Binding`
    - `Game Assets`
    - `Imported Assets`
  - `Current Binding` now acts as the summary and direct override area.
  - `Game Assets` now supports:
    - searchable runtime asset catalog
    - explicit preview-before-apply behavior
    - one-click apply for the selected in-game asset
  - `Imported Assets` now supports:
    - project-local managed asset library browsing
    - preview and detail inspection for imported images
    - reusing a previously imported image without forcing another import
- Added reusable project-asset rebinding support in [ProjectAssetBindingService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Services/ProjectAssetBindingService.cs).
  - New method: `BindProjectAsset(...)`
  - This allows an imported managed asset already stored in the project to be applied to another entry directly.
- Integrated the upgraded visual graph editor from [ModStudioGraphCanvasView.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioGraphCanvasView.cs) into the host screen.
  - The graph canvas now raises `GraphChanged` and `SelectedNodeChanged`.
  - [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs) now listens to graph changes and syncs:
    - graph JSON
    - validation panel
  - The graph tab is no longer only “visualize then hand-edit JSON”; it is now an editable graph workspace with host sync.
- Extended bilingual UI coverage in [ModStudioLocalization.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Utilities/ModStudioLocalization.cs) for the new browser and asset workflow labels.

## Validation
- Main mod build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Existing smoke test:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - Result: `PASS`
- Attempted shipped-game startup smoke from the terminal tool.
  - Result:
    - blocked by desktop shell policy when launching the external game process from the tool layer
  - Impact:
    - no new shipped-game runtime confirmation was produced in this stage
    - this stage is therefore validated by build + smoke test, but not by a fresh terminal-driven game-launch proof

## Not Developed Yet
- The left browser is now much clearer, but it still does not render rich custom widgets such as:
  - rarity chips
  - owner/pool chips
  - image thumbnails inside the browser row
- The asset workflow is now split and much easier to follow, but it is still embedded in the main asset tab.
  - It has not yet been promoted to a separate popup browser / drawer system like the reference project.
- The graph editor is now editable and synchronized, but still does not support:
  - drag-from-palette node creation
  - graph minimap
  - structured per-node schema forms driven by descriptor metadata
- No automated in-game click-through was completed for:
  - entering Mod Studio
  - switching browser scope filters
  - selecting runtime assets
  - reusing imported project assets
  - editing node properties live in the shipped game UI

## Issues Encountered
- Reusing an already-bound project asset had a hidden data-loss edge case.
  - If the current entry re-selected the same managed project asset, the previous binding cleanup path could delete the managed file before re-adding it.
  - Resolution:
    - added an early-return guard in [ProjectAssetBindingService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Services/ProjectAssetBindingService.cs) so rebinding the same asset becomes a safe no-op result.
- Host-side graph synchronization risked creating a JSON/canvas feedback loop.
  - Resolution:
    - added a suppression flag in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs) so graph-canvas updates can refresh JSON and validation without immediately re-triggering a full rebind loop.
- Terminal-driven game launch was blocked by the current shell policy in this desktop environment.
  - Resolution:
    - kept this stage validated via `dotnet build` and `Stage03SmokeTest`
    - marked real shipped-game click validation as still pending

## Next Step
- Continue into a UI verification and refinement pass:
  - verify the new browser rows and asset tabs inside the running game
  - confirm the imported asset reuse flow is intuitive with real project data
  - stress test graph inspector edits and layout persistence inside the shipped game UI
- After that, the next best UX step is to split assets and large graph tooling into dedicated popup workspaces so the main editor stays focused on “select one entry, edit one entry”.

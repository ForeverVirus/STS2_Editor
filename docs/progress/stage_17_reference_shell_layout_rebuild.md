# Stage 17 - Reference Shell Layout Rebuild

## Date
- 2026-03-23

## Developed
- Re-cloned the reference repository to [references/sts2-CardArtEditor](/F:/sts2_mod/mod_projects/STS2_editor/references/sts2-CardArtEditor) and re-read its UI shell implementation directly from source.
- Mapped the reference project’s main shell structure to our editor using the following source files as the primary reference:
  - [SkinCenterRoot.cs](/F:/sts2_mod/mod_projects/STS2_editor/references/sts2-CardArtEditor/UIV2/SkinCenterRoot.cs)
  - [TopBarView.cs](/F:/sts2_mod/mod_projects/STS2_editor/references/sts2-CardArtEditor/UIV2/views/TopBarView.cs)
  - [ResourceListView.cs](/F:/sts2_mod/mod_projects/STS2_editor/references/sts2-CardArtEditor/UIV2/views/ResourceListView.cs)
  - [PreviewView.cs](/F:/sts2_mod/mod_projects/STS2_editor/references/sts2-CardArtEditor/UIV2/views/PreviewView.cs)
  - [DetailDrawerView.cs](/F:/sts2_mod/mod_projects/STS2_editor/references/sts2-CardArtEditor/UIV2/views/DetailDrawerView.cs)
  - [BottomBarView.cs](/F:/sts2_mod/mod_projects/STS2_editor/references/sts2-CardArtEditor/UIV2/views/BottomBarView.cs)
  - [PackBrowserWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/references/sts2-CardArtEditor/PackSystem/views/PackBrowserWindow.cs)
- Rebuilt the top-level Mod Studio shell in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - Replaced the older boxed layout with a full-screen backdrop + margin shell pattern closer to `SkinCenterRoot`.
  - Converted the root into:
    - top shell bar
    - expandable page area
    - bottom source-of-truth note bar
- Rebuilt the `Project Workspace` layout in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs) to follow the reference project’s shell split instead of the previous “single right-side tab pile”.
  - The workspace now uses a three-column shell:
    - left: entity browser
    - center: main stage
    - right: inspector drawer
  - Added a bottom action bar for save/export/remove actions and live status.
- Reassigned responsibilities across the workspace so the layout is much closer to the reference project’s interaction model.
  - Left column:
    - category switch
    - search
    - scope filter
    - entity list
  - Center stage:
    - `Overview`
    - `Metadata`
    - `Assets`
    - `Graph`
  - Right inspector drawer:
    - `Details`
    - `Graph Tools`
    - `Actions`
- Promoted the graph canvas to the main stage instead of leaving it buried under stacked presets and JSON panels.
  - The center `Graph` tab is now primarily the visual graph canvas.
  - Presets, validation, JSON, and graph toggles moved into the right-side inspector drawer.
- Added new inspector tab localization keys in [ModStudioLocalization.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Utilities/ModStudioLocalization.cs).
- Tightened root sizing behavior in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - `NModStudioScreen` now requests expand-fill sizing.
  - The top-level backdrop also requests expand-fill sizing.
  - This stage is intended to address the previous in-game symptom where the whole editor appeared squeezed into the upper-left quadrant.

## Validation
- Main mod build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Existing smoke test:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - Result: `PASS`

## Not Developed Yet
- The new shell structure is now reference-like, but `Project Home` and `Package Mode` have not yet been redesigned to the same degree as the main workspace.
- The asset system is still embedded inside the center-stage `Assets` tab.
  - It is clearer than before, but it is not yet broken out into a dedicated popup browser plus side drawer workflow like the reference project’s pack browser + picker drawer pairing.
- The graph area is now stage-first, but it still is not a full blueprint-style node authoring environment with:
  - palette drag-create
  - context-menu add node
  - zoom/minimap affordances tuned to large graphs
- This stage still has not produced a fresh in-game visual proof that the full-screen squeeze issue is completely resolved.
  - The code-side fix is now in place.
  - Final acceptance for that specific bug still depends on one more real game UI check.
- No fresh shipped-game visual verification was produced in this stage because the current terminal environment blocked direct external process launch for the game executable.

## Issues Encountered
- The reference repository folder existed in the workspace but was not visible in the earlier shallow listing, which initially looked like a missing clone.
  - Resolution:
    - rechecked with `-Force` and verified the repo contents before continuing
- The workspace rebuild briefly broke compilation because the new workspace builder replaced the original method body but left the method name as `BuildProjectWorkspacePanelLegacy`.
  - Resolution:
    - restored the live method name to `BuildProjectWorkspacePanel`
- Direct terminal-driven real-game launch was blocked again by the desktop shell policy.
  - Resolution:
    - validated this stage with build + smoke test
    - documented live-game visual verification as still pending

## Next Step
- Perform a dedicated in-game visual verification pass for the rebuilt shell:
  - confirm the workspace now occupies the expected full-screen area
  - confirm the left/center/right columns render with correct proportions
  - confirm the graph canvas is no longer visually buried below auxiliary panels
- After that, continue Stage 17 into the next reference-driven pass:
  - split package/asset browsing into dedicated popup or drawer workflows
  - refine the graph authoring experience toward a stronger node-editor interaction model

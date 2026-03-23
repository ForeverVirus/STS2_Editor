# Stage 18 - Reference Shell Port And Drawer Workspace

## Date
- 2026-03-23

## Developed
- Continued the reference-driven UI rewrite by aligning the editor shell more directly with the structure used in [SkinCenterRoot.cs](/F:/sts2_mod/mod_projects/STS2_editor/references/sts2-CardArtEditor/UIV2/SkinCenterRoot.cs).
- Reworked the root page host in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - Replaced the old horizontal `pages` container with an overlay-style `ModePageHost`.
  - `ProjectPage` and `PackagePage` now fill the same screen slot instead of competing for layout width.
  - Removed the always-visible bottom source-of-truth note bar from the main shell so the workspace can reclaim vertical space.
- Reworked `ProjectPage` in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - Removed the extra `Project Home / Project Workspace` step row from the page shell.
  - `ProjectHomePanel` and `ProjectWorkspacePanel` now live inside an overlay host instead of a vertical layout stack.
  - This avoids hidden panels continuing to distort workspace height.
- Rebuilt `ProjectWorkspacePanel` in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs) toward a much closer copy of the reference shell:
  - top-level workspace shell is now effectively:
    - left browser
    - center stage
    - right drawer
    - bottom action bar
  - removed the old workspace-specific top toolbar layer
  - tightened the left browser into a denser list-first layout
  - reduced redundant intro text that previously consumed list height
- Changed the right side from a persistent tab stack to a drawer-style inspector in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - The drawer now starts hidden.
  - It becomes visible only when a browser entity is selected.
  - The drawer width was reduced to reference-like proportions (`356px` minimum instead of `420px`).
- Reworked the center stage in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - `Overview` is now almost entirely the large preview stage.
  - `Metadata` now uses a split layout instead of one long stacked form.
  - `Assets` stays in the center stage, but its inner layout was widened and simplified.
  - `Graph` no longer keeps its graph controls inline in the center-stage header; those controls live in the drawer.
- Reworked the asset workspace in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - `Runtime Assets` and `Imported Assets` now use wider left-side list panes (`480px` and `520px` minimum).
  - Removed the extra explanatory blocks at the top of those tabs to give more height to the list and preview split.
  - Switched the asset tabs to fill-style layout so the asset lists can use the full stage height.
- Fixed an existing asset-details ownership bug in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - The center asset page and the right drawer were both fighting over `_assetDetails`.
  - Introduced a separate drawer binding details view and synchronized both through one helper.
- Tightened browser density in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - category row height reduced
  - list entry minimum height reduced from `64` to `52`
  - list buttons now left-align and wrap text by default
- Reworked [ModStudioGraphCanvasView.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioGraphCanvasView.cs) so the graph canvas reclaims the stage.
  - Removed the internal split that reserved a large visible inspector column inside the graph canvas itself.
  - The old node-inspector widgets are still instantiated in a hidden container for compatibility during this transition.
  - The visible result is a graph page that behaves like a real center canvas instead of a canvas plus embedded side list.

## Validation
- Main mod build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Smoke test:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - Result: `PASS`

## Not Developed Yet
- This stage intentionally focused on shell structure, not final visual polish.
  - spacing, typography, and color treatment still need another pass once the live in-game proportions are confirmed
- `Package Mode` still uses the older split layout and has not yet been ported to the same reference shell style.
- The right drawer is now drawer-like in layout behavior, but it is not yet animated or manually collapsible by a dedicated toggle.
- `Graph` is now much more stage-centric, but it is still not a full UE-style node authoring experience.
  - no right-click add-node menu
  - no drag-from-palette creation flow
  - no minimap
  - no dedicated node-inspector drawer yet
- External asset flows are clearer than before, but they still live inside the center stage instead of being split into a dedicated popup browser plus picker drawer pair like the reference project.
- No fresh shipped-game visual acceptance proof was produced in this stage because the current terminal environment still cannot directly launch the game executable for desktop UI validation.

## Issues Encountered
- The old layout bugs were not just sizing bugs.
  - hidden pages and hidden workspace panels were still being housed inside layout containers
  - this meant invisible screens could still distort width or height
  - resolution:
    - converted those switches to overlay-style hosts
- The graph canvas refactor initially broke compilation in [ModStudioGraphCanvasView.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioGraphCanvasView.cs).
  - cause:
    - the old inspector subtree was partially removed while duplicate local variables still existed
  - resolution:
    - kept the legacy node-inspector widgets in a hidden container so the stage can expand without breaking current graph editing hooks
- The asset binding details UI had an existing field collision in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - cause:
    - both the asset workspace and the inspector drawer were assigning the same `_assetDetails` field
  - resolution:
    - split the drawer details into a separate binding target and synchronized both displays through a helper

## Next Step
- Perform a real in-game visual validation pass focused on:
  - whether the project workspace now fills the expected full width
  - whether the left browser shows materially more entries per screen
  - whether the right drawer now behaves like a true side drawer instead of a permanent third column
  - whether the asset page runtime/imported lists are finally wide enough to use comfortably
  - whether the graph tab now reads as a center canvas first
- After that, continue the reference port with a second shell pass:
  - port `Package Mode` to the same top/main/bottom shell
  - add an explicit drawer toggle
  - split assets into a dedicated picker/browser flow
  - move graph node properties into a proper external drawer instead of the temporary hidden legacy container

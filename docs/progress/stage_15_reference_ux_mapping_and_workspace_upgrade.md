# Stage 15 - Reference UX Mapping And Workspace Upgrade

## Date
- 2026-03-23

## Developed
- Reviewed the reference project [sts2-CardArtEditor](https://github.com/2145057603/sts2-CardArtEditor) with both:
  - direct GitHub inspection via Playwright
  - local source inspection after cloning to [references/sts2-CardArtEditor](/F:/sts2_mod/mod_projects/STS2_editor/references/sts2-CardArtEditor)
- Extracted the most valuable UX conclusion from the reference project:
  - the primary editor must stay focused on single-entity editing
  - asset/package management should be split into side flows or separate windows
  - the main workspace should prioritize:
    - clear searchable list
    - large preview area
    - separate detail inspector
    - compact global actions
- Upgraded the Mod Studio workspace in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - Reduced the outer margin from a boxed layout to near full screen, giving the editor significantly more usable area.
  - Replaced the browser button stack with a real `ItemList` browser.
  - Added a browser summary row so the user can immediately see how many entries exist in the current category.
  - Moved category switching and search into a more toolbar-like workspace control row.
  - Added a large overview preview panel to the overview tab instead of showing only text details.
- Improved the asset editing area in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - The asset workflow is now visually split into:
    - left side runtime asset catalog
    - right side large asset preview
    - top action row for runtime binding, manual path apply, external import, clear override
  - This is a step toward the reference project's “asset browser separate from main entity list” pattern.
- Added a reusable visual graph component in [ModStudioGraphCanvasView.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioGraphCanvasView.cs).
  - Renders `BehaviorGraphDefinition` using `GraphEdit` + `GraphNode`
  - Shows nodes and connections visually
  - Supports drag repositioning
  - Writes node layout back into `BehaviorGraphDefinition.Metadata`
  - Includes a right-side selected node detail panel
- Integrated the visual graph canvas into the Graph tab in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - Graph tab now contains:
    - graph toggle
    - graph id
    - presets
    - visual graph canvas
    - advanced JSON
    - validation
    - node catalog
  - The graph canvas refreshes when:
    - selected entity changes
    - graph JSON changes
    - graph id changes
  - Save flow now exports layout metadata from the canvas before persisting the graph.
- Fixed the main project build to safely keep the reference repository in the workspace.
  - Added `references\**\*.cs` exclusion in [STS2_Editor.csproj](/F:/sts2_mod/mod_projects/STS2_editor/STS2_Editor.csproj)
  - This prevents cloned reference sources from being compiled into the main Mod project.

## Validation
- Main mod build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Existing smoke test:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - Result: `PASS`
- Shipped-game startup smoke:
  - launched `F:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe`
  - let the game reach main menu
  - terminated intentionally after smoke capture
  - checked [godot.log](C:/Users/Administrator/AppData/Roaming/SlayTheSpire2/logs/godot.log)
  - observed:
    - `Mod Studio main menu entry attached.`
    - no startup-time crash introduced by the workspace upgrade or graph canvas integration

## Not Developed Yet
- The current entity browser is now a real list, but it is still a generic `ItemList`.
  - It does not yet render richer custom rows like:
    - name
    - type
    - rarity
    - owner / pool
    - replacement / override state badges
- Asset management is improved, but it is not yet split into a fully independent “asset package browser window” plus “quick picker drawer” like the reference project.
- The graph editor is now visual, but it is still early-stage.
  - It visualizes nodes and connections.
  - It persists layout.
  - It does not yet provide full palette-based node creation, drag-to-create-node, or structured node property editing.
- No automated in-game click-through was performed for:
  - entering Mod Studio
  - selecting categories
  - verifying the new browser list visually inside the running game
  - dragging graph nodes live inside the shipped game

## Issues Encountered
- Cloning the reference repo into the workspace immediately polluted the main build because the Godot project compiles `*.cs` by wildcard.
  - Resolution:
    - exclude `references/**` in the main project file
- The first version of the new graph canvas hit Godot API differences.
  - `GraphEdit.AllowNodeSelection` and `GetHoveredNode()` were not available in this runtime binding.
  - Resolution:
    - removed unsupported API usage
    - replaced empty-space deselection with a local hit test over node rectangles
- The reference project was helpful for workflow structure, but not all of its implementation should be copied directly.
  - Its left list is actually lower information density than what Mod Studio needs.
  - The best reusable value was its workflow separation, not its exact row rendering.

## Next Step
- Continue Stage 15 into a deeper workspace pass:
  - upgrade the browser list from generic `ItemList` to richer custom rows
  - split assets into:
    - full asset browser window
    - quick asset picker
  - add structured graph node property editing next to the visual graph canvas
- After that, run a focused in-game manual verification pass specifically for:
  - cards
  - relics
  - potions
  - graph node drag layout persistence

# Stage 03 - Graph Authoring UI And Terminal Smoke Tests

## Date
- 2026-03-23

## Developed
- The in-game graph area is now a usable lightweight authoring surface instead of only raw JSON editing.
  - `Quick Presets` was added to the project editor for one-click starter graphs.
  - `Graph Validation` now shows live validation status, errors, and warnings for the current graph JSON.
  - `Node Catalog` now renders the active `GraphRegistry.Definitions` so the editor UI reflects the real runtime node surface.
- A shared graph preset layer was added so UI and verification tools use the same source of truth.
  - Added `BehaviorGraphTemplateDescriptor`.
  - Added `BehaviorGraphTemplateFactory`.
  - The UI helper now wraps the shared template factory instead of keeping an unrelated duplicate preset table.
- The graph editor flow in `NModStudioScreen` was extended to:
  - apply a scaffold graph or preset graph to the currently selected runtime entity
  - turn on graph behavior automatically when a preset is applied
  - save the generated graph into the current project
  - keep graph validation panels refreshed while editing
- A standalone terminal smoke-test project was added:
  - `tools/Stage03SmokeTest/Stage03SmokeTest.csproj`
  - `tools/Stage03SmokeTest/Program.template`
- The smoke test now validates these non-UI workflows end to end:
  - graph registry and graph validation
  - project JSON roundtrip
  - `.sts2pack` export/import roundtrip
  - package load-order override precedence
  - multiplayer session negotiation intersection
- Main project build stability was restored after the new tooling landed.
  - `tools/**` is now excluded from the main mod compile item set.
  - duplicate assembly-attribute generation was fixed in `STS2_Editor.csproj`.

## Validation
- Main mod build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Stage 03 smoke-test build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj`
  - Result: success
- Stage 03 smoke-test execution:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - Result: `PASS`
- Smoke-test highlights:
  - `Definitions: 17, Executors: 17`
  - graph validation pass/fail behavior is working
  - package order `AB` vs `BA` changes the winning override as expected
  - multiplayer negotiation disables packages missing on peers as expected

## Not Developed Yet
- There is still no visual node editor; graph authoring is still template-assisted plus JSON editing.
- There is not yet an automated in-game combat smoke test that launches a run and proves:
  - a graph-driven card effect executes in combat
  - a graph-driven potion executes in combat
  - a relic or enchantment graph hook executes during hook dispatch
- External asset import and preview workflow is still incomplete in the editor UI.
- Event graph/runtime override coverage is still incomplete.
- New content injection into `ModelDb`, card pools, relic pools, potion pools, and event routing is still not finished.
- Monster encyclopedia/editor, Spine replacement, and custom event scene layout remain out of scope for the current stage.

## Issues Encountered
- The parallel UI branch initially introduced a second preset definition surface.
  - Resolution:
    - add a shared `BehaviorGraphTemplateFactory`
    - make UI helpers wrap the shared factory instead of duplicating graph presets
- The main mod project started compiling the Stage 03 smoke-test console sources.
  - Resolution:
    - exclude `tools\**\*.cs` from `STS2_Editor.csproj`
- The Godot SDK and the default .NET SDK both generated assembly-level metadata, which caused duplicate attribute errors.
  - Resolution:
    - disable the extra default assembly info generation in `STS2_Editor.csproj`
    - keep the Godot build output path as the authoritative mod assembly output
- Existing runtime/graph files are still mostly untracked in the current worktree.
  - This is a repository hygiene issue, not a runtime blocker, but it should be cleaned up before later stages grow larger.

## Next Step
- Stage 04 should move from “authoring and backend smoke” into “real gameplay proof”.
- Recommended Stage 04 targets:
  - create one controlled in-game sample project that overrides a known card with a graph preset
  - create one controlled in-game sample project that overrides a known potion with a graph preset
  - verify the graph-driven behavior in a real run
  - capture the exact tested entity IDs, expected behavior, and observed outcome in a new stage document
- After that, continue into:
  - external asset import and preview
  - runtime event behavior coverage
  - new content injection
  - package conflict UX and multiplayer live-session verification

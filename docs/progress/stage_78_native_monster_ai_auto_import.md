# Stage 78 - Native Monster AI Auto-Import

## Completed

- Added [NativeMonsterAiImporter.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/NativeMonsterAiImporter.cs) as the Stage 78 monster-specific FSM importer.
- Extended [NativeBehaviorGraphAutoImporter.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/NativeBehaviorGraphAutoImporter.cs) with a method-level import entry so monster move `_onPerform` delegates can reuse the existing IL-to-graph path.
- Extended [NativeBehaviorAutoGraphService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/NativeBehaviorAutoGraphService.cs) with monster AI orchestration.
- Extended [EntityEditorViewCache.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Models/EntityEditorViewCache.cs) with auto-imported monster AI / graph caches.
- Wired monster AI auto-import into [NModStudioProjectWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.cs) and [NModStudioProjectWindow.Tail.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.Tail.cs):
  - when a monster has no authored `MonsterAi`, the editor now attempts a native import
  - imported move graphs are cached and can be promoted into project graphs when the user edits/saves
- Updated [BehaviorGraphPaletteFilter.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/BehaviorGraphPaletteFilter.cs) so monster move graphs can actually edit allowed node families.

## Validation

- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`

## Notes

- This is the Stage 78 minimum path, not the final fidelity target from the plan. The importer currently focuses on:
  - extracting FSM structure into `MonsterAiDefinition`
  - translating move delegates through the existing native graph importer
- Conditional branch lambdas are no longer left as blanket `native_condition_N` placeholders in the shipped path. The importer resolves the current native conditional branches into editor-visible expressions, and Stage 81 now hard-fails if any placeholder condition reappears.
- Imported monsters now populate the dedicated `Turns` catalog and phase graph together, so hybrid/random/conditional native FSM targets no longer depend on `OpeningTurns` as an implicit shared registry.

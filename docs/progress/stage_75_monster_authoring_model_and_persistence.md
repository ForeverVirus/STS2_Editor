# Stage 75 - Monster Authoring Model And Persistence

## Completed

- Added the Stage 75 monster authoring DTO layer in [MonsterAiDefinition.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Models/MonsterAiDefinition.cs), including:
  - `MonsterAiDefinition`
  - reusable turn catalog, opening turns, loop phases, and phase branches
  - state variables, lifecycle hooks, event triggers, intent declarations
  - Stage 75 enums and clone helpers
- Extended [EntityDraft.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Models/EntityDraft.cs) with `MonsterAiDraft`.
- Extended [EntityOverrideEnvelope.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Models/EntityOverrideEnvelope.cs) with `MonsterAi`.
- Wired `MonsterAi` through project/package/runtime clone paths so the field is not dropped during duplication, packaging, or runtime resolution:
  - [EditorProjectStore.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/EditorProjectStore.cs)
  - [PackageArchiveService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/Packaging/PackageArchiveService.cs)
  - [RuntimeOverrideResolver.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeOverrideResolver.cs)
  - [RuntimePackageCatalog.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimePackageCatalog.cs)
  - [AiEditExecutor.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/AI/AiEditExecutor.cs)
- Added monster browsing and basic metadata editing support:
  - [ModelMetadataService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Services/ModelMetadataService.cs)
  - [ModStudioEntityBrowserPanel.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioEntityBrowserPanel.cs)
  - [ModStudioFieldDisplayNames.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioFieldDisplayNames.cs)
  - [FieldChoiceProvider.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/FieldChoiceProvider.cs)
  - [NModStudioProjectWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.cs)
  - [NModStudioProjectWindow.Tail.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.Tail.cs)
  - [NModStudioProjectWindow.Support.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.Support.cs)
- Monster now appears as a first-class browser tab.
- Monster basic fields currently exposed in Project Mode:
  - `title`
  - `min_initial_hp`
  - `max_initial_hp`
- `New Entry` is now disabled in the browser when `Monster` is selected, matching the Stage 75 gate.

## Validation

- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`

## Notes

- Stage 75 intentionally does not enable graph editing or monster runtime override execution yet. Monster currently participates in the browser/project/persistence layer only.
- `MonsterAiDefinition` is wired into the standard JSON serializer path through `EntityOverrideEnvelope`. A dedicated monster-specific roundtrip harness has not been added yet; that can be folded into later proof tooling if needed.
- The delivered model now keeps loop-targetable turns in a dedicated `Turns` catalog so phase references are not restricted to the opening-turn sequence.

# Stage 77 - Monster Editor UI

## Completed

- Added the dedicated monster AI editor controls:
  - [ModStudioMonsterAiEditor.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/MonsterAi/ModStudioMonsterAiEditor.cs)
  - [ModStudioMonsterTurnEditor.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/MonsterAi/ModStudioMonsterTurnEditor.cs)
  - [ModStudioMonsterPhaseEditor.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/MonsterAi/ModStudioMonsterPhaseEditor.cs)
- Extended [ModStudioCenterEditor.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioCenterEditor.cs) with a dedicated `Monster AI` tab.
- Wired the monster editor into [NModStudioProjectWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.cs) and [NModStudioProjectWindow.Tail.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.Tail.cs):
  - monster-specific dirty/save/revert flow
  - monster move graph selection context
  - `Edit Graph` from a move switches into the existing graph tab and edits that move graph
  - monster graph saves update the selected move's `GraphId` instead of using `EntityOverrideEnvelope.GraphId`
- Kept the existing graph canvas/inspector path for monster move graphs by routing move-level graph editing through the normal `Graph` tab.

## Validation

- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`

## Notes

- This is a functional Stage 77 baseline, not the final polished UX from the plan. It supports authored turns/phases/state/hooks/triggers and move-level graph editing, but does not yet include richer reorder UX, drag handles, or intent icon polish.
- The structure tab now exposes both `Opening Turns` and a reusable `Turns` catalog so imported or authored loop phases can target turn ids that are not consumed as one-shot combat openings.
- Monster AI editing currently keeps unsaved structure changes live while the user switches between the `Monster AI` tab and the `Graph` tab for the same entity.

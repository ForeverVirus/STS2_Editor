# Stage 79 - Monster Nodes And Executors

## Completed

- Added `monster.*` node definitions to [BuiltInBehaviorNodeDefinitionProvider.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/BuiltInBehaviorNodeDefinitionProvider.cs):
  - `monster.attack`
  - `monster.gain_block`
  - `monster.apply_power`
  - `monster.heal`
  - `monster.summon`
  - `monster.talk`
  - `monster.inject_status_card`
  - `monster.set_state`
  - `monster.get_state`
  - `monster.check_state`
  - `monster.animate`
  - `monster.play_sfx`
  - `monster.remove_player_card`
  - `monster.check_ally_alive`
  - `monster.count_allies`
  - `monster.force_transition`
- Added the matching executor registrations and implementations in [BuiltInBehaviorNodeExecutors.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/BuiltInBehaviorNodeExecutors.cs).
- Extended [RuntimeMonsterDispatcher.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeMonsterDispatcher.cs) with force-transition support used by `monster.force_transition`.
- Extended inspector labels in [ModStudioFieldDisplayNames.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioFieldDisplayNames.cs) for the new monster-node properties.
- Extended [GraphDescriptionGenerator.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/GraphDescriptionGenerator.cs) and [GraphDescriptionTemplateGenerator.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/GraphDescriptionTemplateGenerator.cs) so the new monster nodes participate in auto-description generation.

## Validation

- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`

## Notes

- Existing generic `combat.* / creature.* / value.*` nodes still remain usable for monster graphs; Stage 79 adds explicit `monster.*` nodes mainly for semantics, palette clarity, and monster-specific runtime actions that were missing before.
- `monster.attack` currently uses a hybrid path: it uses the monster attack command for multi-target attacks and a direct creature-damage path for single-target cases.

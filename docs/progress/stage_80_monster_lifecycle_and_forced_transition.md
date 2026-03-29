# Stage 80 - Monster Lifecycle And Forced Transition Parity

## Completed

- Extended [RuntimeMonsterPatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeMonsterPatches.cs) with monster lifecycle patch points for:
  - `CombatManager.RemoveCreature(...)`
  - `Creature.InvokeDiedEvent()`
  - `MonsterModel.OnDieToDoom()`
  - `Hook.AfterCurrentHpChanged(...)`
  - `Creature.StunInternal(...)`
- Extended [RuntimeMonsterDispatcher.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeMonsterDispatcher.cs) with:
  - before-removed / before-death / on-die-to-doom / after-current-hp-changed hook execution
  - forced phase transition support
  - stun-resume support through `ResumeTurnId`
- Extended [MonsterRuntimeState.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/MonsterRuntimeState.cs) with resume-turn tracking.
- Added [MonsterLifecycleManager.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/MonsterLifecycleManager.cs) to centralize ally-death / hp-changed event trigger handling and lifecycle graph execution.

## Validation

- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`

## Notes

- Stage 80 is now structurally wired end to end, but there is still no dedicated monster combat proof harness covering Queen/TestSubject/WaterfallGiant lifecycle parity in combat.
- `ally death` and `hp changed` event triggers currently execute through the shared monster lifecycle manager rather than piggybacking on the relic/enchantment hook dispatcher.

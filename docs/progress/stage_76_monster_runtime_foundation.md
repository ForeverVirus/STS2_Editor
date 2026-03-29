# Stage 76 - Monster Runtime Foundation

## Completed

- Added monster runtime state storage in [MonsterRuntimeState.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/MonsterRuntimeState.cs).
- Added authored monster turn/phase selector in [MonsterGraphStateMachine.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/MonsterGraphStateMachine.cs).
- Added monster runtime dispatcher in [RuntimeMonsterDispatcher.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeMonsterDispatcher.cs).
- Added Harmony patch entry points in [RuntimeMonsterPatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeMonsterPatches.cs) for:
  - `MonsterModel.SetUpForCombat()`
  - `MonsterModel.RollMove(...)`
  - `MonsterModel.PerformMove()`
  - `CombatManager.AfterCreatureAdded(...)`
- Extended [BehaviorGraphExecutionContext.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/BehaviorGraphExecutionContext.cs) with:
  - `Monster`
  - `MonsterCreature`
  - `MonsterState`
  - monster-aware `SourceCreature`
  - monster-side reference resolution and default target resolution
- Updated [BuiltInBehaviorNodeExecutors.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/BuiltInBehaviorNodeExecutors.cs) so the key combat/power paths use `context.SourceCreature` instead of assuming `context.Owner?.Creature`.
- Extended [RuntimeGraphDispatcher.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeGraphDispatcher.cs) with monster-facing shared helpers:
  - `TryGetMonsterOverride(...)`
  - `TryGetResolvedGraph(...)`
  - `ResolveGraphEntryNode(...)`
  - `ExecuteResolvedGraphAsync(...)`

## Validation

- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`

## Notes

- This is the runtime foundation only. The dedicated monster editor UI is still pending in Stage 77.
- The current authored loop-phase implementation resolves `TargetTurnId` against the Stage 75 turn catalog currently stored in `OpeningTurns`; if the model evolves to a separate reusable turn pool later, Stage 76 runtime selection logic will need a matching update.
- No dedicated monster combat proof harness has been added yet. Existing validation currently proves that the new runtime code compiles and does not regress Stage03 smoke coverage.

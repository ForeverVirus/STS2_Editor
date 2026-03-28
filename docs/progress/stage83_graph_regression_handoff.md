# Stage 83 Graph Regression Handoff

## Goal
- Build a repeatable full-regression harness for auto-generated graph mutations across:
  - `Card`
  - `Relic`
  - `Potion`
  - `Event`
  - `Enchantment`
- For each supported auto-graph entity:
  - mutate one editable graph node field,
  - export a package,
  - install / hot-reload it,
  - start a real run,
  - verify the mutation in-game,
  - write per-case `json`,
  - write per-failure `md`.

## Current Status
- `Manifest generation`: working
- `Case preparation (.sts2pack)`: working
- `Card sample execution`: working
- `Relic sample execution`: working
- `Potion sample execution`: working
- `Small relic batch execution`: working
- `Event forced-entry`: partially working
- `Enchantment carrier flow`: not implemented
- `Single-process hot-reload batch loop`: partially implemented, not yet fully wired into Stage83 runner

## Working Outputs
- Manifest JSON:
  - [graph_regression_manifest.json](/F:/sts2_mod/mod_projects/STS2_editor/coverage/graph-regression/graph_regression_manifest.json)
- Manifest summary:
  - [graph_regression_manifest.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/reference/graph_regression_manifest.md)
- Current regression auto-failure folder:
  - [auto](/F:/sts2_mod/mod_projects/STS2_editor/docs/regressions/auto)

## Current Scale
- Current manifest size:
  - `1011` entity graphs
  - `1908` mutation cases
- Supported sample kinds already proven:
  - `Card`
  - `Relic`
  - `Potion`

## Main Files

### Core regression tool
- [Program.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage83GraphRegression/Program.cs)
- [Stage83GraphRegression.csproj](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage83GraphRegression/Stage83GraphRegression.csproj)

### Runtime fixes already landed
- [RuntimeGraphDispatcher.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeGraphDispatcher.cs)
- [RuntimeGraphPatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeGraphPatches.cs)
- [BuiltInBehaviorNodeExecutors.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/BuiltInBehaviorNodeExecutors.cs)

### Event proof plumbing
- [RuntimeProofHarness.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeProofHarness.cs)
- [RuntimeProofHarnessPatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeProofHarnessPatches.cs)

### Menu control mod
- [MenuActionService.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/external/STS2-MenuControl/MenuActionService.cs)
- [MenuStateService.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/external/STS2-MenuControl/MenuStateService.cs)
- [STS2MenuControl.csproj](/F:/sts2_mod/mod_projects/STS2_editor/tools/external/STS2-MenuControl/STS2MenuControl.csproj)

### Game MCP state/action bridge
- [McpMod.StateBuilder.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/external/STS2MCP/McpMod.StateBuilder.cs)
- [McpMod.Actions.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/external/STS2MCP/McpMod.Actions.cs)

## Commands That Work

### Generate manifest
```powershell
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- .
```

### Prepare a single case
```powershell
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- --prepare-case "relic::CRACKED_CORE::orb_channel_0::property::orb_id" .
```

### Run a single sample case
```powershell
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- --run-prepared-case "relic::CRACKED_CORE::orb_channel_0::property::orb_id" .
```

### Run tiny sample batches
```powershell
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- --run-batch Relic --limit 2 --offset 0 .
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- --run-batch Card --limit 1 --offset 0 .
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- --run-batch Potion --limit 1 --offset 0 .
```

## Verified Good Cases

### Relic sample
- Mutation:
  - `relic::CRACKED_CORE::orb_channel_0::property::orb_id`
- Workspace:
  - [prepared_case.json](/F:/sts2_mod/mod_projects/STS2_editor/coverage/graph-regression/workspace/relic__cracked_core__orb_channel_0__property__orb_id/prepared_case.json)
- Result:
  - [run_result.json](/F:/sts2_mod/mod_projects/STS2_editor/coverage/graph-regression/workspace/relic__cracked_core__orb_channel_0__property__orb_id/run_result.json)
- Observed:
  - mutation reached live combat,
  - `FROST_ORB` was present in the player orb bar,
  - case returned `success=true`.

### Card sample
- Mutation:
  - `card::ABRASIVE::combat_apply_power_0__0::property::amount`
- Failure report was initially produced due runner logic bug:
  - [card__abrasive__combat_apply_power_0__0__property__amount.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/regressions/auto/card__abrasive__combat_apply_power_0__0__property__amount.md)
- After fixing the `card_select` flow assumption, the sample batch passed.

### Potion sample
- Mutation:
  - first potion sample batch passed using starting potion injection and in-combat validation.

## Important Runtime Fix Already Completed

### Cracked Core Frost freeze
- Dedicated note:
  - [cracked_core_frost_combat_freeze.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/regressions/cracked_core_frost_combat_freeze.md)
- Root cause summary:
  - `trigger.default` was incorrectly honored even when `allowDefaultFallback: false`.
  - This caused hook graphs to execute in unrelated paths.
  - Result was repeated `orb.channel` execution and combat freeze.
- This is fixed and validated in real combat.

## Event Work: What Is Already True

### Forced event entry is now happening
- The game log shows:
  - `[ModStudio.Proof] Entering proof event 'ABYSSAL_BATHS'`
  - `[ModStudio.Event] Initializing template event ABYSSAL_BATHS -> page INITIAL`
  - `[ModStudio.Event] Applied template page ABYSSAL_BATHS:INITIAL options=2`
  - sometimes also:
    - `[ModStudio.Event] Executing option ABYSSAL_BATHS:INITIAL:ABSTAIN`
    - `[ModStudio.Event] Applied template page ABYSSAL_BATHS:ABSTAIN options=0`

### Forced event entry paths currently present
- Startup-arg proof path:
  - `--modstudio-proof-event=...`
- Menu-control explicit action:
  - `force_enter_event`

## Event Work: Current Problem
- The runner still records event sample as failure even though logs prove the target event was entered.
- Current failure file:
  - [event__abyssal_baths__event_option_1__property__title.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/regressions/auto/event__abyssal_baths__event_option_1__property__title.md)

### Most likely reason
- The event validation branch in [Program.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage83GraphRegression/Program.cs) is not yet consuming the correct returned state at the correct moment.
- There is evidence of state mismatch / stale failure recording:
  - logs show event initialization and page execution,
  - but `run_result.json` still reports timeout waiting for forced event.

### What to inspect first
1. [Program.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage83GraphRegression/Program.cs)
   - `AdvanceIntroToMap()`
   - `WaitForEventState()`
   - `RunPreparedCase()`
   - `RunPreparedCaseInSession()`
2. Confirm whether event branch is actually using:
   - `PostMenuAction("force_enter_event", ...)`
   - then `WaitForEventState(...)`
3. Add temporary logging inside Stage83 runner, not only game log, to print:
   - every sampled `state_type`
   - `event.event_id`
   - current `event.options[*].title`
4. Confirm `run_result.json` is overwritten on the latest attempt and not reading an older file.

## Enchantment Work: Not Yet Implemented

### Current state
- `Enchantment` is in the manifest.
- No real execution driver exists yet.
- No carrier synthesis exists yet.

### Recommended approach
- Do not rely on native random sources to discover carriers.
- Synthesize a test carrier card graph:
  - create a temporary custom card,
  - on play, use `card.enchant` with target `self`,
  - inject that card into starting deck,
  - play it in first combat,
  - then verify the mutated enchantment effect or state.

### Relevant code
- Node definition:
  - [BuiltInBehaviorNodeDefinitionProvider.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/BuiltInBehaviorNodeDefinitionProvider.cs)
- Execution:
  - [BuiltInBehaviorNodeExecutors.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/BuiltInBehaviorNodeExecutors.cs)
- Card targeting resolution:
  - [BuiltInBehaviorNodeExecutors.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/BuiltInBehaviorNodeExecutors.cs)
  - `ResolveCards(...)`

## Hot Reload Work: Partially Implemented

### Added menu control actions
- `hot_reload_modstudio_packages`
- `abandon_current_run`
- `force_enter_event`

### Why this matters
- Full regression cannot finish if every case restarts the game.
- The intended next step is:
  1. keep one game process alive for `Card/Relic/Potion`,
  2. switch package file in published root,
  3. update session.json,
  4. call `hot_reload_modstudio_packages`,
  5. abandon current run,
  6. start next run,
  7. validate next case.

### Current batch state
- [Program.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage83GraphRegression/Program.cs) already contains:
  - `RuntimeRegressionSession`
  - `RunStartingScenarioBatch(...)`
- This needs to be finished and trusted before running large card/relic/potion sweeps.

## Known Weak Points In Current Runner
- `run_result.json` correctness for event branch is not yet trustworthy.
- Some old failure `md` files reflect runner bugs that were already fixed later.
- Current card/relic/potion validation is intentionally shallow:
  - enough for smoke + mutation reachability,
  - not yet enough for all semantic correctness.

## Recommended Next Steps For Opus 4.6

1. Finish event runner first.
   - Make `WaitForEventState()` authoritative.
   - Validate mutated event option/page text directly from returned `event.options`.
   - Re-run the `ABYSSAL_BATHS` sample until `success=true`.

2. Finish single-process batch loop for `Card/Relic/Potion`.
   - Reuse one game process.
   - Use `hot_reload_modstudio_packages`.
   - Use `abandon_current_run`.
   - Then start expanding batch size.

3. Implement enchantment carrier synthesis.
   - Build temporary custom carrier card package per enchantment case.
   - Inject into starting deck.
   - Use in first combat.
   - Verify mutated enchantment state/effect.

4. Only after event + enchantment are green:
   - run real batches by kind,
   - keep writing failure `md` per case.

## Useful Existing Failure Reports
- [cracked_core_frost_combat_freeze.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/regressions/cracked_core_frost_combat_freeze.md)
- [card__abrasive__combat_apply_power_0__0__property__amount.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/regressions/auto/card__abrasive__combat_apply_power_0__0__property__amount.md)
- [event__abyssal_baths__event_option_1__property__title.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/regressions/auto/event__abyssal_baths__event_option_1__property__title.md)

## Bottom Line
- `Card / Relic / Potion` automation is real and already running.
- `Event` is no longer blocked on entry; it is blocked on final state capture / assertion.
- `Enchantment` is still missing a carrier execution path.
- Full regression is not complete yet.

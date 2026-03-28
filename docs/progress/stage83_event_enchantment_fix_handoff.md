# Stage 83 Event / Enchantment Fix Handoff

## Current Truth

### Already working
- `Card` sample runner: pass
- `Relic` sample runner: pass
- `Potion` sample runner: pass
- `Small Relic batch`: pass

### Not working yet
- `Event` sample runner: still fails
- `Enchantment` sample runner: still fails with real runtime errors

This means the automation foundation is real, but `Event` and `Enchantment` are still unfinished.

## Main Goal
- Make `Event` sample cases return `PASS`
- Make `Enchantment` sample cases return `PASS`
- Then continue full regression from the existing Stage83 harness

## Key Files

### Regression harness
- [Program.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage83GraphRegression/Program.cs)
- [Stage83GraphRegression.csproj](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage83GraphRegression/Stage83GraphRegression.csproj)

### Runtime event proof path
- [RuntimeProofHarness.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeProofHarness.cs)
- [RuntimeProofHarnessPatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeProofHarnessPatches.cs)

### Menu control mod
- [MenuActionService.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/external/STS2-MenuControl/MenuActionService.cs)
- [MenuStateService.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/external/STS2-MenuControl/MenuStateService.cs)
- [STS2MenuControl.csproj](/F:/sts2_mod/mod_projects/STS2_editor/tools/external/STS2-MenuControl/STS2MenuControl.csproj)

### Game MCP raw state/action side
- [McpMod.StateBuilder.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/external/STS2MCP/McpMod.StateBuilder.cs)
- [McpMod.Actions.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/external/STS2MCP/McpMod.Actions.cs)

## Event: What Is Already Confirmed
- The target event is being force-entered successfully.
- Log evidence already shows:
  - `[ModStudio.Proof] Entering proof event 'ABYSSAL_BATHS'`
  - `[ModStudio.Event] Initializing template event ABYSSAL_BATHS -> page INITIAL`
  - `[ModStudio.Event] Applied template page ABYSSAL_BATHS:INITIAL options=2`
  - later sometimes:
    - `[ModStudio.Event] Executing option ABYSSAL_BATHS:INITIAL:ABSTAIN`
    - `[ModStudio.Event] Applied template page ABYSSAL_BATHS:ABSTAIN options=0`

So the blocker is no longer "cannot enter event". It is now "runner does not turn the entered event into a PASS result."

## Event: Current Failure Record
- [event__abyssal_baths__event_option_1__property__title.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/regressions/auto/event__abyssal_baths__event_option_1__property__title.md)

## Event: Suspected Root Problem
- The event force-enter flow now succeeds, but the Stage83 runner still fails to capture the returned event state correctly.
- There are likely stale assumptions in:
  - `AdvanceIntroToMap()`
  - `WaitForEventState()`
  - the event branch inside `RunPreparedCase()` / `RunPreparedCaseInSession()`
- A previous issue also existed where `STS2_MCP` used `event.event_id` but the runner looked for `id`.
- That key mismatch was already corrected, but event sample still does not resolve to `PASS`, so there is still a timing / state-capture issue.

## Event: Concrete Advice
1. Add explicit temporary logging inside Stage83 runner for:
   - every observed `state_type`
   - every observed `event.event_id`
   - every observed option title
2. Verify that after `force_enter_event`:
   - `GET http://localhost:15526/api/v1/singleplayer?format=json`
   - eventually returns `state_type == event`
3. If `MCP` still returns a non-event state while logs show event room initialization:
   - inspect whether `NMapScreen` / overlay state is still masking event state
   - inspect `tools/external/STS2MCP/McpMod.StateBuilder.cs`
4. Once event state is captured reliably:
   - `targetPresent` should compare `event.event_id == ABYSSAL_BATHS`
   - `mutationObserved` should look for the mutated string, e.g. `Exit Baths [RG]`

## Enchantment: Current Failure
- Latest failure record:
  - [enchantment__adroit__combat_gain_block_0__0__property__amount.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/regressions/auto/enchantment__adroit__combat_gain_block_0__0__property__amount.md)

## Enchantment: Current Runtime Errors
Observed in logs during sample run:
- `LocException: Key=STRIKE_R.title not found in table=cards`
- `InvalidOperationException: You monster!`
- stack points into:
  - `MockCardPool`
  - `MockCardModel.MockCanonical()`
  - `NCardGrid`

## Enchantment: Interpretation
- The current enchantment sample path is synthesizing or resolving a temporary card/carrier in a way that drags the mock card pool / mock canonical path into normal runtime UI.
- That is not safe for the shipped-game UI path.

## Enchantment: Concrete Advice
1. Inspect how Stage83 currently generates the enchantment carrier package.
2. Do not use a mock-card-pool-backed card for real runtime validation.
3. Prefer one of these:
   - use a real canonical base card as the carrier and override it via graph
   - or create a dynamic card with complete localization / metadata so UI lookups do not explode
4. Validate that:
   - the carrier appears in the starting deck
   - it can be played in first combat
   - the target enchantment mutation can be observed without touching `MockCardPool`

## How To Build

### Build main mod
```powershell
dotnet build .\STS2_Editor.csproj -c Debug
```

### Build menu control mod
```powershell
$env:STS2GameDir='F:\SteamLibrary\steamapps\common\Slay the Spire 2'
dotnet build .\tools\external\STS2-MenuControl\STS2MenuControl.csproj -c Release
```

### Copy menu control mod into game
```powershell
$modDir='F:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\STS2MenuControl'
New-Item -ItemType Directory -Path $modDir -Force | Out-Null
Copy-Item '.\tools\external\STS2-MenuControl\bin\Release\net9.0\STS2MenuControl.dll' $modDir -Force
Copy-Item '.\tools\external\STS2-MenuControl\mod_manifest.json' (Join-Path $modDir 'STS2MenuControl.json') -Force
```

### Build regression harness
```powershell
dotnet build .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -c Debug
```

## How To Run Tests

### Regenerate manifest
```powershell
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- .
```

### Run event sample
```powershell
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- --run-batch Event --limit 1 --offset 0 .
```

### Run enchantment sample
```powershell
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- --run-batch Enchantment --limit 1 --offset 0 .
```

### Re-run card/relic/potion sanity checks
```powershell
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- --run-batch Card --limit 1 --offset 0 .
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- --run-batch Relic --limit 2 --offset 0 .
dotnet run --project .\tools\Stage83GraphRegression\Stage83GraphRegression.csproj -- --run-batch Potion --limit 1 --offset 0 .
```

## Where To Read Results

### Failure markdowns
- [auto](/F:/sts2_mod/mod_projects/STS2_editor/docs/regressions/auto)

### Per-case workspaces
- [workspace](/F:/sts2_mod/mod_projects/STS2_editor/coverage/graph-regression/workspace)

Each case workspace contains:
- `prepared_case.json`
- exported `.sts2pack`
- `run_result.json`

## Recommended Next Fix Order
1. Fix event state capture in Stage83 and get `Event` sample to `PASS`
2. Fix enchantment carrier generation and get `Enchantment` sample to `PASS`
3. Only then start scaling full batches

# Cracked Core Frost Combat Freeze

## Entity
- Kind: `Relic`
- Id: `CRACKED_CORE`

## Mutation
- Replaced the original `LIGHTNING_ORB` channel behavior with `FROST_ORB` in the auto-generated graph override.

## Reproduction
1. Export package to `F:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\STS2_Editor\mods\modtest2.sts2pack`.
2. Hot-load / restart with the package enabled.
3. Use menu control to start a singleplayer `DEFECT` run.
4. Enter the first monster room.

## Failure
- The game stalled during combat room initialization.
- `STS2_MCP` stopped responding when the first combat room was entered.
- `godot.log` stopped around `Creating NCombatRoom ...`.

## Root Cause
- Auto-imported hook graphs carried `trigger.default`.
- `RuntimeGraphDispatcher.ResolveEntryNode(..., allowDefaultFallback: false)` still accepted `trigger.default`, so unrelated runtime paths executed the relic graph body outside its intended hook.
- That caused `orb.channel(FROST_ORB)` to run repeatedly with the wrong context and eventually hang combat initialization.
- The hook patch path for `BeforeSideTurnStart` / `BeforePlayPhaseStart` also did not match the native `HookPlayerChoiceContext.AssignTaskAndWaitForPauseOrCompletion(...)` waiting semantics.

## Fix
- Respect `allowDefaultFallback: false` for `trigger.default` in [RuntimeGraphDispatcher.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeGraphDispatcher.cs).
- Restore native hook wait semantics in [RuntimeGraphPatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeGraphPatches.cs).
- Keep `orb.channel` on hook-time contexts using `BlockingPlayerChoiceContext` in [BuiltInBehaviorNodeExecutors.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/BuiltInBehaviorNodeExecutors.cs).

## Validation
- Started a `DEFECT` run with menu control.
- Reached the first monster room with `破损核心: 充能1个冰霜。`
- `STS2_MCP` successfully read live combat state.
- Verified combat started normally and the player had `1/3` orb slots occupied by `冰霜`.

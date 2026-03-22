# Stage 02 - Graph Runtime Dispatch And Build Stabilization

## Date
- 2026-03-23

## Developed
- The existing graph runtime chain in the workspace was audited and aligned instead of replaced.
  - Confirmed the active execution path is:
    - `BehaviorGraphRegistry.RegisterBuiltIns()`
    - `BuiltInBehaviorNodeExecutors.RegisterInto(...)`
    - `BehaviorGraphExecutor.ExecuteAsync(...)`
    - `RuntimeGraphDispatcher`
    - `RuntimeGraphPatches`
- `BehaviorGraphExecutionContext` was corrected so target selectors now resolve safely for:
  - `target/current_target`
  - `self/owner/source_creature`
  - `all_players`
  - `all_allies`
  - `all_enemies`
- The build-facing graph node surface is now coherent for Phase 1 runtime use:
  - flow nodes
  - value nodes
  - debug node
  - combat nodes
  - player gain nodes
- Runtime gameplay graph dispatch is now present and build-verified for:
  - card play wrapper override path
  - potion use wrapper override path
  - relic/enchantment hook-style dispatch paths that route through `Hook.*`
- A real assembly-signature drift issue was fixed.
  - Decompiled source suggested a `CardPileCmd.RemoveFromCombat(..., skipVisuals)` overload shape.
  - The referenced `sts2.dll` did not expose that same callable shape to the mod project.
  - The runtime override code was adjusted to use the callable overload that the real assembly accepts.
- The project builds successfully again and copies the mod DLL into the game `mods` directory.

## Not Developed Yet
- No in-game smoke test has been executed yet for:
  - a card with `BehaviorSource.Graph`
  - a potion with `BehaviorSource.Graph`
  - a relic/enchantment graph hook override
- The in-game graph editor UI is still only a shell.
  - Users can enable graph mode and create graph scaffolds.
  - Users still cannot visually author or edit graph nodes and edges in-game.
- External asset import UI and package asset extraction workflow are still incomplete.
- New content injection is still not finished for:
  - new cards
  - new relics
  - new potions
  - new events
  - new enchantments
- Monster encyclopedia/editor, Spine replacement, and event custom scene layout remain out of scope for the current stage.

## Issues Encountered
- The workspace already contained a more advanced graph runtime implementation than the previous running summary described.
  - Files such as `BehaviorGraphExecutor`, `BuiltInBehaviorNodeExecutors`, `RuntimeGraphDispatcher`, `RuntimeGraphOverrides`, and `RuntimeGraphPatches` already existed.
  - Starting a second parallel implementation path would have created two competing graph runtimes.
  - Resolution:
    - remove the duplicate runtime files introduced during exploration
    - standardize on the existing `BehaviorGraphExecutor -> RuntimeGraphDispatcher -> RuntimeGraphPatches` chain
- Decompiled source and the real referenced assembly were not identical for some callable API details.
  - `CardPileCmd.RemoveFromCombat` was the concrete example in this stage.
  - Resolution:
    - trust the real compiled assembly at build time
    - use decompiled source for guidance, not as the final callable contract
- Existing graph/runtime files were still untracked in the current worktree.
  - This means the runtime graph path was present locally but had not yet been committed into project history.

## Validation
- `dotnet build STS2_Editor.csproj` passed with `0 warning / 0 error`.
- The mod DLL was copied to:
  - `F:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\STS2_Editor\`
- The validated runtime entry files for graph dispatch in this stage are:
  - `Scripts/Editor/Graph/BehaviorGraphExecutor.cs`
  - `Scripts/Editor/Graph/BuiltInBehaviorNodeExecutors.cs`
  - `Scripts/Editor/Runtime/RuntimeGraphDispatcher.cs`
  - `Scripts/Editor/Runtime/RuntimeGraphPatches.cs`

## Next Step
- Stage 03 should focus on actual usable testing, not just build stability.
- Recommended Stage 03 targets:
  - add a minimal in-game graph authoring/editing workflow, or at least a graph JSON editor panel
  - create one sample project/package that turns a known card into a graph-driven card
  - create one sample project/package that turns a known potion into a graph-driven potion
  - run a real in-game smoke test and capture:
    - project creation
    - override save
    - export and install
    - package enable
    - entering combat
    - playing the graph-driven card
    - using the graph-driven potion
- After that, continue into:
  - external asset workflow
  - new content injection
  - package conflict and multiplayer real-session validation

# Monster Move Graph V1 Plan

## Summary
- Goal: bring vanilla `Monster` authoring into Mod Studio with editable HP, auto-imported monster AI, turn-level move lists, move-level graphs, explicit monster state, and full in-game proof.
- Scope of V1:
  - vanilla monster overrides only
  - no new monster creation
  - no encounter/room authoring
  - no user-visible scripting layer
- Locked UX decisions:
  - dedicated monster editor, not a single generic graph tab
  - turn editor uses `Opening Turns + Loop Phases`
  - each turn contains an ordered move list
  - each move opens its own graph
  - multi-move turns are supported inside the turn layer
  - combat intent shows a full-turn summary

## Global Rules
- This file is the single source of truth for monster AI delivery.
- Execution must proceed stage by stage without skipping ahead.
- `Stage N+1` must not begin before `Stage N` is complete.
- A stage is complete only when:
  - code is implemented
  - stage gates pass
  - the stage log is written to `docs/progress/`
- If a later stage reveals a regression in an earlier stage, return to that earlier stage, fix it, update that stage log, and rerun its gates before moving forward again.
- Every stage log must use the fixed 5-section structure:
  - `已开发内容`
  - `未开发内容`
  - `遇到的问题`
  - `后续如何解决`
  - `验证结果`

## Worker Split
- Worker A: monster source audit, importer, runtime monster dispatcher
- Worker B: editor data model, monster UI, authoring UX
- Worker C: proof tooling, real-game automation, batch execution
- Worker D: audit docs, support matrix, stage logs, gate docs
- Parallel work is allowed only inside the current stage.

## Stage Sequence

### Stage 74 - Plan Landing And Monster Source Audit
- Goal:
  - land this plan
  - produce a truthful source audit for all vanilla monsters
- Outputs:
  - `codex_plan_monster_move_graph_v1.md`
  - `docs/reference/monster_ai_source_audit.md`
  - `docs/reference/monster_ai_gap_matrix.md`
  - `docs/progress/stage_74_monster_ai_plan_and_audit.md`
- Gate:
  - all monster AI structure categories and runtime hook points are documented

### Stage 75 - Monster Authoring Model And Persistence
- Goal:
  - add monster project data structures and editor surfacing
- Required changes:
  - add `MonsterBehaviorDefinition` and related project models
  - add monster metadata support for browsing and basic HP editing
  - surface `Monster` in the entity browser
  - disallow `New Entry` for monsters
- Outputs:
  - `docs/reference/monster_ai_authoring_model.md`
  - `docs/reference/monster_project_persistence.md`
  - `docs/progress/stage_75_monster_authoring_model_and_persistence.md`
- Gate:
  - vanilla monsters can be browsed and their project authoring data can be saved/reloaded

### Stage 76 - Monster Runtime Foundation
- Goal:
  - wire monster override execution into combat
- Required changes:
  - add monster runtime dispatcher
  - patch `AfterCreatureAdded`, `PrepareForNextTurn`, `TakeTurn`, and stun resume handling
  - extend graph execution context to support monster ownership and monster state
  - generate turn-summary intents compatible with current combat UI
- Outputs:
  - `docs/reference/monster_runtime_hooks.md`
  - `docs/progress/stage_76_monster_runtime_foundation.md`
- Gate:
  - a hand-authored monster override can replace native turn selection and move execution in combat

### Stage 77 - Monster Layered Editor UI
- Goal:
  - deliver the dedicated monster authoring UX
- Required changes:
  - add `Monster AI` page with:
    - `Opening Turns`
    - `Loop Phases`
    - `Moves`
    - `State Variables`
    - `Lifecycle Hooks`
  - support adding/removing/reordering moves per turn
  - support opening move graphs directly from turn rows
- Outputs:
  - `docs/reference/monster_ai_editor_ux.md`
  - `docs/progress/stage_77_monster_editor_ui.md`
- Gate:
  - a monster turn/phase/move/state structure can be fully authored and reloaded in the editor

### Stage 78 - Native Monster AI Auto-Import
- Goal:
  - auto-convert vanilla monster AI into the layered monster authoring model
- Required changes:
  - import `MoveState`, `RandomBranchState`, `ConditionalBranchState`, `StarterMoveIdx`, `MustPerformOnceBeforeTransitioning`
  - translate move methods into move graphs
  - import monster mutable AI state as explicit state variables
  - import lifecycle logic that affects combat behavior
- Outputs:
  - `docs/reference/monster_import_rules.md`
  - `docs/reference/monster_import_parity_matrix.md`
  - `docs/progress/stage_78_monster_auto_import.md`
- Gate:
  - representative monsters import into correct opening-turn and loop-phase layouts

### Stage 79 - Monster Nodes And Executors
- Goal:
  - cover the monster-specific gameplay actions not handled by the current player-centric graph runtime
- Required changes:
  - add or adapt execution support for:
    - enemy summon
    - status card injection
    - generated card injection
    - player card removal/steal
    - creature HP/state mutation
  - expose monster-specific read-only context references
- Outputs:
  - `docs/reference/monster_graph_node_catalog.md`
  - `docs/progress/stage_79_monster_node_and_executor_completion.md`
- Gate:
  - imported monster moves can express the main native gameplay patterns without native fallback

### Stage 80 - Lifecycle And Forced-Transition Parity
- Goal:
  - support monster AI behaviors driven by death hooks, ally death, forced move switching, and special transition constraints
- Outputs:
  - `docs/reference/monster_lifecycle_compatibility.md`
  - `docs/progress/stage_80_monster_lifecycle_and_forced_transition.md`
- Gate:
  - representative edge-case monsters behave correctly under runtime proof

### Stage 81 - Monster Proof Tooling
- Goal:
  - extend the current proof toolchain to generate and run monster proof batches
- Outputs:
  - `coverage/monster-proof/proof_manifest.json`
  - `coverage/monster-proof/batches/*.json`
  - `coverage/monster-proof/results/*.json`
  - `coverage/monster-proof/report.md`
  - `docs/progress/stage_81_monster_proof_tooling.md`
- Gate:
  - all monster proof batches can be generated and executed automatically

### Stage 82 - Full In-Game Monster Regression And Delivery
- Goal:
  - run full game regression for all vanilla monsters and publish final delivery docs
- Outputs:
  - `docs/reference/monster_ai_delivery_summary.md`
  - `docs/reference/monster_regression_matrix.md`
  - `docs/progress/stage_82_monster_full_game_regression_and_delivery.md`
- Gate:
  - all planned monster proof batches pass
  - all vanilla monsters complete the final regression sweep
  - final delivery docs are current and complete

## Core Gates
- Build:
  - `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
- Existing smoke:
  - `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
- Monster proof:
  - monster-specific batches generated by the extended proof runner

## Assumptions
- Monster V1 targets gameplay and authoring parity first.
- Animation, SFX, VFX, and banter are best-effort in V1 and must not block monster gameplay delivery.
- The current Steam/game paths remain valid for proof automation.

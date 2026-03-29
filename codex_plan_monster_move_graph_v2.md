# Monster Move Graph V1 - Refined Implementation Plan

## Context

STS2_Editor is a Slay the Spire 2 mod editor that supports visual behavior graph editing for 5 entity types (Card, Relic, Potion, Event, Enchantment). Each entity has a single behavior graph. Monsters are fundamentally different: they have a **hierarchical state machine** — phases containing turns, turns containing moves, each move having its own behavior graph. The existing `EntityDraft` with a single `GraphDraft` cannot represent this.

The game has **127 vanilla monsters** with AI patterns ranging from simple sequential moves to complex conditional/random branching with mutable state, lifecycle hooks, and forced transitions. The goal is to bring monster authoring into the editor with:
- Editable HP and basic metadata
- Turn-level move lists (multi-move per turn)
- Opening Turns + Loop Phases structure
- Flow control (sequential, random branch, conditional branch)
- Full coverage of all vanilla monster abilities
- Easy-to-use, beginner-friendly UX

### Key Game Source Types (read-only, at `F:/sts2_mod/STS2_Proj/`)
- `MonsterModel` (`src/Core/Models/MonsterModel.cs`) — abstract base, methods: `GenerateMoveStateMachine()`, `SetUpForCombat()`, `RollMove()`, `PerformMove()`, `AfterAddedToRoom()`, `SetMoveImmediate()`
- `MonsterMoveStateMachine` (`src/Core/MonsterMoves/MonsterMoveStateMachine/`) — FSM with `States` dict, `StateLog` history, `RollMove()` → `FindNextMoveState()`
- `MoveState` — actual move: `Intents[]`, `_onPerform` async Task, `FollowUpStateId`, `MustPerformOnceBeforeTransitioning`
- `RandomBranchState` — weighted random selection with `StateWeight` (repeatType, maxTimes, cooldown, weightLambda)
- `ConditionalBranchState` — ordered condition/state pairs, first-true wins
- `MoveRepeatType` — CanRepeatForever, CanRepeatXTimes, CannotRepeat, UseOnlyOnce

---

## Stage 74 — Plan Landing & Monster Source Audit

**Goal**: Land plan, produce factual source audit of all 127 vanilla monsters.

**Outputs**:
- `docs/reference/monster_ai_source_audit.md` — per-monster: class name, HP range, FSM topology (sequential/random/conditional/hybrid), mutable state fields, lifecycle hooks, special mechanics
- `docs/reference/monster_ai_gap_matrix.md` — matrix: monster × capability (damage, multi-hit, apply_power, summon, status_card, heal, block, talk, conditional_branch, random_branch, forced_transition, ally_death_hook, mutable_state)
- `docs/progress/stage_74_monster_ai_plan_and_audit.md`

**Audit approach**: Scan all `*.cs` in `F:/sts2_mod/STS2_Proj/src/Core/Models/Monsters/`, parse `GenerateMoveStateMachine()` to classify FSM topology, catalog all `MoveState`/`RandomBranchState`/`ConditionalBranchState` instances, catalog mutable fields and lifecycle hooks.

**Gate**: All monsters classified, gap matrix complete, zero unaudited monsters.

---

## Stage 75 — Monster Authoring Model & Persistence

**Goal**: Define data model for monster AI authoring, wire into project system for browsing and save/load.

### Data Model (new file: `Scripts/Editor/Core/Models/MonsterAiDefinition.cs`)

```csharp
// Top-level definition stored on EntityOverrideEnvelope
public sealed class MonsterAiDefinition
{
    public string MonsterId { get; set; }
    public List<MonsterTurnDefinition> OpeningTurns { get; set; }    // Play once in order
    public List<MonsterPhaseDefinition> LoopPhases { get; set; }     // Cycle after opening
    public List<MonsterStateVariableDefinition> StateVariables { get; set; }
    public List<MonsterLifecycleHookDefinition> LifecycleHooks { get; set; }
    public List<MonsterEventTriggerDefinition> EventTriggers { get; set; }
    public Dictionary<string, string> Metadata { get; set; }         // HP overrides etc.
}

public sealed class MonsterTurnDefinition
{
    public string TurnId { get; set; }
    public string DisplayName { get; set; }
    public List<MonsterMoveDefinition> Moves { get; set; }           // Ordered move list
    public List<MonsterIntentDeclaration> Intents { get; set; }      // UI intent icons
    public bool MustPerformOnceBeforeTransitioning { get; set; }
}

public sealed class MonsterMoveDefinition
{
    public string MoveId { get; set; }
    public string DisplayName { get; set; }
    public string GraphId { get; set; }  // → EditorProject.Graphs[GraphId]
}

public sealed class MonsterPhaseDefinition
{
    public string PhaseId { get; set; }
    public string DisplayName { get; set; }
    public MonsterPhaseKind PhaseKind { get; set; }  // Sequential | RandomBranch | ConditionalBranch
    public List<MonsterPhaseBranch> Branches { get; set; }
}

public sealed class MonsterPhaseBranch
{
    public string BranchId { get; set; }
    public string? TargetTurnId { get; set; }         // Direct turn ref
    public string? TargetPhaseId { get; set; }        // Or another phase
    public float Weight { get; set; } = 1.0f;         // For RandomBranch
    public string Condition { get; set; }              // For ConditionalBranch: "$state.x < 3"
    public MoveRepeatType RepeatType { get; set; }
    public int MaxRepeats { get; set; }
    public int Cooldown { get; set; }
}

public sealed class MonsterStateVariableDefinition { Name, Type (int/bool/float/string), InitialValue }
public sealed class MonsterLifecycleHookDefinition { HookType (after_added/before_removed/on_die_to_doom/before_death), GraphId }
public sealed class MonsterEventTriggerDefinition { EventKind (AllyDied/HpChanged), FilterMonsterId?, TargetPhaseId?, GraphId? }
public sealed class MonsterIntentDeclaration { IntentType (SingleAttack/MultiAttack/Buff/...), Parameters dict }
public enum MonsterPhaseKind { Sequential, RandomBranch, ConditionalBranch }
```

### Files to Modify

| File | Change |
|------|--------|
| `Scripts/Editor/Core/Models/EntityDraft.cs` | Add `MonsterAiDraft? MonsterAi` property; new `MonsterAiDraft` class with Working/Original `MonsterAiDefinition` |
| `Scripts/Editor/Core/Models/EntityOverrideEnvelope.cs` | Add `MonsterAiDefinition? MonsterAi` for serialization |
| `Scripts/Editor/Core/Services/ModelMetadataService.cs` | Add `BuildMonsterMetadata()`, `BuildMonsterItem()`, Monster cases in all switch statements, Monster `AssetBindings` |
| `Scripts/Editor/Graph/EditorProjectStore.cs` | Handle MonsterAi in save/load envelope serialization |
| `Scripts/Editor/UI/ModStudioEntityBrowserPanel.cs` | Add Monster tab button |
| `Scripts/Editor/UI/ModStudioFieldDisplayNames.cs` | Add monster field display names (min_initial_hp, max_initial_hp, etc.) |
| `Scripts/Editor/UI/FieldChoiceProvider.cs` | Populate `GetMonsterChoices()`, add intent type / phase kind choices |

**Key design**: Move graphs stored in existing `EditorProject.Graphs` dict (keyed by `MonsterMoveDefinition.GraphId`), reusing entire graph infrastructure. `MonsterAiDefinition` stored on `EntityOverrideEnvelope.MonsterAi`.

**Gate**: Monsters browsable in entity browser, metadata editable, MonsterAiDefinition round-trips JSON, "New Entry" disabled for monsters, compile + Stage03 passes.

---

## Stage 76 — Monster Runtime Foundation

**Goal**: Wire monster override execution into combat.

### Harmony Patch Targets

| Target | Patch Type | Purpose |
|--------|-----------|---------|
| `MonsterModel.SetUpForCombat()` | Prefix | Replace native FSM with authored AI |
| `MonsterModel.RollMove(IEnumerable<Creature>)` | Prefix | Use authored turn selection |
| `MonsterModel.PerformMove()` | Prefix | Execute authored move graphs |
| `CombatManager.AfterCreatureAdded(Creature)` | Postfix | Execute lifecycle hooks |

### New Files

| File | Purpose |
|------|---------|
| `Scripts/Editor/Runtime/MonsterRuntimeState.cs` | Per-instance state: current turn index, current phase, state variables, move history for repeat/cooldown tracking |
| `Scripts/Editor/Runtime/MonsterGraphStateMachine.cs` | Replaces native FSM using `MonsterAiDefinition`. Replicates `FindNextMoveState` semantics: opening turns first → loop phases cycle, `MustPerformOnceBeforeTransitioning` honored, random weights via game `Rng`, conditional evaluation via state variables, cooldown/repeat constraints via move history |
| `Scripts/Editor/Runtime/RuntimeMonsterDispatcher.cs` | Static dispatch: `ShouldUseOverride()`, `ExecuteSetUp()`, `ExecuteRollMove()`, `ExecutePerformMove()`, `ExecuteLifecycleHook()` |
| `Scripts/Editor/Runtime/RuntimeMonsterPatches.cs` | `[HarmonyPrefix]` patches on above targets |

### Files to Modify

| File | Change |
|------|--------|
| `Scripts/Editor/Graph/BehaviorGraphExecutionContext.cs` | Add `MonsterModel? Monster`, `Creature? MonsterCreature`, `MonsterRuntimeState? MonsterState` properties. Add `"monster"`, `"monster_creature"`, `"monster_state"` to `ResolveReference()`. Extend `ResolveTargets()` for monster-side targeting (monsters target players by default). |
| `Scripts/Editor/Runtime/RuntimeGraphDispatcher.cs` | Add `TryGetMonsterOverride(string monsterId, out MonsterAiDefinition?)` |

### Intent Generation
At `RollMove`, build `AbstractIntent[]` from `MonsterTurnDefinition.Intents` declarations. Map `IntentType` string → concrete intent class (`SingleAttackIntent`, `MultiAttackIntent`, `BuffIntent`, etc.).

### State Variable Evaluation
Conditional branch conditions like `"$state.counter < 3"` evaluated via existing `ResolveObject`/`ResolveBool` infrastructure on `BehaviorGraphExecutionContext`.

### Instance Tracking
Use `ConditionalWeakTable<MonsterModel, MonsterRuntimeState>` to track per-mutable-instance runtime state.

**Gate**: Hand-crafted MonsterAiDefinition for a simple monster executes in combat — correct turn selection, move graph execution, correct intents, native monsters unaffected.

**Risk**: Highest-risk stage. Mitigations: async execution chains naturally (executor already async), single-thread (no concurrency), stun handling must skip authored override when `SpawnedThisTurn` is true.

---

## Stage 77 — Monster Layered Editor UI

**Goal**: Dedicated monster AI editing interface.

### UI Layout

```
+-- Monster AI Editor: [Name] ---- HP: [min]-[max] --+
| Tabs: [Basic Info] [AI Structure] [State] [Hooks]   |
+------------------------------------------------------+
| AI Structure:                                        |
| ┌─ Opening Turns ─────────────────────────────────┐ |
| │ Turn 1: "Boot Up" [MustPerformOnce: ✓]          │ |
| │   Move 1: "Block + Buff"  [Edit Graph] [×]      │ |
| │   [+ Add Move]                                    │ |
| │ Turn 2: "Attack"                                  │ |
| │   Move 1: "Slash"  [Edit Graph] [×]             │ |
| │   [+ Add Move]                                    │ |
| │ [+ Add Opening Turn]                              │ |
| └──────────────────────────────────────────────────┘ |
| ┌─ Loop Phases ────────────────────────────────────┐ |
| │ Phase 1: "Main Loop" [Kind: RandomBranch ▾]     │ |
| │   → "One-Two" [Wt: 1.0] [MaxRepeat: 2]         │ |
| │   → "Sharpen" [Wt: 1.0] [CannotRepeat]          │ |
| │   → "Uppercut" [Wt: 1.0] [MaxRepeat: 2]        │ |
| │   [+ Add Branch]                                  │ |
| │ [+ Add Loop Phase]                                │ |
| └──────────────────────────────────────────────────┘ |
+------------------------------------------------------+
```

### New Files

| File | Purpose |
|------|---------|
| `Scripts/Editor/UI/MonsterAi/ModStudioMonsterAiEditor.cs` | Top-level monster AI editor with tabs |
| `Scripts/Editor/UI/MonsterAi/ModStudioMonsterTurnEditor.cs` | Per-turn: move list add/remove/reorder, intent display, "Edit Graph" buttons |
| `Scripts/Editor/UI/MonsterAi/ModStudioMonsterPhaseEditor.cs` | Per-phase: kind selector, branch list with weight/condition/repeat controls |

### Files to Modify

| File | Change |
|------|--------|
| `Scripts/Editor/UI/NModStudioProjectWindow.cs` | Route Monster kind to `ModStudioMonsterAiEditor` instead of standard graph editor. Wire "Edit Graph" → `ModStudioGraphCanvasView` with move's `BehaviorGraphDefinition`. |
| `Scripts/Editor/UI/NModStudioProjectWindow.Tail.cs` | Monster-specific save/load for MonsterAiDefinition + associated move graphs |
| `Scripts/Editor/Graph/BehaviorGraphPaletteFilter.cs` | Add Monster case with allowed prefixes: `flow.`, `value.`, `debug.`, `combat.`, `creature.`, `power.`, `monster.` |

### Beginner-Friendly UX Decisions
1. Clear section labels with tooltips ("Opening turns play once at combat start", "Loop phases repeat cyclically")
2. Intent icons shown per turn for visual scanning
3. "Edit Graph" opens familiar graph canvas
4. Phase kind dropdown with descriptions
5. Drag handles for reorder
6. Default templates: "Add Sequential Turn", "Add Random Phase", "Add Conditional Phase"

**Gate**: Full authoring of monster turn/phase/move/state structure, save/load round-trip.

---

## Stage 78 — Native Monster AI Auto-Import

**Goal**: Auto-convert vanilla `GenerateMoveStateMachine()` into editor model.

### Import Strategy

Cannot use IL analysis on `GenerateMoveStateMachine()` directly (it builds a runtime object graph). Instead:
1. **Runtime FSM inspection**: Call `GenerateMoveStateMachine()` on canonical instances, traverse the returned object graph via reflection
2. **Move graph IL analysis**: For each `MoveState._onPerform` delegate, use existing IL analysis to create `BehaviorGraphDefinition`

### Algorithm
1. Call `monster.GenerateMoveStateMachine()` on canonical instance
2. Inspect `States` dict, find initial state, traverse `FollowUpState`/`FollowUpStateId` chains
3. For `MoveState`: extract `Intents`, `MustPerformOnceBeforeTransitioning`, `FollowUpStateId`
4. For `RandomBranchState`: reflect on `States` list → extract weights, repeat types, cooldowns
5. For `ConditionalBranchState`: reflect on `States` list → conditions are opaque lambdas → store as `"native_condition_N"` placeholders
6. Build `MonsterAiDefinition` from traversal
7. For each move's `_onPerform` delegate target method → use existing `NativeBehaviorGraphAutoImporter` IL analysis → create move graph

### New Files

| File | Purpose |
|------|---------|
| `Scripts/Editor/Graph/NativeMonsterAiImporter.cs` | FSM → MonsterAiDefinition conversion. `TryImportMonster(MonsterModel, out MonsterAiDefinition, out List<BehaviorGraphDefinition>)` |

### Files to Modify

| File | Change |
|------|--------|
| `Scripts/Editor/Graph/NativeBehaviorGraphAutoImporter.cs` | Add `TryCreateMonsterMoveGraph()` for IL analysis on `_onPerform` delegates. Add Monster case in `TryCreateGraph()` dispatch (line ~99). |
| `Scripts/Editor/Graph/NativeBehaviorGraphTranslator.cs` | Add monster-specific step translations: `monster.damage_from_monster`, `monster.gain_block_self`, `monster.apply_power_to_self`, `monster.summon`, `monster.talk` |
| `Scripts/Editor/Graph/NativeBehaviorAutoGraphService.cs` | Add monster import orchestration |

### Import Challenges

| Challenge | Mitigation |
|-----------|-----------|
| `ConditionalBranchState` lambdas are opaque | Store as placeholder; document decompiled condition; user can edit to state variable check |
| Dynamic `weightLambda` | Evaluate once at import for static weight; flag as partial |
| Mutable state fields | IL-inspect monster class for `AssertMutable()` patterns |
| Unique mechanics (Queen ally death, KnowledgeDemon card choice) | Mark as partial import; leave `native_fallback` placeholder |

**Gate**: ≥80% of 127 monsters import full/partial, sequential monsters 100% fidelity, random branch structure correct, move graphs contain correct nodes.

---

## Stage 79 — Monster Nodes & Executors

**Goal**: Monster-specific graph node types covering all vanilla move abilities.

### New Node Types (add to `BuiltInBehaviorNodeDefinitionProvider.cs`)

| Node Type | Properties | Game Command |
|-----------|-----------|-------------|
| `monster.attack` | amount, hit_count, target | `DamageCmd.Attack().FromMonster().WithHitCount().Execute()` |
| `monster.gain_block` | amount | `CreatureCmd.GainBlock(creature, amount)` |
| `monster.apply_power` | power_id, amount, target | `PowerCmd.Apply<T>(targets, amount, applier)` |
| `monster.heal` | amount, target | `CreatureCmd.Heal(creature, amount)` |
| `monster.summon` | monster_id | `CreatureCmd.Add<T>(combatState)` |
| `monster.talk` | text, duration | `TalkCmd.Play(text, creature, duration)` |
| `monster.inject_status_card` | card_id, count, pile | Status card injection |
| `monster.set_state` | variable_name, value | Write `MonsterRuntimeState.Variables` |
| `monster.get_state` | variable_name | Read `MonsterRuntimeState.Variables` |
| `monster.check_state` | variable_name, operator, value | Branch on state variable |
| `monster.animate` | animation_id, wait_duration | `CreatureCmd.TriggerAnim()` |
| `monster.play_sfx` | sfx_path | `SfxCmd.Play()` |
| `monster.remove_player_card` | card_id, count | Player deck manipulation |
| `monster.check_ally_alive` | monster_id → bool | For conditional mechanics |
| `monster.count_allies` | → count | `combatState.GetTeammatesOf()` |
| `monster.force_transition` | target_turn_id | `SetMoveImmediate()` equivalent |

### Files to Modify

| File | Change |
|------|--------|
| `Scripts/Editor/Graph/BuiltInBehaviorNodeDefinitionProvider.cs` | Add ~16 `MonsterXxx()` node definitions |
| `Scripts/Editor/Graph/BuiltInBehaviorNodeExecutors.cs` | Add executor implementations |
| `Scripts/Editor/Graph/GraphDescriptionGenerator.cs` | Description templates for monster nodes |
| `Scripts/Editor/Graph/GraphDescriptionTemplateGenerator.cs` | Template generation |
| `Scripts/Editor/Graph/NativeBehaviorGraphTranslator.cs` | Translation catalog entries |

**Gate**: All nodes registered and in palette, executors call correct game commands, descriptions correct, Stage03 passes.

---

## Stage 80 — Lifecycle & Forced-Transition Parity

**Goal**: Death hooks, ally death triggers, forced move switching, special transitions.

### Mechanics to Support
1. `AfterAddedToRoom` lifecycle graphs (e.g., Queen subscribes to `Creature.Died`)
2. Ally death triggers → phase transition (declarative `MonsterEventTriggerDefinition`)
3. `ForceCurrentState` from lifecycle hooks
4. `BeforeDeath` hooks
5. `AfterCurrentHpChanged` hooks
6. `OnDieToDoom`
7. Stun resume

### New Files

| File | Purpose |
|------|---------|
| `Scripts/Editor/Runtime/MonsterLifecycleManager.cs` | Manage lifecycle hook subscriptions per monster instance, event-driven phase transitions |

### Files to Modify

| File | Change |
|------|--------|
| `Scripts/Editor/Runtime/RuntimeMonsterPatches.cs` | Add patches: `AfterAddedToRoom` (Postfix), `BeforeRemovedFromRoom` (Prefix), `OnDieToDoom` (Prefix), stun handling in `PrepareForNextTurn` |
| `Scripts/Editor/Runtime/RuntimeMonsterDispatcher.cs` | Add lifecycle dispatch methods |

**Gate**: Queen ally-death phase transition, Crusher lifecycle hooks, KnowledgeDemon counter conditions, stun resume correct.

---

## Stage 81 — Monster Proof Tooling

**Goal**: Automated import + runtime verification.

### New Tool: `tools/Stage81MonsterProof/`

**Test categories**:
1. **Import coverage**: Import all 127 monsters, verify structure matches audit
2. **Structure roundtrip**: Serialize/deserialize MonsterAiDefinition, verify equality
3. **Runtime proof**: ~20 representative monsters execute correct move sequences in mock combat
4. **Regression**: All existing Stage03+ tests pass

**Gate**: 127/127 import attempted, ≥100 full/partial, 20 runtime proofs pass, zero regressions.

---

## Stage 82 — Full In-Game Regression & Delivery

**Goal**: All monster overrides verified in actual game combat.

**Process**: Generate `.sts2pack` for all importable monsters → load via `RuntimePackageBackend` → run combat encounters → record move selections → compare against native behavior.

**Outputs**: `coverage/monster-proof/report.md`, `docs/reference/monster_ai_delivery_summary.md`, `docs/reference/monster_regression_matrix.md`

**Gate**: All proof batches pass, full regression matrix published, no non-monster regressions.

---

## Dependency Graph

```
Stage 74 (Audit)
    ↓
Stage 75 (Data Model)
    ├──→ Stage 76 (Runtime) ──→ Stage 79 (Nodes) ──→ Stage 80 (Lifecycle)
    └──→ Stage 77 (Editor UI)                              ↓
              ↓                                       Stage 81 (Proof)
         Stage 78 (Import) ─────────────────────────→      ↓
                                                     Stage 82 (Regression)
```

Parallelization: Stage 76 & 77 can run in parallel after 75. Stage 79 can start once 76 is done.

---

## Verification

After each stage, run in order:
```bash
dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false
dotnet run --project tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- tools\stage03-smoke-run
```

After Stage 81+:
```bash
dotnet run --project tools\Stage81MonsterProof\Stage81MonsterProof.csproj -- .
```

After Stage 82:
```bash
dotnet run --project tools\Stage70FullGameProof\Stage70FullGameProof.csproj -- --run-all .
```

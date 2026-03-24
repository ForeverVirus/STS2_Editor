# Mod Studio Dynamic Values And Editor Polish

## Summary
- Push `Mod Studio` to a delivery-ready state focused on dynamic graph values, graph authoring coverage, event graph editing, full Chinese coverage, typed selectors, and editor responsiveness.
- Stages 36-42 are implemented continuously under this plan and each stage must produce a progress document in `docs/progress/`.
- Runtime/source-of-truth remains the decompiled game source and live runtime model state; `sts2_guides` stays auxiliary-only.

## Implementation Changes
### Stage 36 — Structured dynamic graph values
- Add structured dynamic value data for numeric graph properties instead of only string `amount` values.
- Support three value source kinds: `Literal`, `DynamicVar`, `FormulaRef`.
- Keep legacy graphs compatible by treating old raw numeric properties as literal values.
- Route runtime graph execution through a single evaluator before invoking built-in node executors.

### Stage 37 — Dynamic descriptions and preview context
- Add graph preview context editing and dynamic preview output.
- Generate both template-oriented descriptions and resolved preview text.
- Reuse original localization/dynamic variable concepts where possible and keep manual description overrides higher priority.

### Stage 38 — Graph node coverage expansion
- Extend graph node coverage for common card/combat flow and event authoring needs.
- Upgrade native auto-import so dynamic values prefer dynamic bindings/formula references instead of fixed literals.

### Stage 39 — Pure graph event authoring
- Author events purely through graph nodes and compile them into the existing runtime event template metadata model.
- Reuse current runtime template execution/resume/combat-return behavior.

### Stage 40 — Localization audit and typed selectors
- Audit the whole editor for Chinese coverage gaps and centralize UI strings.
- Convert all enumerable fields from free-text entry to dropdowns, searchable pickers, or list selectors.

### Stage 41 — Performance and partial refresh
- Add caches for current-entity editor state and reduce full-screen rebuilds on revert/select flows.
- Make graph/assets lazy-load on first entry and refresh only the affected region when possible.

### Stage 42 — Validation and delivery
- Run build/smoke validation, real-game regression checks where possible, and update support/known-limits docs.

## Public Interfaces / Types
- New:
  - `DynamicValueDefinition`
  - `DynamicValueSourceKind`
  - `DynamicValueOverrideMode`
  - `DynamicPreviewContext`
  - `DynamicValuePreviewResult`
  - `DynamicValueEvaluator`
  - `DynamicPreviewService`
  - `GraphDescriptionTemplateGenerator`
  - `EventGraphCompiler`
  - `EventGraphValidationResult`
  - `EventGraphPageDefinition`
  - `EventGraphChoiceBinding`
  - `ModStudioLocalizationCatalog`
  - `FieldChoiceProvider`
  - `EntityEditorViewCache`
- Updated:
  - `BehaviorGraphNodeDefinition`
  - `GraphDescriptionGenerator`
  - `NativeBehaviorAutoGraphService`
  - `NativeBehaviorGraphAutoImporter`
  - `ModStudioBasicEditor`
  - `ModStudioProjectDetailPanel`
  - `NModStudioProjectWindow`

## Test Plan
- Dynamic values: literal, dynamic-var, and formula-ref execution/preview all work for supported node families.
- Descriptions: graph changes keep preview text and card/event-facing text aligned unless the user manually overrides.
- Nodes: new nodes can be created, connected, saved, and reloaded.
- Events: graph-authored events can show pages/options and enter/return from combat correctly.
- Localization/selectors: Chinese coverage gaps are removed from primary flows and enumerable inputs use typed controls.
- Performance: revert/select operations no longer trigger obvious hitching in common editor flows.

## Assumptions
- Event authoring remains graph-only in this phase; no separate event template form editor is added.
- Formula editing first supports original formula references with `base/extra` override controls instead of arbitrary scripting.
- When a value cannot be previewed perfectly from editor-only state, the editor shows the best available resolved preview plus explicit context inputs.

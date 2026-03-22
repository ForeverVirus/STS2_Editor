# codex_plan.md

## Summary
- Goal: implement an in-game `Mod Studio` with `Project Mode` and `Package Mode`.
- Phase 1 supports:
  - Edit existing: characters, cards, relics, potions, events, enchantments.
  - Create new: cards, relics, potions, events, enchantments.
  - Not supported: character creation, monster browsing/editing, character/monster external Spine replacement, custom Godot event layouts.
- Existing vanilla content can switch to `graph` behavior source; newly created content defaults to `graph`.
- Conflict rule: same object uses whole-object override, with later package load order winning.
- Multiplayer rule: packages are enabled per-package only when every player has the same package checksum. Local-only packages are disabled for that session.

## Source Of Truth
- Runtime truth always comes from the decompiled source and runtime model registry, especially `ModelDb`, `*Model`, `Hook`, `RunState`, and `ModManager`.
- `sts2_guides` is auxiliary only. It can be shown as supplemental knowledge, but it is never the canonical gameplay data source.
- If `sts2_guides` disagrees with runtime source, runtime source wins.
- Editor browsing should enumerate from runtime `ModelDb` first and only layer guide text on top when helpful.

## Architecture
- `Editor.UI`: menu entry, project mode, package mode, browser, detail panels, preview panels, graph shell.
- `Editor.Core`: project lifecycle, shared contracts, command state, search/filter helpers, metadata adapters.
- `Editor.Runtime`: whole-object overrides, vanilla graph replacement, dynamic content registration, multiplayer negotiation, load-order resolution.
- `Editor.Packaging`: project format, package format, checksum, install state, import/export, managed asset import.
- `Editor.Graph`: graph schema, node registry, validation, executor contracts, extension points.

## Phase 1 Scope
- Characters:
  - Edit name/localization, starting HP/gold/energy, starting deck, starting relics, starting potions, original resource references.
  - No external Spine or character creation.
- Cards / relics / potions / events / enchantments:
  - Edit name, description, numeric metadata, resources, behavior source.
  - Existing objects may switch to `graph`.
  - New objects may be registered into existing pools/routes.
- Events:
  - Support normal template events and battle-template events.
  - No custom Godot scene layouts.
- Monsters:
  - Phase 1 does not display or edit monster content.
  - Runtime/registry architecture must keep extension points for future monster browsing and AI graph work.

## Runtime And Packaging
- Installed package order is persisted locally.
- Whole-object override is applied by package order.
- New content is appended by package order.
- Multiplayer negotiation uses the intersection of package ids/checksums across peers.
- Project storage uses folder projects for editability.
- Exported distribution uses a single binary container such as `.sts2pack`.
- Package content includes manifest, project snapshot, graph data, localization overrides, assets, and checksums.
- `sts2_guides` content is never packaged into the runtime override chain.

## UI Workflow
- Add `Mod Studio` to the main menu.
- `Project Mode`:
  - Create, open, clone, rename, delete, export projects.
  - Browse characters, cards, relics, potions, events, enchantments.
  - Layout: category/filter/search on the left, list/grid in the center, detail and preview on the right.
  - Reuse native preview nodes where possible, especially `NCard`, `NRelic`, and `NPotion`.
- `Package Mode`:
  - Import, enable/disable, reorder, inspect conflicts, persist installed state.
- Graph editor:
  - Phase 1 ships with built-in node definitions and a shell editor UI.
  - Third-party custom node authoring is not implemented yet, but runtime/editor extension points are reserved.

## Parallel Agent Plan
- Recommended implementation workers:
  - Worker 1: `Editor.UI`
  - Worker 2: `Editor.Packaging + Asset Import`
  - Worker 3: `Editor.Graph`
  - Worker 4: `Editor.Runtime`
- Main agent responsibilities:
  - Create shared contracts and directory skeleton first.
  - Spawn the four workers with disjoint write ownership.
  - Integrate, validate, and reconcile cross-module behavior.

## Test Plan
- Unit tests:
  - Whole-object override resolution.
  - Stable package checksums.
  - Multiplayer package intersection.
  - Graph validation.
  - Asset import/export integrity.
- Integration tests:
  - New card/relic/potion/event/enchantment registration.
  - Vanilla content switching to `graph`.
  - Character starting-state overrides.
  - Asset import/export round-trip.
  - Load order `A C B` override precedence.
  - Session behavior when local `ABC` meets remote `AB`.
- Manual acceptance:
  - Project create/save/reopen.
  - Package import/disable/reorder/persist.
  - Browse/search/filter/detail refresh.
  - Multiplayer disabled-package messaging.

## Assumptions
- No field-level merge, package dependency resolution, custom event layout authoring, monster editing, or external Spine replacement in Phase 1.
- `sts2_guides` is never a runtime truth source.
- External assets must be imported into managed editor storage before runtime/package use.

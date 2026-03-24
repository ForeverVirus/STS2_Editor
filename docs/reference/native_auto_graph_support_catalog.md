# Native Auto Graph Support Catalog

## Summary
- Phase 1 auto-graph now uses a single entry point: `NativeBehaviorAutoGraphService`.
- Strategy order is fixed:
  1. `reflection-import`
  2. `description-fallback`
- Current auto-graph coverage is intentionally limited to the gameplay slices that already have runtime graph dispatch support in the mod:
  - `card.on_play`
  - `potion.on_use`
  - relic hook subset:
    - `relic.before_card_played`
    - `relic.after_card_played`
    - `relic.after_card_played_late`
    - `relic.before_potion_used`
    - `relic.after_potion_used`

## Supported Step Kinds
- `combat.damage`
- `combat.gain_block`
- `combat.heal`
- `combat.draw_cards`
- `combat.apply_power`
- `combat.create_card`
- `combat.remove_card`
- `combat.transform_card`
- `combat.discard_cards`
- `combat.exhaust_cards`
- `card.autoplay`
- `card.apply_keyword`
- `card.apply_single_turn_sly`
- `cardpile.auto_play_from_draw_pile`
- `cardpile.shuffle`
- `creature.set_current_hp`
- `orb.passive`
- `player.gain_energy`
- `player.gain_gold`
- `player.gain_stars`

## Supported Auto-Translation Shapes
- Single-step cards, potions, or relic hooks that directly call one supported command.
- Two-step and short linear chains composed of supported commands.
- Examples that fit Phase 1 well:
  - `StrikeIronclad` -> `combat.damage`
  - `DefendIronclad` -> `combat.gain_block`
  - `Bash` -> `combat.damage -> combat.apply_power`
  - `Backflip` -> `combat.gain_block -> combat.draw_cards`
  - `ShrugItOff` -> `combat.gain_block -> combat.draw_cards`
  - `Adrenaline` -> `player.gain_energy -> combat.draw_cards`
  - `Havoc` -> `cardpile.auto_play_from_draw_pile`
  - `Turbo` -> `combat.create_card` / `card.apply_single_turn_sly` style chains when the source IL exposes concrete card identity
  - `Afterimage` -> `combat.apply_power`
  - `Venerate` -> `player.gain_stars`
  - `BlockPotion` -> `combat.gain_block`
  - `BloodPotion` -> `combat.heal`
  - `EnergyPotion` -> `player.gain_energy`
  - `SwiftPotion` -> `combat.draw_cards`
  - `DexterityPotion` -> `combat.apply_power`

## Partial Support
- `reflection-import` is partial when the method body contains both recognized and unsupported gameplay calls.
- `description-fallback` is partial when the description contains unsupported keywords or only a subset of clauses can be mapped.
- The translator catalog already marks these as partial categories:
  - `event.reward`
  - `event.choice`
- `combat.create_card` is partial when the importer cannot recover a concrete `card_id`, `count`, or `target_pile` from the source call and has to fall back to the node defaults.
- `cardpile.shuffle` also covers `ShuffleIfNecessary`; the conditional guard itself is collapsed during import.
- `cardpile.auto_play_from_draw_pile` is imported with best-effort defaults when the source call arguments are not statically recoverable.

## Known Unsupported Patterns
- Event scene / layout logic
- Monster AI
- Reward drafting / choose-one flows that are not already backed by a graph node
- Random targeting and repeated loops
- Branch-heavy or condition-heavy native logic where the IL importer can only see called methods but not preserve the original condition semantics reliably
- X-cost or battle-state-calculated effects that depend on runtime expressions instead of direct canonical dynamic vars
- Any command path that still depends on runtime card collections or reflection-only generic resolution for exact identity recovery

## Relic Scope Notes
- The current runtime dispatcher does not yet execute arbitrary relic triggers; it only covers the relic hooks already patched by `RuntimeGraphDispatcher`.
- Because of that, even if a relic description looks easy to parse, it should not be advertised as auto-supported unless the trigger is already wired end to end.

## Description Generation Rules
- When a graph only uses supported node kinds, `GraphDescriptionGenerator` can produce an automatic description.
- Manual description edits always win over future auto-generated descriptions.
- If the current description is blank, or still matches the last auto-generated cache value, saving the graph updates the entity description automatically.

## Practical Authoring Guidance
- Best Phase 1 flow:
  1. Open an original card / potion / simple relic
  2. Import the auto-generated graph
  3. Adjust node values in the graph inspector
  4. Let auto-description fill the text if the graph remains within the supported node set
  5. Manually override description only when the graph becomes too custom to describe cleanly

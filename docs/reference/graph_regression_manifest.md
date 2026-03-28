# Graph Regression Manifest

## Summary
- Generated at: `2026-03-28T10:35:26.7610664+00:00`
- Entries: `1011`
- Supported: `932`
- Partial: `79`
- Unsupported: `0`
- Mutation cases: `1908`

## Entity Counts
- `Card`: total `577` / supported-or-partial `500`
- `Enchantment`: total `24` / supported-or-partial `24`
- `Event`: total `57` / supported-or-partial `57`
- `Potion`: total `64` / supported-or-partial `62`
- `Relic`: total `289` / supported-or-partial `289`

## Top Node Types
- `flow.entry`: `1011`
- `flow.exit`: `1011`
- `combat.apply_power`: `292`
- `combat.damage`: `232`
- `combat.gain_block`: `99`
- `combat.create_card`: `93`
- `card.select_cards`: `76`
- `combat.draw_cards`: `64`
- `event.page`: `57`
- `event.proceed`: `57`
- `event.option`: `56`
- `event.goto_page`: `52`
- `flow.branch`: `49`
- `value.compare`: `49`
- `player.gain_energy`: `41`
- `card.upgrade`: `36`
- `combat.repeat`: `34`
- `value.set`: `34`
- `modifier.damage_additive`: `28`
- `modifier.damage_multiplicative`: `27`
- `orb.channel`: `25`
- `modifier.block_additive`: `24`
- `modifier.block_multiplicative`: `24`
- `modifier.play_count`: `24`
- `combat.heal`: `18`

## Mutation Kinds
- `numeric_increment`: `1286`
- `text_suffix`: `424`
- `bool_toggle`: `103`
- `reference_swap`: `72`
- `target_swap`: `15`
- `pile_swap`: `4`
- `status_toggle`: `4`

## Sample Entries
- `Card:ABRASIVE` status=`supported` graph=`native_auto_card_abrasive` mutations=`2`
- `Card:ACCELERANT` status=`supported` graph=`native_auto_card_accelerant` mutations=`1`
- `Card:ACCURACY` status=`supported` graph=`native_auto_card_accuracy` mutations=`1`
- `Card:ACROBATICS` status=`supported` graph=`native_auto_card_acrobatics` mutations=`3`
- `Card:ADAPTIVE_STRIKE` status=`partial` graph=`native_auto_card_adaptive_strike` mutations=`3`
- `Card:ADRENALINE` status=`supported` graph=`native_auto_card_adrenaline` mutations=`2`
- `Card:AFTERIMAGE` status=`supported` graph=`native_auto_card_afterimage` mutations=`1`
- `Card:AFTERLIFE` status=`supported` graph=`native_auto_card_afterlife` mutations=`1`
- `Card:AGGRESSION` status=`supported` graph=`native_auto_card_aggression` mutations=`1`
- `Card:ALCHEMIZE` status=`supported` graph=`native_auto_card_alchemize` mutations=`1`
- `Card:ALIGNMENT` status=`supported` graph=`native_auto_card_alignment` mutations=`1`
- `Card:ALL_FOR_ONE` status=`supported` graph=`native_auto_card_all_for_one` mutations=`2`
- `Card:ANGER` status=`partial` graph=`native_auto_card_anger` mutations=`2`
- `Card:ANOINTED` status=`partial` graph=`native_auto_card_anointed` mutations=`1`
- `Card:ANTICIPATE` status=`supported` graph=`native_auto_card_anticipate` mutations=`1`
- `Card:APOTHEOSIS` status=`supported` graph=`native_auto_card_apotheosis` mutations=`0`
- `Card:APPARITION` status=`supported` graph=`native_auto_card_apparition` mutations=`1`
- `Card:ARMAMENTS` status=`supported` graph=`native_auto_card_armaments` mutations=`2`
- `Card:ARSENAL` status=`supported` graph=`native_auto_card_arsenal` mutations=`1`
- `Card:ASCENDERS_BANE` status=`partial` graph=`auto_card_ASCENDERS_BANE` mutations=`0`
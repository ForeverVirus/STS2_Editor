# Stage 74 - Monster AI Plan And Audit

## Completed

- Re-reviewed `codex_plan_monster_move_graph_v2.md` and aligned the Stage 74 outputs with the actual monster source tree.
- Rebuilt `docs/reference/monster_ai_source_audit.md` as an inheritance-aware source audit covering all top-level monster types under `src/Core/Models/Monsters`.
- Added `docs/reference/monster_ai_gap_matrix.md` with the requested capability matrix: damage, multi-hit, apply_power, summon, status_card, heal, block, talk, conditional/random branch, forced transition, ally-death hook, and mutable state.
- Added this stage progress note so Stage 74 now has all three planned deliverables.

## Key Findings

- Effective topology split is `Sequential=80`, `Random=25`, `Conditional=14`, `Hybrid=2`.
- `4` source types inherit their effective FSM from an ancestor instead of defining it locally.
- `0` HP entries remained unresolved by the static parser; all current monster types resolved successfully if this stays at `0`.
- The earlier draft's `None` bucket was an artifact of file-local scanning and is not present after following inherited monster behavior.

## Validation

- `python tools/stage74_monster_audit.py`

## Notes

- This stage is still a structural audit. It does not yet perform runtime FSM traversal or `_onPerform` delegate translation; that work remains in Stage 78 and Stage 81.
- The gap matrix is intentionally conservative. It tracks whether the capability appears in the effective source chain, not whether the editor/runtime implementation already supports it.

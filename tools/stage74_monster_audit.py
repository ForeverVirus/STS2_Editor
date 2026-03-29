from __future__ import annotations

import pathlib
import re
from collections import Counter
from dataclasses import dataclass


REPO_ROOT = pathlib.Path(__file__).resolve().parents[1]
MONSTER_ROOT = pathlib.Path(r"F:\sts2_mod\STS2_Proj\src\Core\Models\Monsters")
AUDIT_PATH = REPO_ROOT / "docs" / "reference" / "monster_ai_source_audit.md"
GAP_MATRIX_PATH = REPO_ROOT / "docs" / "reference" / "monster_ai_gap_matrix.md"
PROGRESS_PATH = REPO_ROOT / "docs" / "progress" / "stage_74_monster_ai_plan_and_audit.md"

TEST_ONLY_NAMES = {
    "BigDummy",
    "BattleFriendV1",
    "BattleFriendV2",
    "BattleFriendV3",
    "MultiAttackMoveMonster",
    "OneHpMonster",
    "SingleAttackMoveMonster",
    "TenHpMonster",
}


@dataclass(frozen=True)
class MonsterSource:
    name: str
    base_name: str
    is_abstract: bool
    path: pathlib.Path
    text: str


@dataclass(frozen=True)
class HpValue:
    base: str
    tough: str


def main() -> None:
    monster_sources = load_monster_sources()
    audit_rows = [build_audit_row(name, monster_sources) for name in sorted(monster_sources)]
    audit_text = build_audit_markdown(audit_rows)
    gap_matrix_text = build_gap_matrix_markdown(audit_rows)
    progress_text = build_progress_markdown(audit_rows)

    AUDIT_PATH.write_text(audit_text, encoding="utf-8", newline="\n")
    GAP_MATRIX_PATH.write_text(gap_matrix_text, encoding="utf-8", newline="\n")
    PROGRESS_PATH.write_text(progress_text, encoding="utf-8", newline="\n")

    print(f"Wrote {AUDIT_PATH}")
    print(f"Wrote {GAP_MATRIX_PATH}")
    print(f"Wrote {PROGRESS_PATH}")


def load_monster_sources() -> dict[str, MonsterSource]:
    monster_sources: dict[str, MonsterSource] = {}
    class_pattern = re.compile(r"public\s+(abstract\s+)?(?:sealed\s+)?class\s+(\w+)\s*:\s*(\w+)")

    for path in sorted(MONSTER_ROOT.glob("*.cs")):
        text = path.read_text(encoding="utf-8")
        match = class_pattern.search(text)
        if match is None:
            continue

        monster_sources[match.group(2)] = MonsterSource(
            name=match.group(2),
            base_name=match.group(3),
            is_abstract=bool(match.group(1)),
            path=path,
            text=text,
        )

    return monster_sources


def build_audit_row(name: str, monster_sources: dict[str, MonsterSource]) -> dict[str, object]:
    lineage = get_lineage(name, monster_sources)
    combined_text = "\n".join(source.text for source in reversed(lineage))
    fsm_provider = find_effective_provider(
        name,
        monster_sources,
        lambda source: "GenerateMoveStateMachine(" in source.text,
    )
    fsm_body = extract_method_body(fsm_provider.text, "GenerateMoveStateMachine") if fsm_provider else ""

    topology = classify_topology(fsm_body)
    move_ids = extract_move_ids(fsm_body)
    mutable_fields = extract_mutable_fields(lineage)
    lifecycle_flags = collect_lifecycle_flags(lineage)
    capability_flags = collect_capability_flags(combined_text, fsm_body, mutable_fields)
    hp_text = format_hp_summary(name, monster_sources)

    specials: list[str] = []
    if fsm_provider and fsm_provider.name != name:
        specials.append(f"InheritedFSM:{fsm_provider.name}")
    if capability_flags["summon"]:
        specials.append("Summon")
    if capability_flags["status_card"]:
        specials.append("StatusCard")
    if capability_flags["forced_transition"]:
        specials.append("ForcedTransition")
    if capability_flags["talk"]:
        specials.append("Talk")
    if "UpdateMusicParameter(" in combined_text:
        specials.append("Music")
    if "MustPerformOnceBeforeTransitioning" in fsm_body:
        specials.append("MustPerformOnce")
    if name in TEST_ONLY_NAMES:
        specials.append("TestOnly")

    return {
        "name": name,
        "base_name": monster_sources[name].base_name,
        "is_abstract": monster_sources[name].is_abstract,
        "hp": hp_text,
        "topology": topology,
        "fsm_provider": fsm_provider.name if fsm_provider else "-",
        "move_ids": move_ids,
        "move_count": len(move_ids),
        "mutable_fields": mutable_fields,
        "lifecycle": lifecycle_flags,
        "specials": specials,
        "capabilities": capability_flags,
        "path": monster_sources[name].path.name,
    }


def get_lineage(name: str, monster_sources: dict[str, MonsterSource]) -> list[MonsterSource]:
    lineage: list[MonsterSource] = []
    current_name = name

    while current_name in monster_sources:
        current = monster_sources[current_name]
        lineage.append(current)
        if current.base_name == "MonsterModel":
            break
        current_name = current.base_name

    return lineage


def find_effective_provider(
    name: str,
    monster_sources: dict[str, MonsterSource],
    predicate,
) -> MonsterSource | None:
    for source in get_lineage(name, monster_sources):
        if predicate(source):
            return source
    return None


def extract_method_body(text: str, method_name: str) -> str:
    match = re.search(
        rf"(?:public|protected)\s+override[\w\s<>]*\b{re.escape(method_name)}\s*\(",
        text,
    )
    if match is None:
        return ""

    brace_start = text.find("{", match.end())
    if brace_start < 0:
        return ""

    depth = 0
    for index in range(brace_start, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return text[brace_start + 1 : index]

    return ""


def extract_property_expression(text: str, property_name: str) -> str:
    patterns = [
        rf"public\s+override\s+int\s+{property_name}\s*=>\s*(.+?);",
        rf"public\s+override\s+int\s+{property_name}\s*\{{\s*get\s*\{{\s*return\s+(.+?);",
    ]

    for pattern in patterns:
        match = re.search(pattern, text, re.S)
        if match is not None:
            return " ".join(match.group(1).split())

    return ""


def classify_topology(fsm_body: str) -> str:
    has_random = "RandomBranchState" in fsm_body
    has_conditional = "ConditionalBranchState" in fsm_body

    if has_random and has_conditional:
        return "Hybrid"
    if has_random:
        return "Random"
    if has_conditional:
        return "Conditional"
    if fsm_body:
        return "Sequential"
    return "None"


def extract_move_ids(fsm_body: str) -> list[str]:
    return re.findall(r'new\s+MoveState\s*\(\s*"([^"]+)"', fsm_body)


def extract_mutable_fields(lineage: list[MonsterSource]) -> list[str]:
    fields: set[str] = set()
    pattern = re.compile(r"AssertMutable\(\);\s*([A-Za-z_]\w*)\s*=\s*value\s*;", re.S)
    for source in lineage:
        for match in pattern.finditer(source.text):
            fields.add(match.group(1))
    return sorted(fields)


def collect_lifecycle_flags(lineage: list[MonsterSource]) -> list[str]:
    flags: list[str] = []
    if any("AfterAddedToRoom(" in source.text for source in lineage):
        flags.append("AAR")
    if any("BeforeRemovedFromRoom(" in source.text for source in lineage):
        flags.append("BRR")
    if any("OnDieToDoom(" in source.text for source in lineage):
        flags.append("ODT")
    if any(".Died +=" in source.text for source in lineage):
        flags.append("DE")
    return flags


def collect_capability_flags(
    combined_text: str,
    fsm_body: str,
    mutable_fields: list[str],
) -> dict[str, bool]:
    died_subscription_lines = [line.strip() for line in combined_text.splitlines() if ".Died +=" in line]
    non_self_died_subscriptions = [
        line
        for line in died_subscription_lines
        if "base.Creature.Died +=" not in line and "Creature.Died +=" not in line
    ]

    return {
        "damage": "DamageCmd.Attack(" in combined_text,
        "multi_hit": "WithHitCount(" in combined_text or "MultiAttackIntent(" in combined_text,
        "apply_power": "PowerCmd.Apply" in combined_text,
        "summon": "CreatureCmd.Add(" in combined_text or "CreatureCmd.Add<" in combined_text,
        "status_card": (
            "CardSelectCmd" in combined_text
            or "CardPileCmd.AddGeneratedCardToCombat" in combined_text
            or "CardPileCmd.AddToCombatAndPreview" in combined_text
        ),
        "heal": "CreatureCmd.Heal(" in combined_text or "SetMaxAndCurrentHp(" in combined_text or "DoReattach(" in combined_text,
        "block": "GainBlock(" in combined_text,
        "talk": "TalkCmd.Play(" in combined_text,
        "conditional_branch": "ConditionalBranchState" in fsm_body,
        "random_branch": "RandomBranchState" in fsm_body,
        "forced_transition": "SetMoveImmediate(" in combined_text or "ForceCurrentState(" in combined_text,
        "ally_death_hook": bool(non_self_died_subscriptions),
        "mutable_state": bool(mutable_fields),
    }


def format_hp_summary(name: str, monster_sources: dict[str, MonsterSource]) -> str:
    min_value = parse_hp_value(name, "MinInitialHp", monster_sources)
    max_value = parse_hp_value(name, "MaxInitialHp", monster_sources)
    if min_value is None or max_value is None:
        return "unresolved"

    if min_value.base == max_value.base:
        base_text = min_value.base
    else:
        base_text = f"{min_value.base}-{max_value.base}"

    if min_value.tough == max_value.tough:
        tough_text = min_value.tough
    else:
        tough_text = f"{min_value.tough}-{max_value.tough}"

    return f"base {base_text} | tough {tough_text}"


def parse_hp_value(
    source_name: str,
    property_name: str,
    monster_sources: dict[str, MonsterSource],
    depth: int = 0,
) -> HpValue | None:
    if depth > 8:
        return None

    provider = find_effective_provider(
        source_name,
        monster_sources,
        lambda source: bool(extract_property_expression(source.text, property_name)),
    )
    if provider is None:
        return None

    expression = extract_property_expression(provider.text, property_name).strip()
    if not expression:
        return None

    if expression in {"MinInitialHp", "MaxInitialHp"}:
        return parse_hp_value(source_name, expression, monster_sources, depth + 1)

    if expression.startswith("base."):
        return parse_hp_value(provider.base_name, expression.split(".", 1)[1], monster_sources, depth + 1)

    expression = resolve_constants(expression, get_lineage(provider.name, monster_sources))
    expression = expression.strip()

    if re.fullmatch(r"[A-Za-z_]\w*", expression):
        member_expression = find_int_member_expression(expression, get_lineage(provider.name, monster_sources))
        if member_expression:
            resolved_member = resolve_constants(member_expression, get_lineage(provider.name, monster_sources))
            numeric_text = simplify_numeric_token(resolved_member)
            if numeric_text:
                return HpValue(base=numeric_text, tough=numeric_text)
            ascension_match = re.search(
                r"AscensionHelper\.GetValueIfAscension\([^,]+,\s*([^,]+),\s*([^)]+)\)",
                resolved_member,
            )
            if ascension_match is not None:
                tough_text = simplify_numeric_token(ascension_match.group(1))
                base_text = simplify_numeric_token(ascension_match.group(2))
                if tough_text and base_text:
                    return HpValue(base=base_text, tough=tough_text)

    ascension_match = re.search(
        r"AscensionHelper\.GetValueIfAscension\([^,]+,\s*([^,]+),\s*([^)]+)\)",
        expression,
    )
    if ascension_match is not None:
        tough_text = simplify_numeric_token(ascension_match.group(1))
        base_text = simplify_numeric_token(ascension_match.group(2))
        if tough_text and base_text:
            return HpValue(base=base_text, tough=tough_text)

    numeric_text = simplify_numeric_token(expression)
    if numeric_text:
        return HpValue(base=numeric_text, tough=numeric_text)

    return None


def resolve_constants(expression: str, lineage: list[MonsterSource], depth: int = 0) -> str:
    if depth > 8:
        return expression

    constants: dict[str, str] = {}
    for source in lineage:
        for match in re.finditer(r"const\s+\w+\s+([A-Za-z_]\w*)\s*=\s*([^;]+);", source.text):
            constants.setdefault(match.group(1), match.group(2).strip())

    previous = expression
    for _ in range(8):
        updated = previous
        for constant_name, constant_value in constants.items():
            updated = re.sub(rf"\b{re.escape(constant_name)}\b", constant_value, updated)
        if updated == previous:
            break
        previous = updated

    return previous


def find_int_member_expression(member_name: str, lineage: list[MonsterSource]) -> str:
    patterns = [
        rf"(?:public|private|protected)\s+int\s+{member_name}\s*=>\s*(.+?);",
        rf"(?:public|private|protected)\s+int\s+{member_name}\s*\{{\s*get\s*\{{\s*return\s+(.+?);",
    ]

    for source in lineage:
        for pattern in patterns:
            match = re.search(pattern, source.text, re.S)
            if match is not None:
                return " ".join(match.group(1).split())

    return ""


def simplify_numeric_token(token: str) -> str:
    simplified = token.strip()
    simplified = simplified.removesuffix("m").removesuffix("f")
    if re.fullmatch(r"-?\d+", simplified):
        return simplified
    return ""


def build_audit_markdown(rows: list[dict[str, object]]) -> str:
    topology_counter = Counter(row["topology"] for row in rows)
    source_type_count = len(rows)
    concrete_count = sum(not row["is_abstract"] for row in rows)
    abstract_count = sum(bool(row["is_abstract"]) for row in rows)
    after_added_count = sum("AAR" in row["lifecycle"] for row in rows)
    before_removed_count = sum("BRR" in row["lifecycle"] for row in rows)
    on_doom_count = sum("ODT" in row["lifecycle"] for row in rows)
    died_subscription_count = sum("DE" in row["lifecycle"] for row in rows)
    summon_count = sum(row["capabilities"]["summon"] for row in rows)
    forced_transition_count = sum(row["capabilities"]["forced_transition"] for row in rows)
    talk_count = sum(row["capabilities"]["talk"] for row in rows)
    status_card_count = sum(row["capabilities"]["status_card"] for row in rows)
    music_count = sum("Music" in row["specials"] for row in rows)
    must_perform_count = sum("MustPerformOnce" in row["specials"] for row in rows)
    mutable_state_count = sum(row["capabilities"]["mutable_state"] for row in rows)

    lines = [
        "# Monster AI Source Audit",
        "",
        f"Source: `{MONSTER_ROOT.as_posix()}/`",
        "",
        "## Scope",
        "",
        "- This pass is inheritance-aware: if a monster does not override `GenerateMoveStateMachine()`, HP, or lifecycle hooks locally, it is classified from the nearest ancestor implementation.",
        "- HP is reported as `base / tough` when the value can be resolved from `AscensionHelper.GetValueIfAscension(...)` or a constant.",
        "- The table covers all top-level monster source types in `src/Core/Models/Monsters`, including the shared abstract base `DecimillipedeSegment`.",
        "",
        "## Summary Statistics",
        "",
        "| Category | Count |",
        "|----------|-------|",
        f"| Total source types | {source_type_count} |",
        f"| Concrete monster types | {concrete_count} |",
        f"| Abstract base types | {abstract_count} |",
        f"| Sequential topology | {topology_counter['Sequential']} |",
        f"| Random topology | {topology_counter['Random']} |",
        f"| Conditional topology | {topology_counter['Conditional']} |",
        f"| Hybrid topology | {topology_counter['Hybrid']} |",
        f"| With `AfterAddedToRoom` override | {after_added_count} |",
        f"| With `BeforeRemovedFromRoom` override | {before_removed_count} |",
        f"| With `OnDieToDoom` override | {on_doom_count} |",
        f"| With any `.Died +=` subscription | {died_subscription_count} |",
        f"| With summon capability | {summon_count} |",
        f"| With forced transition capability | {forced_transition_count} |",
        f"| With talk / banter | {talk_count} |",
        f"| With status-card injection or card-choice logic | {status_card_count} |",
        f"| With music parameter writes | {music_count} |",
        f"| With `MustPerformOnceBeforeTransitioning` | {must_perform_count} |",
        f"| With mutable runtime state | {mutable_state_count} |",
        "",
        "## Corrections Versus Draft Audit",
        "",
        "- `MysteriousKnight` does not have a local FSM override, but it inherits `FlailKnight`'s random-branch state machine and HP profile.",
        "- `DecimillipedeSegmentBack`, `DecimillipedeSegmentFront`, and `DecimillipedeSegmentMiddle` inherit the random FSM, HP range, and `AfterAddedToRoom` hook from `DecimillipedeSegment`.",
        "- The earlier draft's `None` topology bucket disappears once inherited behavior is taken into account.",
        "",
        "## Per-Type Audit",
        "",
        "| # | Monster | Base | Abstract | HP (base/tough) | Topology | FSM Source | Moves | Mutable Fields | Lifecycle | Special |",
        "|---|---------|------|----------|-----------------|----------|------------|-------|----------------|-----------|---------|",
    ]

    for index, row in enumerate(rows, start=1):
        move_summary = "-"
        if row["move_ids"]:
            move_summary = f"{row['move_count']} ({', '.join(row['move_ids'])})"

        mutable_summary = "-"
        if row["mutable_fields"]:
            mutable_summary = f"{len(row['mutable_fields'])} ({', '.join(row['mutable_fields'])})"

        lifecycle_summary = "-" if not row["lifecycle"] else ", ".join(row["lifecycle"])
        special_summary = "-" if not row["specials"] else ", ".join(row["specials"])
        abstract_text = "Y" if row["is_abstract"] else "-"

        lines.append(
            f"| {index} | {row['name']} | {row['base_name']} | {abstract_text} | {row['hp']} | {row['topology']} | {row['fsm_provider']} | {move_summary} | {mutable_summary} | {lifecycle_summary} | {special_summary} |"
        )

    lines.extend(
        [
            "",
            "## Notes",
            "",
            "- `DE` in the lifecycle column means the type subscribes to a `Died` event somewhere in its effective inheritance chain.",
            "- `FSM Source` identifies the class that actually provides the effective `GenerateMoveStateMachine()` implementation.",
            "- The capability matrix in `monster_ai_gap_matrix.md` is a structural source scan, not a runtime proof. Dynamic lambdas and helper indirection are still called out conservatively.",
        ]
    )

    return "\n".join(lines) + "\n"


def build_gap_matrix_markdown(rows: list[dict[str, object]]) -> str:
    lines = [
        "# Monster AI Gap Matrix",
        "",
        f"Source: `{MONSTER_ROOT.as_posix()}/`",
        "",
        "Legend: `Y` = capability detected from the effective source chain, `-` = not detected by the structural scan.",
        "",
        "| Monster | Abstract | damage | multi_hit | apply_power | summon | status_card | heal | block | talk | conditional_branch | random_branch | forced_transition | ally_death_hook | mutable_state | Notes |",
        "|---------|----------|--------|-----------|-------------|--------|-------------|------|-------|------|--------------------|---------------|-------------------|-----------------|---------------|-------|",
    ]

    for row in rows:
        flags = row["capabilities"]
        note_parts: list[str] = []
        if row["fsm_provider"] != row["name"]:
            note_parts.append(f"InheritedFSM:{row['fsm_provider']}")
        if "Music" in row["specials"]:
            note_parts.append("Music")
        if row["name"] in TEST_ONLY_NAMES:
            note_parts.append("TestOnly")
        notes = "-" if not note_parts else ", ".join(note_parts)
        abstract_text = "Y" if row["is_abstract"] else "-"

        lines.append(
            "| {name} | {abstract} | {damage} | {multi_hit} | {apply_power} | {summon} | {status_card} | {heal} | {block} | {talk} | {conditional_branch} | {random_branch} | {forced_transition} | {ally_death_hook} | {mutable_state} | {notes} |".format(
                name=row["name"],
                abstract=abstract_text,
                damage=flag_text(flags["damage"]),
                multi_hit=flag_text(flags["multi_hit"]),
                apply_power=flag_text(flags["apply_power"]),
                summon=flag_text(flags["summon"]),
                status_card=flag_text(flags["status_card"]),
                heal=flag_text(flags["heal"]),
                block=flag_text(flags["block"]),
                talk=flag_text(flags["talk"]),
                conditional_branch=flag_text(flags["conditional_branch"]),
                random_branch=flag_text(flags["random_branch"]),
                forced_transition=flag_text(flags["forced_transition"]),
                ally_death_hook=flag_text(flags["ally_death_hook"]),
                mutable_state=flag_text(flags["mutable_state"]),
                notes=notes,
            )
        )

    return "\n".join(lines) + "\n"


def build_progress_markdown(rows: list[dict[str, object]]) -> str:
    topology_counter = Counter(row["topology"] for row in rows)
    inherited_fsm_count = sum(row["fsm_provider"] != row["name"] for row in rows)
    unresolved_hp_count = sum(row["hp"] == "unresolved" for row in rows)

    lines = [
        "# Stage 74 - Monster AI Plan And Audit",
        "",
        "## Completed",
        "",
        "- Re-reviewed `codex_plan_monster_move_graph_v2.md` and aligned the Stage 74 outputs with the actual monster source tree.",
        "- Rebuilt `docs/reference/monster_ai_source_audit.md` as an inheritance-aware source audit covering all top-level monster types under `src/Core/Models/Monsters`.",
        "- Added `docs/reference/monster_ai_gap_matrix.md` with the requested capability matrix: damage, multi-hit, apply_power, summon, status_card, heal, block, talk, conditional/random branch, forced transition, ally-death hook, and mutable state.",
        "- Added this stage progress note so Stage 74 now has all three planned deliverables.",
        "",
        "## Key Findings",
        "",
        f"- Effective topology split is `Sequential={topology_counter['Sequential']}`, `Random={topology_counter['Random']}`, `Conditional={topology_counter['Conditional']}`, `Hybrid={topology_counter['Hybrid']}`.",
        f"- `{inherited_fsm_count}` source types inherit their effective FSM from an ancestor instead of defining it locally.",
        f"- `{unresolved_hp_count}` HP entries remained unresolved by the static parser; all current monster types resolved successfully if this stays at `0`.",
        "- The earlier draft's `None` bucket was an artifact of file-local scanning and is not present after following inherited monster behavior.",
        "",
        "## Validation",
        "",
        "- `python tools/stage74_monster_audit.py`",
        "",
        "## Notes",
        "",
        "- This stage is still a structural audit. It does not yet perform runtime FSM traversal or `_onPerform` delegate translation; that work remains in Stage 78 and Stage 81.",
        "- The gap matrix is intentionally conservative. It tracks whether the capability appears in the effective source chain, not whether the editor/runtime implementation already supports it.",
    ]

    return "\n".join(lines) + "\n"


def flag_text(value: bool) -> str:
    return "Y" if value else "-"


if __name__ == "__main__":
    main()

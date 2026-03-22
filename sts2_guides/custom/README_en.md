# Custom Knowledge Guide

This folder is for player-maintained Slay the Spire 2 knowledge.

The custom layer is loaded before the built-in `core` layer.

If a custom JSON entry uses the same `id` or `slug` as a built-in entry, it overrides the built-in one.

For the Chinese version of this guide, see `README.md`.

## 1. When to use JSON vs Markdown

- Use JSON first for structured knowledge such as cards, relics, potions, powers, enemies, events, enchantments, and mechanic rules.
- Use Markdown for long-form strategy notes, character guides, or overview-style documents.
- If you only want to correct one gameplay rule, `game_mechanics.json` is usually the best place.

## 2. Supported JSON file names

- `characters.json`
- `builds.json`
- `cards.json`
- `relics.json`
- `potions.json`
- `powers.json`
- `enemies.json`
- `events.json`
- `enchantments.json`
- `game_mechanics.json`

The file name must match one of the built-in names above, or it will not be loaded.

## 3. How to write JSON

You only need to write the entries you want to add or override. You do not need to copy the full built-in file.

For example, a single `game_mechanics.json` entry uses:

- `id`: unique rule key. Reuse the built-in `id` if you want to override an existing rule.
- `title`: rule title.
- `summary`: the actual rule text. The best pattern is: mechanic first, decision constraint second.
- `source`: recommended value is `custom`.

Recommended style:

- First describe how the game actually resolves the rule.
- Then state what the agent should not assume.
- It is often helpful to include both Chinese and English in the same `title` or `summary` so both languages can retrieve the rule more easily.

Example idea:

- `id = energy-resource`
- `title = Unused energy does not carry over by default / 未用完的能量默认不会保留到下一回合`
- `summary = actual mechanic + do not treat leftover energy as banked future energy`

## 4. How Markdown custom knowledge works

Important: not every Markdown file name in this folder is loaded as game knowledge.

The Markdown files that are actually consumed by the knowledge loader are mainly:

- Overview: `overview.md` or `00_OVERVIEW.md`
- General strategy: `general_strategy.md` or `sts2_knowledge_base.md`
- Character guides using character slug or English-name aliases, such as:
  - `ironclad.md`
  - `ironclad_complete_guide.md`
  - `silent.md`
  - `defect.md`
  - `regent.md`
  - `necrobinder.md`

This `README_en.md` file is documentation for players. It is not the recommended place for actual in-game knowledge entries.

## 5. Markdown writing recommendations

- Keep it focused on game knowledge, mechanics, build paths, route choices, event judgment, or boss planning.
- Plain headings, short paragraphs, and bullet points are enough.
- A strong note usually includes:
  - when it applies,
  - the conclusion,
  - important exceptions.
- If you want to override a character guide, the simplest approach is to add a character guide Markdown file with the matching supported name.

Suggested structure:

- Title
- Core goal of the character or topic
- Early-game priorities
- Mid/late-game pivot points
- Common mistakes
- Important exceptions

## 6. Markdown restrictions

Custom Markdown is validated before loading. The following kinds of content are rejected:

- triple-backtick code blocks
- URL links with explicit protocols
- non-game prompt-injection or instruction-injection content
- shell commands, script injection, editor protocols, or local-path protocol style content

In simple terms:

- write normal gameplay notes
- do not write prompts
- do not include links
- do not include code blocks

## 7. Size and maintenance suggestions

- Keep custom files reasonably small. The default per-file limit is about 256 KB.
- Let one rule solve one recurring mistake whenever possible.
- If the agent keeps making the same reasoning mistake, add a short high-constraint mechanic rule before writing a long guide.

## 8. Override vs add-new strategy

- To correct an existing rule: reuse the built-in `id` or `slug`.
- To add new knowledge: create a new non-conflicting `id`.
- If a built-in rule exists but is too weakly phrased, override it with the same `id` and rewrite it as a stronger decision guardrail.

## 9. Recommended workflow

1. Review the bad log or bad recommendation.
2. Identify what kind of mistake it is:
   `game_mechanics.json` for rule constraints.
   `cards.json` for card understanding.
   `relics.json` for relic understanding.
   `events.json` for event options.
   character Markdown for broader playstyle guidance.
3. Add a short corrective rule first, then retest.
4. Only add longer Markdown guides when short corrective rules are not enough.

## 10. Best file to start with

- `game_mechanics.json`

If you are mainly fixing repeated decision mistakes, this is usually the best first file to edit.

# Mod Studio Release V1 Plan

## Summary
- Release V1 only covers `Card / Relic / Potion / Event / Enchantment`.
- Delivery is not complete until three lines are green at the same time:
  - semantic graph support
  - description roundtrip
  - command-driven real game proof
- Execution must continue stage by stage without waiting for another "continue" message, unless an external blocker makes progress impossible.

## Global Rules
- This file is the single source of truth for the remaining work.
- A stage is complete only when:
  - code is implemented
  - the stage gate commands pass
  - the stage log file is written to `docs/progress/`
- `Stage N+1` must not begin before `Stage N` is complete.
- If a later stage reveals a regression in an earlier stage, return to that earlier stage, fix it, update that stage log, and rerun its gates before moving forward again.
- `debug.log` placeholder graphs do not count as real support.
- `passive_only` is allowed only for entities with no active gameplay behavior and must be explicitly documented.

## Stage Sequence

### Stage 67 - Truthful Coverage Rebaseline
- Goal:
  - replace the current "all supported" statistical view with a truthful semantic support baseline
- Required changes:
  - update `tools/Stage59CoverageBaseline` so `debug.log`-only graphs are audited separately and excluded from real support
  - exclude helper/non-model source files from source scans
  - add explicit passive-only allowlist/reporting
- Outputs:
  - `docs/reference/coverage_baseline.md`
  - `docs/reference/debug_fallback_audit.md`
  - `docs/reference/passive_only_allowlist.md`
  - `docs/progress/stage_67_truthful_coverage_and_passive_policy.md`
- Gate:
  - no entity reported as real `supported` may have `debug.log` as its only gameplay node

### Stage 68 - Semantic Graph Completion
- Goal:
  - replace every remaining fallback or placeholder entity with a real semantic graph or an approved passive-only scaffold
- Mandatory focus:
  - `Begone / 下去！` default values, save behavior, description parity, runtime execution
  - remaining fallback-heavy `Card`, `Potion`, `Relic`, and `Event` entities
- Outputs:
  - updated semantic baseline and support catalog
  - `docs/progress/stage_68_semantic_graph_completion.md`
- Gate:
  - all five entity kinds are semantically supported under truthful coverage rules

### Stage 69 - Description Roundtrip Completion
- Goal:
  - reduce `Stage61DescriptionRoundtrip` to all supported
- Required changes:
  - extend both `GraphDescriptionGenerator` and `GraphDescriptionTemplateGenerator`
  - tighten `Stage61DescriptionRoundtrip` so unsupported node types and empty preview text fail the stage
  - preserve sentence skeletons after editing graph values
- Outputs:
  - `coverage/description-roundtrip/description_roundtrip.json`
  - `docs/reference/description_roundtrip.md`
  - `docs/progress/stage_69_description_roundtrip_zero.md`
- Gate:
  - `Card / Relic / Potion / Event / Enchantment` are all `supported` in description roundtrip

### Stage 70 - Full Game Proof Tooling
- Goal:
  - build a reusable, command-driven real game proof runner
- Required changes:
  - add `tools/Stage70FullGameProof`
  - generate proof manifest, proof batches, launch scripts, and report files
  - reuse the existing Stage 06/09/12/13 proof package patterns and `godot.log` parsing approach
- Outputs:
  - `coverage/release-proof/proof_manifest.json`
  - `coverage/release-proof/batches/*.json`
  - `coverage/release-proof/run_batch_*.ps1`
  - `coverage/release-proof/report.md`
  - `docs/progress/stage_70_full_game_proof_tooling.md`
- Gate:
  - the tooling can generate and launch every planned batch without manual editing

### Stage 71 - Command-Driven Full Game Proof
- Goal:
  - run the full proof matrix in the actual game executable
- Required proof groups:
  - `Card`
  - `Relic`
  - `Potion`
  - `Event`
  - `Enchantment`
  - `passive_only`
- Outputs:
  - finalized proof reports under `coverage/release-proof/`
  - `docs/progress/stage_71_command_driven_full_game_proof.md`
- Gate:
  - every planned proof batch passes and every expected marker appears in `godot.log`

### Stage 72 - Final Release Validation And Delivery
- Goal:
  - rerun all gates and publish final delivery docs
- Required reruns:
  - `Stage03SmokeTest`
  - `Stage59CoverageBaseline`
  - `Stage61DescriptionRoundtrip`
  - `Stage65CoverageArtifacts`
  - `Stage70FullGameProof`
- Outputs:
  - final release summary/reference docs
  - `docs/progress/stage_72_final_release_validation_and_delivery.md`
- Gate:
  - semantic support, description roundtrip, and real game proof are all green
  - final docs are complete and current

## Parallel Work Rules
- Parallel work is allowed only inside the current stage.
- Default worker split:
  - Worker A: semantic graph/import/runtime behavior
  - Worker B: description/template/roundtrip
  - Worker C: proof tooling and game launch automation
  - Worker D: baseline/report/doc guards
- No worker may implement a later stage before the current stage gate is passed.

## Core Gates
- Build:
  - `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
- Smoke:
  - `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
- Truthful semantic coverage:
  - `dotnet run --project tools\\Stage59CoverageBaseline\\Stage59CoverageBaseline.csproj -- .`
- Description roundtrip:
  - `dotnet run --project tools\\Stage61DescriptionRoundtrip\\Stage61DescriptionRoundtrip.csproj -- .`
- Coverage artifacts:
  - `dotnet run --project tools\\Stage65CoverageArtifacts\\Stage65CoverageArtifacts.csproj -- .`
- Full game proof:
  - `dotnet run --project tools\\Stage70FullGameProof\\Stage70FullGameProof.csproj -- <workspace>`

## Assumptions
- Existing Steam and game executable locations remain valid.
- The release continues to target only `Card / Relic / Potion / Event / Enchantment`.
- Real delivery means game-verified behavior, not just editor/runtime graph generation.

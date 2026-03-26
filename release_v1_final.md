# Release V1 连续交付计划

## Summary
- 目标不是继续“做绿统计”，而是连续推进到最终可交付版本：`Card / Relic / Potion / Event / Enchantment` 五类实体同时满足四个条件。
- 条件固定为：
- 原版实体都能自动生成与原版功能一致的 graph。
- graph 默认值与原版效果一致。
- 只改节点值、不改节点结构时，保存后描述格式保持原版句式，只变化值。
- 改值后的 graph 能在游戏里正确匹配并执行，并且由命令驱动的全量实机 proof 证明。
- 当前仓库事实是：
- `coverage_baseline` 已经显示全量 `supported`，但其中包含一批 `debug.log`/说明型 fallback 的假绿，不能直接当交付结论。
- `Stage61DescriptionRoundtrip` 还没清零，说明“描述一致性”没有收口。
- `Stage66` 还缺真正的全量命令驱动实机 proof。

## Execution Protocol
- 第一步必须把这份计划写回 [codex_plan_release_v1.md](/F:/sts2_mod/mod_projects/STS2_editor/codex_plan_release_v1.md)，并作为唯一执行源。
- 从开始执行那一刻起，连续推进，不再等待用户再说“继续”。
- 只有一种情况允许中断：遇到外部不可恢复阻塞，例如游戏二进制无法启动、Steam/系统权限阻断、proof flag 完全无效。否则持续执行到最终验收通过。
- 严格阶段门禁：
- `Stage N` 未满足“代码完成 + 本阶段命令通过 + 阶段日志落盘”，禁止进入 `Stage N+1`。
- 如果后续阶段发现前序阶段回归，立即回滚到对应前序阶段修复，补写该阶段日志并重跑门禁后，才能继续。
- 禁止跨步骤提前实现：
- 新发现的大块工作，只能登记到当前阶段日志或追加新阶段，不能直接越级落代码。
- 每个阶段结束必须写一篇 `docs/progress/stage_XX_*.md`，继续使用固定 5 段结构。
- 执行时自动创建并复用 4 个子 agent，且只允许处理当前阶段子任务：
- `Worker A`：语义 graph / importer / translator / runtime patch。
- `Worker B`：描述系统 / template / roundtrip runner。
- `Worker C`：proof package / 游戏启动 / 日志解析。
- `Worker D`：baseline / coverage / 报告 / 文档守门。
- 子 agent 禁止跨阶段提前改后续内容；主 agent 负责集成、门禁、阶段切换和日志落盘。

## Stage Plan
### Stage 67 — Truthful Coverage Rebaseline
- 目标：把当前“统计全绿”收紧成“真实支持全绿”。
- 固定改动：
- 重写 [codex_plan_release_v1.md](/F:/sts2_mod/mod_projects/STS2_editor/codex_plan_release_v1.md)，追加 `Stage 67` 到 `Stage 72` 和本计划的门禁规则。
- 修正 [tools/Stage59CoverageBaseline/Program.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage59CoverageBaseline/Program.cs) 统计口径：
- `debug.log` 唯一动作图一律不算真实支持。
- 只有显式 `passive_only` 白名单实体允许走非主动行为脚手架。
- `VakuuCardSelector` 这类非 `CardModel / RelicModel / PotionModel / EventModel / EnchantmentModel` helper 必须从 baseline 源扫描里排除。
- 产物固定为：
- `coverage/baseline/coverage_baseline.json`
- `docs/reference/coverage_baseline.md`
- `docs/reference/debug_fallback_audit.md`
- `docs/reference/passive_only_allowlist.md`
- `docs/progress/stage_67_truthful_coverage_and_passive_policy.md`
- 验收固定为：
- baseline 中不存在任何 `supported` 且唯一动作节点为 `debug.log` 的实体。
- helper 不再污染覆盖统计。
- 允许这个阶段暂时把 `coverage_baseline` 打回非全绿。

### Stage 68 — Semantic Graph Completion
- 目标：把 `Stage 67` 暴露出来的假绿实体全部替换成真实语义图，或者进入合法 `passive_only`。
- 先做 `Begone / 下去！` 专项：
- auto-import 默认值必须与原版一致，尤其是 transform 目标卡和升级分支。
- graph 保存后重新生成描述时，完整句式必须保留，不能退化成只剩伤害。
- 节点值修改后，描述格式必须与原版一致，只允许值或卡牌名变化。
- 再按实体类型清剩余假绿：
- `Card`：状态牌、诅咒牌、被动牌先分为“真实 trigger”与“passive_only”两组；有主动行为就补真实 trigger，没有主动行为才允许 `passive_only`。
- `Potion`：剩余 fallback 全部转成真实 on-use 语义图。
- `Relic`：剩余 `ModifyCardPlayCount / TryModifyEnergyCostInCombat / TryModifyStarCost / ModifyPowerAmountGiven / ModifyPowerAmountReceived / ModifyWeakMultiplier / ModifyVulnerableMultiplier / TryModifyRestSiteOptions / TryModifyCardRewardAlternatives / ModifyOrbPassiveTriggerCounts` 全部转成真实语义图，不再保留说明型 fallback。
- `Event`：取消“只有 scaffold 也算支持”的做法；多页奖励、战斗返回、非本地化动作链必须转成真实 event graph。
- 产物固定为：
- 更新后的 `coverage_baseline`
- `docs/reference/real_semantic_support.md`
- `docs/progress/stage_68_semantic_graph_completion.md`
- 验收固定为：
- 五类实体在真实口径下全部 `supported`
- 无 `debug.log` 假绿
- `Begone` 的默认值、保存后描述、改值后描述、运行时静态验证全部通过

### Stage 69 — Description Roundtrip Completion
- 目标：把 [tools/Stage61DescriptionRoundtrip/Stage61DescriptionRoundtrip.csproj](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage61DescriptionRoundtrip/Stage61DescriptionRoundtrip.csproj) 清零。
- 固定通过标准：
- `UnsupportedNodeTypes` 为空
- `PreviewDescription` 非空
- `TemplateDescription` 非空
- 修改 graph 值后保存/重载，模板句式不坍缩
- 节点补齐顺序固定：
- `value.set / value.add / value.multiply / value.compare`
  - 作为内部辅助节点处理，不进入用户可见描述，也不算 incomplete
- `card.select_cards`
- `player.add_pet`
- `potion.procure`
- `orb.add_slots / orb.remove_slots / orb.evoke_next`
- `reward.card_options_upgrade / reward.card_options_enchant / reward.mark_card_rewards_rerollable`
- `modifier.damage_additive / modifier.damage_multiplicative / modifier.block_additive / modifier.block_multiplicative / modifier.play_count / modifier.hand_draw / modifier.x_value / modifier.max_energy`
- `card.apply_keyword / card.discard_and_draw / card.set_cost_delta / card.set_cost_this_combat / card.add_cost_until_played`
- `relic.obtain / relic.replace / map.replace_generated / map.remove_unknown_room_type / player.end_turn`
- 这一步必须同步改：
- [GraphDescriptionGenerator.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/GraphDescriptionGenerator.cs)
- [GraphDescriptionTemplateGenerator.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/GraphDescriptionTemplateGenerator.cs)
- [tools/Stage61DescriptionRoundtrip/Program.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage61DescriptionRoundtrip/Program.cs)
- 产物固定为：
- `coverage/description-roundtrip/description_roundtrip.json`
- `docs/reference/description_roundtrip.md`
- `docs/progress/stage_69_description_roundtrip_zero.md`
- 验收固定为：
- `Card / Relic / Potion / Event / Enchantment` 在 description roundtrip 全部 `supported`
- `Begone` 修改节点值后保存/重载，描述句式不坍缩为“只剩伤害”

### Stage 70 — Proof Tooling
- 目标：补齐“命令驱动全量实机 proof”工具链，但先不跑最终全量批次。
- 新增 [tools/Stage70FullGameProof](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage70FullGameProof)：
- 生成 `coverage/release-proof/proof_manifest.json`
- 生成 `coverage/release-proof/batches/*.json`
- 生成 `coverage/release-proof/run_batch_*.ps1`
- 生成 `coverage/release-proof/report.md`
- proof 分批固定为：
- `cards_transform_select_generate.json`
- `cards_cost_playcount.json`
- `cards_status_curse_passive.json`
- `potions_combat.json`
- `potions_noncombat.json`
- `relics_stateful_combat.json`
- `relics_modifier_and_merchant.json`
- `events_multipage_reward.json`
- `events_combat_resume.json`
- `enchantments_modifiers.json`
- `passive_only.json`
- 统一日志格式固定为：
- `[ModStudio.Proof] <kind>:<id>:<trigger>: expected=<...> actual=<...> result=<pass|fail>`
- 统一游戏启动方式固定为：
- 复用现有 `Stage06 / Stage09 / Stage12 / Stage13` 工具模式
- 调用 `SlayTheSpire2.exe --autoslay --seed <seed> ...proof flags...`
- 产物固定为：
- `coverage/release-proof/*`
- `docs/progress/stage_70_full_game_proof_tooling.md`
- 验收固定为：
- proof tool 能生成 manifest、batch、launcher、report
- 每个 batch 都能被命令调用
- `godot.log` 解析器能正确收集 proof marker

### Stage 71 — Command-Driven Full Game Proof
- 目标：用 `Stage70FullGameProof` 真的把全量批次跑完。
- proof 分层固定：
- `Card / Potion / Relic / Enchantment`：战斗 proof
- `Event`：多页、奖励、战斗返回 proof
- `passive_only`：导入、保存、运行时加载、描述 proof，不要求战斗触发
- 任一批次失败时固定处理：
- 立即定位到所属实现阶段
- 回退该阶段修复
- 补写该阶段日志
- 重新跑当前 batch
- 阶段日志固定为：
- `docs/progress/stage_71_command_driven_full_game_proof.md`
- 验收固定为：
- 所有 batch 命令执行成功
- `godot.log` 中所有预期 marker 齐全且全为 `pass`
- 没有遗漏实体，没有人工口头 proof 替代

### Stage 72 — Final Release Validation And Delivery
- 目标：把支持、描述、实机 proof 三条线一起收口成最终交付。
- 最终必须统一重跑：
- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
- `dotnet run --project tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- tools\stage03-smoke-run`
- `dotnet run --project tools\Stage59CoverageBaseline\Stage59CoverageBaseline.csproj -- .`
- `dotnet run --project tools\Stage61DescriptionRoundtrip\Stage61DescriptionRoundtrip.csproj -- .`
- `dotnet run --project tools\Stage65CoverageArtifacts\Stage65CoverageArtifacts.csproj -- .`
- `dotnet run --project tools\Stage70FullGameProof\Stage70FullGameProof.csproj -- <workspace>`
- 所有 `run_batch_*.ps1`
- 更新最终文档：
- [release_v1_todo.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/reference/release_v1_todo.md) 改为最终交付摘要
- `stage_61 / stage_66 / stage_70 / stage_71` 互相引用
- 最终 release 文档必须区分：
- 真实语义图支持
- `passive_only` 支持
- 已完成的实机 proof 范围
- 阶段日志固定为：
- `docs/progress/stage_72_final_release_validation_and_delivery.md`
- 验收固定为：
- 五类实体支持全绿
- description roundtrip 全绿
- 全量命令驱动实机 proof 全绿
- 最终文档落盘
- 当前仓库可直接作为交付版本

## Test Plan
- 每个阶段结束前都必须完成：
- 代码完成
- 本阶段门禁命令通过
- 本阶段日志 md 落盘
- 通用门禁命令固定为：
- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
- `dotnet run --project tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- tools\stage03-smoke-run`
- `dotnet run --project tools\Stage59CoverageBaseline\Stage59CoverageBaseline.csproj -- .`
- `dotnet run --project tools\Stage61DescriptionRoundtrip\Stage61DescriptionRoundtrip.csproj -- .`
- `dotnet run --project tools\Stage65CoverageArtifacts\Stage65CoverageArtifacts.csproj -- .`
- `Begone` 必须贯穿 `Stage 68 / 69 / 71` 三阶段，作为固定回归样本：
- 默认节点值正确
- 改值后保存/重载正确
- 描述句式正确
- 游戏内执行正确

## Assumptions And Defaults
- 默认继续使用当前 proof 工具已经验证过的 Windows / Steam 安装路径和 `godot.log` 解析方式。
- 执行开始后自动创建并复用 4 个子 agent，不再等待手动分派。
- `debug.log` 唯一动作图只允许作为开发过渡，不计入最终支持。
- `passive_only` 只允许进入显式 allowlist，且只用于确实没有主动 gameplay 行为的实体。
- 这份计划落地到 `codex_plan_release_v1.md` 后，后续执行默认持续推进到 `Stage 72` 全通过为止，不再中途停下来等用户说“继续”。

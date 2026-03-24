# Stage 49 - Auto Graph 节点覆盖补全

## 已开发内容
- 补齐了高频 auto-import 命令族的节点接线与导入映射，重点包括：
  - `CreatureCmd.Damage`
  - `CardCmd.ApplySingleTurnSly`
  - `CardPileCmd.AutoPlayFromDrawPile`
  - `CardPileCmd.ShuffleIfNecessary`
  - `CreatureCmd.SetCurrentHp`
  - `OrbCmd.Passive`
  - `ForgeCmd.Forge`
  - `RewardsCmd.OfferCustom`
- 扩展了 `card.select_cards` 的选择模式支持：
  - `deck_for_enchantment`
  - `choose_a_card_screen`
  - `choose_bundle`
  - `simple_grid_rewards`
- 新增或补齐了以下 graph 节点：
  - `player.forge`
  - `reward.offer_custom`
  - `card.apply_single_turn_sly`
  - `cardpile.auto_play_from_draw_pile`
  - `orb.passive`
  - `creature.set_current_hp`
- Translator、Description、FieldChoiceProvider、字段显示名也同步补上，避免 importer 识别到了却无法落成节点或无法编辑。
- 当前 graph registry 已提升到 `68 definitions / 68 executors`。

## 未开发内容
- `RewardsCmd.OfferCustom` 目前先导成“可编辑 placeholder”节点，仍不是对所有原版奖励列表的 100% 精准还原。
- `CardSelectCmd.PushSelector` 这类自定义 selector 栈逻辑仍未自动翻译。
- 复杂奖励组合、复杂 bundle 选择、以及更深的 IL 参数恢复，仍属于后续精细化增强范围。

## 遇到的问题
- `CreatureCmd.Damage` 在源码里非常高频，但调用目标既可能是自伤，也可能是单体或群体，单靠方法名无法 100% 恢复原始目标来源。
- `AddGenerated*` / `AddCurses*` 这类命令只有在 IL 链里能追到 `CreateCard<T>` 时，才能较稳定回填 `card_id`。
- `RewardsCmd.OfferCustom` 只从方法调用层面很难精确恢复 reward list 的具体构成，所以第一版先做成可编辑 placeholder，避免整个 graph 为空。

## 后续如何解决
- 后续可继续增强 IL operand 解析，恢复更多常量参数、局部变量来源和构造器参数。
- 对 `RewardsCmd.OfferCustom` 可进一步做 reward constructor 级分析，把 `GoldReward / RelicReward / PotionReward / SpecialCardReward` 直接还原成更精确的 graph 节点参数。
- 对 `CreatureCmd.Damage` 可加入更多目标推断启发式，或在导入后自动标记“请确认目标”。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 结果：通过，`0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 结果：`PASS`
  - 当前摘要：`Definitions: 68, Executors: 68`

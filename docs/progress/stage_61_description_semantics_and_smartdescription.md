# Stage 61 - Description Semantics And SmartDescription

## 已开发内容
- 新增 `tools/Stage61DescriptionRoundtrip/Stage61DescriptionRoundtrip.csproj`
- 新增 `coverage/description-roundtrip/description_roundtrip.json`
- 新增 `docs/reference/description_roundtrip.md`
- `Stage61DescriptionRoundtrip` 会对 `Card / Relic / Potion / Event / Enchantment` 的全量 auto-graph 跑描述生成：
  - `TemplateDescription`
  - `PreviewDescription`
  - `UnsupportedNodeTypes`
  - 说明性 notes
- `Stage03SmokeTest` 继续覆盖描述生成、动态模板、公式预览
- `Stage59CoverageBaseline` 继续生成 `description_semantics_baseline.md`

## 未开发内容
- `description_roundtrip` 仍未清零
- 当前描述系统还缺少一批高频节点的模板化说明
- `smartDescription / formatter / placeholder` 的“游戏内显示完全一致性”仍缺真实游戏 proof

## 遇到的问题
- 当前剩余缺口已经不在 auto-graph 支持，而在描述生成器覆盖面
- `GraphDescriptionGenerator` 和 `GraphDescriptionTemplateGenerator` 对一批编辑/辅助节点仍未完全支持
- 一部分图虽然运行时可用，但模板描述仍为空或只依赖 fallback

## 后续如何解决
- 继续补 description node coverage，优先清：
  - `card.select_cards`
  - `player.add_pet`
  - `potion.procure`
  - `orb.add_slots / orb.remove_slots / orb.evoke_next`
  - `reward.card_options_*`
  - `modifier.*`
  - 其余辅助 `value.*` 节点的模板策略
- 清空 `docs/reference/description_roundtrip.md` 的 incomplete 样本
- 在此基础上再补真实游戏内 description proof

## 验证结果
- `dotnet run --project tools\\Stage61DescriptionRoundtrip\\Stage61DescriptionRoundtrip.csproj -- .`
  - 通过
  - 已生成 `docs/reference/description_roundtrip.md`
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
  - 通过
  - `graph description generation` PASS
  - `dynamic template and preview semantics` PASS
- `dotnet run --project tools\\Stage59CoverageBaseline\\Stage59CoverageBaseline.csproj -- .`
  - 通过
  - 已生成 `docs/reference/description_semantics_baseline.md`

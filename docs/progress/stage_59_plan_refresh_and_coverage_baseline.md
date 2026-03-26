# Stage 59 - Plan Refresh And Coverage Baseline

## 已开发内容
- 新建了第一版主计划文件 [codex_plan_release_v1.md](/F:/sts2_mod/mod_projects/STS2_editor/codex_plan_release_v1.md)，并明确锁定了 V1 的收口目标：
  - 全量 `卡牌 / 遗物 / 药水 / 事件 / 附魔` 自动转 Graph
  - 描述占位符 / formatter / `smartDescription` 全量兼容
  - 事件完整动作链 authoring
  - Graph Inspector 上下文感知显示
  - 中文化、强类型输入、覆盖工程/包/报告
- 新增了 Stage 59 基线审计工具：
  - [tools/Stage59CoverageBaseline/Stage59CoverageBaseline.csproj](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage59CoverageBaseline/Stage59CoverageBaseline.csproj)
  - [tools/Stage59CoverageBaseline/Program.cs](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage59CoverageBaseline/Program.cs)
- 该工具当前会扫描原版：
  - `Cards`
  - `Relics`
  - `Potions`
  - `Events`
  - `Enchantments`
- 审计产物已经生成：
  - [docs/reference/coverage_baseline.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/reference/coverage_baseline.md)
  - [docs/reference/description_semantics_baseline.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/reference/description_semantics_baseline.md)
  - [coverage/baseline/coverage_baseline.json](/F:/sts2_mod/mod_projects/STS2_editor/coverage/baseline/coverage_baseline.json)
  - [coverage/baseline/description_semantics_baseline.json](/F:/sts2_mod/mod_projects/STS2_editor/coverage/baseline/description_semantics_baseline.json)
- 当前基线结论已经可以明确看出 V1 最大缺口：
  - `Enchantment` 目前整体不在 auto-import 主路径
  - `Event` 目前整体还是 `missing_event_compiler_mapping`
  - 行为侧剩余缺口主要集中在 `missing_auto_import_mapping`
  - 描述侧已经确认原版确实存在大量 token、formatter 和 `smartDescription` 语义，必须单独收口

## 未开发内容
- Stage 59 目前只是建立了“覆盖基线”，还没有清零覆盖缺口。
- `coverage_baseline` 还没有把“已有 graph override”纳入真实项目级统计，当前默认按原版对象基线扫描。
- `description_semantics_baseline` 目前是“全局语义基线”，还没有做到对象级精确映射。
- 事件、附魔、复杂遗物/药水的自动转 Graph 仍未完成。
- Graph Inspector 的上下文感知显示规则还没有正式进入 Stage 63 实现。

## 遇到的问题
- 旧仓库里已经有不少阶段文档和部分实现，但缺少一个“面向 V1 发布”的统一基线，导致之前更多是修单点问题，不利于判断“还有哪些对象没覆盖”。
- 原版描述语义的扫描如果直接扫全部 localization，会引入大量无关 token，报告会变得不可读。
- 构建主工程时游戏进程如果占用 DLL，会导致复制到游戏目录失败；因此 Stage 59 的验证改用 `Compile` 和独立工具先完成，不依赖复制部署。

## 后续如何解决
- Stage 60 将直接以 [docs/reference/coverage_baseline.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/reference/coverage_baseline.md) 为清单，逐类补齐 auto-import 行为缺口。
- Stage 61 将以 [docs/reference/description_semantics_baseline.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/reference/description_semantics_baseline.md) 为清单，补齐 placeholder、formatter、`smartDescription` 兼容层。
- Stage 62 将优先收口 `Event`，因为当前 68 个事件全部是 `partial`，且这是用户当前最明显感知到的问题。
- Stage 63 将基于当前已有的 `SetPreviewContextVisible(...)` 和右侧属性生成逻辑，正式接入“实体类型 + 节点类型 + 值来源”的显示矩阵。

## 验证结果
- `dotnet run --project tools\\Stage59CoverageBaseline\\Stage59CoverageBaseline.csproj -- .`
  - 通过
  - 输出了 Stage 59 基线 JSON 和 reference 报告
- `dotnet msbuild STS2_Editor.csproj /t:Compile`
  - 通过
- 当前覆盖基线摘要：
  - `Card`: total `577`, supported `552`, partial `7`, unsupported `18`
  - `Relic`: total `290`, supported `218`, partial `35`, unsupported `37`
  - `Potion`: total `64`, supported `59`, partial `2`, unsupported `3`
  - `Event`: total `68`, supported `0`, partial `68`, unsupported `0`
  - `Enchantment`: total `23`, supported `5`, partial `3`, unsupported `15`

# Stage 48 - Dynamic Formula Template Rules And Preview Context Cleanup

## 已开发内容
- 重写 `DynamicValueEvaluator`，统一收敛了动态值的三种来源：
  - `Literal`
  - `DynamicVar`
  - `FormulaRef`
- 取消了编辑主路径中对 `preview_multiplier_value` 的依赖：
  - 公式预览现在只由“公式本身 + 右侧预览上下文”共同决定
  - `PreviewMultiplierValue` 仅保留为兼容旧数据字段，不再作为主编辑语义使用
- 重做了 Graph 模板描述规则：
  - 动态变量模式：
    - 不覆盖时保留原始 token，例如 `{Damage:diff()}`
    - 绝对值覆盖时显示固定值
    - 增量覆盖时显示 `3 + {Damage:diff()}` / `{Damage:diff()} - 2`
  - 公式模式：
    - 未改动公式时保留原始公式 token，例如 `{CalculatedDamage:diff()}`
    - 一旦改了基础值、额外值或上下文来源，则切换成显式公式模板，例如 `原基础值 + 2 x 手牌数`
- 重写了 `GraphDescriptionTemplateGenerator`，让 Graph 模板描述和右侧动态值摘要使用同一套规则。
- 重写了 `ModStudioFieldDisplayNames` 和 `FieldChoiceProvider` 中动态值相关字段的展示与枚举：
  - `preview_multiplier_key` 改成更清晰的“公式上下文来源”
  - 可选项统一显示为可理解的中文
- 右侧节点动态值摘要新增/明确区分：
  - 原版模板
  - Graph 模板
  - 数值预览
  - 描述预览
  - 计算说明

## 未开发内容
- 旧的 `BuildSelectedNodeDynamicSummaryText` / `BuildSelectedNodeDynamicSummaryTextV2` 仍留在文件中作为历史实现，当前主流程已切到 `V3`，后续可以在清理阶段删除。
- 兼容字段 `PreviewMultiplierValue` 仍保留在数据结构和旧图迁移路径里，后续若确认社区项目已迁移完，可考虑彻底移除。
- 这轮没有新增新的自动化实机验证脚本；仍以已有 smoke test 和用户人工验收为主。

## 遇到的问题
- 多个旧文件内仍有历史乱码字符串，直接原地 patch 容易匹配失败。
- `NModStudioProjectWindow.Tail.cs` 体积较大，局部替换旧方法时上下文容易因为编码问题失配。

## 后续如何解决
- 当前采用“新增 `V3` 方法并切调用”的低风险方案，避免继续在旧乱码块上做高风险替换。
- 后续若继续收 Graph，可做一轮专门的 `Tail.cs` 清理，把旧 `V1/V2` 动态摘要方法删除。
- 若后续再出现语义混淆，可继续补一个独立的“动态值帮助弹窗”，把 `DynamicVar / FormulaRef / 上下文预览` 的差异可视化说明。

## 验证结果
- `dotnet build STS2_Editor.csproj` 通过，`0 warning / 0 error`
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run` 结果 `PASS`
- Smoke test 中图描述输出为正常中文，例如：
  - `造成 8 点伤害，目标 当前目标。 获得 5 点格挡。`

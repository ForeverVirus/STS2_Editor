# Stage 49 - Event Option Runtime Action Editor Fix

## 已开发内容
- 修复了事件 Graph 里 `event.option` 节点“看起来只有标题能改”的问题。
  - 在右侧属性编辑器生成可编辑字段时，当前会先合并节点注册表里的默认属性。
  - 这意味着旧图或未显式写入完整属性的 `event.option / event.reward / event.start_combat / event.goto_page` 节点，也会稳定显示出应有的可编辑字段。
- 事件相关字段的右侧编辑器补齐了更多强类型选项：
  - `encounter_id`
  - `reward_target`
  - `reward_props`
  - `reward_power_id`
- 事件节点右侧摘要不再只适用于动态值节点。
  - 选中 `event.option` 时，现在会明确显示：
    - 当前显示文本
    - 页面 ID / 选项 ID
    - 实际奖励
    - 下一步动作
    - 并明确提示“标题和描述只是显示文本，不会自动改变真正执行的奖励”
  - 选中 `event.reward / event.proceed / event.goto_page / event.start_combat / event.page` 时，也会显示对应的运行时语义说明。
- `event.proceed` 的语义在 UI 中明确化：
  - 该节点只负责结束当前事件交互
  - 奖励、跳页、战斗不在 `proceed` 节点里配置，而是在 `event.reward / event.goto_page / event.start_combat` 或 `event.option` 的奖励字段里配置

## 未开发内容
- `page_id / next_page_id / resume_page_id` 目前仍然主要是自由文本输入，还没有做成“同图页面下拉选择器”。
- `event.option` 的标题文本还没有和奖励字段做自动联动模板生成。
  - 目前仍然是“显示文本”和“实际奖励字段”分离的设计。
- `reward_kind` 的显示名映射仍有历史编码残留，虽然不影响运行时，但部分值在极个别位置可能仍显示为原始 key。

## 遇到的问题
- 事件运行时后端本身一直是有奖励/跳页/战斗能力的，问题主要出在 UI：
  - 右侧编辑器只按当前 `node.Properties` 展示字段
  - 旧图或只写了少数属性的节点，就会表现成“只有标题”
- 工程里部分旧文件存在编码污染，导致直接在原有中文映射函数上做局部替换风险较高。

## 后续如何解决
- 下一步优先补事件页面 ID 的图内下拉选择器，这样 `next_page_id / resume_page_id` 就不需要作者手打。
- 如果后续事件作者体验仍然不够直观，可以再补一层：
  - 当 `event.option` 配置了 `reward_kind / reward_amount` 时，自动生成推荐标题模板
  - 例如“获得60金币”“受到8点伤害”
- 后续继续做全量原版事件 auto-graph 覆盖时，要一起补“事件动作节点与右侧语义摘要”的回归验证。

## 验证结果
- `dotnet msbuild STS2_Editor.csproj /t:Compile`
  - 通过
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
  - 结果 `PASS`

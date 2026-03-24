# Stage 57 - ZAP Orb Channel Description And Default Orb Fix

## 已开发内容
- 修复 `orb.channel` 未进入 graph 描述系统的问题。
- 新增 `orb.channel` 的模板描述、预览描述和节点卡片描述生成。
- 新增球体名称解析，描述会优先显示原版本地化球体名称，而不是回退到 scaffold。
- 新增空下拉默认值回写逻辑，避免 `orb_id` 这类字段“视觉上已选中、实际仍为空”。
- 新增 `orb.channel / orb.passive` 节点创建时的默认 `orb_id` 填充，减少新节点空值保存。
- 补充 smoke test，覆盖双 `orb.channel` graph 的描述生成。

## 未开发内容
- 还没有对所有球体相关卡牌做逐张真机验收。
- 还没有补“旧工程里已保存的空 `orb_id` 节点”自动迁移脚本；当前依赖重新保存 graph 修复。

## 遇到的问题
- `ZAP` 导出包中的第二个 `orb.channel` 节点实际保存成了空 `orb_id`，导致运行时只充能一个球。
- `GraphDescriptionGenerator` 和 `GraphDescriptionTemplateGenerator` 都不认识 `orb.channel`，因此一旦图里只有球体节点，就会回退到 scaffold 描述。

## 后续如何解决
- 继续真机扫 `电击`、`双重释放`、以及其他球体相关牌，确认不再出现空 `orb_id`。
- 如果发现旧项目里仍有空 `orb_id` 历史数据，再补一层项目加载期迁移修正。

## 验证结果
- `dotnet build STS2_Editor.csproj` 通过。
- `Stage03SmokeTest` 已补双球体 graph 断言，用于防止再次回退到 scaffold。

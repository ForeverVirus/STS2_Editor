# Stage 37 - Dynamic Preview Context And Description Sync

## 已开发内容
- 新增动态预览上下文：
  - `DynamicPreviewContext`
  - `DynamicPreviewService`
- 新增描述模板生成：
  - `GraphDescriptionTemplateGenerator`
- `GraphDescriptionGenerator` 已改为双轨输出：
  - `TemplateDescription`
  - `PreviewDescription`
- `ProjectMode` 的 Graph 右栏已加入预览上下文编辑区，当前至少支持：
  - 升级状态
  - 目标
  - 当前格挡
  - 当前星数
  - 当前能量
  - 手牌数
  - 抽牌堆/弃牌堆/消耗堆数量
  - 已损失生命
- `ProjectWindow` 已把预览上下文接进 Graph 绑定与描述刷新链，节点说明和 Graph 自动描述会跟随上下文变化刷新。
- 保存 Graph 时，自动描述会按当前动态预览结果同步回 `Basic` 描述预览链路。

## 未开发内容
- 还没有做更复杂的“战斗局面预设管理器”，当前预览上下文仍是单组编辑值。
- 对部分复杂 formatter 的预览仍属于近似预览，不代表 100% 还原所有复杂战斗态。

## 遇到的问题
- `ProjectWindow` 在并行重构中出现过尾部方法块截断，导致 Graph/描述/保存链路残缺；已通过 partial 文件恢复。
- 预览上下文最初只存在于类型层，没有真正接进 UI 事件流；这一阶段补齐了接线。

## 后续如何解决
- Stage 40 继续把 Graph 右栏剩余英文/不清晰字段收口到中文化和强类型控件。
- 后续如需更强的编辑体验，可增加多套预览上下文预设。

## 验证结果
- `dotnet build STS2_Editor.csproj` 通过
- `Stage03SmokeTest` 通过
- 自动描述与节点动态说明已重新接入同一条预览上下文链路


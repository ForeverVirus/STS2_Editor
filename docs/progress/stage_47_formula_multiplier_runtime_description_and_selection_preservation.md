# Stage 47 - Formula Multiplier Semantics, Runtime Description Sync, and Selection Preservation

## 已开发内容
- 调整了公式引用节点的乘数语义：
  - 选择了上下文乘数来源时，`乘数系数` 作为缩放系数参与计算
  - 未选择来源时，`乘数系数` 作为直接乘数使用
- Graph 右侧的 `preview_multiplier_key / preview_multiplier_value` 帮助说明已经更新，避免“看起来没生效”的误解。
- 修复了 Graph 右侧“预览上下文”任意字段变更后，当前选中节点被清空的问题：
  - 不再在上下文变更时整张图重新绑定
  - 改为仅更新画布的预览上下文和节点展示
- 调整了 Graph 自动描述的应用顺序：
  - 编辑器内部的 `applied description` 现在优先使用预览描述，模板描述作为回退
- 新增运行时 Graph 描述服务：
  - 对启用了 Graph 行为的卡牌，在运行时读取卡牌描述时优先生成当前 Graph 预览描述
  - 这样可以避免卡牌仍显示旧的动态 token 文本，降低“执行逻辑已改但卡面描述没变”的割裂感
- 同步扩展了公式引用在运行时的乘数计算：
  - 支持从真实战斗上下文读取 `energy / hand_count / stars / current_block / draw_pile / discard_pile / exhaust_pile / missing_hp`

## 未开发内容
- 还没有把“公式来源说明”做成单独的可点击帮助弹窗，目前仍是 tooltip + 节点动态摘要两种形式。
- 运行时描述目前优先返回 Graph 预览描述，不是“真正重新接入原版 token 格式化链后的新公式 token”。
- 还没有把 Graph 右侧字段名里与“乘数”相关的中文标签进一步细化成更口语化的命名。

## 遇到的问题
- 之前 `preview_multiplier_key` 在预览中优先级高于 `preview_multiplier_value`，导致用户看到“填了值但没作用”。
- 之前 `OnPreviewContextChanged` 会重新 `BindGraph(...)`，从而把选中节点状态清空。
- 之前即使 Graph 已经改成公式引用，保存后描述链仍更偏向原 token，给人“是不是根本没生效”的感受。

## 后续如何解决
- 下一轮如果用户仍觉得公式引用不够直观，可以继续补一个专门的“公式说明”按钮或 popup。
- 若后续仍需要保留“模板 token 显示”和“运行时实际显示”两种模式，可在 Graph 右侧加入显示策略切换。
- 如果需要进一步提升作者理解成本，可以把“乘数来源 + 乘数系数 + 当前上下文值 + 最终乘数”做成单独的可视化卡片，而不是只放在摘要文本里。

## 验证结果
- `dotnet build STS2_Editor.csproj`
  - 通过，`0 warning / 0 error`
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
  - 通过，`PASS`

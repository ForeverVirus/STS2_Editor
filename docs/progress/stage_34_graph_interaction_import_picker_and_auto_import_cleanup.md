# Stage 34 - Graph Interaction, Import Picker, And Auto Import Cleanup

## 已开发内容
- Graph 右键空白区域改为走 `GraphEdit.PopupRequest`，不再由自定义 `GuiInput` 粗暴拦截全部右键事件。
- Graph 画布启用了 `GraphEdit` 的 `PanningScheme` 和小地图，以减少自定义输入逻辑对连线/断线的干扰。
- 手动连线现在会立即调用 `ConnectNode(...)` 同步到画布；手动断线会立即调用 `DisconnectNode(...)`。
- Graph 左上角提示条改为固定宽度且不自动换行，避免再次出现竖排单字提示。
- 节点描述改为优先根据当前属性实时生成：
  - `combat.damage`
  - `combat.gain_block`
  - `combat.heal`
  - `combat.draw_cards`
  - `player.gain_energy`
  - `player.gain_gold`
  - `player.gain_stars`
  - `combat.apply_power`
- 右侧 Graph 信息区现在会随着节点属性修改实时刷新，自动描述不再只有保存后才变化。
- `导入` 按钮改成先弹选择器，不再默认直接导入当前条目的原始 graph。
  - 支持同类型项目条目和游戏内条目搜索
  - 选择后会复制 source graph，而不是共享引用
- 原版反射导入增加了“连续重复步骤压缩”启发式，减少同一个原始调用链被错误翻译成重复节点的概率。

## 未开发内容
- 尚未在真实游戏里逐项验证“连线选中”和“线级交互”的最终手感。
- `导入` 选择器当前按同一实体类型列出 source 条目，但还没有做更细的来源分组和更丰富的摘要。
- 自动导入重复步骤压缩目前是启发式，不保证一次性覆盖所有原版硬编码链式 builder 情况。

## 遇到的问题
- 之前自定义的背景左键拖拽和平移会拦住 `GraphEdit` 自己的线级交互，这也是“看起来能拖线但松手不连上”的重要原因之一。
- 自动导入阶段会把 IL 中连续重复的同类调用都转成 step，导致某些卡牌看上去像“多打了一次伤害”。

## 后续如何解决
- 下一轮如果你实测后仍觉得“线不可选中 / 不好断开”，我会继续往 `GraphEdit` 原生信号深入，补线级选择状态和更明确的断线入口。
- 如果自动导入的某些卡牌仍然出现异常链路，需要继续细化 `NativeBehaviorGraphAutoImporter` 对 builder/包装调用的过滤规则。
- 如果导入选择器的工作流还不够顺手，可以继续补“按项目 / 游戏分区”和“复制当前卡/遗物/药水的 graph 模板”。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 通过，`0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 结果 `PASS`
- 本阶段仍缺少真实游戏内 Graph 手工交互复验，需要你在游戏内确认连线、断线、导入选择器和自动描述是否符合预期。

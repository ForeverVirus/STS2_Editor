# Stage 38-39 Graph Node Coverage And Event Graph Compiler

## 已开发内容
- 补齐了 Graph 内置节点定义与执行器，覆盖以下节点：
  - `combat.discard_cards`
  - `combat.exhaust_cards`
  - `combat.create_card`
  - `combat.remove_card`
  - `combat.transform_card`
  - `combat.repeat`
  - `player.lose_hp`
  - `player.gain_max_hp`
  - `event.page`
  - `event.option`
  - `event.goto_page`
  - `event.proceed`
  - `event.start_combat`
  - `event.reward`
- `NativeBehaviorGraphTranslator` 已升级：
  - 对支持的数值节点尽量保留动态绑定而不是硬编码固定数值
  - 自动导入会优先把原版动态变量翻译成结构化动态值定义
- 新增事件 Graph 编译链：
  - `EventGraphCompiler`
  - `EventGraphValidationResult`
  - `EventGraphPageDefinition`
  - `EventGraphChoiceBinding`
  - 事件 Graph 会被编译成 `RuntimeEventTemplateSupport` 能消费的 metadata 键
- `RuntimeEventTemplateSupport` 已扩展：
  - 读取 `event_start_page_id`
  - 读取 `event_page.*` / `event_option.*`
  - 支持简单即时奖励字段

## 未开发内容
- Graph 右侧属性面板的事件专用编辑器仍然比较基础，尚未完全做到“页面 / 选项 / 跳页 / 奖励 / 战斗”的完整可视化作者体验。
- 事件 Graph 的运行时编译还只覆盖了可直接落到 metadata 的一部分结构，复杂分支和更细粒度的事件脚本仍需后续补齐。
- 自动转 graph 的动态值覆盖还没有完全扩展到所有原版动态变量类型。

## 遇到的问题
- `NModStudioProjectWindow` 当前工作区里原先存在一段 UI 尾部方法缺失和 partial 拆分不完整的问题，导致整工程编译被阻塞。
- `NModStudioProjectWindow.Tail.cs` 还缺少少量命名空间引用和辅助方法，例如 `Dual`、`BuildPreviewContext`、`ResolveSourceModel`、`MetadataOrFallback`。
- 某些事件 reward 类型无法直接在现有即时 runtime 入口里完整落地，只能先做可行的即时奖励子集。

## 后续如何解决
- 继续把 Graph 的动态值体系往原版 `DynamicVar` / `CalculatedVar` / formatter 方向补齐，减少固定数值退化。
- 继续扩展事件 Graph 的节点语义覆盖，把页面、选项、奖励、战斗、返回等能力做得更完整。
- 继续把 UI 侧的事件 Graph 属性编辑器做强类型化，减少作者手输错误。
- 后续若再出现 partial 拆分问题，优先用新增 helper partial 的方式补齐，不再直接大改主 UI 文件。

## 验证结果
- `dotnet build STS2_Editor.csproj` 通过，`0 warning / 0 error`
- `Stage03SmokeTest` 通过，Graph registry、description generation、package roundtrip、session negotiation 都保持正常

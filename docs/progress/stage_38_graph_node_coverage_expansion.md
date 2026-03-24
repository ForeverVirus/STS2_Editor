# Stage 38 - Graph Node Coverage Expansion

## 已开发内容
- 补齐了本阶段计划中的 Graph 节点定义与执行器：
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
- `BuiltInBehaviorNodeDefinitionProvider` 与 `BuiltInBehaviorNodeExecutors` 已同步覆盖上述节点。
- `BehaviorGraphExecutor` 仍可正常注册并执行整套内置节点。
- `NativeBehaviorGraphTranslator` / `NativeBehaviorGraphAutoImporter` 已升级，自动转 Graph 时尽量保留动态值语义。
- `GraphDescriptionGenerator` 已纳入新增节点类型，不会再把这些节点全部当作未知项。

## 未开发内容
- 复杂 reward 语义仍有扩展空间，目前优先覆盖 Phase 1 常见玩法节点。
- 自动导入仍是“尽量保留动态语义”，不是对所有原版硬编码逻辑的完全逆向表达。

## 遇到的问题
- 节点数量扩展后，自动描述、自动导入、Graph UI 侧的分组和显示名需要同步扩展，否则会出现“节点能执行但作者难以理解”的问题。

## 后续如何解决
- Stage 39 将新增事件 Graph 编译链与 runtime metadata 接线。
- Stage 40 继续补节点面板的中文化、下拉化和可读性。

## 验证结果
- `dotnet build STS2_Editor.csproj` 通过
- `Stage03SmokeTest` 通过
- 构建时节点/执行器注册数已提升到 `31`


# Stage 39 - Pure Graph Event Authoring

## 已开发内容
- 新增事件 Graph 编译链：
  - `EventGraphCompiler`
  - `EventGraphValidationResult`
  - `EventGraphPageDefinition`
  - `EventGraphChoiceBinding`
- 事件 Graph 现在可以编译成 runtime metadata：
  - `event_start_page_id`
  - `event_page.*`
  - `event_option.*`
- `RuntimeOverrideResolver` 已把事件 Graph 编译接入运行时覆盖解析：
  - 当事件实体启用 `BehaviorSource.Graph` 时，会先编译再写入 metadata 覆盖
- `RuntimeEventTemplateSupport` 继续复用原有模板后端消费编译结果，不需要另起一套事件 runtime。
- 事件 Graph 节点已经覆盖：
  - 页面
  - 选项
  - 跳页
  - Proceed
  - 战斗并返回
  - 奖励

## 未开发内容
- 更复杂的脚本型事件、稀有奖励组合、完整即时 reward 语义仍有扩展空间。
- 当前重点是“可编译、可消费 runtime metadata”的第一版，不是事件编辑器的最终形态上限。

## 遇到的问题
- 事件 runtime 后端内部模板结构并没有公开，需要通过 metadata 约定来对接，而不是直接构造内部对象。
- Graph 里的事件动作链需要编译器做遍历和归并，否则 runtime 端无法消费。

## 后续如何解决
- 后续如要扩展事件玩法，只需继续扩展事件节点和 `EventGraphCompiler` 的 metadata 映射。
- 可在后续阶段继续补更完整的 reward 即时应用语义。

## 验证结果
- `dotnet build STS2_Editor.csproj` 通过
- `Stage03SmokeTest` 通过
- 事件 Graph 编译链已接入 runtime override 解析主链


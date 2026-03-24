# Stage 46 - Graph Template/Preview Split And Dynamic Value Usability

## 已开发内容
- 修复了“点开 Graph 页签就强制改写 Basic 描述”的问题。
  - 现在 Graph 实时编辑过程中，不再把 `Basic` 页里的描述字段自动改成 Graph 预览文本。
  - Graph 预览、模板、节点说明仍会实时刷新，但真正写回实体描述只在 `Save Graph` 时执行。
- 调整了 Graph 自动描述的应用策略。
  - 自动生成描述时，优先使用 `TemplateDescription` 作为保存用描述。
  - 若没有模板描述，才回退到 `PreviewDescription`。
  - 这避免了像 `{Damage:diff()}` 这种原本应为动态模板的描述，被错误写成固定值。
- Graph 右侧详情区现在会同时显示：
  - 原版描述
  - 模板描述
  - 预览描述
  - 节点数 / 连线数 / Graph ID / 模式
- 新增节点级动态值摘要面板。
  - 选中节点后，右侧会显示：
    - 值来源
    - 动态变量
    - 公式引用
    - 当前模板
    - 数值预览
    - 描述预览
    - 计算说明
  - 这样作者能直接看懂当前节点是固定值、原版动态变量还是原版公式。
- 动态值字段按来源裁剪显示。
  - `固定值`：只显示固定数值。
  - `原版动态变量`：只显示动态变量和基础值覆盖。
  - `原版公式`：显示动态变量、公式引用、基础值覆盖、额外值覆盖、预览乘数。
  - 不再把与当前来源无关的字段全塞给用户。
- 为 Graph 动态值相关字段增加了悬浮说明。
  - 包括：
    - 值来源
    - 动态变量
    - 公式引用
    - 基础值覆盖方式/数值
    - 额外值覆盖方式/数值
    - 预览乘数键/值
    - 固定数值
    - 目标
    - 属性标记
- 清理了 Graph 描述生成链和字段选择器中的乱码中文。
  - `GraphDescriptionGenerator`
  - `GraphDescriptionTemplateGenerator`
  - `FieldChoiceProvider`

## 未开发内容
- 还没有做“字段级动态值帮助弹窗/按钮”的专门交互，目前使用的是：
  - Graph 总览中的模板/预览文本
  - 节点动态值摘要
  - 属性字段悬浮说明
- `GraphDescriptionTemplateGenerator` 目前仍主要覆盖数值类节点模板，对更复杂的事件节点/组合节点模板描述还可继续增强。
- 还没有把“原版动态变量”和“原版公式”的完整源码表达式反编译成更接近源码级的人类可读文本，目前展示的是：
  - 模板 token
  - 当前预览值
  - 计算摘要

## 遇到的问题
- 之前 Graph 实时刷新链把 `PreviewDescription` 当成了自动描述真值，导致只要切到 Graph 或改节点，就会把 `Basic` 描述改成固定预览文本。
- 动态值虽然在后端已经支持 `Literal / DynamicVar / FormulaRef`，但前端没有把“模板”和“预览”分开展示，作者很难判断：
  - 最终游戏卡面会显示什么
  - 编辑器当前是在用什么上下文预览
  - 哪些字段只对公式模式有效
- 部分 Graph 右栏和选择器中仍有旧乱码，影响动态值字段理解。

## 后续如何解决
- 如果后续你仍觉得“公式到底是什么样”还不够直观，下一步建议做专门的“公式说明弹窗”：
  - 显示原版变量来源
  - 显示 base / extra / multiplier 的结构化说明
  - 显示当前预览上下文下的代入过程
- 对于更复杂的原版公式，如果能继续从源码里提取更多 `DynamicVar / CalculatedVar` 关系，可以进一步把节点级摘要做得更接近真实公式。
- 事件 Graph 和复杂复合节点的模板描述后续还可以继续增强，但不影响本轮核心可用性修复。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 结果：通过
  - `0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 结果：`PASS`

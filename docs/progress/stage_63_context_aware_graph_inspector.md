# Stage 63 - Context-Aware Graph Inspector

## 已开发内容
- Graph 右侧 Inspector 已从“统一堆字段”推进到“按上下文裁剪显示”。
- 当前已经落地的上下文感知规则包括：
  - `Event` 节点默认不再显示卡牌/战斗用的 preview context。
  - `flow.entry / flow.exit / event.proceed` 不再显示无意义的属性输入。
  - 动态值属性会按 `Literal / DynamicVar / FormulaRef` 三种来源裁剪：
    - `Literal` 只保留固定值主字段
    - `DynamicVar` 只保留动态变量与基础覆盖字段
    - `FormulaRef` 只保留公式、base/extra 和上下文来源字段
- 事件节点属性区已按节点类型拆开：
  - `event.page`
  - `event.option`
  - `event.reward`
  - `event.goto_page`
  - `event.start_combat`
  - `event.proceed`
- 事件 authoring 相关的图内动态选择器已经接入到 Inspector：
  - `next_page_id`
  - `resume_page_id`
  - `page_id`（对引用页的节点）
  - `option_order`
- `option_order` 不再是纯手填 CSV：
  - 现在是可增删列表
  - 列表项来源于当前图内 `event.option` 节点
- 相关代码：
  - [ModStudioProjectDetailPanel.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioProjectDetailPanel.cs)
  - [NModStudioProjectWindow.Tail.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.Tail.cs)

## 未开发内容
- `page_id` 对 `event.page` 本身仍然保留自由输入，没有做成“重命名页 ID”的专用安全编辑器。
- `option_order` 当前支持增删和选项来源联动，但还没有做拖拽排序。
- Inspector 的上下文矩阵目前主要完成了：
  - Event
  - Flow
  - DynamicValue
  还没有把所有 Card / Relic / Potion / Enchantment 的节点族全部逐一细化到最终态。
- 右侧字段的中文显示名虽然已明显改善，但仍然有部分历史字段需要继续补中文标签映射。

## 遇到的问题
- 右侧属性编辑器原本是“给什么 key 就直接渲染什么”，没有上下文层，导致事件节点会看到大量无意义字段。
- `option_order` 这种图内引用字段如果继续用通用 `LineEdit`，不仅容易输错，而且根本不适合新手作者。
- 事件引用型字段不能只靠静态 `FieldChoiceProvider`，必须引入当前图上下文才能正确生成候选项。

## 后续如何解决
- 在 `Stage 62` 继续把事件 authoring 收完整时，会把：
  - `page_id`
  - `next_page_id`
  - `resume_page_id`
  - `option_order`
  的图内引用编辑再做深一层，补齐排序和更安全的重命名路径。
- 在 `Stage 64` 继续补齐 Inspector 剩余字段的中文化与强类型化。
- 在后续对 Card / Relic / Potion / Enchantment 节点族补齐时，继续细化当前的 Inspector 过滤矩阵，直到所有主要节点都只显示真正有意义的信息。

## 验证结果
- `dotnet msbuild STS2_Editor.csproj /t:Compile`
  - 通过
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
  - 结果 `PASS`

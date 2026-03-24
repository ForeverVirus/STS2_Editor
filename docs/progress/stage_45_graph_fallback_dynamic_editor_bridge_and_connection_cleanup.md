# Stage 45 - Graph Fallback, Dynamic Editor Bridge, And Connection Cleanup

## 已开发内容
- 修复了 `ProjectMode` 中原版自动转 graph 失败时的空画布问题。
  - 当当前实体没有现成 graph、自动转 graph 也失败时，不再直接清空 graph 绑定。
  - 现在会自动创建一个带 `Entry / Exit` 的默认 scaffold，并绑定完整节点注册表。
  - 这样即使像“冲锋！”这类特殊效果卡牌暂时不能自动转 graph，也仍然可以手工开始编辑。
- 修复了右键添加节点时节点列表为空的问题。
  - 根因是之前空 graph 分支直接 `ClearGraph()`，导致 `GraphCanvasView` 的 registry 也被清空。
  - 改成 scaffold fallback 后，右键节点菜单会继续显示完整可用节点。
- 修复了 graph 重建后旧连线残留的问题。
  - 在 `ModStudioGraphCanvasView.ClearGraphEdit()` 中，重建节点前会先尝试清空 `GraphEdit` 里已存在的连接层。
  - 这样可以避免旧图上的 `damage -> exit` 视觉残影和新图叠在一起。
- 把动态值/公式编辑真正接到了右侧 Graph 属性编辑器。
  - 不再只把 `node.Properties` 传给详情栏。
  - 现在会把 `node.DynamicValues["amount"]` 桥接成结构化编辑字段：
    - `amount`
    - `dynamic_source_kind`
    - `dynamic_var_name`
    - `formula_ref`
    - `base_override_mode`
    - `base_override_value`
    - `extra_override_mode`
    - `extra_override_value`
    - `preview_multiplier_key`
    - `preview_multiplier_value`
    - 有值时还会暴露 `template_text / preview_format`
- 兼容了旧图里的遗留动态字段格式。
  - 旧项目若仍使用 `amount_source_kind / amount_var_name / amount_formula_ref` 这套 legacy key，右侧面板现在会自动转换成新的结构化动态值编辑项。
  - 同时隐藏 legacy key，避免一张节点出现两套重复编辑项。
- 动态值编辑修改后会立即参与图描述和节点描述刷新。
  - 右侧修改动态值来源、公式引用、base/extra 覆盖后：
    - 节点卡片描述会刷新
    - 节点属性摘要会刷新
    - Graph 概览和自动描述会刷新

## 未开发内容
- 还没有做“全卡牌自动转 graph 覆盖率审计工具”。
  - 当前修复的是系统级 fallback：即使自动转失败，也能手工编辑。
  - 但还没有输出一份“哪些卡牌仍无法自动转 graph”的完整统计。
- 还没有完全证明 `damage.out -> exit` 与 `damage.out -> gain -> exit` 的问题 100% 都是视觉残影。
  - 目前已优先修复最可能的根因：重建时旧连接层未清空。
  - 仍需要实机再次验证是否还有真正的数据层重复连接案例。
- 预览乘数字段相关的旧乱码残留仍需继续清理。
  - 本轮主目标是先修 Graph 可用性和动态值编辑桥接。

## 遇到的问题
- 原版特殊效果卡牌无法自动转 graph 时，UI 直接走 `ClearGraph()`，把节点注册表一起清掉，导致作者进入 Graph 后既没有图，也没有节点菜单。
- 动态值后台结构虽然已经存在，但右侧属性面板仍只读取 `node.Properties`，导致 `DynamicVar / FormulaRef / base / extra` 这些能力在 UI 中完全不可见。
- Graph 重建时只移除了节点视图，没有主动清 GraphEdit 的连接层，导致旧连线可能残留在画布上，和新图叠加。

## 后续如何解决
- 下一步优先建议做两类回归：
  - 特殊效果卡牌批量抽查，确认“自动转失败 -> scaffold fallback”已经稳定可用。
  - 多步原版效果卡牌抽查，确认不再出现重复 `-> exit` 的残留连线。
- 如果后续仍发现少量卡牌自动图中存在真实重复连接，而不是视觉残影，需要继续审计 `NativeBehaviorGraphTranslator` 的 step 串接逻辑并加入图规范化去重。
- 预览乘数字段和少量动态值相关下拉文本仍建议在下一轮做一次集中中文清理。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 结果：通过
  - `0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 结果：`PASS`

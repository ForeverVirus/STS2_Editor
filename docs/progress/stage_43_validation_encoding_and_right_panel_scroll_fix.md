# Stage 43 - Validation Encoding And Right Panel Scroll Fix

## 已开发内容
- 修复了 `ProjectMode` / Graph 相关界面中的中文乱码，覆盖以下区域：
  - Graph 顶部工具条与状态栏
  - 右侧详情栏标题、页签、Graph 字段、节点字段
  - 字段显示名与 Graph 本地化目录
  - Graph 自动描述与模板描述中文文本
  - ProjectMode 里的项目打开/新建/未打开项目提示
- 为右侧详情栏加入整体纵向滚动能力，避免 Graph 新增字段后下半部分超出屏幕无法编辑。
- 保留原有 Graph 属性编辑逻辑，只调整滚动与文案，不改变数据结构和保存链路。

## 未开发内容
- 这轮没有继续扩展新的 Graph 节点能力。
- 这轮没有继续推进事件 Graph 的新功能，只做验证暴露问题的修复。
- 右侧栏当前是纵向滚动优先方案，后续如果字段继续增多，可以再考虑分组折叠。

## 遇到的问题
- 多个 UI 文件中存在已经损坏的中文字面量，导致中文模式下直接显示乱码。
- Graph 右侧栏随着动态值和预览上下文字段增加，原有静态布局超出可视区域。
- 乱码并不集中在一个文件里，而是同时存在于 UI 文案、字段显示名、Graph 描述生成器中。

## 后续如何解决
- 如果后续再新增中文文案，统一优先进入 `ModStudioLocalizationCatalog` 或字段显示名映射，避免再次散落硬编码。
- 如果右侧 Graph 面板继续膨胀，下一步建议把“预览上下文”和“节点属性”改成可折叠分组。
- 若后续人工验收仍发现漏网乱码，再按相同方式继续做全工程文案审计。

## 验证结果
- `dotnet build STS2_Editor.csproj`
  - 0 warning / 0 error
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
  - PASS

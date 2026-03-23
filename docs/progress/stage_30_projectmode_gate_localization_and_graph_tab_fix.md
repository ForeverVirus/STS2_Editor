# Stage 30 - ProjectMode Gate, Field Localization, And Graph Tab Fix

## 已开发内容
- `ProjectMode` 现在不再默认直接进入可编辑状态。
  - 当当前没有已打开项目时，会先显示全屏 gate overlay。
  - gate overlay 提供：
    - `新建项目`
    - `打开项目`
    - `返回模式选择`
  - 只有完成新建或打开项目后，真正的全屏编辑工作区才会显示。
- `Basic` 中栏和右侧只读信息里的字段名做了结构化中文化。
  - 新增字段显示名映射：[ModStudioFieldDisplayNames.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioFieldDisplayNames.cs)
  - `Basic` 表单不再直接显示原始 metadata key。
  - 右侧只读信息也改为显示中文字段名。
- `Graph` 页签的 `connections_layer is missing` 运行时错误做了修复。
  - [ModStudioGraphCanvasView.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioGraphCanvasView.cs)
  - 现在 graph 绑定和画布重建改为 deferred queue 流程，不再在 `GraphEdit` 内部层尚未准备好时立即加节点和连线。
- 当前分类不支持 `Assets` 或 `Graph` 的时候，对应页签会被隐藏。
  - 目前：
    - `Character` 不显示 `Assets` / `Graph`
    - `Card / Relic / Potion / Event / Enchantment` 按现有 Phase 1 能力显示
- 右侧栏主 tab 已隐藏，改成自动跟随中间栏当前页签，不再出现“右侧切走了但中间没变”的理解成本。

## 未开发内容
- 这轮还没有拿到真实游戏内对 `Graph` 页签点击后的最新日志回传，因此最终实机确认还需要再验一次。
- 字段中文名目前先覆盖了 Phase 1 常用字段，若后续再扩展更多 metadata 字段，需要继续补映射表。

## 遇到的问题
- `error_log.txt` 中的 `connections_layer is missing` 根因是 `GraphEdit` 的内部层还没准备好时，`ModStudioGraphCanvasView` 就开始执行 `AddChild` 和 `ConnectNode`。
- 原来的 `ProjectMode` 状态流允许“无项目也能直接进入编辑器”，这会让空工程状态下的用户体验非常差，也容易让后续操作语义变得含糊。

## 后续如何解决
- 真实游戏里再验证一次：
  - 进入 `ProjectMode` 是否先出现项目入口 gate
  - 选 `Character` 时是否自动只剩 `Basic`
  - 选 `Card` 后点 `Graph` 是否不再报 `connections_layer is missing`
- 如果 graph 仍有残余时序问题，下一步会继续把 `ApplyConnections` 改成更强约束的“至少延后一帧”流程，而不只是当前的 deferred rebuild。
- 如果你后续补充更多可编辑字段，我会继续把这些字段的中文说明同步补到字段映射层里。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - `0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - `PASS`

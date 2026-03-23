# Stage 27 - ProjectMode Blank Screen Initialization Fix

## 已开发内容
- 根据 [error_log.txt](/F:/sts2_mod/mod_projects/STS2_editor/error_log.txt) 定位了 `ProjectMode` 打开后整屏空白的根因：
  - `NModStudioProjectWindow.BuildUi()` 在外层接线时，过早访问了子控件的内部节点
  - 但这些子控件自己的 `_Ready()` 还没执行，内部字段仍是 `null`
- 为相关复合控件补了显式 `EnsureBuilt()`：
  - [ModStudioBasicEditor.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioBasicEditor.cs)
  - [ModStudioAssetEditor.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioAssetEditor.cs)
  - [ModStudioGraphEditor.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioGraphEditor.cs)
  - [ModStudioCenterEditor.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioCenterEditor.cs)
  - [ModStudioProjectDetailPanel.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioProjectDetailPanel.cs)
- 在 [NModStudioProjectWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.cs) 里，创建 `ModStudioCenterEditor` 和 `ModStudioProjectDetailPanel` 后，先调用 `EnsureBuilt()` 再做事件接线。
- `ModStudioGraphEditor` 同时顺手做了小幅清理：
  - `CanvasView` 改为惰性可用属性
  - 文案统一回到正常中文/英文

## 未开发内容
- 这轮没有做桌面级自动点击验证，所以还需要一轮真实游戏内人工确认：
  - 进入 `ProjectMode`
  - 确认主窗体正常显示
  - 确认左中右三栏都能看到
- 其他 UI 细节问题如果还有，已经不再是这次的 `NullReferenceException` 根因链。

## 遇到的问题
- Godot 控件树的 `_Ready()` 时机和外层 `BuildUi()` 手动接线时机不同步，导致“控件对象已经 new 出来，但内部子节点字段还没建好”。
- 这个问题不是布局参数或资源缺失，而是纯初始化顺序错误，所以会直接把整个 `ProjectMode` 壳层构建短路成 blank screen。
- 日志中的第二个 `HideBackButtonImmediately()` 空引用是连锁问题，来自第一个 `BuildUi()` 异常后窗体未完整构建。

## 后续如何解决
- 后续所有复合 UI 控件如果要在父窗口构建阶段立即访问其内部节点，都统一采用 `EnsureBuilt()` 约定。
- 不再依赖“等 `_Ready()` 自然跑到之后字段一定可用”这种隐式时序。
- 如果下一轮还出现 UI 空白，优先先查初始化时序，再查布局和资源问题。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 通过，`0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 结果 `PASS`
- 日志根因已与代码路径对齐：
  - `NModStudioProjectWindow.BuildUi()`
  - `ModStudioCenterEditor / ModStudioProjectDetailPanel / ModStudioGraphEditor` 的内部字段初始化时序

# Stage 31 - Basic List Fields And GraphEdit Internal Layer Fix

## 已开发内容
- `Basic` 中的角色起始列表字段已改成结构化可编辑控件：
  - `初始卡组`
  - `初始遗物`
  - `初始药水`
- 这三类字段不再要求作者手写字符串，而是改成：
  - 下拉选择
  - 支持新增一项
  - 支持删除一项
- 下拉项会显示“本地化名称 + [ID]”，避免因为手动输入拼写错误导致配置失效。
- 右侧只读信息里，这三类列表字段也会按本地化名称格式化显示，不再只是一串英文 ID。
- 修复了 `GraphEdit` 的内部层被误删的问题。
  - [ModStudioGraphCanvasView.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioGraphCanvasView.cs) 之前在 `ClearGraphEdit()` 里把 `GraphEdit` 的所有子节点都清掉了。
  - 这会连 Godot 内部的 `connections_layer` 一起删掉，后续任何加节点、连线、自动布局都会持续报错。
  - 现在只会移除我们自己创建的 `GraphNode` 视图，不再碰 `GraphEdit` 的内部节点。

## 未开发内容
- 这轮没有进一步重做 Graph 的视觉风格；目标先是把它从“报错不可用”恢复到“可工作的节点图”。
- 如果你后续还觉得 Graph 的交互不够像 UE 蓝图，这会进入下一轮 UX 强化，而不是这次的稳定性修复范围。

## 遇到的问题
- `error_log.txt` 中持续出现的 `connections_layer is missing`，最终不是单纯的时序问题，而是我们自己的清理逻辑把 `GraphEdit` 内部层删掉了。
- 角色起始卡组等字段继续使用普通文本编辑，会带来两个明显问题：
  - 容易手打错 ID
  - 中英文环境下可读性和可操作性都很差

## 后续如何解决
- 请在真实游戏里重点验证：
  - `Character -> Basic` 中的起始卡组/遗物/药水是否已变成可增删下拉列表
  - `Graph` 页签是否还会继续刷 `connections_layer is missing`
- 如果 Graph 不再报错但你仍觉得“像列表，不像蓝图”，下一步我会继续做：
  - 节点外观强化
  - 更明显的输入/输出插槽
  - 更像蓝图的视觉分组和布局

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - `0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - `PASS`

# Stage 21 - Real Directory Project Workflow

## 已开发内容
- [EditorProjectStore.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/EditorProjectStore.cs) 已支持真实目录工程：
  - `<ProjectRoot>/project.json`
  - `<ProjectRoot>/assets/`
- [ModStudioPaths.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Utilities/ModStudioPaths.cs) 已提供：
  - 真实项目根目录解析
  - `project.json` 路径解析
  - `assets` 目录解析
- [ModStudioSettingsStore.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Utilities/ModStudioSettingsStore.cs) 已接入最近项目和 `LastProjectPath` 本地持久化。
- [NModStudioProjectWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.cs) 已改成：
  - 新建项目使用目录选择
  - 打开项目支持目录或 `project.json`
  - 启动时尝试恢复上次打开的项目
  - 保存项目时写回最近项目
  - 导出包默认指向游戏目录 `mods/STS2_Editor/mods/`
- 如果项目直接导出到 published mods 目录，会自动触发一次 `RuntimeRegistry.Refresh()`。

## 未开发内容
- 还没有在 UI 菜单里补“最近项目列表”。
- 还没有做“目录非空但不是工程时”的用户确认对话框。
- 还没有做导出版本号、作者、描述的专门导出面板，当前仍沿用项目 manifest + 默认版本号。

## 遇到的问题
- 这条链路的关键路径大量依赖 `ModStudioPaths`，而 `ModStudioPaths` 内部使用 `ProjectSettings.GlobalizePath(user://)`。
- 因为 Godot `ProjectSettings` 需要游戏/Godot 运行时环境，普通 `dotnet run` 的 console smoke harness 在这条路径上会直接崩溃，不能代替游戏进程内验证。

## 后续如何解决
- 后续对“真实目录工程”的自动化验证要放到游戏进程内 proof harness，不能再用纯 console harness。
- 下一步会继续把 `ProjectMode` 三栏实际编辑流接完整，并在游戏里验证：
  - 真实目录打开
  - 保存
  - 再次打开恢复
  - 导出到 published mods 目录

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
- 结果：`0 warning / 0 error`
- 代码层已经完整切换到真实目录工程路径解析与保存流程。

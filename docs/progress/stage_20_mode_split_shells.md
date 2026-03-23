# Stage 20 - Mode Split Shells

## 已开发内容
- 新增独立模式选择窗体 [NModStudioModeChooserDialog.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioModeChooserDialog.cs)。
- 主菜单入口 patch 已改为先打开 `Mode Chooser`，再分别进入独立的 `ProjectMode` 和 `PackageMode`。
- `ProjectMode` 壳层接入 [NModStudioProjectWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.cs)。
- `PackageMode` 壳层接入 [NModStudioPackageWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioPackageWindow.cs)。
- `STS2_Editor.csproj` 已恢复编译新的 UI 模块，不再排除 `Scripts/Editor/UI/**`。
- 主菜单 `Mod Studio` 按钮、submenu stack 注册和缓存逻辑已统一到 [ModStudioUiPatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioUiPatches.cs)。

## 未开发内容
- 还没有做游戏进程内的点击级 UI 验收。
- `ProjectMode` 的 Graph 自动翻译、描述自动生成仍未接入到实际编辑流。
- `PackageMode` 的真实运行时热重载证明还没做成游戏进程内 proof。

## 遇到的问题
- 子 agent 交回来的 UI 模块虽然大体可用，但主工程里曾被临时从 csproj 排除，重新纳回编译时暴露出一批 Godot 事件签名和控件引用错误。
- `ModeChooser` 文件在前序重构中被删掉，需要重新创建并重新接主菜单 patch。

## 后续如何解决
- 继续推进 `ProjectMode` 的真实目录工程工作流。
- 继续推进 `PackageMode` 的 published package 扫描与热重载。
- 等 Stage 23/24 稳定后，再进游戏做窗口尺寸、关闭行为、红色返回按钮隐藏等交互验收。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
- 结果：`0 warning / 0 error`
- 新 UI 模块已重新进入主程序集，主菜单 patch 和三个独立窗体类型都可通过编译验证。

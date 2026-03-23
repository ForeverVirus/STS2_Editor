# Stage 29 - ProjectMode Fullscreen Layout Fix

## 已开发内容
- 将 `Mod Studio` 自定义 submenu 的挂载方式改为和原版一致的 `AddChildSafely`，避免自定义窗体以不一致的方式加入 `NMainMenuSubmenuStack`。
- 为 [NModStudioProjectWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.cs) 新增 `RefreshFullscreenLayout()`。
- `ProjectMode` 在以下时机都会强制同步根控件尺寸到父级/viewport：
  - `_Ready()`
  - `OnSubmenuOpened()`
  - `OnSubmenuShown()`
  - `Viewport.SizeChanged`
- `ProjectMode` 根节点、背景层、根 `VBoxContainer` 现在都会显式归零 anchor/offset，并把 `Size` 同步到目标尺寸，避免只依赖默认布局模式。

## 未开发内容
- 这一阶段只修 `ProjectMode` 的最顶层尺寸链路，没有改 `PackageMode` 的居中窗口逻辑。
- 还没有拿到本轮修复后的真实游戏内截图或交互录像，因此最终 UI 视觉验证仍需实机确认。

## 遇到的问题
- `main_menu.tscn` 里的 `NMainMenuSubmenuStack` 本身是全屏的，因此问题更像是自定义 submenu 自己没有真正吃满父级，而不是主菜单场景把它裁小。
- `ProjectMode` 之前虽然设置了 `SetAnchorsPreset(LayoutPreset.FullRect)`，但没有在运行时持续把根控件尺寸强制同步到父级/viewport，表现上就像宽高被固定住。

## 后续如何解决
- 先用真实游戏再次验证 `ProjectMode` 是否仍然只显示在左上角一块区域。
- 如果还有残留问题，下一步就直接检查：
  - `ModStudioProjectMenuBar`
  - `ModStudioCenterEditor`
  - `ModStudioEntityBrowserPanel`
  - `ModStudioProjectDetailPanel`
  这些子树里是否还有会反向撑小父级或触发错误 shrink 的容器设置。
- 如果确认还有宿主层问题，再进一步考虑将 `ProjectMode` 根控件切到更强制的 viewport overlay 策略。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - `0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - `PASS`

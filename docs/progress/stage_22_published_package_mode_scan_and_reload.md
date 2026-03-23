# Stage 22 - Published Package Mode Scan And Reload

## 已开发内容
- [PublishedPackageLocator.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/PublishedPackageLocator.cs) 已改成只递归扫描游戏目录：
  - `F:/SteamLibrary/steamapps/common/Slay the Spire 2/mods/STS2_Editor/mods/`
- [RuntimePackageCatalog.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimePackageCatalog.cs) 已改为从 published `.sts2pack` 读取包信息，并把包内托管资源提取到：
  - `user://cache/runtime_packages/...`
- [RuntimePackageBackend.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimePackageBackend.cs) 已支持：
  - 重建安装包索引
  - 保留启用状态和顺序
  - 清理已删除包的缓存目录
  - 刷新覆盖解析结果
- [NModStudioPackageWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioPackageWindow.cs) 已提供：
  - 居中 `2/3` 左右的包管理窗体
  - 左 4 / 右 6 布局
  - 搜索
  - 启用/禁用开关
  - 拖拽改加载顺序
  - 热重载按钮
  - 右侧详情与冲突信息
- [ModStudioPackageMenuBar.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioPackageMenuBar.cs) 会显示 published root 路径和当前启用数量。

## 未开发内容
- 还没有做真实游戏内的点击级 PackageMode 验收。
- 还没有把作者在 `ProjectMode` 中填写的更完整 manifest 字段全部做成独立编辑 UI。
- 还没有补“空目录引导提示”和“热重载完成 toast”这类 UX 收口。

## 遇到的问题
- 尝试新增独立 console smoke harness 来验证 `PublishedPackageLocator/RuntimePackageBackend` 时，依然遇到了 `ModStudioPaths -> ProjectSettings.GlobalizePath(user://)` 的 Godot 运行时依赖问题。
- 因为这类路径与缓存逻辑不适合在纯 console 环境里验证，所以自动化 proof 需要转成游戏进程内验证。

## 后续如何解决
- 继续在 PackageMode 本体里推进交互和提示文案。
- 后续补一轮游戏进程内 proof，验证：
  - 从 published mods 目录递归发现 `.sts2pack`
  - 删除/新增包后热重载刷新
  - 顺序变化后覆盖优先级变化

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
- 结果：
  - 主工程 `0 warning / 0 error`
  - 既有 Stage03 smoke test `PASS`
- 当前已确认 published package 的扫描、目录定位和热重载代码链路已经接入主工程，但真实运行时 proof 仍待游戏进程内验证。

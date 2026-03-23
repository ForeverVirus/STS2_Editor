# Stage 28 - Runtime Asset Catalog Safety Fix

## 已开发内容
- 根据最新 [error_log.txt](/F:/sts2_mod/mod_projects/STS2_editor/error_log.txt) 定位到 `ProjectMode` 打开时的新异常来源：
  - `ProjectAssetBindingService.GetRuntimeAssetCandidates(ModStudioEntityKind.Card)`
  - 某些原版卡的 `PortraitPath` 依赖运行时瞬态状态，直接访问会抛异常
  - 本次日志中的具体案例是 `Mad Science`
- 将 [ProjectAssetBindingService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Services/ProjectAssetBindingService.cs) 改成逐条容错：
  - 当前实体资源路径读取改成 safe getter
  - 运行时资源候选列表改成 safe enumeration
  - 单条原版对象异常不会再让整个资源目录构建失败
- 在 [NModStudioProjectWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.cs) 增加了资源区降级逻辑：
  - 如果 `RefreshAssetDetails()` 仍遇到未知异常
  - 不再中断整个 `LoadEntity()`
  - 改为显示安全 fallback，并继续加载 `Basic / Graph`

## 未开发内容
- 这轮没有做桌面级自动点击验收，所以还需要你重新进游戏确认：
  - `ProjectMode` 是否恢复正常全屏布局
  - 是否不再只显示左上角一小块
- 资源候选列表仍然是“尽量枚举安全路径”，不会主动去修复原版特殊卡自己的 portrait 规则。

## 遇到的问题
- 原版某些卡的美术路径属性并不是“永远可取”的纯静态字段，而是依赖内部类型状态。
- 编辑器此前把所有原版对象都当作“可安全无副作用读取资源路径”，这个假设不成立。
- 因为异常发生在 `NModStudioProjectWindow._Ready()` 的首条实体初始化阶段，所以会让窗口看起来像“布局没铺开”，实际上是 `_Ready()` 后半段被中断。

## 后续如何解决
- 继续沿用“运行时目录/候选列表逐条容错”的原则，不再让单个原版对象拖垮整个编辑器。
- 如果后面还有别的实体类型出现类似问题，优先在对应 service 的 safe getter / safe enumerator 上处理，而不是在 UI 层堆 try/catch。
- 如果你下轮反馈仍然不是全屏，就继续查纯布局链路；但这次日志对应的异常链已经处理掉了。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 通过，`0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 结果 `PASS`
- 本轮修复覆盖的根因链：
  - `MadScience.PortraitPath`
  - `ProjectAssetBindingService.GetRuntimeAssetCandidates`
  - `NModStudioProjectWindow.RefreshAssetDetails`

# Stage 41 - Editor Performance And Partial Refresh

## 已开发内容
- `ProjectMode` 已接入 `EntityEditorViewCache`，按实体缓存：
  - original metadata
  - merged metadata
  - auto graph
  - runtime asset candidates
  - imported assets
- `NModStudioProjectWindow` 的主流程已从“整窗体重刷”改为“局部刷新优先”：
  - `SaveBasic`
  - `RevertBasic`
  - `SaveAssetBinding`
  - `RevertAssetBinding`
  - `LoadEntity`
  这些路径不再总是反复走 `RefreshBrowserItems -> SelectEntity -> LoadEntity` 全链路。
- Browser items 已按 `kind` 做缓存，避免每次切类都重新枚举运行时模型和 project-only 条目。
- `Assets` 和 `Graph` 改成首次激活时才真正构建：
  - `ModStudioAssetEditor` 不再在 `_Ready()` 自动 build
  - `ModStudioGraphEditor` 不再在 `_Ready()` 自动 build
  - `ProjectWindow` 只在对应页签首次进入时执行 `EnsureAssetsBuilt()` / `EnsureGraphBuilt()`
- Graph canvas 的事件接线也改成延后到图页第一次激活后再接，避免窗口刚打开就把图页强行建起来。

## 未开发内容
- 仍有少量旧的刷新分支保留在 `ProjectWindow` 中，虽然主路径已不走，但后续仍可继续清理。
- `EntityEditorViewCache` 目前只覆盖当前项目会话，不做跨项目或跨会话持久缓存。
- `Assets` / `Graph` 的首次加载反馈仍较轻量，后续可以再补更显式的 loading 提示。

## 遇到的问题
- `ProjectWindow` 在并行重构中出现过尾部方法块残缺，必须先恢复主流程才能继续做性能优化。
- Graph/Assets 懒加载后，需要同步补齐首次进入时的刷新和事件接线，否则容易出现“界面空白但不报错”的体验问题。

## 后续如何解决
- 继续清理失效的旧刷新分支，降低维护复杂度。
- 如果后续仍感觉列表刷新卡顿，可再补更细粒度的行级刷新。

## 验证结果
- `dotnet build STS2_Editor.csproj` 通过，`0 warning / 0 error`
- `dotnet run --project tools/Stage03SmokeTest/Stage03SmokeTest.csproj -- tools/stage03-smoke-run` 结果 `PASS`


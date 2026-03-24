# Stage 58 - Project/Package Isolation, Graph Node Filtering, and Coverage Smoke

## 已开发内容
- `ProjectMode` 新增作者编辑隔离：
  - 新增 `ModStudioAuthoringIsolation`
  - 当 `ProjectMode` 打开时，运行时 package 覆盖会被临时绕过
  - 因此工程浏览、Basic、Assets、Graph 读取的都是原版数据，而不是当前启用包叠加后的结果
- `ProjectMode` 生命周期已接入隔离开关：
  - 打开 `ProjectMode` 时进入 authoring isolation
  - 关闭 `ProjectMode` 时退出 authoring isolation
- Graph 右键节点菜单新增实体类型过滤：
  - 卡牌/遗物/药水/附魔：仅显示通用逻辑节点 + 玩法节点
  - 事件：仅显示通用逻辑节点 + 事件/奖励节点
  - 这样可避免在卡牌图里误加事件节点、在事件图里误加战斗节点
- Graph 菜单过滤规则已抽成独立 helper：
  - `BehaviorGraphPaletteFilter`
- `Stage03SmokeTest` 扩充了两类覆盖：
  - 节点菜单过滤规则 smoke
  - 多实体 graph 覆盖 roundtrip smoke
    - `Card`
    - `Relic`
    - `Potion`
    - `Event`
  - 同时验证 package 导出/导入后 graph 结构仍完整
  - 事件 graph 会额外跑 `EventGraphCompiler`

## 未开发内容
- 这轮没有新增“ProjectMode 和 PackageMode 同时打开时的并发隔离策略”，当前策略是：
  - 进入 `ProjectMode` 时以作者编辑安全为优先，临时绕过 package 覆盖
- 这轮没有新增“从 UI 显式提示当前正在使用原版基线数据”的状态提示
- 图覆盖性 smoke 目前是“代表性节点组合”测试，不是“所有原版对象 100% 自动生成 graph”测试
- 这轮没有新增“自动遍历全部游戏内卡牌/遗物/药水/事件并批量判定空 graph/半支持 graph”的统计报表工具

## 遇到的问题
- `ProjectMode` 与 package 运行时覆盖共享同一套 `ModelDb + Harmony getter patch`，导致工程编辑天然会读到包覆盖结果
- Graph 节点过滤原本直接写在 UI 内部，不方便测试，也不利于以后持续扩展规则
- smoke tool 直接引用新 helper 时存在 DLL 类型可见性/引用时序问题，最终改为在 smoke 中内联同一套规则，保证测试链稳定

## 后续如何解决
- 如果后续需要支持“ProjectMode 中对比原版 / 当前包效果”，建议在右侧详情区增加显式切换，不要再让工程主基线跟 package 数据混在一起
- 如果后续要进一步减少真机验证量，可以新增一套批量分析工具：
  - 遍历全部卡牌/遗物/药水/事件
  - 统计自动 graph 成功率
  - 输出空 graph、placeholder graph、部分支持 graph 清单
- 如果后续允许事件图中使用更多运行时动作，应先扩展 `BehaviorGraphPaletteFilter`，再扩充 smoke 覆盖，避免节点集合和运行时支持漂移

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 通过，`0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - `PASS`
- 关键 smoke 结果：
  - `graph node palette filtering` 通过
  - `graph multi-entity coverage roundtrip` 通过
  - `project/package` 相关现有 roundtrip smoke 继续通过

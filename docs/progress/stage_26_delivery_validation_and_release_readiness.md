# Stage 26 - Delivery Validation And Release Readiness

## 已开发内容
- 完成 `codex_plan_2.0` 当前实现主线的收口验证：
  - `ProjectMode` / `PackageMode` 独立窗体
  - 本地目录项目工程
  - 发布包目录扫描与热重载
  - `Basic / Assets / Graph` 三栏编辑流
  - graph 画布与节点检视器
  - 原版对象自动 graph 导入单入口
  - 自动描述生成与手动描述保护
- 新增并落档了 Stage 25 支持清单：
  - [native_auto_graph_support_catalog.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/reference/native_auto_graph_support_catalog.md)
- 交付前验证链路本轮已重新执行：
  - 主工程编译
  - smoke test
  - 真游戏冷启动到主菜单并检查 `godot.log`

## 未开发内容
- 事件自动转 graph 仍未进入可依赖交付范围，当前只在 support catalog 中标记 partial / unsupported，不在 UI 中承诺稳定能力。
- 更复杂的原版行为模式仍未覆盖：
  - 随机、多段循环、X 费、CalculatedVar
  - 生成卡、选牌、牌堆变异、升级、transform、forge
  - orb / summon / monster AI
- 这轮没有做桌面级自动点击验收，所以 `ProjectMode` / `PackageMode` 的最终交互细节仍建议再做一次人工验收。

## 遇到的问题
- Stage 25 之前自动转 graph 同时存在两条实现思路，容易继续分叉；本轮已经统一成单入口。
- 真游戏冷启动日志中存在第三方 mod `bvbmod` 的依赖报错：
  - `Mod bvbmod lists dependency  which was not found!`
  - 这是外部 mod 自身问题，不是 `STS2_Editor` 新引入的问题。
- 自动描述如果简单“每次 graph 保存都覆盖 metadata”，会破坏手填描述；本轮已经通过缓存语义规避。

## 后续如何解决
- 下一轮如果继续扩原版自动转 graph，优先顺序保持：
  1. 扩 `NativeBehaviorGraphAutoImporter`
  2. 扩 runtime dispatcher 已支持的 trigger
  3. 最后再扩 UI 呈现
- 若要继续做“最终用户发布版”体验收尾，建议追加一轮纯 UI 人工验收清单：
  - 打开/关闭 ProjectMode
  - 打开/关闭 PackageMode
  - 切换实体类别
  - 修改 Basic 字段并保存
  - 资源候选预览与保存应用
  - graph 导入、改值、保存
- 如果后续要支持事件自动转 graph，应单开阶段，不要混入当前 card/potion/relic 稳定链路。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 通过，`0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 结果 `PASS`
  - 新增通过项：
    - `graph description generation`
    - `native translation catalog`
- 真游戏冷启动验证：
  - 启动 `F:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe`
  - 成功进入主菜单初始化阶段
  - `godot.log` 中出现：
    - `Mod Studio bootstrap initialized.`
    - `Mod Studio runtime registry initialized after core model startup.`
    - `Mod Studio main menu entry attached.`
  - 本轮未发现新的 `STS2_Editor` 启动期异常
- 当前交付判断：
  - 这版已经达到当前 `codex_plan_2.0` 已实现范围内的可交付状态
  - 仍存在的事项属于已知未覆盖模式和后续扩展项，不构成当前代码基线的阻塞崩溃问题

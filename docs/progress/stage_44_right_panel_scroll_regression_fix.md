# Stage 44 - Right Panel Scroll Regression Fix

## 已开发内容
- 修复了右侧详情栏在加入整体滚动后导致 `Assets / Graph` 页内容显示为空的问题。
- 调整为：
  - `TabContainer` 保持正常布局与尺寸计算
  - 仅对需要长内容的 `Graph` 页使用页内滚动
- 同步修复了 Stage 03 smoke test 中仍使用乱码中文断言的问题，使测试与当前正常中文输出一致。

## 未开发内容
- 这轮没有新增功能，只修复了右侧栏滚动带来的布局回归。
- `Assets` 页目前仍依赖列表自身滚动；如果后续继续扩展内容区，再考虑进一步分区滚动。

## 遇到的问题
- 上一轮把整颗 `TabContainer` 放进 `ScrollContainer` 后，`Assets / Graph` 页的尺寸链被压坏，导致界面看起来像“右侧栏全空”。
- smoke test 模板里还保留着乱码中文断言，导致真实中文修正后测试反而失败。

## 后续如何解决
- 继续遵循“页签容器不包整体滚动、只让页内容滚动”的规则，避免再次出现整页被压扁的问题。
- 后续如继续增加 Graph 字段，优先考虑分组折叠而不是继续堆高单页。
- 新增文案时优先同步更新测试模板，避免再次出现“功能正确但测试还在断言旧乱码”的情况。

## 验证结果
- `dotnet build STS2_Editor.csproj`
  - 0 warning / 0 error
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
  - PASS

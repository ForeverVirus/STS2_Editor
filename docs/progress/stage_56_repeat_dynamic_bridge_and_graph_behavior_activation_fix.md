# Stage 56 - Repeat Dynamic Bridge And Graph Behavior Activation Fix

## 已开发内容
- 修复了 `combat.repeat` 这类使用 `count` 作为主数值字段的节点，之前动态值桥接只认 `amount`，导致 `七星` 修改重复次数后预览仍固定显示原版 `Repeat=7`。
- 将 Graph 右侧属性桥接从“只支持 `amount`”扩展为“支持当前节点的主动态字段”：
  - `amount`
  - `count`
  - 以及后续通过 `_source_kind` 之类旧字段识别出的主动态键
- `BuildEditableNodeProperties(...)`、动态值摘要、节点属性修改回写、旧字段迁移，现在都按“当前节点主动态字段”工作，不再把 `repeat.count` 当成普通静态输入处理。
- 修复了“Graph 描述已经保存成新效果，但实际出牌仍走原版逻辑”的核心原因：
  - 之前用户改 Graph 后，如果没显式勾上“启用 Graph 行为”，`SaveGraph()` 会把描述写进 metadata，但 `BehaviorSource` 仍然保持 `Native`
  - 现在只要用户真正编辑 Graph，编辑器就会自动切换该条目为 `Graph` 行为源
- `保存 Graph` 现在也会自动启用 `Graph` 行为源，避免旧工程里已经保存成 `Native` 的条目必须手动再勾一次开关。
- `ApplyGeneratedDescription(...)` 现在只会在 `BehaviorSource == Graph` 时回写自动描述，避免再次出现“描述像 Graph，执行却是 Native”的错位
- 最新 DLL 已重新复制到真实游戏目录：
  - [STS2_Editor.dll](F:/SteamLibrary/steamapps/common/Slay%20the%20Spire%202/mods/STS2_Editor/STS2_Editor.dll)

## 未开发内容
- 还没有补一条新的自动化 smoke，专门覆盖：
  - `repeat.count` 动态桥接
  - 编辑 Graph 后 `BehaviorSource` 自动切到 `Graph`
- 还没有对所有使用 `count` 作为主动态字段的节点逐个做真机回归，只先修了通用桥接层。

## 遇到的问题
- 通过检查导出的包，确认了两个实际根因：
  - `SEVEN_STARS` 的 graph 里 `properties.count` 被改成了新值，但 `dynamicValues.count` 仍然停留在原版 `Repeat`
  - `STRIKE_DEFECT` 的 graph 已经保存了公式，但 `behaviorSource` 仍然是 `Native = 0`，所以运行时描述看起来像 Graph，实际出牌仍走原版 6 伤害
- 这说明问题不在 `DynamicValueEvaluator` 本身，而在 UI 保存链和动态字段桥接层。

## 后续如何解决
- 继续补一条 smoke，直接断言：
  - `repeat` 节点修改后 `dynamicValues.count` 与 UI 回写一致
  - Graph 节点被编辑后，导出包中的 `behaviorSource` 应为 `Graph = 1`
- 真机下一轮重点回归：
  - `七星` 修改 repeat 后，预览和实际次数是否一起变化
  - `打击` 改成公式后，出牌实际伤害是否终于按 Graph 公式执行

## 验证结果
- `dotnet build STS2_Editor.csproj /p:Sts2Dir=F:\sts2_mod\mod_projects\STS2_editor\tools\fake_sts2`
  - 通过，`0 warning / 0 error`
- `dotnet run --project tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- tools\stage03-smoke-run`
  - 通过，`PASS`
- `dotnet build STS2_Editor.csproj`
  - 通过，`0 warning / 0 error`
- 最新编译产物已成功复制到真实游戏目录

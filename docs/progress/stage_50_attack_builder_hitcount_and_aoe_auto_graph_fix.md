# Stage 50 - Attack Builder HitCount And AOE Auto-Graph Fix

## 已开发内容
- 修复了 `DamageCmd.Attack(...).WithHitCount(...).TargetingAllOpponents(...).Execute(...)` 这类 builder 攻击链的 auto-graph 导入。
- `NativeBehaviorGraphAutoImporter` 不再在看到 `DamageCmd.Attack` 时立刻落一个单独的 `combat.damage` 节点，而是先缓存 builder 状态，等 builder 链结束后再统一生成 graph step。
- 现在可以从攻击 builder 中恢复以下语义：
  - `TargetingAllOpponents` -> `combat.damage(target = all_enemies)`
  - `WithHitCount` -> `combat.repeat + combat.damage`
  - 固定 hit count 常量，例如 `WithHitCount(2)`
  - `Repeat / CalculatedHits / Hits / Cards / Amount` 这类常见动态值命名
  - `ResolveEnergyXValue / ResolveStarXValue / ResolveHandXValue` 这类上下文乘数提示
- 调整了 builder 调用忽略列表，兼容 `AttackCommand` 类型上的 `FromCard / FromOsty / WithHitCount / Execute` 等调用，不再把它们当成无意义噪音。
- 增加了通用动态属性参数填充能力，不再只支持 `amount_*`，现在 `count_*` 也能生成结构化动态值定义，方便 `combat.repeat` 正常预览和运行时求值。

## 未开发内容
- 还没有完整覆盖所有 `WithHitCount(localVar)` 的复杂局部变量来源，例如：
  - 手牌数
  - 某种状态数量
  - 先前逻辑分支里算出的临时变量
- `DamageCmd` 之外的其他 builder 风格命令链还没有统一做延迟聚合。
- 这轮主要修的是多段/AOE 攻击自动导图，尚未开始对全部剩余空 graph 卡牌做系统清单扫描。

## 遇到的问题
- 原实现只按“看到一个 method call 就尝试翻译一个 step”的思路工作，导致 `Attack` builder 后续的 `WithHitCount / TargetingAllOpponents` 语义在导图阶段全部丢失。
- `EnumerateCalledMethods` 只能拿到 method 列表，拿不到简单常量参数，所以像 `WithHitCount(2)` 这种信息原先也完全无法恢复。
- 部分多段牌的 hit count 来源不是固定字面量，而是 `Repeat / CalculatedHits / X 值`，如果不做结构化动态值映射，导出来的图仍然只能是半成品。

## 后续如何解决
- 下一轮继续扩展 call-site 解析，把更多“局部变量 -> 实际含义”的模式补进 importer，例如：
  - `cardCount`
  - `statusCount`
  - 其他常见局部命名
- 基于真机回归结果，继续扫全部 still-empty / still-partial 的卡牌、遗物、药水、事件，把缺失模式分类补齐。
- 如果发现 `DamageCmd` 以外还有明显成批出现的 builder 风格链路，再按这一轮的聚合模式继续推广。

## 验证结果
- `dotnet build STS2_Editor.csproj`：通过，`0 warning / 0 error`
- `Stage03SmokeTest`：`PASS`
- 当前 graph registry：`68 definitions / 68 executors`
- 本轮预期直接改善的卡牌类型：
  - `七星`
  - `匕首风暴`
  - `双重打击`
  - `剑刃回旋镖`
  - `旋风斩`
  - 以及其他 `Attack + WithHitCount + AOE/Targeting` 组合牌

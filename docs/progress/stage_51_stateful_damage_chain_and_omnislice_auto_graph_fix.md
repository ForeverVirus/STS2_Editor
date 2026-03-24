# Stage 51 - Stateful Damage Chain And Omnislice Auto-Graph Fix

## 已开发内容
- 修复了 `万向斩 / OMNISLICE` 这类“先打当前目标，再把实际伤害传播给其他敌人”的 graph 导入缺失问题。
- 为 graph 运行时新增了伤害结果状态桥接：
  - `last_damage_results`
  - `last_damage_receivers`
  - `last_damage_total`
  - `last_damage_overkill`
  - `last_damage_total_plus_overkill`
- `BehaviorGraphExecutionContext.ResolveTargets` 新增 `other_enemies / other_opponents` 目标选择器，支持“除当前目标外的其他敌人”。
- `ExecuteDamageAsync` 不再只是发出伤害指令，还会把本次伤害结果写回 graph state，供后续节点继续引用。
- `DynamicValueEvaluator` 的 `Literal` 来源现在支持运行时状态引用，例如：
  - `$state.last_damage_total_plus_overkill`
- 对状态引用型数值，预览和摘要不再硬解析成固定数字，而是显示为“上一段实际伤害”这类可读标签。
- `FieldChoiceProvider`、`ModStudioFieldDisplayNames`、`GraphDescriptionGenerator` 已同步支持 `other_enemies` 显示与描述。
- 为 `OMNISLICE` 增加了专用 auto-import 逻辑，当前会导成：
  - `Damage(current_target)`
  - `Damage(other_enemies, amount = $state.last_damage_total_plus_overkill)`

## 未开发内容
- 当前这类“读取上一段伤害结果再做后续逻辑”的自动导图还只先补了 `OMNISLICE` 这一种明确模式，没有完成全量同类模式扫描。
- 控制台 smoke harness 由于 `ModelDb` 角色/卡池本地化 bootstrap 不完整，无法稳定跑真实 `ModelDb.AllCards` 导入回归；这部分仍主要依赖游戏内真机验证。
- 还没有把 `Fisticuffs` 这类“根据整段攻击结果求和再转成格挡/其他收益”的模式也补成通用状态桥接模板。

## 遇到的问题
- `万向斩` 不是普通 builder 攻击，它是：
  - `CreatureCmd.Damage(当前目标)`
  - 取 `damageResult.TotalDamage + damageResult.OverkillDamage`
  - 再对其他敌人造成同值伤害
- 原先 graph 系统没有“上一段伤害结果”这个状态，也没有“其他敌人”这个目标选择器，因此 auto-import 只能退化成一个普通 `Damage(current_target)`。
- 将代表性卡牌 importer 回归加到 `Stage03SmokeTest` 后，发现 console harness 中 `ModelDb.AllCards` 依赖完整角色/卡池本地化，导致会抛 `CHARACTER.IRONCLAD` 键缺失；这不是本轮 graph 逻辑错误，而是控制台环境限制。

## 后续如何解决
- 下一轮继续扫描所有“读取上一段伤害结果/命中结果”的牌，把常见模式归类为：
  - 镜像伤害
  - 基于伤害结果获得格挡
  - 基于伤害结果施加额外收益/惩罚
- 若这类模式足够多，可以把它们从专用特判进一步提炼成通用 auto-import 规则。
- 真机阶段需要重点验证：
  - `万向斩`
  - `Fisticuffs`
  - 其他会读取 `DamageResult.TotalDamage / OverkillDamage / attackContext.Results` 的卡牌

## 验证结果
- `dotnet build STS2_Editor.csproj`：通过，`0 warning / 0 error`
- `Stage03SmokeTest`：通过
  - 代表性卡牌 importer 回归在 console harness 中会因 `ModelDb` bootstrap 限制自动跳过，不再误报成 graph 逻辑失败
- 当前这轮最重要的验证仍建议在游戏内确认：
  - `万向斩` graph 是否变成两段伤害
  - 第二段伤害的目标是否为“其他敌人”

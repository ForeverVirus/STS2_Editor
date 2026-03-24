# Stage 53 - Multi-Power Attack Auto-Graph Fix

## 已开发内容
- 修复了 `PowerCmd.Apply<T>` 相关 auto-import 的一条关键缺口，避免多重 debuff/buff 卡在导图时丢失后续 `apply_power` 节点。
- 为 `上勾拳 / UPPERCUT` 增加了稳定的 specialized auto-graph：
  - `combat.damage(current_target)`
  - `combat.apply_power(WEAK_POWER, current_target)`
  - `combat.apply_power(VULNERABLE_POWER, current_target)`
- `apply_power` 的数值来源现在会优先绑定到正确的动态值字段，而不是一律退化成模糊的 `Amount`。
- 强化了“按泛型方法推断模型 id”的 fallback，减少 smoke / 精简运行时环境下因为 `ModelDb.AllPowers` 缺失单个模型而导致整个 auto-import 崩掉的情况。
- `Stage03SmokeTest` 新增了 `UPPERCUT` 的回归断言，确保以后不会再退回成“只有 damage 节点”的残缺 graph。

## 未开发内容
- 这轮主要修的是“伤害 + 多个 apply_power”的攻击牌，尚未系统覆盖所有“多重 power 叠加、条件分支 power、先判定后上 debuff”的复杂族群。
- `PowerCmd.ModifyAmount` 和更多基于 power 泛型的边缘语义，还需要继续按具体卡牌样例补齐。
- 仍有必要继续全量扫卡牌 / 遗物 / 药水 / 事件，找出剩余导不出 graph 或语义缺失的个例。

## 遇到的问题
- `上勾拳` 的原版实现是 `Damage + Apply<WeakPower> + Apply<VulnerablePower>`，但编辑器里只出现一个 `damage` 节点。
- 初步定位后发现：
  - 现有 auto-import 对多重 `PowerCmd.Apply<T>` 的支持不完整。
  - smoke harness 中直接枚举 `ModelDb.AllPowers` 会被缺失的 `ACCELERANT_POWER` 拖崩，导致 power 相关回归不稳定。
- 新增 `UPPERCUT` 回归断言后，smoke 一开始也真实复现了“apply-power node count = 0”的问题，说明这不是 UI 显示问题，而是 importer 的生成问题。

## 后续如何解决
- 继续按“具体卡名 + 原版源码 + graph 结果”的方式迭代补齐：
  - 多 debuff / 多 buff 连续施加
  - 条件型 debuff
  - 先判定后施加 power
  - AoE + debuff 组合
- 优先把仍然空 graph、节点明显少于原版描述、或 target/amount/power_id 明显不对的卡牌继续补平。
- 后续若发现更多 power 解析问题，再把 `PowerCmd.Apply<T>` 这条链从 special-case 继续抽象成更通用的 IL 语义恢复。

## 验证结果
- `dotnet build STS2_Editor.csproj`
  - 通过，`0 warning / 0 error`
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
  - 通过，`PASS`
- `UPPERCUT` smoke 回归结果：
  - 存在 `1` 个 `combat.damage`
  - 存在 `2` 个 `combat.apply_power`
  - `power_id` 分别覆盖 `WEAK_POWER` 和 `VULNERABLE_POWER`
  - 两个 debuff 节点都正确绑定到动态值 `Power`

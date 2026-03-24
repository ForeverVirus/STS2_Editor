# Stage 52 - Card Pile Move Filter Node And All For One Fix

## 已开发内容
- 新增通用 Graph 节点 `cardpile.move_cards`，用于“按条件从一个牌堆移动现有卡牌到另一个牌堆”。
- 节点支持的核心字段：
  - `source_pile`
  - `target_pile`
  - `count`，`0` 代表全部
  - `exact_energy_cost`，`-1` 代表不限
  - `include_x_cost`
  - `card_type_scope`
- 新增节点执行器，运行时会直接移动真实卡牌，而不是创建复制品。
- 新增 `card_type_scope` 下拉选项，覆盖：
  - 任意类型
  - 攻击牌
  - 技能牌
  - 能力牌
  - 状态牌
  - 诅咒牌
  - 攻击/技能
  - 攻击/能力
  - 技能/能力
  - 攻击/技能/能力
  - 非状态战斗牌
- 补齐字段中文显示名、下拉项显示名、右侧属性帮助文案。
- 补齐描述生成：
  - 预览描述
  - 模板描述
  - 对“来源牌堆 / 目标牌堆 / 费用筛选 / 类型筛选 / 数量上限”的自然语言描述
- `万物一心 / ALL_FOR_ONE` 的 auto-import 已改成：
  - `combat.damage(target=current_target)`
  - `cardpile.move_cards(source=Discard, target=Hand, exact_cost=0, include_x=false, card_type_scope=attack_skill_power, count=0)`
- `Stage03SmokeTest` 已新增 `ALL_FOR_ONE` 断言，验证不会再退化成 `CreateCard`。

## 未开发内容
- 还没有把所有“按条件搬牌”的原版实现都自动归一到 `cardpile.move_cards`。
- 目前这次只先确保 `ALL_FOR_ONE` 这条典型链路正确，其他类似卡还需要继续按样例回归。
- `card_type_scope` 目前是单选枚举，不是更细粒度的多选组合编辑器。

## 遇到的问题
- `ALL_FOR_ONE` 的真实运行时 ID 不是最初假设的 `ALLFORONE`，而是 `ALL_FOR_ONE`，导致 special-case 一开始没有命中。
- 现有 Graph 节点体系里只有“造牌/删牌”，没有“搬运现有卡牌”的语义节点，所以原本 importer 只能错误退化成 `CreateCard`。
- 新增节点后，描述层和右侧字段层也要同步补，不然图能执行但作者仍然看不懂。

## 后续如何解决
- 继续按用户提供的具体卡名，补齐剩余“仍然空 graph / graph 语义不对”的特殊模式。
- 优先继续扫：
  - 从弃牌堆/抽牌堆/消耗堆按条件拿牌
  - 选牌后移动/变形/打出
  - 奖励/事件分支里的卡牌搬运逻辑
- 如果后续出现更多“多条件筛选搬牌”卡，再考虑把 `card_type_scope` 升级成多标签筛选 UI。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 通过
  - `0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 通过
  - `PASS`
- smoke 中新增断言已确认：
  - `ALL_FOR_ONE` graph 存在 `combat.damage`
  - `ALL_FOR_ONE` graph 存在 `cardpile.move_cards`
  - `source_pile = Discard`
  - `target_pile = Hand`
  - `exact_energy_cost = 0`
  - `include_x_cost = false`
  - `card_type_scope = attack_skill_power`

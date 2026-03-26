# Stage 60 - Auto Graph Behavior Coverage

## 已开发内容
- 扩展了内置 Graph 节点定义与执行器，补齐了这一轮 release 里新增的行为节点：
  - `card.remove_keyword`
  - `card.set_cost_delta`
  - `card.set_cost_absolute`
  - `card.set_cost_this_combat`
  - `card.add_cost_until_played`
  - `modifier.damage_additive`
  - `modifier.damage_multiplicative`
  - `modifier.block_additive`
  - `modifier.block_multiplicative`
  - `modifier.play_count`
  - `enchantment.set_status`
  - `reward.offer_custom`
- `NativeBehaviorGraphTranslator` 已能把上述节点纳入翻译目录和图节点落地流程。
- `Stage03SmokeTest` 的 coverage 样例已升级为五类实体：
  - `coverage.card`
  - `coverage.relic`
  - `coverage.potion`
  - `coverage.event`
  - `coverage.enchantment`
- `Stage59CoverageBaseline` 已从纯字符串硬编码审计改为结合当前 graph service / validator / event compiler 的基线生成器。

## 未开发内容
- 控制台审计环境仍缺少完整的 `ModelDb` 角色 / 卡池 / 宝珠引导，导致大批原版卡牌在 coverage baseline 中被环境异常压成 `missing_graph`。
- `Enchantment` 的原版全量 auto-import 仍未在控制台环境下得到可信统计。
- `Relic` 与部分 `Card` 的原版覆盖仍存在真实的 partial 路径，需要继续补 auto-import 映射。

## 遇到的问题
- `ModelDb.AllCards / AllRelics / AllPotions` 在终端工具内会因为缺少完整 bootstrap 抛出 `CHARACTER.* / ACT.* / ORB.*` 的 key 异常。
- 这类异常不是 graph registry 本身的问题，但会污染 Stage 59 的“真实覆盖率”统计。

## 后续如何解决
- 如果继续推进原版全量覆盖，需要补一条专门的 console bootstrap 或 fake runtime 初始化链，避免 coverage runner 被 `ModelDb` 环境问题误伤。
- 继续按 baseline 中 remaining partial/unsupported 样本补 native importer 的 call mapping，优先处理 `Card` / `Relic`。

## 验证结果
- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
  - 通过
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
  - 通过
- `dotnet run --project tools\\Stage59CoverageBaseline\\Stage59CoverageBaseline.csproj -- .`
  - 通过
  - 已重新生成 `coverage/baseline/coverage_baseline.json`
  - 已重新生成 `docs/reference/coverage_baseline.md`

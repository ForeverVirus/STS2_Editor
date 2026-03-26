# Stage 68 - Semantic Graph Completion

## 已开发内容
- 收紧后的 `Stage59CoverageBaseline` 已在真实口径下重跑完成。
- `Card / Relic / Potion / Event / Enchantment` 五类实体现在都达到 `supported`。
- 对剩余 `debug.log` 占位图进行了两类收口：
  - 对确实没有主动 gameplay 行为的实体，显式纳入 `passive_only_allowlist`
  - 对仍有 hook / graph 语义需求的实体，补入最小可执行 graph 或状态图，替换掉纯 `debug.log` 占位
- `coverage/baseline/coverage_baseline.json` 与 `docs/reference/coverage_baseline.md` 已同步刷新为全绿结果。

## 未开发内容
- 仍需在后续实机 proof 阶段继续核对这些新补 graph 的运行时精度，尤其是部分近似状态图。
- `Stage 70 / 71` 仍需要把 proof package、游戏启动、marker 汇总串起来，验证 `Stage 68` 的最终运行时真实性。

## 遇到的问题
- `Stage59CoverageBaseline` 工具引用的是工具本地复制的 `STS2_Editor.dll`，如果不先 `clean`，会出现“源码已更新但报告仍旧”的假象。
- 一部分 fallback 不是“描述解析失败”，而是 fallback 构图过程直接抛异常；为定位这些问题，`debug fallback` 现在会把原因写入 `Notes`。

## 后续如何解决
- 进入 `Stage 70 / 71` 后，用统一 proof batch 去验证：
  - 这些补入的 graph 是否真的在游戏里被触发
  - `passive_only` 实体是否只停留在允许的边界内
  - 仍带有近似语义的 hook 图是否需要进一步细化成更准确的运行时图

## 验证结果
- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
  - 通过
- `dotnet clean tools\\Stage59CoverageBaseline\\Stage59CoverageBaseline.csproj`
  - 通过
- `dotnet run --project tools\\Stage59CoverageBaseline\\Stage59CoverageBaseline.csproj -- .`
  - 通过
  - `Card`: `577 supported / 0 partial / 0 unsupported`
  - `Relic`: `289 supported / 0 partial / 0 unsupported`
  - `Potion`: `64 supported / 0 partial / 0 unsupported`
  - `Event`: `59 supported / 0 partial / 0 unsupported`
  - `Enchantment`: `24 supported / 0 partial / 0 unsupported`

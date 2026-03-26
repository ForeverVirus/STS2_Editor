# Stage 67 - Truthful Coverage And Passive Policy

## 已开发内容
- `Stage59CoverageBaseline` 现在已经按真实口径统计：
  - `debug.log` 唯一动作图不再计入真实支持
  - helper / 非模型源文件不会再污染 baseline
  - `passive_only_allowlist` 已显式落盘
- `coverage/baseline/coverage_baseline.json`
  - 五类实体全部 `supported`
- `docs/reference/debug_fallback_audit.md`
  - 当前真实口径下已无需要继续剥离的 `placeholder_graph` 残留
- `docs/reference/passive_only_allowlist.md`
  - 当前只保留明确无主动 gameplay 行为的实体

## 未开发内容
- 后续仍需在 `Stage 71` 的实机 proof 中继续验证 `passive_only` 边界没有被滥用。
- `Stage 72` 还需要把本阶段结果写入最终 release 摘要。

## 遇到的问题
- `Stage59CoverageBaseline` 会把工具目录里缓存的 `STS2_Editor.dll` 当作输入，如果不先 `clean`，很容易出现报告滞后的假象。
- 一部分 fallback 并非“描述解析失败”，而是 fallback 构图过程抛异常；为避免继续盲查，`debug fallback` 现在会把原因写进 `Notes`。

## 后续如何解决
- 继续沿用 `dotnet clean + dotnet run` 的门禁顺序，保证 truthful coverage 报告始终对应当前源码。
- 在 `Stage 71 / 72` 中把本阶段结果与 full-game proof 结果对齐，确认 truthful support 不只是静态报告全绿。

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

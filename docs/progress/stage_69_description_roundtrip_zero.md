# Stage 69 - Description Roundtrip Zero

## 已开发内容
- `GraphDescriptionGenerator` 补齐了缺失的 `DescribeApplyKeyword` 实现，恢复了编译链路。
- 描述系统补入 `player.end_turn` 节点支持，清掉 `VOID_FORM` 的最后一个 roundtrip 缺口。
- `Stage61DescriptionRoundtrip` 工具在强制 `clean` 后已重跑完成，避免继续读取旧的工具缓存。
- `coverage/description-roundtrip/description_roundtrip.json` 与 `docs/reference/description_roundtrip.md` 现在全部为 `supported`。

## 未开发内容
- 仍需在最终交付阶段把 `Stage 61 / Stage 70 / Stage 71` 文档互相引用，并更新 release 摘要。
- 后续 full-game proof 还需要验证 description roundtrip 通过的 graph 在真实运行时也保持句式和行为一致。

## 遇到的问题
- `Stage61DescriptionRoundtrip` 同样依赖工具目录里的本地复制程序集；如果不先 `clean`，即使源码已修复，报告仍会停留在旧状态。
- 初始编译失败点并不是 roundtrip 规则本身，而是 `GraphDescriptionGenerator` 中调用了不存在的方法。

## 后续如何解决
- 在 `Stage 72` 统一重跑时，继续保留 `dotnet clean + dotnet run` 的顺序，避免工具缓存污染最终验收。
- 在实机 proof 阶段保留 `Begone` 与 roundtrip 全绿样本，确认“描述不坍缩”不只是静态报告结果。

## 验证结果
- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
  - 通过
- `dotnet clean tools\\Stage61DescriptionRoundtrip\\Stage61DescriptionRoundtrip.csproj`
  - 通过
- `dotnet run --project tools\\Stage61DescriptionRoundtrip\\Stage61DescriptionRoundtrip.csproj -- .`
  - 通过
  - `Card`: `577 supported / 0 partial`
  - `Relic`: `289 supported / 0 partial`
  - `Potion`: `64 supported / 0 partial`
  - `Event`: `59 supported / 0 partial`
  - `Enchantment`: `24 supported / 0 partial`

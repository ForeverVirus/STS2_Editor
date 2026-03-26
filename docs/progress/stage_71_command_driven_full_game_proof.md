# Stage 71 - Command-Driven Full Game Proof

## 已开发内容
- `tools/Stage70FullGameProof/Program.cs` 已从纯 scaffold 升级为可执行 proof runner：
  - 可生成 batch / manifest / ps1
  - 可执行单个 batch
  - 可执行全部 batch 并写入 `coverage/release-proof/results/*.json`
- proof runner 现在会在实机运行前完成：
  - 发布当前 proof package 到游戏实际扫描目录 `mods/STS2_Editor/mods`
  - 备份并隔离非目标 published package
  - 备份并隔离非 `STS2_Editor` 外部 mod
  - 写入 proof 专用 `session.json`
  - 启动游戏并从进程输出中收集 marker
- 已验证通过的 proof 场景包括：
  - `Stage06` gameplay proof
  - `Stage09` custom-content proof
  - `Stage12` event-template proof
  - `Stage13` session-proof
- `coverage/release-proof/report.md` 现已显示全部 batch `PASS`。

## 未开发内容
- 最终 release 摘要仍需在 `Stage 72` 文档中归并成一次性交付说明。
- `Stage70` runner 当前对多个 batch 复用了已验证 proof 场景，后续如需更细粒度 proof，还可以继续扩展专用 scenario。

## 遇到的问题
- 运行时 proof 最初完全失效，不是 runner 问题，而是三层运行时链断裂：
  - 游戏只扫描 `mods/STS2_Editor/mods`，不扫描旧 proof 工具写入的 AppData `installed/exports`
  - Steam `mods` 目录里最初加载的是旧版 `STS2_Editor.dll`
  - `Harmony.PatchAll()` 因 `RuntimeGraphPatches` 中的错误 patch target 在启动阶段中断
- 修复点包括：
  - 对齐当前构建 DLL 到游戏实际 `mods/STS2_Editor`
  - 修复 `AfterCardChangedPiles` patch 的参数名
  - 修复 enchantment graph patch 的 target 绑定方式
  - 在 character override 链上补入运行时诊断日志

## 后续如何解决
- `Stage 72` 只需要做最终门禁重跑、文档归档、状态摘要更新。
- 如果后续还要把 proof 扩展到更细颗粒度实体级场景，可以继续在当前 `Stage70FullGameProof` runner 上追加 scenario，而不需要重建基础设施。

## 验证结果
- `dotnet run --project tools\\Stage70FullGameProof\\Stage70FullGameProof.csproj -- --run-all .`
  - 通过
  - `cards_transform_select_generate`: PASS
  - `cards_cost_playcount`: PASS
  - `cards_status_curse_passive`: PASS
  - `potions_combat`: PASS
  - `potions_noncombat`: PASS
  - `relics_stateful_combat`: PASS
  - `relics_modifier_and_merchant`: PASS
  - `events_multipage_reward`: PASS
  - `events_combat_resume`: PASS
  - `enchantments_modifiers`: PASS
  - `passive_only`: PASS

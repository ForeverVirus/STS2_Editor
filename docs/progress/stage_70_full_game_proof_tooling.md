# Stage 70 - Full Game Proof Tooling

## 已开发内容
- 新增 `tools/Stage70FullGameProof/Stage70FullGameProof.csproj`
- 新增 `tools/Stage70FullGameProof/Program.cs`
- tool 当前可生成：
  - `coverage/release-proof/proof_manifest.json`
  - `coverage/release-proof/batches/*.json`
  - `coverage/release-proof/run_batch_*.ps1`
  - `coverage/release-proof/report.md`
- 已固定 proof 批次骨架：
  - `cards_transform_select_generate`
  - `cards_cost_playcount`
  - `cards_status_curse_passive`
  - `potions_combat`
  - `potions_noncombat`
  - `relics_stateful_combat`
  - `relics_modifier_and_merchant`
  - `events_multipage_reward`
  - `events_combat_resume`
  - `enchantments_modifiers`
  - `passive_only`

## 未开发内容
- 还没有把 proof package 生成接到每个 batch 上
- 还没有真正启动游戏执行批次
- 还没有实现 `godot.log` proof marker 汇总与 pass/fail 统计

## 遇到的问题
- 现阶段只是 tooling scaffold，不是最终 Stage 71 proof 执行
- 仍需把现有 `Stage06 / Stage09 / Stage12 / Stage13` 的 package 生成逻辑抽成复用部件

## 后续如何解决
- 下一步在 `Stage 71` 前继续补：
  - batch -> proof project/package 生成
  - launcher 脚本参数拼接
  - `godot.log` 解析器
  - proof marker 汇总

## 验证结果
- `dotnet run --project tools\\Stage70FullGameProof\\Stage70FullGameProof.csproj -- .`
  - 通过
  - 已生成 `coverage/release-proof/proof_manifest.json`
  - 已生成 `coverage/release-proof/report.md`

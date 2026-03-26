# Stage 66 - Release Candidate Validation

## 已开发内容
- 已完成本轮 release candidate 的自动化验证闭环：
  - 主工程编译
  - Stage 03 smoke
  - Stage 59 baseline 重新生成
  - Stage 65 coverage artifacts 重新生成
- 事件 coverage 样例已通过 event compiler 校验。
- enchantment coverage 样例已通过 graph registry 校验，且 package/export/import roundtrip 正常。

## 未开发内容
- 真实游戏内的最终人工验收证明这次没有补做，尤其是：
  - 多页奖励事件
  - 战斗返回事件
  - 复杂附魔样例
- Stage 59 的覆盖统计仍受 console harness bootstrap 限制，不能把当前 baseline 直接等同于游戏内真实支持率。

## 遇到的问题
- 终端工具环境缺少完整 `ModelDb` bootstrap，导致 Stage 59 baseline 中仍有大量 `CHARACTER.* / ACT.* / ORB.*` 环境异常样本。
- 因此本轮更适合作为“可交付 release candidate”，而不是“已经完成所有真实游戏 proof 的最终黄金版”。

## 后续如何解决
- 若要继续冲最终发布证明，需要补 console bootstrap / fake runtime 初始化链，或者补真正的游戏内 proof runner。
- 在现有基础上，下一步优先应该把 Stage 59 的环境限制剥离掉，再刷新一次真实覆盖率。

## 验证结果
- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`
  - 通过
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
  - 结果 `PASS`
- `dotnet run --project tools\\Stage59CoverageBaseline\\Stage59CoverageBaseline.csproj -- .`
  - 通过
- `dotnet run --project tools\\Stage65CoverageArtifacts\\Stage65CoverageArtifacts.csproj -- .`
  - 通过

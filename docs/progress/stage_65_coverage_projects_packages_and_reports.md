# Stage 65 - Coverage Projects Packages And Reports

## 已开发内容
- 新增 `tools/Stage65CoverageArtifacts` 生成器，用于程序化产出 release V1 的 coverage 交付物。
- 生成器会输出固定目录：
  - `coverage/cards`
  - `coverage/relics`
  - `coverage/potions`
  - `coverage/events`
  - `coverage/enchantments`
  - `coverage/aggregate`
- 每个目录都包含：
  - graph JSON
  - single-graph project JSON
  - single-graph package
  - `report.md`
- aggregate 目录包含：
  - `coverage-project.json`
  - `coverage-package.sts2pack`
  - `report.md`
- `Stage03SmokeTest` 的 coverage 样例也同步升级到五类实体，并覆盖 `reward.offer_custom` 与 `Enchantment` 节点。

## 未开发内容
- 当前 coverage 项目仍是“代表性样例工程”，不是“每个原版对象逐一出包”的全量归档。
- aggregate 报告目前聚焦于交付物路径与图数量，尚未汇总 node-family 级别覆盖矩阵。

## 遇到的问题
- 生成工具起初引用的是 `.godot/mono/temp/bin/Debug/STS2_Editor.dll`，该路径不是最新编译产物，导致 registry 缺少新节点定义。
- 已统一切换为 `.godot/mono/temp/obj/Debug/STS2_Editor.dll`，生成器和 smoke 现在能看到最新 graph 节点集。

## 后续如何解决
- 如果继续推进 Stage 65，可在 aggregate 报告中追加 node-family 覆盖矩阵和导入回读摘要。
- 若需要更强 proof，可再补自动安装/归一化后的 package 内容校验报告。

## 验证结果
- `dotnet build tools\\Stage65CoverageArtifacts\\Stage65CoverageArtifacts.csproj`
  - 通过
- `dotnet run --project tools\\Stage65CoverageArtifacts\\Stage65CoverageArtifacts.csproj -- .`
  - 通过
  - 已生成 `coverage/aggregate/report.md`
  - 已生成五类子目录下的 `project/package/report`
- `dotnet run --project tools\\Stage03SmokeTest\\Stage03SmokeTest.csproj -- tools\\stage03-smoke-run`
  - 通过

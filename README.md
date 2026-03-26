# STS2 Editor

[English](README_en.md)

`STS2 Editor` 是一个面向 **Slay the Spire 2** 的可视化 Mod 编辑器，当前仓库对应 **V1.0** 交付版本。

它把 `Project Mode`、`Package Mode`、Graph 编辑、素材绑定、导包、热重载和交付验证工具链整合在同一个仓库中，目标是让 `Card / Relic / Potion / Event / Enchantment` 五类内容能够稳定编辑、稳定导出、稳定验证。

## V1.0 状态

当前仓库已经完成 `Release V1` 的最终门禁，结论是：

- `Card / Relic / Potion / Event / Enchantment` 五类实体在真实口径下已全绿
- description roundtrip 已全绿
- command-driven full game proof 已全绿
- 当前仓库可直接作为 `V1.0` 交付版本

详细摘要见：

- [docs/reference/release_v1_todo.md](docs/reference/release_v1_todo.md)
- [docs/progress/stage_72_final_release_validation_and_delivery.md](docs/progress/stage_72_final_release_validation_and_delivery.md)
- [coverage/release-proof/report.md](coverage/release-proof/report.md)

## 核心功能

- `Project Mode`
  用于创建和编辑工程，支持基础字段编辑、Graph 行为编辑、素材绑定、导出包。
- `Package Mode`
  用于管理已导出的 `.sts2pack` 包，查看已发布包并执行 `Hot Reload`。
- Graph 工作流
  支持原版行为自动生成、节点值调整、描述同步、图校验。
- 双语界面
  编辑器界面支持中文 / English 切换，并保存本地语言设置。
- 内置说明
  编辑器顶部菜单支持直接打开 GitHub、作者主页和内置使用说明。

## V1.0 覆盖范围

| 类型 | 状态 |
| --- | --- |
| Card | 577 / 577 supported |
| Relic | 289 / 289 supported |
| Potion | 64 / 64 supported |
| Event | 59 / 59 supported |
| Enchantment | 24 / 24 supported |

对应参考：

- [docs/reference/coverage_baseline.md](docs/reference/coverage_baseline.md)
- [docs/reference/description_roundtrip.md](docs/reference/description_roundtrip.md)

## 快速开始

### 1. 环境要求

- Windows
- 已安装本地 `Slay the Spire 2`
- `.NET 9 SDK`

### 2. 配置游戏目录

仓库当前默认从 [STS2_Editor.csproj](STS2_Editor.csproj) 里的 `Sts2Dir` 读取游戏目录：

```xml
<Sts2Dir>F:\SteamLibrary\steamapps\common\Slay the Spire 2</Sts2Dir>
```

如果你的游戏不在这个路径，需要先改成你自己的本地安装目录。

### 3. 编译

```powershell
dotnet msbuild STS2_Editor.csproj /t:Compile
```

编译完成后，mod 会被复制到：

```text
<Slay the Spire 2>\mods\STS2_Editor\
```

注意：

- 如果游戏正在运行，`STS2_Editor.dll` 可能会被锁定，导致复制失败
- 这种情况下先关闭游戏，再重新编译

## `.sts2pack` 指定目录

编辑器 `Package Mode` 默认扫描的已发布包目录是：

```text
<Slay the Spire 2>\mods\STS2_Editor\mods\
```

例如：

```text
F:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\STS2_Editor\mods\
```

这意味着：

- 你通过编辑器导出的 `.sts2pack`，推荐直接保存到这个目录
- 如果包放在别的目录，`Package Mode` 默认不会显示它

## 编辑器内说明入口

进入编辑器后，可以通过顶部菜单直接打开：

- `关于 -> github地址`
- `关于 -> 使用说明`
- 右上角作者链接

完整使用说明文件也已随仓库提供：

- [docs/editor_user_guide.html](docs/editor_user_guide.html)

## 验证与交付门禁

V1.0 最终门禁包含以下命令：

```powershell
dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false
dotnet run --project tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- tools\stage03-smoke-run
dotnet run --project tools\Stage59CoverageBaseline\Stage59CoverageBaseline.csproj -- .
dotnet run --project tools\Stage61DescriptionRoundtrip\Stage61DescriptionRoundtrip.csproj -- .
dotnet run --project tools\Stage65CoverageArtifacts\Stage65CoverageArtifacts.csproj -- .
dotnet run --project tools\Stage70FullGameProof\Stage70FullGameProof.csproj -- .
dotnet run --project tools\Stage70FullGameProof\Stage70FullGameProof.csproj -- --run-all .
```

当前仓库对应的最终结果见：

- [coverage/release-proof/report.md](coverage/release-proof/report.md)
- [docs/progress/stage_71_command_driven_full_game_proof.md](docs/progress/stage_71_command_driven_full_game_proof.md)
- [docs/progress/stage_72_final_release_validation_and_delivery.md](docs/progress/stage_72_final_release_validation_and_delivery.md)

## 仓库结构

```text
Scripts/Editor/
  Core/        核心路径、配置、数据模型、基础服务
  Graph/       Graph 定义、导入、描述与打包逻辑
  Runtime/     运行时桥接、动态内容注册、proof 支持
  UI/          主菜单入口、Project Mode、Package Mode、各类编辑器 UI

docs/
  reference/   交付摘要、coverage、description roundtrip 等参考文档
  progress/    各阶段实施日志

coverage/
  release-proof/  全量实机 proof 结果与批次报告

tools/
  Stage03/59/61/65/70... 交付验证与 proof 工具
```

## 作者与仓库

- 作者：`禽兽-云轩`
- Bilibili：<https://space.bilibili.com/8729996>
- GitHub：<https://github.com/ForeverVirus/STS2_Editor>


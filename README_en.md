# STS2 Editor

[简体中文](README.md)

`STS2 Editor` is a visual mod editor for **Slay the Spire 2**. This repository corresponds to the **V1.0** release-ready delivery.

It combines `Project Mode`, `Package Mode`, graph editing, asset binding, package export, hot reload, and proof tooling in one codebase. The goal of V1.0 is stable authoring and validation for `Card / Relic / Potion / Event / Enchantment`.

## V1.0 Status

This repository has already passed the final `Release V1` gates:

- truthful support is fully green for `Card / Relic / Potion / Event / Enchantment`
- description roundtrip is fully green
- command-driven full game proof is fully green
- the current repository is ready to ship as `V1.0`

See:

- [docs/reference/release_v1_todo.md](docs/reference/release_v1_todo.md)
- [docs/progress/stage_72_final_release_validation_and_delivery.md](docs/progress/stage_72_final_release_validation_and_delivery.md)
- [coverage/release-proof/report.md](coverage/release-proof/report.md)

## Core Features

- `Project Mode`
  Create and edit projects, including metadata editing, graph authoring, asset binding, and package export.
- `Package Mode`
  Manage exported `.sts2pack` files, inspect published packages, and run `Hot Reload`.
- Graph workflow
  Supports native behavior import, value editing, description sync, and validation.
- Bilingual UI
  The editor supports Chinese and English and persists the selected language locally.
- Built-in help
  The top menu can open GitHub, the author page, and the built-in user guide directly.

## V1.0 Coverage

| Kind | Status |
| --- | --- |
| Card | 577 / 577 supported |
| Relic | 289 / 289 supported |
| Potion | 64 / 64 supported |
| Event | 59 / 59 supported |
| Enchantment | 24 / 24 supported |

References:

- [docs/reference/coverage_baseline.md](docs/reference/coverage_baseline.md)
- [docs/reference/description_roundtrip.md](docs/reference/description_roundtrip.md)

## Quick Start

### 1. Requirements

- Windows
- a local `Slay the Spire 2` installation
- `.NET 9 SDK`

### 2. Configure the game path

The repository currently reads the game directory from `Sts2Dir` in [STS2_Editor.csproj](STS2_Editor.csproj):

```xml
<Sts2Dir>F:\SteamLibrary\steamapps\common\Slay the Spire 2</Sts2Dir>
```

Change this value to your own local install path before building.

### 3. Build

```powershell
dotnet msbuild STS2_Editor.csproj /t:Compile
```

After a successful build, the mod is copied to:

```text
<Slay the Spire 2>\mods\STS2_Editor\
```

Notes:

- if the game is running, `STS2_Editor.dll` may be locked
- in that case, close the game and build again

## `.sts2pack` Published Package Directory

`Package Mode` scans this published package root by default:

```text
<Slay the Spire 2>\mods\STS2_Editor\mods\
```

For example:

```text
F:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\STS2_Editor\mods\
```

This means:

- exported `.sts2pack` files should be saved there if you want them to appear in `Package Mode`
- packages stored elsewhere are not listed by default

## Built-in Documentation

Inside the editor, the top menu provides direct links to:

- `About -> GitHub`
- `About -> User Guide`
- the author link on the top right

The bundled guide is also available in the repository:

- [docs/editor_user_guide.html](docs/editor_user_guide.html)

## Validation Gates

The final V1.0 gate chain includes:

```powershell
dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false
dotnet run --project tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- tools\stage03-smoke-run
dotnet run --project tools\Stage59CoverageBaseline\Stage59CoverageBaseline.csproj -- .
dotnet run --project tools\Stage61DescriptionRoundtrip\Stage61DescriptionRoundtrip.csproj -- .
dotnet run --project tools\Stage65CoverageArtifacts\Stage65CoverageArtifacts.csproj -- .
dotnet run --project tools\Stage70FullGameProof\Stage70FullGameProof.csproj -- .
dotnet run --project tools\Stage70FullGameProof\Stage70FullGameProof.csproj -- --run-all .
```

Final reports:

- [coverage/release-proof/report.md](coverage/release-proof/report.md)
- [docs/progress/stage_71_command_driven_full_game_proof.md](docs/progress/stage_71_command_driven_full_game_proof.md)
- [docs/progress/stage_72_final_release_validation_and_delivery.md](docs/progress/stage_72_final_release_validation_and_delivery.md)

## Repository Layout

```text
Scripts/Editor/
  Core/        paths, settings, models, core services
  Graph/       graph definitions, importers, descriptions, packaging logic
  Runtime/     runtime bridge, dynamic content registration, proof support
  UI/          main menu entry, Project Mode, Package Mode, editor UI

docs/
  reference/   release summary, coverage, description roundtrip references
  progress/    stage logs

coverage/
  release-proof/  full game proof outputs and batch reports

tools/
  Stage03/59/61/65/70... validation and proof runners
```

## Author and Repository

- Author: `禽兽-云轩`
- Bilibili: <https://space.bilibili.com/8729996>
- GitHub: <https://github.com/ForeverVirus/STS2_Editor>


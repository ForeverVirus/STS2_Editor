# Stage 10 - External Asset Import Pack Install Proof

## Date
- 2026-03-23

## Developed
- Added a standalone external-asset proof tool in [Stage10ExternalAssetProof.csproj](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage10ExternalAssetProof/Stage10ExternalAssetProof.csproj) and [Program.template](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage10ExternalAssetProof/Program.template).
  - The tool generates three simple PNG files at runtime for `card`, `relic`, and `potion`.
  - The tool copies those files into the project-managed asset layout under `user://sts2_editor/projects/<projectId>/assets/...`.
  - The tool creates a brand-new custom card, relic, and potion using the same graph-driven approach as the earlier proof stage.
  - The proof package is exported, imported, normalized, and installed into the real game user directory.
  - The resulting metadata uses `modstudio://asset/...` references instead of loose absolute paths.
- Reused the package export/import/install flow already present in the repository without modifying the Stage 09 proof files.

## Validation
- Tool build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\tools\Stage10ExternalAssetProof\Stage10ExternalAssetProof.csproj`
  - Result: `0 warning / 0 error`
- Tool run:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage10ExternalAssetProof\Stage10ExternalAssetProof.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage10-proof-run`
  - Result:
    - generated three PNGs
    - exported a `.sts2pack`
    - installed the package into the real AppData editor directories
    - updated `session.json`
    - confirmed the installed asset files exist on disk

## Not Developed Yet
- This stage does not yet execute a full real-game autoslay run for the external-asset package.
- The tool only proves the packaging and installation path, not the in-combat rendering of those imported textures.
- No UI changes were made in this stage.

## Issues Encountered
- The first implementation used `AssetImportService` and `ModStudioPaths` directly, which caused a fatal startup crash in a standalone console process because those helpers depend on Godot runtime initialization.
  - Resolution:
    - switch the Stage 10 tool to a pure .NET managed-asset copy path that matches the repository's project asset layout.
- The first installation flow deleted the package directory after extracting assets, which removed the files we just installed.
  - Resolution:
    - move asset extraction to happen after the package directory is recreated.
- A malformed interpolated string in the report caused a compile failure during the first fix-up pass.
  - Resolution:
    - replace the escaped string literal with a normal C# interpolation expression.

## Next Step
- Use the existing runtime and autoslay proof flow to validate that the imported PNGs are actually rendered in game.
- If that passes, the next logical expansion is to expose these asset-binding actions cleanly in the editor UI so authors can pick original assets or imported external assets from one place.

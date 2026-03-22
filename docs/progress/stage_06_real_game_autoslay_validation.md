# Stage 06 - Real Game Autoslay Validation

## Date
- 2026-03-23

## Developed
- Added a dedicated gameplay proof tool in [Stage06GameplayProof.csproj](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage06GameplayProof/Stage06GameplayProof.csproj) and [Program.template](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage06GameplayProof/Program.template).
  - The tool creates a controlled sample project.
  - It exports a `.sts2pack`, installs it into the real game user directory, and persists a matching project file for inspection.
- Added an autoslay-only runtime patch in [RuntimeAutoslayPatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeAutoslayPatches.cs).
  - `NGame.IsReleaseGame()` is forced to `false` only when the game is started with `--autoslay`.
  - This keeps the autoslay proof path isolated from normal player sessions.
- Fixed a real browsing/runtime bug in [ModelMetadataService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Services/ModelMetadataService.cs).
  - Asset-path reads now go through a safe wrapper so problematic models such as `MadScience` do not throw while the editor browser is enumerating content.
- Fixed the mod manifest dependency entry in [STS2_Editor.json](/F:/sts2_mod/mod_projects/STS2_editor/STS2_Editor.json).
  - `dependencies` is now an empty array instead of an array containing an empty string.

## Validation
- Main build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Gameplay proof tool build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\tools\Stage06GameplayProof\Stage06GameplayProof.csproj`
  - Result: `0 warning / 0 error`
- Gameplay proof tool run:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage06GameplayProof\Stage06GameplayProof.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage06-proof-run`
  - Result:
    - created sample project/package artifacts
    - installed package into the real game editor directories
- Real game autoslay run:
  - executable: `F:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe`
  - command used: `SlayTheSpire2.exe --autoslay --seed stage06-proof-2`
  - proof log checked at [godot.log](C:/Users/Administrator/AppData/Roaming/SlayTheSpire2/logs/godot.log)
  - observed markers:
    - `[INFO] [AutoSlay] Using potion: BLOCK_POTION`
    - `[INFO] [ModStudio.Graph] STAGE06_POTION_OK`
    - repeated `[INFO] [AutoSlay] Playing NEUTRALIZE`
    - repeated `[INFO] [ModStudio.Graph] STAGE06_CARD_OK`
- Conclusion:
  - installed package discovery works in the real game
  - character starting deck and starting potion overrides take effect in an actual run
  - graph-driven card and potion overrides execute in real combat instead of only in smoke tests

## Not Developed Yet
- The proof package is intentionally narrow and does not validate all editable metadata fields.
- The sample graph uses very low damage and is only for proof, not for long-form automated test throughput.
- Event runtime graph execution, new content registration, and monster-related expansion are still outside this stage.

## Issues Encountered
- Autoslay was not starting in the release build because the upstream code only enables it when `NGame.IsReleaseGame()` returns `false`.
  - Resolution:
    - add a Harmony patch gated behind the `autoslay` command-line argument only
- Godot's own `--log-file` handling is not a reliable place to inspect Mod Studio proof markers.
  - Resolution:
    - inspect the standard real-game `godot.log`
- Editor metadata browsing could throw on certain asset getters.
  - Resolution:
    - wrap asset-path reads in a safe accessor

## Next Step
- Build on this proof by closing more editor-to-runtime gaps.
- Recommended next targets:
  - patch more editable metadata into live runtime getters
  - expose package conflict inspection in `Package Mode`
  - then move into larger gaps such as new content registration and event runtime templates

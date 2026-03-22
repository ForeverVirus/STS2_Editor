# Stage 05 - Portable Asset Packages And Binding Workflow

## Date
- 2026-03-23

## Developed
- Added a stable package-asset reference scheme in [ModStudioAssetReference.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Utilities/ModStudioAssetReference.cs).
  - Distributed asset references now use `modstudio://asset/<packageKey>/<assetId>/<fileName>` instead of author-machine absolute paths.
  - The helper can also resolve that reference back into a local installed asset path.
- Expanded installed-package path utilities in [ModStudioPaths.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Utilities/ModStudioPaths.cs).
  - Added helpers for installed package directories and installed asset file locations.
- Updated package export/import logic in [PackageArchiveService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/Packaging/PackageArchiveService.cs).
  - Exported package JSON now strips machine-local asset paths for managed assets and keeps only stable package references.
  - Exported archives still include managed asset bytes.
  - Checksum computation now ignores machine-specific paths so the same package hashes consistently across machines.
  - Imported packages can normalize assets into a local installed directory and extract managed asset files there.
- Updated installation flow in [EditorPackageStore.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/Packaging/EditorPackageStore.cs).
  - Installed packages now materialize as a directory package with `manifest.json`, `project.json`, and extracted asset files.
  - This gives the receiver a locally readable asset path instead of a foreign absolute path.
- Updated runtime package discovery in [RuntimePackageCatalog.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimePackageCatalog.cs).
  - Discovery now prefers directory-installed packages when both archive and directory forms exist.
  - Runtime checksum fallback logic now matches the packager checksum fields.
- Updated runtime path normalization in [RuntimeAssetLoader.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeAssetLoader.cs).
  - Runtime load paths now accept `modstudio://asset/...` and resolve them to local installed files.
  - Existing `res://`, `user://`, and rooted filesystem paths remain supported.
- Restored and stabilized the asset-binding editor entry points in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs).
  - The asset binding panel now compiles again and routes through the binding service.
  - This was needed because the asset workflow UI hooks had become disconnected during previous partial edits.
- Fixed the only remaining nullable warning in [ProjectAssetBindingService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Services/ProjectAssetBindingService.cs).

## Validation
- Main mod build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Smoke-test build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj`
  - Result: `0 warning / 0 error`
- Smoke-test run:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - Result: `PASS`
  - The smoke run now verifies:
    - package export/import roundtrip
    - portable asset install roundtrip
    - package order override precedence
    - session negotiation intersection

## Not Developed Yet
- The asset binding workflow is now technically wired, but the editor UI still needs polishing and clearer asset-role affordances.
- No live in-game combat verification has been added yet for a real graph-driven card or potion effect.
- Event authoring still does not expose a full visual editor for custom layout-driven events.
- Monster/Spine editing remains out of scope for Phase 1.
- There is still no automatic migration tool for older packages that may have been authored before the new stable asset reference format.

## Issues Encountered
- The asset workflow had two independent problems:
  - package export still carried machine-local asset paths in JSON
  - installed packages were not being normalized into a local readable asset directory
- The first compile/test pass also exposed duplicated asset-editor methods in `NModStudioScreen.cs`.
  - Resolution:
    - keep the existing asset-binding implementation
    - rename the duplicate later declarations to avoid compile conflicts
- A few compile runs were interrupted by parallel build/process file locks.
  - Resolution:
    - rerun build and smoke validation sequentially
    - final validation passed cleanly

## Next Step
- The next delivery stage should move from packaging correctness into gameplay proof.
- Recommended next target:
  - pick one card and one potion as controlled samples
  - bind them to stable package assets or external imports
  - verify the runtime path resolution in a real run
  - then verify the gameplay effect hook path in combat

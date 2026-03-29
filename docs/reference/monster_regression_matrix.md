# Monster Regression Matrix

| Area | Status | Evidence |
|------|--------|----------|
| Compile | PASS | `dotnet msbuild STS2_Editor.csproj /t:Build /p:CopyModAfterBuild=false` |
| Existing Stage03 regression | PASS | `tools\\Stage03SmokeTest` |
| Monster import coverage | PASS | [stage81-report.md](/F:/sts2_mod/mod_projects/STS2_editor/coverage/monster-proof/stage81-report.md) |
| Monster JSON roundtrip | PASS | [stage81-report.md](/F:/sts2_mod/mod_projects/STS2_editor/coverage/monster-proof/stage81-report.md) |
| Monster runtime execution | PASS | Imported monster runtime paths execute in both direct monster proof rooms and encounter-context proof rooms; see [report.md](/F:/sts2_mod/mod_projects/STS2_editor/coverage/monster-proof/report.md) |
| Full in-game monster regression | PASS | `tools\\Stage82MonsterRegression` completed `102/102` live-game batches; see [report.md](/F:/sts2_mod/mod_projects/STS2_editor/coverage/monster-proof/report.md) |

# Stage 04 - Bug Triage And Bilingual UI

## Date
- 2026-03-23

## Developed
- Triaged the current `Mod Studio` runtime errors from [error_log.txt](/F:/sts2_mod/mod_projects/STS2_editor/error_log.txt) and separated them into actionable Stage 03 bugs instead of treating them as “not implemented yet”.
- Fixed the main-menu `Mod Studio` button font/theme bug in [ModStudioUiPatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioUiPatches.cs).
  - The injected `MegaLabel` now copies a real theme font override from an existing main-menu button label.
  - This addresses the `MegaLabel ... has no theme font override` crash source.
- Fixed the metadata browser localization-formatting spam in [ModelMetadataService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Services/ModelMetadataService.cs).
  - Browser text now reads safe raw localization text instead of calling `LocString.GetFormattedText()` in contexts that do not provide the required formatting extensions.
  - This removes the card-description formatting errors seen in the log when browsing card data.
- Added a bilingual Mod Studio language layer with local persistence:
  - [ModStudioLocalization.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Utilities/ModStudioLocalization.cs)
  - [ModStudioSettings.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Core/Models/ModStudioSettings.cs)
- The Mod Studio UI now defaults to Chinese and can switch to English in-game.
  - The selected language is saved locally in `user://sts2_editor/settings.json`.
  - On the next launch, the last selected language is restored automatically.
- Localized the core Mod Studio chrome in [NModStudioScreen.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioScreen.cs):
  - title
  - tabs
  - section labels
  - action buttons
  - graph helper panels
  - project/package detail labels
  - most status feedback
  - category button labels
- Localized graph helper text in [ModStudioGraphUiHelpers.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/ModStudioGraphUiHelpers.cs).
- Kept main build stability intact while landing the UI/localization work.

## Error Log Triage
- `MegaLabel '/root/.../ModStudioButton/Label' has no theme font override`
  - Classification: real Stage 03 regression bug.
  - Cause: the injected main-menu label set a theme, but did not provide the required font override expected by `MegaLabelHelper`.
  - Status: fixed in this stage.
- `Localization formatting error ... No source extension could handle selector ...`
  - Classification: real Stage 03 browser bug, not an expected “unfinished phase” error.
  - Cause: `ModelMetadataService` was calling `LocString.GetFormattedText()` while building browser/detail text for objects whose descriptions depend on runtime formatting extensions that are not available in this browsing path.
  - Status: fixed in this stage by switching the browser to safe raw localization text.

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

## Not Developed Yet
- There is still no full visual node editor; graph editing remains template-assisted plus JSON editing.
- The current browser now avoids formatting crashes, but it still shows raw loc text in places where the fully formatted combat/runtime text would require additional context.
- No automated real in-game combat verification has been added yet for:
  - one graph-driven card override
  - one graph-driven potion override
  - one hook-driven relic/enchantment override
- External asset import/preview, event runtime coverage, and new content injection are still pending.

## Issues Encountered
- The main-menu injected label used a custom creation path that did not match the assumptions of `MegaLabel`.
  - Resolution:
    - reuse a real menu label’s theme font override when creating the injected label
- Bilingual UI touched many hardcoded strings spread across the screen builder, status paths, and helper panels.
  - Resolution:
    - centralize strings in `ModStudioLocalization`
    - keep the current screen on live refresh instead of rebuilding the whole UI and wiping editor text fields
- The smoke-test project briefly hit an `obj` file lock when build and run were triggered in parallel.
  - Resolution:
    - rerun the smoke-test build sequentially
    - final validation passed cleanly

## Next Step
- The next delivery stage should focus on real gameplay proof instead of more editor chrome.
- Recommended next target:
  - create a controlled sample project that overrides one known card with a graph preset
  - create a controlled sample project that overrides one known potion with a graph preset
  - enter a real run and verify the behavior in combat
  - document the tested entity ids, expected effects, and observed results

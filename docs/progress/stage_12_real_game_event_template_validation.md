# Stage 12 - Real Game Event Template Validation

## Date
- 2026-03-23

## Developed
- Added a deterministic event-entry proof harness:
  - [RuntimeProofHarness.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeProofHarness.cs)
  - [RuntimeProofHarnessPatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeProofHarnessPatches.cs)
  - The harness supports `--modstudio-proof-event=<EVENT_ID>` and redirects autoslay from the map screen into a specific event.
- Strengthened template-event runtime support in [RuntimeEventTemplateSupport.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeEventTemplateSupport.cs).
  - Added detailed logs for page initialization, page application, option execution, combat start, resume, and proceed.
  - Added support for `is_proceed=true` to actually exit back to the map when the option is chosen.
  - Event options now use explicit `LocString` construction so template pages do not crash when the base localization table has no shipped entry.
  - Template-created proceed options now set the runtime `EventOption.IsProceed` flag via reflection so existing event UI and autoslay logic can recognize them.
- Fixed event-localization overlay routing in [RuntimeOverrideMetadata.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeOverrideMetadata.cs).
  - Event page / option text is now resolved before the generic `<eventId>.title` lookup.
  - This fixes the false-negative lookup that caused template option titles to throw `LocException`.
- Added a standalone proof package generator:
  - [Stage12EventTemplateProof.csproj](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage12EventTemplateProof/Stage12EventTemplateProof.csproj)
  - [Program.template](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage12EventTemplateProof/Program.template)
  - The proof package creates a brand-new shared template event with:
    - initial page
    - event-triggered combat
    - resume page after combat
    - proceed exit back to the map

## Validation
- Main mod build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Proof tool build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\tools\Stage12EventTemplateProof\Stage12EventTemplateProof.csproj`
  - Result: `0 warning / 0 error`
- Proof tool run:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage12EventTemplateProof\Stage12EventTemplateProof.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage12-proof-run`
  - Result:
    - exported `.sts2pack`
    - installed the proof event package
    - updated `session.json`
- Real-game autoslay proof:
  - command used:
    - `F:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe --autoslay --seed stage12-event-proof-fix3 --modstudio-proof-event=ED_STAGE12_EVENT001`
  - proof log checked at [godot.log](C:/Users/Administrator/AppData/Roaming/SlayTheSpire2/logs/godot.log)
  - observed runtime evidence:
    - `[ModStudio.Proof] Entering proof event 'ED_STAGE12_EVENT001'`
    - `[ModStudio.Event] Initializing template event ED_STAGE12_EVENT001 -> page INITIAL`
    - `[ModStudio.Event] Applied template page ED_STAGE12_EVENT001:INITIAL options=1`
    - `[ModStudio.Event] Executing option ED_STAGE12_EVENT001:INITIAL:PROVE_COMBAT`
    - `[ModStudio.Event] Starting template combat ED_STAGE12_EVENT001 encounter=LOUSE_PROGENITOR_NORMAL resume=AFTER_COMBAT`
    - `[ModStudio.Event] Resuming template event ED_STAGE12_EVENT001 -> page AFTER_COMBAT`
    - `[ModStudio.Event] Applied template page ED_STAGE12_EVENT001:AFTER_COMBAT options=1`
    - `[ModStudio.Event] Executing option ED_STAGE12_EVENT001:AFTER_COMBAT:PROCEED`
    - `[ModStudio.Event] Proceeding out of template event ED_STAGE12_EVENT001 via option PROCEED`
- Conclusion:
  - a brand-new dynamic event can be registered in the shipped game
  - template pages are applied in real runtime, not only in editor data
  - event-triggered combat and post-combat resume both work in the shipped game
  - template proceed exit works in the shipped game

## Not Developed Yet
- This stage proves the metadata-driven template event path, not a node-graph-driven custom event behavior system.
- It does not yet prove custom Godot event layouts.
- It does not yet prove real multiplayer session negotiation with remote peers.

## Issues Encountered
- First proof attempt crashed in `EventOption.ToString()` because template-created options could produce `null` title / description when routed through `GetIfExists()`.
  - Resolution:
    - switch template option creation to explicit `LocString` objects.
- Second proof attempt still failed because event option localization keys were being swallowed by the generic event-title fallback path.
  - Resolution:
    - reorder event text matching in [RuntimeOverrideMetadata.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeOverrideMetadata.cs).
- Third proof attempt resumed the event correctly but autoslay could not detect the post-combat exit option.
  - Resolution:
    - set `EventOption.IsProceed` on template-generated proceed options
    - simplify the proof package so the resume page exits directly
- The final autoslay run later failed in a normal non-proof combat because autoslay stalled around combat turn 100.
  - Classification:
    - unrelated to the event-template proof chain, because all event proof markers had already completed successfully before that later failure.

## Next Step
- Move to the multiplayer/package-intersection gap.
- Recommended target:
  - add a real-game session proof harness that feeds peer package snapshots into the runtime registry,
  - log resulting `SessionEnabled`, `DisabledReason`, applied package order, and conflict winners from the shipped game.

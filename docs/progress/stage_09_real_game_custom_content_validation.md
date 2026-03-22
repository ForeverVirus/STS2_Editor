# Stage 09 - Real Game Custom Content Validation

## Date
- 2026-03-23

## Developed
- Fixed a Harmony patch signature bug in [RuntimeOverridePatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeOverridePatches.cs).
  - `LocString.GetRawText()` override interception now uses a real prefix instead of an invalid postfix signature.
- Moved runtime package initialization to a safe post-core point.
  - [ModStudioBootstrap.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/ModStudioBootstrap.cs) now separates service bootstrap from runtime package activation.
  - [RuntimeCoreInitializationPatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeCoreInitializationPatches.cs) initializes the runtime registry after `ModelDb.InitIds()`.
- Added custom `ModelIdSerializationCache` extension support in [RuntimeModelIdSerializationCacheBridge.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeModelIdSerializationCacheBridge.cs).
  - Brand-new custom content now receives runtime entry mappings without crashing startup.
  - Dynamic registration can safely append custom entries to the multiplayer serialization cache.
- Added runtime ID alias resolution for generated models.
  - [RuntimeDynamicContentRegistry.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeDynamicContentRegistry.cs) now maps generated runtime IDs such as `ED_STAGE09_CARD001` back to project editor IDs such as `ed_stage09__card_001`.
  - [EditorRuntimeRegistry.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/Runtime/EditorRuntimeRegistry.cs) now resolves overrides through that alias bridge.
  - This unblocks metadata, localization, graph dispatch, and asset-path lookup for dynamically generated content.
- Reworked abstract getter patching to avoid Harmony startup failures.
  - [RuntimeConcretePropertyOverridePatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeConcretePropertyOverridePatches.cs) now patches concrete potion/relic property getters instead of abstract base getters.
  - Shared override logic remains centralized in [RuntimeOverridePatches.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeOverridePatches.cs).

## Validation
- Main build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Real game autoslay validation:
  - executable: `F:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe`
  - command used: `SlayTheSpire2.exe --autoslay --seed stage09-proof-3`
  - proof log checked at [godot.log](C:/Users/Administrator/AppData/Roaming/SlayTheSpire2/logs/godot.log)
  - observed runtime evidence:
    - `Mod Studio registered dynamic Card 'ed_stage09__card_001'`
    - `Mod Studio registered dynamic Relic 'ed_stage09__relic_001'`
    - `Mod Studio registered dynamic Potion 'ed_stage09__potion_001'`
    - `[INFO] [ModStudio.Graph] STAGE09_CARD_OK`
    - `[INFO] [ModStudio.Graph] STAGE09_POTION_OK`
    - `[INFO] [ModStudio.Graph] STAGE09_RELIC_OK`
- Conclusion:
  - brand-new custom content registration works in the real shipped game
  - starter deck / starter relic / starter potion overrides can force brand-new custom content into an actual run
  - graph execution for newly created card / potion / relic content works in real combat, not just in smoke tests

## Not Developed Yet
- This validation uses original in-game asset references and does not yet prove external imported art in a real run.
- Event runtime templates still need an equivalent real-game proof package.
- Conflict UI is present, but conflict behavior has not yet been proven with a dedicated real-game multi-package scenario.
- Multiplayer package intersection for custom-content runs still needs an end-to-end proof.

## Issues Encountered
- First real-game run failed during mod initialization because `LocString.GetRawText()` was patched with an invalid Harmony signature.
  - Resolution:
    - convert it to a proper prefix patch.
- Second real-game run failed because dynamic content registration happened before core model ID caches were safe to consume.
  - Resolution:
    - defer runtime package initialization until after `ModelDb.InitIds()`.
- Startup also failed because abstract getters like `RelicModel.get_Rarity()` cannot be prepared by Harmony.
  - Resolution:
    - patch the concrete subclass getters instead of the abstract base method.
- The first successful startup still failed in combat because generated runtime IDs did not match editor project IDs.
  - Resolution:
    - add runtime ID alias mapping so generated model IDs resolve back to the original editor entity IDs for metadata, localization, and graph lookup.

## Next Step
- Move to the next user-facing gap in the plan: real-game validation of imported external assets.
- Recommended immediate target:
  - create a proof package that imports external card/relic/potion images,
  - uses them in a real autoslay run,
  - and confirms the visuals resolve through the managed asset pipeline instead of only `res://` assets.

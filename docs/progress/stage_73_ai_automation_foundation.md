# Stage 73 - AI Automation Foundation

## Completed

- Added persistent AI settings fields to `ModStudioSettings`.
- Added an `AI` menu with `AI 助手` and `AI 设置` in Project Mode.
- Added `ModStudioAiConfigDialog` and `ModStudioAiChatPanel`.
- Added OpenAI-compatible client, protocol parser, session contracts, local context service, and preview/apply executor foundation.
- Integrated AI config, chat session handling, `/new`, automatic session rollover, preview apply, and discard flow into `NModStudioProjectWindow`.
- Added the primary implementation plan document at `docs/reference/ai_automation_editor_plan.md`.

## Current Scope

- v1 supports project-local AI planning and preview/apply for:
  - basic metadata edits
  - runtime/imported asset binding switches
  - graph creation and metadata edits
  - graph node add/update/remove/connect/disconnect
  - project-local entity creation for card/relic/potion/event/enchantment

## Validation

- `dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false`

## Notes

- The AI flow currently relies on JSON-only assistant responses over OpenAI-compatible `chat/completions`.
- Session retention is in-memory only for the current editor runtime.

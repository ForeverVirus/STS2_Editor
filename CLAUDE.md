# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

STS2_Editor is a **Slay the Spire 2 mod** that provides a visual editor ("Mod Studio") for authoring and overriding game entity behaviors. It ships as a Godot C# plugin with Harmony runtime patching. It covers 5 entity types: Card, Relic, Potion, Event, Enchantment.

## Build & Test Commands

### Compile (without deploying to game)
```
dotnet msbuild STS2_Editor.csproj /t:Compile /p:CopyModAfterBuild=false
```

### Full build (compiles + copies DLL to STS2 mods folder)
```
dotnet build STS2_Editor.csproj
```

### Gate tests (run these in order to validate changes)
```
dotnet run --project tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- tools\stage03-smoke-run
dotnet run --project tools\Stage59CoverageBaseline\Stage59CoverageBaseline.csproj -- .
dotnet run --project tools\Stage61DescriptionRoundtrip\Stage61DescriptionRoundtrip.csproj -- .
dotnet run --project tools\Stage65CoverageArtifacts\Stage65CoverageArtifacts.csproj -- .
dotnet run --project tools\Stage70FullGameProof\Stage70FullGameProof.csproj -- tools\stage70-proof-run\workspace
```

Stage03 is the core smoke test (graph registry, validation, description generation, dynamic values, native translation, roundtrip). Run it first after any change.

### Run a single tool/test
Each tool under `tools/` is an independent .NET 9.0 console app. Run any with:
```
dotnet run --project tools\<ToolName>\<ToolName>.csproj -- <workspace-dir>
```

## Tech Stack

- **SDK:** Godot.NET.Sdk 4.5.1, .NET 9.0, C# 12
- **Runtime patching:** Harmony (`0Harmony.dll`)
- **Game reference:** `sts2.dll` (Slay the Spire 2 engine, not included in repo)
- **Nullable reference types** enabled; **unsafe blocks** allowed

## Architecture

### Entry Point
`Scripts/Entry.cs` — `[ModInitializer]` that runs `ModStudioBootstrap.Initialize()`, applies all Harmony patches, and registers Godot scripts.

### Core Layers

**Graph System** (`Scripts/Editor/Graph/`)
The heart of the mod. A behavior graph is a DAG of typed nodes (100+ built-in types defined in `BuiltInBehaviorNodeDefinitionProvider`). Key subsystems:
- **Auto-import pipeline:** `NativeBehaviorGraphAutoImporter` reflects over native game code to auto-generate equivalent behavior graphs. `NativeBehaviorGraphTranslator` converts native behavior steps to semantic graph nodes. `NativeBehaviorAutoGraphService` orchestrates with regex-based description parsing as fallback.
- **Execution:** `BehaviorGraphExecutor` walks the graph DAG at runtime. `DynamicValueEvaluator` handles parameterized value computations.
- **Description generation:** `GraphDescriptionGenerator` produces human-readable text from graph structure. `GraphDescriptionTemplateGenerator` creates templates with dynamic placeholders. Roundtrip consistency is a key invariant.
- **Validation:** `BehaviorGraphValidator` checks structural correctness (no cycles, valid connections, port types).

**Runtime Integration** (`Scripts/Editor/Runtime/`)
Harmony patches that intercept game calls to execute authored/overridden graphs:
- `RuntimeGraphDispatcher` — central dispatch for in-game graph execution
- `RuntimeGraphPatches` / `RuntimeOverridePatches` — Harmony patches per entity type
- `EventGraphCompiler` — compiles event graphs to runtime state machines
- `RuntimePackageBackend` — loads `.sts2pack` packages at runtime

**UI** (`Scripts/Editor/UI/`)
Godot-based editor panels:
- `ModStudioGraphCanvasView` — interactive graph editor canvas
- `ModStudioBasicEditor` — generic metadata field editor
- `ModStudioProjectDetailPanel` — main project editing view
- `FieldChoiceProvider` — dynamic dropdown options for fields

**Core Models** (`Scripts/Editor/Core/`)
- `EditorProject` — top-level model: manifest + overrides + graphs + assets
- `EntityDraft` — in-progress entity being edited
- `ModelMetadataService` — entity metadata lookup

### Data Flow
1. Native game entities are auto-imported into semantic behavior graphs
2. User edits/overrides graphs in the Mod Studio UI
3. Graphs are validated, descriptions generated, and packaged as `.sts2pack`
4. At runtime, Harmony patches intercept game calls and dispatch to authored graphs

### Packaging
Packages use `.sts2pack` format (JSON-based). The build target copies the compiled DLL + manifest JSON to `Slay the Spire 2\mods\STS2_Editor\`.

## Key Conventions

- The `tools/` directory is excluded from the main compilation (`<Compile Remove="tools\**\*.cs" />`). Each tool is its own .NET console project that references `STS2_Editor.dll`.
- Progress is tracked in `docs/progress/` with numbered stages (stage_01 through stage_72). Each stage doc has: Goal, Changes, Outputs, Gates, Status.
- Coverage baselines and reports live in `coverage/`.
- Game namespace is `MegaCrit.Sts2.*`; mod namespace is `STS2_Editor.Scripts.Editor.*`.

## Plan Execution Workflow (from AGENTS.MD)

When executing a multi-step plan: continue through all items without stopping for user confirmation unless blocked. If new information requires changing the plan, update and keep going. Only pause for missing requirements, conflicts, or unsafe actions.

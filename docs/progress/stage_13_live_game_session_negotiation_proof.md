# Stage 13 - Live Game Session Negotiation Proof

## Date
- 2026-03-23

## Developed
- Added a shipped-game session proof harness in [RuntimeSessionProofHarness.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeSessionProofHarness.cs).
  - Supports `--modstudio-proof-peers=<jsonPath>`.
  - Applies `RemotePeerPackageSnapshot` negotiation immediately after runtime registry initialization.
  - Logs:
    - focused package `Enabled / SessionEnabled / DisabledReason / LoadOrder`
    - applied package order after negotiation
    - conflict winners for focused entities
- Wired the proof harness into runtime startup in [ModStudioBootstrap.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/ModStudioBootstrap.cs).
- Added a Stage 13 proof generator:
  - [Stage13SessionProof.csproj](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage13SessionProof/Stage13SessionProof.csproj)
  - [Program.template](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage13SessionProof/Program.template)
  - The tool creates three local packages:
    - `stage13_session_a@1.0.0`
    - `stage13_session_b@1.0.0`
    - `stage13_session_c@1.0.0`
  - All three override the same runtime card entity `COOLHEADED`.
  - Local load order is normalized to `A -> C -> B`.
  - The generated remote peer snapshot omits package `C`, which simulates the real rule “missing on a peer means disabled for the session.”
- The proof therefore validates the live-game behavior of the negotiation backend:
  - `C` must become session-disabled
  - `A` and `B` remain active
  - conflict winner for `Card:COOLHEADED` must be `B`, because host order is filtered to the active intersection and later active package still wins

## Validation
- Main mod build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - Result: `0 warning / 0 error`
- Proof tool build:
  - `dotnet build F:\sts2_mod\mod_projects\STS2_editor\tools\Stage13SessionProof\Stage13SessionProof.csproj`
  - Result: `0 warning / 0 error`
- Proof tool run:
  - `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage13SessionProof\Stage13SessionProof.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage13-proof-run`
  - Result:
    - generated and installed packages `A / B / C`
    - normalized local `session.json`
    - generated peer snapshot JSON at [stage13-session-proof-peers.json](/F:/sts2_mod/mod_projects/STS2_editor/tools/stage13-proof-run/workspace/stage13-session-proof-peers.json)
- Real-game proof:
  - launch command:
    - `F:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe --modstudio-proof-peers="F:\sts2_mod\mod_projects\STS2_editor\tools\stage13-proof-run\workspace\stage13-session-proof-peers.json"`
  - after startup reached main menu, the proof run was terminated intentionally to avoid leaving a background game process open
  - proof log checked at [godot.log](C:/Users/Administrator/AppData/Roaming/SlayTheSpire2/logs/godot.log)
  - observed runtime evidence:
    - `[ModStudio.SessionProof] Package stage13_session_a@1.0.0 enabled=True sessionEnabled=True`
    - `[ModStudio.SessionProof] Package stage13_session_c@1.0.0 enabled=True sessionEnabled=False ... missing on peer 'peer-ab'`
    - `[ModStudio.SessionProof] Package stage13_session_b@1.0.0 enabled=True sessionEnabled=True`
    - `[ModStudio.SessionProof] Applied package order: stage13_session_a@1.0.0 -> stage13_session_b@1.0.0`
    - `[ModStudio.SessionProof] Conflict Card:COOLHEADED winner=stage13_session_b@1.0.0 participants=[stage13_session_a@1.0.0@4, stage13_session_b@1.0.0@6]`
- Conclusion:
  - the session negotiator is now proven inside the shipped game process, not only by a terminal smoke test
  - live runtime resolution respects:
    - peer intersection disabling
    - host-side load order among the remaining active packages
    - later-package-wins conflict resolution

## Not Developed Yet
- This stage does not yet send package snapshots over the real multiplayer transport.
- Real host/join/live-session handshake integration is still not wired to collect and exchange peer package snapshots automatically.
- No two-instance networked playtest was performed in this stage.

## Issues Encountered
- The first Stage 13 tool attempt crashed in a standalone process because [RuntimePackageCatalog.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimePackageCatalog.cs) depends on Godot `ProjectSettings`.
  - Resolution:
    - keep the shipped-game proof harness in runtime code
    - make the standalone Stage 13 tool read installed package manifests directly from `installed/*/manifest.json`
- Proving session negotiation with autoslay would have added unrelated gameplay noise.
  - Resolution:
    - run the shipped game to main menu with the proof argument,
    - collect the session negotiation logs,
    - terminate the process after proof capture.

## Next Step
- Decide whether Phase 1 acceptance requires:
  - only shipped-game proof of session negotiation logic, or
  - full real transport integration into multiplayer host/join handshake.
- If full transport integration is required, the next stage should patch the real lobby / join flow to exchange `RemotePeerPackageSnapshot` automatically and then rerun a two-instance proof.

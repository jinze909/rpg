# Unity project context

Analyzed development baseline: `03c6b954ef9a2bec347564edcdd29485d806d90e` (`codex/dev`).
Inspection date: 2026-11-07 (America/Los_Angeles).

## Confirmed repository state

All 43 tracked files at this baseline were read, including hidden role definitions,
workflow skills, documentation, Python installer, C# editor tooling, scene YAML,
Unity metadata and project configuration. `main` directs development to `codex/dev`.
The project root is `/workspace/rpg`; use the existing isolated checkout.

- Unity: **6000.6.8f1**, revision `237b6593792b`.
- Rendering: documented Built-in pipeline; no SRP package is declared. Bootstrap
  contains an orthographic camera and AudioListener, with no gameplay or ORK starter.
- UI: UGUI `2.0.0`; 2D sprite package `1.0.0`. Tilemap, physics, animation and
  other engine modules are declared in `Packages/manifest.json`.
- Input: no Input System package or game input implementation is present;
  actual input configuration remains unverified.
- No networking implementation, test assemblies, asmdefs, CI workflow or package
  lock exists in this baseline. No Unity MCP is available in this session.

## Architecture and entry point

`BACKLOG.md` sets the goal of a polished, enjoyable 2D RPG. Previous game-design
constraints have been withdrawn by the user. Theme, protagonist, locations,
enemies, combat format, perspective and art specifications are undecided;
do not reinstate them from older commits. ORK owns combatants,
combat, inventory, equipment, quests and saves; Makinom owns interactions and
story schematics; Unity owns rendering, physics, input and animation. Gameplay
systems have not yet been implemented.

`Assets/Game/Scenes/Bootstrap.unity` is the sole enabled build scene. Its GUID
matches `ProjectSettings/EditorBuildSettings.asset`.
`Assets/Game/Editor/OrkBootstrapSetup.cs` is editor-only tooling in namespace
`Rpg.Editor`. It locates the ORK starter by reflection, loads the saved project
at `Assets/Gaming Is Love/_Data/Project.asset`, and wires exactly one active
starter. It requires domain and scene reload and rejects empty project data.
The saved project asset and starter are not currently in the checkout.

## Vendor package integration

`tools/install_ork.py` declares ORK 3.22.0 / Makinom 2.21.0 package SHA-256:
`ba2879da647514dac8087531c69c58612aac42270fa4c8fce56d37ea3124eb35`.
It restores original asset paths and metadata under `Assets/Gaming Is Love`
and `Assets/Gizmos`, rejects conflicting files and symlink writes, and reuses
identical files. Package previews and archive transport records are not Unity
project assets. Bundled source archives remain intact as delivered.

Vendor assets are now tracked in this private repository; original `.unitypackage`
archives remain ignored by `.gitignore`.
Game-authored `_Data` can be tracked, except vendor editor data and backups.
Do not infer that vendor files are installed from the README or historical test
claims. This checkout now contains all 136 package files with original bytes and GUIDs.
The source ZIPs remain intact alongside the DLLs.

## Validation and current limitations

The acceptance story is `production/stories/001-ork-bootstrap.md` (Pending).
Required checks are Unity import/compile, package resolution, repeated starter
wiring, batch validation and two Play Mode runs. Batch acceptance requires
exit code 0 and `RPG_BOOTSTRAP_VALIDATED`; it does not replace Play Mode checks.
Follow `README.md` for exact commands and `production/workflow.md` for same-SHA
evidence. Preserve original GUIDs and paired `.meta` files.

During this inspection Git read access succeeded and the working tree initially
matched the development baseline. Unity was not found on PATH or in `/opt`,
and no connected Editor tool is available. Editor checks have not run.

The replacement RAR attachment (27,905,140 bytes) transferred successfully.
Its single Unity package (34,981,620 bytes) matches the pinned SHA-256 exactly.
Independent archive enumeration found 73 assets and 136 project files; all
installed bytes and original GUIDs match. Reinstall added zero files.
`python tools/verify_ork.py` checks file hashes, GUID collisions and source ZIP
CRCs without the original attachment. See `docs/ork-integration.md` for evidence.
Editor import, compilation, saved project data and starter wiring remain pending.

## Guidance and sources

Project instructions: `AGENTS.md`, `CHATGPT.md`, `AI2-STATUS.md`, `BACKLOG.md`,
`production/team.md`, `production/workflow.md`, `production/session-state/active.md`.
Implementation/configuration evidence: `tools/install_ork.py`, `.gitignore`,
`Packages/manifest.json`, `ProjectSettings/*`, `Assets/Game/**`.
All remaining tracked role, workflow and license files were also read.

Producer, implementation, lead review and QA responsibilities may be performed
sequentially in one session. No subagents were used. Keep unverified acceptance
items Pending and do not treat historical validation as current-run evidence.

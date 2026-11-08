# Active development session

Current story: [001 · Validate the ORK bootstrap](../stories/001-ork-bootstrap.md).

## Baseline

- Unity 2022.3.53f1, built-in rendering, 2D orthographic startup scene at `Assets/Game/Scenes/Bootstrap.unity`.
- ORK 3.22.0 and Makinom 2.21.0 are pinned by package SHA-256. The installer preserves package assets and their Unity metadata.
- `Tools > RPG > Wire ORK Bootstrap` assigns a saved Makinom Project asset to the ORK starter in the bootstrap scene. The first starter only initializes the framework. Starting a new game is deferred until the project has a player and a start schematic.

## Verification

- Verified locally that the pinned archive extracts all 136 files byte for byte. A second install made zero changes. A changed archive and a conflicting local asset were rejected. Static scene links and the build scene GUID were verified.
- Editor import, compilation, starter wiring, Play Mode and player builds await a Unity 2022.3.53f1 environment.

## Next acceptance check

Open Bootstrap in Unity with the licensed framework installed and run the current story’s acceptance checks. Record Editor import, repeated starter wiring, and Play Mode results in the story before moving on to the player and interaction.

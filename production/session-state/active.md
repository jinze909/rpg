# Active development session

Current story and next acceptance check are maintained in [AI2-STATUS.md](../../AI2-STATUS.md).

## Baseline

- Unity 6000.6.8f1, built-in rendering, 2D orthographic startup scene at `Assets/Game/Scenes/Bootstrap.unity`.
- ORK 3.22.0 and Makinom 2.21.0 are pinned by package SHA-256. The installer preserves package assets and their Unity metadata.
- `Tools > RPG > Wire ORK Bootstrap` assigns a saved Makinom Project asset to the ORK starter in the bootstrap scene. The first starter only initializes the framework. Starting a new game is deferred until the project has a player and a start schematic.

## Verification

- Verified locally that the pinned archive extracts all 136 files byte for byte. A second install made zero changes. A changed archive and a conflicting local asset were rejected. Static scene links and the build scene GUID were verified.
- Editor import, compilation, starter wiring, Play Mode and player builds await a Unity 6000.6.8f1 environment.

## Current narrative baseline

- Fun comes first. The user authorized a detailed outline for the working story “归来” (Return): [main outline](../../docs/narrative/STORY_OUTLINE.md), [characters](../../docs/narrative/CHARACTERS.md), [regional stories](../../docs/narrative/REGIONAL_STORIES.md).
- The protagonist returns from captivity and rescues Shen Li (沈砾) around the midpoint. The latter half follows shared adventures and mutual growth; local stories have their own people, stakes and later encounters.
- v0.1 is a writing/design baseline, not implemented gameplay. Next narrative work is the prologue scene script, a complete local story and the continuous post-reunion character scenes. Technical story 001 remains Pending.

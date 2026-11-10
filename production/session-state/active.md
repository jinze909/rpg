# Active development session

Current story and next acceptance check are maintained in [AI2-STATUS.md](../../AI2-STATUS.md).

## Baseline

- Unity 6000.6.8f1, URP 17.6.0 with one Renderer2D as the active rendering pipeline; 2D orthographic startup scene at `Assets/Game/Scenes/Bootstrap.unity`.
- M1 URP 2D final asset SHA `3a3465225636212effcb1d452deb6d7802bb0d16` passed clean Unity verification and was merged to `codex/dev` as `8c36229e011248d019f0ef4d8a9a162dd6354afe`.
- M2 New Input System migration is in isolated candidate `codex/pending/m2-new-input-system`; keep Active Input Handling Both during ORK compatibility validation. ORK Input Key vendor assets remain untouched.
- ORK 3.22.0 and Makinom 2.21.0 are pinned by package SHA-256. The installer preserves package assets and their Unity metadata.
- `Tools > RPG > Wire ORK Bootstrap` assigns a saved Makinom Project asset to the ORK starter in the bootstrap scene. The first starter only initializes the framework. Starting a new game is deferred until the project has a player and a start schematic.

## Verification

- Verified locally that the pinned archive extracts all 136 files byte for byte. A second install made zero changes. A changed archive and a conflicting local asset were rejected. Static scene links and the build scene GUID were verified.
- Current native bootstrap and Soundwell acceptance are recorded by exact candidate SHA and CI links in AI2-STATUS.md and stories 001/002. This narrative revision does not rerun or extend those results.

## Current narrative baseline

- Fun comes first. The user authorized a detailed outline for the working story “归来” (Return): [main outline](../../docs/narrative/STORY_OUTLINE.md), [characters](../../docs/narrative/CHARACTERS.md), [regional stories](../../docs/narrative/REGIONAL_STORIES.md).
- The protagonist returns from captivity and rescues Shen Li (沈砾) around the midpoint. The latter half follows shared adventures and mutual growth; local stories have their own people, stakes and later encounters.
- Narrative v0.2 integrates the user-provided Claude drafts: the Whitewater crossing mystery, roll calls, five recurring people and bounded fog echoes. [Facts](../../docs/narrative/NARRATIVE_FACTS.md) and [S00–M00 script](../../docs/narrative/SCENE_SCRIPT_S00_M00.md) resolve chronology and branching; S00 is narrative, distinct from technical P0. These remain writing drafts, not implemented gameplay or playtest evidence. Next narrative work is to refine the opening, one local story and M08–M11 scenes. Existing technical acceptance is unchanged by this narrative-only update.

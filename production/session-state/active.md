# Active development session

Current policy (2026-11-11): framework and configuration changes are allowed after sufficient isolated final-SHA validation. Agents own development, verification and issue recording; user playtests, media reviews or GitHub feedback are not prerequisites. Subjective enjoyment remains unknown without evidence. Prefer integrating ready verified work before unrelated governance; reuse equivalent exact-SHA evidence. Historical verification below is not live status. See [known issues](../KNOWN_ISSUES.md).

Current story and next acceptance check are maintained in [AI2-STATUS.md](../../AI2-STATUS.md).

## Baseline

- Unity 6000.6.8f1, URP 17.6.0 with one Renderer2D as the active rendering pipeline; 2D orthographic startup scene at `Assets/Game/Scenes/Bootstrap.unity`.
- M1 URP 2D final asset SHA `3a3465225636212effcb1d452deb6d7802bb0d16` passed clean Unity verification and was merged to `codex/dev` as `8c36229e011248d019f0ef4d8a9a162dd6354afe`.
- M2 game-owned Soundwell controls now use the New Input System adapter, merged into `codex/dev` at `2eee996adaec6563fd379416e7ec6fad8e1b9a62`. Keep Active Input Handling Both for ORK compatibility; ORK Input Key vendor assets remain untouched. That statement describes the historical M2 candidate only; later UGUI focus and input regressions were verified in subsequent dev runs. Check the current SHA before treating any remaining device coverage as unresolved.
- ORK 3.22.0 and Makinom 2.21.0 are pinned by package SHA-256. The installer preserves package assets and their Unity metadata.
- `Tools > RPG > Wire ORK Bootstrap` assigns a saved Makinom Project asset to the ORK starter in the bootstrap scene. The first starter only initializes the framework. Starting a new game is deferred until the project has a player and a start schematic.

## Verification

- Verified locally that the pinned archive extracts all 136 files byte for byte. A second install made zero changes. A changed archive and a conflicting local asset were rejected. Static scene links and the build scene GUID were verified.
- Current native bootstrap and Soundwell acceptance are recorded by exact candidate SHA and CI links in AI2-STATUS.md and stories 001/002. The historical candidate `2eee996adaec6563fd379416e7ec6fad8e1b9a62` passed [Run 42766341628](https://github.com/jinze909/rpg/actions/runs/42766341628): 8/8 Editor tests, two Play Mode cycles, independent Batch and `passed-clean-candidate`; [artifact](https://github.com/jinze909/rpg/actions/runs/42766341628/artifacts/13740231633). At the time of that historical M2 run, Story 003 was still in progress. The later [Story 003 mobile rescue acceptance](../stories/003-soundwell-turn-rescue.md) has since verified the real ORK attack, guard, gate interaction and movement path; the full fixed-formation playable version, growth/persistence and subjective play experience remain separate work.

## Current narrative baseline

- Fun comes first. The user authorized a detailed outline for the working story “归来” (Return): [main outline](../../docs/narrative/STORY_OUTLINE.md), [characters](../../docs/narrative/CHARACTERS.md), [regional stories](../../docs/narrative/REGIONAL_STORIES.md).
- The protagonist returns from captivity and rescues Shen Li (沈砾) around the midpoint. The latter half follows shared adventures and mutual growth; local stories have their own people, stakes and later encounters.
- Current [《归来》 v1.2 writing baseline](../../docs/narrative/STORY_OUTLINE.md) is the user's authoritative 39-file package. Its [handoff](../../docs/narrative/HANDOFF_FOR_CODEX.md) provides the reading order; [facts](../../docs/narrative/NARRATIVE_FACTS.md), [P0](../../docs/narrative/SCENE_SCRIPT_P0.md), [M00](../../docs/narrative/SCENE_SCRIPT_M00.md), and the remaining 27 chapter scripts are writing references only. Narrative P0 differs from technical P0 and existing Unity S00; this v1.2 script set has not been implemented or playtested. Superseded v1.1 story/variable proposals are not competing canon. Existing technical acceptance is unchanged.

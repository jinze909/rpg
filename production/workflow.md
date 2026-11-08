# Studio workflow

This workflow is adapted from [Claude Code Game Studios](https://github.com/Donchitos/Claude-Code-Game-Studios/tree/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418), pinned to commit `05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418`. Its producer, Unity specialist, QA lead, `dev-story`, and `story-done` definitions informed the project versions under `.claude/`. See the [MIT license](../docs/CLAUDE_CODE_GAME_STUDIOS_LICENSE.txt).

Both Claude Code and Codex use this file. Claude Code exposes `/dev-story [story-path]` and `/story-done [story-path]`. Codex follows the same steps through `AGENTS.md`. Each role can be performed in the current session. When the execution environment supports and authorizes delegation, the Claude Code role files describe the inputs and outputs for separate agents.

## Prepare

1. Verify the target repository and branch. Start from the latest `codex/dev` without overwriting local or remote work.
2. Read [the checkpoint](session-state/active.md). Choose the current story or the user's specified work. Address a blocker on the next playable sequence first.
3. The producer records one observable outcome, prerequisites, and required acceptance checks in the story. QA identifies how to verify the outcome before editing.

## Implement

1. Set the story to In progress once actual work starts.
2. The Unity specialist implements the smallest complete increment. Follow the ownership rules in `AGENTS.md`. Prefer game-authored ORK data and Makinom Schematics, and preserve original Unity asset GUIDs.
3. If the Editor, license, or dependency is unavailable, record the observed blocker. Complete independent checks and keep the implementation scope tied to the story.

## Verify

Run `git diff --check` and `git diff --cached --check`, then select evidence based on the changed surface.

| Change | Required evidence |
| --- | --- |
| Unity code, scenes, assemblies, or project data | Import and compile with the pinned Editor. Exercise the affected scene path when runtime behavior changes. |
| Combat, movement, interactions, or saves | A targeted Unity test or reproducible Play Mode check that exercises the actual behavior. |
| HUD, menus, or visual presentation | Play Mode observation and a screenshot or recording of the affected state. |
| Workflow or documentation | Check frontmatter, local references, and the relevant invocation where its client is available. |

QA records each criterion as Passed, Failed, Blocked, or Not run with the actual command or manual steps and observed result. Use In review once implementation is ready and evidence is under review. Mark a story Done only after every required criterion has passing evidence. Record additional regressions and blocked checks separately.

## Handoff

Update the checkpoint with the current story, observed validation, blockers, and one next acceptance check. When publishing is authorized, verify the latest remote HEAD, commit the scoped changes and checkpoint, and push by fast-forward. Link the commit and any retained evidence in the run report.

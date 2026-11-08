# RPG development

- Work in `jinze909/rpg` only. Fetch the latest `codex/dev` and read `production/session-state/active.md` before editing.
- Follow [the studio workflow](production/workflow.md) for every story. Use its producer, implementation, and QA checks and carry the outcome into the session checkpoint.
- ORK 3 owns combatants, combat, inventory, equipment, quests and saves. Makinom owns interactions and story schematics. Unity owns 2D rendering, physics, input and animation. Keep each gameplay state in one place.
- Keep original asset GUIDs and commit each game-owned asset with its `.meta`. Keep licensed dependencies in an authorized checkout or runner unless the applicable license and repository access allow storing them in Git.
- Check `git diff --check`, Unity import and compile, and the affected player path. Record what ran, what passed, and the next acceptance check in the session checkpoint.
- Use fast-forward pushes. Keep the repository private. The user manages development scheduling.

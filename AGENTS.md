# RPG development

- Work in `jinze909/rpg` only. Fetch the latest `codex/dev` and read `production/session-state/active.md` before editing.
- For each increment, define a player-visible or reproducible acceptance check, implement it, and verify it. Apply the producer, implementation, and QA responsibilities from the [pinned studio workflow](https://github.com/Donchitos/Claude-Code-Game-Studios/blob/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418/docs/WORKFLOW-GUIDE.md).
- ORK 3 owns combatants, combat, inventory, equipment, quests and saves. Makinom owns interactions and story schematics. Unity owns 2D rendering, physics, input and animation. Keep each gameplay state in one place.
- Keep original asset GUIDs and commit each game-owned asset with its `.meta`. Keep licensed dependencies in an authorized checkout or runner unless the applicable license and repository access allow storing them in Git.
- Check `git diff --check`, Unity import and compile, and the affected player path. Record what ran, what passed, and the next acceptance check in the session checkpoint.
- Use fast-forward pushes. Keep the repository private. The user manages development scheduling.

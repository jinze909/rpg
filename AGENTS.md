# RPG development

- Work in `jinze909/rpg`. Resolve the live `codex/dev` HEAD and read [AI2-STATUS](AI2-STATUS.md), [BACKLOG](BACKLOG.md), and [the team roster](production/team.md) at that revision before editing.
- Follow [the studio workflow](production/workflow.md). Producer selects and hands off the work, relevant specialists implement it, lead-programmer reviews the final diff, and qa-lead verifies the acceptance evidence. These responsibilities apply in a single conversation as well as in a multi-agent session.
- ORK 3 owns combatants, combat, inventory, equipment, quests and saves. Makinom owns interactions and story schematics. Unity owns 2D rendering, physics, input and animation. Keep each gameplay state in one place. G1 uses UGUI and SpriteRenderer / Tilemap.
- Keep original asset GUIDs and commit each game-owned asset with its `.meta`. Keep licensed dependencies in an authorized checkout or runner unless the applicable license and repository access allow storing them in Git.
- Use fast-forward updates. Keep the repository private. The user manages development scheduling. The user runs ongoing development from standard ChatGPT chat with the GitHub plugin. Follow [the ChatGPT entry point](CHATGPT.md).

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

`BACKLOG.md` sets the goal of a polished, enjoyable 2D RPG, with fun as the highest
product priority. The user authorized the current [Return narrative baseline](../narrative/STORY_OUTLINE.md),
with [characters](../narrative/CHARACTERS.md) and [regional stories](../narrative/REGIONAL_STORIES.md).
It follows a returning former captive, a midpoint rescue of Shen Li (沈砾), and
substantial shared adventures afterward. It is a writing/design draft, not
implemented content or evidence of player enjoyment. The [gameplay baseline](../gameplay/BASELINE.md)
sets regional exploration and short party-based turn combat as the direction;
movement versus fixed positions and other prototype controls remain unvalidated.
Protagonist customization, final camera and art specifications remain open. Previously withdrawn
settings must not be reinstated from older commits. ORK owns combatants,
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
sequentially in one session. The original technical inspection used no subagents;
later narrative drafting used separate character/regional authors and a continuity
reviewer. Keep unverified acceptance items Pending and do not treat historical
validation or narrative document checks as current gameplay evidence.


## 2026-11-09 技术基线补充（以上历史快照不再代表实时仓库）

原审查段落严格属于 2026-11-07 的旧 SHA，诸如“没有已保存 ORK 项目数据/测试/CI”“私有仓库”都是当时的记录，**不能当作现状**。当前抽查 `codex/dev` SHA `790d9486dd8e163c278adefb9c25ae1be5821aa9` 已有两份 Unity 场景、Soundwell 游戏代码、CI 测试/证据以及已保存 ORK 项目；目前用户决定仓库保持 Public，许可约束继续有效。

- `Packages/manifest.json` 尚无 URP、Input System、Cinemachine、2D Animation、Tilemap Extras、Pixel Perfect、Localization、Addressables、Memory Profiler；并不证明这些包在所有本地未提交缓存中不存在。
- `ProjectSettings/GraphicsSettings.asset` 的 `m_CustomRenderPipeline` 是 `{fileID: 0}`，Quality 的渲染管线也均为 `{fileID: 0}`：目前使用 Built-in。自有 Soundwell 仍有旧 `Input.GetAxisRaw`/`GetKeyDown` 和手写相机逻辑，未见动画/图集/混音器的独立已提交资产。
- **用户最新硬限制：ORK 3.22.0 / Makinom 2.21.0 保持原版，不更新、不碰厂商文件和设置、不新增付费依赖；UGUI 保留，不迁移 UI Toolkit。** 现代化规范见 [现代工作室技术基线](../technical/MODERN_2D_RPG_STACK.md)；这只是规划，任何实际迁移须分支独立验收。
- 当前 003 的独立候选 `codex/pending/p2-soundwell-party-runtime` 有失败的 [同 SHA CI](https://github.com/jinze909/rpg/actions/runs/42732604423)：Soundwell 现有重载测试报 `Reload must have exactly one player`。此为未解决的玩法/运行回归，不能通过迁移 URP/Input 或写文档掩盖。主线已有 [通过的验收](https://github.com/jinze909/rpg/actions/runs/42728344621)。

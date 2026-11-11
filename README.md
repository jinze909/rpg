# RPG

Unity 2D RPG 工程。当前工作剧情为“归来”：被宣布战死的主人公归国，救回旧友沈砾，再与她共同打开幸存者的归途。基调为有战争伤痕、有烟火气、幽默与希望的奇幻冒险。玩法采用区域探索与队伍短回合骨架，移动/固定阵位及具体参数待原型验证，主角外观等仍开放。ORK Framework 3.22.0 和 Makinom 2.21.0 管理 RPG 逻辑，项目采用 Claude Code Game Studios 的制作、实现和验收流程。

## 剧情与内容设计

**好玩第一，一切建立在好玩的基础上。** 当前剧情 v0.2 是写作与任务分解基线，本版剧情演出尚未实现、真实玩家试玩未进行，不以文本规模证明质量。

- [详细主线大纲](docs/narrative/STORY_OUTLINE.md)：世界、序章至尾声、关键行动、路线与结局。
- [人物与关系](docs/narrative/CHARACTERS.md)：核心人物、沈砾重逢后的双向成长、合作与分离。
- [地区与长期故事](docs/narrative/REGIONAL_STORIES.md)：六条有自身人物冲突和回访变化的地方线。
- [卖点与开场](docs/narrative/PITCH_AND_HOOK.md)：点名、“在”与白水渡序幕的叙事钩子。
- [S00—M00 场景稿](docs/narrative/SCENE_SCRIPT_S00_M00.md)：已补写的开场对白与可参与动作草稿。
- [叙事事实登记](docs/narrative/NARRATIVE_FACTS.md) · [写作准则](docs/narrative/NARRATIVE_GUIDE.md) · [v0.2 整合记录](docs/narrative/REVISION_NOTES.md)。
- [一页玩法基线](docs/gameplay/BASELINE.md)：已定体验、暂定原型条件、移动/固定阵位对照与乐趣裁决边界。

剧情文档包含完整剧透。“归来”是工作标题；叙事场景、游戏内容与技术验收仍需逐步落实。

## 可选本地启动

以下供希望自行打开工程的人参考，不是用户必须执行的步骤；日常由智能体使用现有 Actions 完成生成与验证。

1. 用 Unity Hub 安装 **Unity 6000.6.8f1**。克隆本仓库，先切换到 `codex/dev`。
2. 此仓库已完整纳入 ORK 3.22.0 / Makinom 2.21.0 的插件、Gizmos、说明、源码压缩包及原始 `.meta`，克隆后无需再次导入原包。在仓库根目录用 Python 3 校验资源。Windows 上可将 `python` 换为 `py -3`。

   ```sh
   python tools/verify_ork.py
   ```

   校验清单见 `docs/ork-package-manifest.json`。如需用原包恢复缺失文件，仍可运行 `python tools/install_ork.py "/path/to/package.unitypackage"`；安装器校验固定 SHA-256，复用相同文件并拒绝覆盖不同内容。源码 ZIP 保持原样，不要将其中源码与 DLL 同时放入 Unity 编译路径。整合证据见 [ORK 包整合记录](docs/ork-integration.md)。
3. 在 Unity 打开工程后，到 `Edit > Project Settings > Editor > When entering play mode` 选择 `Reload Domain and Scene`，并把 Unity 保存的 `ProjectSettings/EditorSettings.asset` 纳入首次验收提交。
4. 打开 `Assets/Game/Scenes/Bootstrap.unity`。从 `Window > Gaming Is Love > ORK Framework` 打开框架编辑器。仅在尚无项目配置时首次保存设置。项目数据位于 `Assets/Gaming Is Love/_Data/Project.asset`。
5. 运行 `Tools > RPG > Wire ORK Bootstrap`，然后连续进入并退出 Play Mode 两次，每次检查 Console 和 ORK 初始化。开发者可提交自己的场景及框架配置数据。第一阶段验证启动与框架初始化，角色生成和交互见接力记录。

Unity 6.6 本工程验证过的 UGUI 2.6.0 已包含 TextMesh Pro。首次成功解析依赖后，提交 Unity 生成的 `Packages/packages-lock.json`，供其他工作环境复现。ORK 3.22.0 和 Makinom 2.21.0 使用现有的固定包，组合兼容性由下方验收记录确认。

### 批处理启动验收

接线并提交场景及游戏自有的 ORK 项目数据后，在干净的目标提交上使用已授权的 Unity 6000.6.8f1，关闭同工程的其他 Unity 实例。在仓库根目录运行以下命令，将 Unity 路径换成实际可执行文件路径。

```sh
git rev-parse HEAD
"/path/to/Unity" -batchmode -nographics -quit -projectPath "." -executeMethod Rpg.Editor.OrkBootstrapSetup.ValidateBatch -logFile "bootstrap-validation.log"
```

记录提交 SHA、退出码及日志。退出码为 0 且日志含 `RPG_BOOTSTRAP_VALIDATED` 才算批处理配置检查通过。Play Mode 启动是单独的验收项。

## 现代技术方向与成本约束

项目目标是在已验证的 Unity 6.6 / UGUI / ORK 3 / Makinom 2 基础上，按现代 2D RPG 工作室的可维护流程逐步升级，而不是一次换光框架。完整、可执行的技术矩阵、路线图、官方文档、分支隔离及验收门槛见 **[现代 2D RPG 工作室技术基线](docs/technical/MODERN_2D_RPG_STACK.md)**。

**用户最新决定（2026-11-11）：充分验证后允许修改 ORK/Makinom 框架及配置，旧全面冻结撤销。** 修改在隔离候选中进行，按[验证规则](production/workflow.md#框架修改与验证)记录影响、回退和同 SHA 证据。新增购买成本 $0；不迁移 UI Toolkit，继续 UGUI。 有价值的候选技术包括 URP 2D、Input System Both 过渡、Cinemachine 3、2D 动画/Tilemap、Audio Mixer、Sprite Atlas V2、可选 Pixel Perfect、Localization、Memory Profiler 和 Build Profiles；Addressables 等按需评估。各阶段已有的工程贡献和剩余验收见[事实核查](docs/technical/MODERNIZATION_VERIFIED_STATUS.md)，不重复安装，也不把安装等同完整验收。

## 工作室开发流程

GitHub Actions 的首次配置准备、两次 Play Mode 与干净提交验收见 [Unity / ORK P0 CI](docs/unity-p0-ci.md)。Unity 6000.6.8f1 已在固定依赖与干净提交上通过 P0 引导验收：[P0 验证](https://github.com/jinze909/rpg/actions/runs/42551491951)。声井 002 的自动化回归在 `82d0f99dc167b8425d04306fd685dedf3e2a8daa` 通过 4 项测试与独立批处理：[codex/dev 同 SHA 验证](https://github.com/jinze909/rpg/actions/runs/42586562395)。上述为历史证据。现行开发和验证由智能体承担，不要求用户试玩、评审或提交反馈；无法验证的真实输入/听感/理解写入[问题记录](production/KNOWN_ISSUES.md)，CI 绿色不代表乐趣得到验证。

后续开发默认在普通 ChatGPT 聊天中使用 GitHub 插件，从 [对话入口](CHATGPT.md) 读取实时任务并提交。Codex 通过 `AGENTS.md`，Claude Code 通过 `/dev-story` 和 `/story-done` 使用同一套规则。

[18 个角色](production/team.md) · [当前状态](AI2-STATUS.md) · [冻结范围与任务](BACKLOG.md) · [工作室流程](production/workflow.md) · [上游许可](docs/CLAUDE_CODE_GAME_STUDIOS_LICENSE.txt)

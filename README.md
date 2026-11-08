# RPG

Unity 2D RPG 工程。当前工作剧情为“归来”：被宣布战死的主人公归国，救回旧友沈砾，再与她共同打开幸存者的归途。基调为有战争伤痕、有烟火气、幽默与希望的奇幻冒险；具体战斗形式与主角外观等仍待设计。ORK Framework 3.22.0 和 Makinom 2.21.0 管理 RPG 逻辑，项目采用 Claude Code Game Studios 的制作、实现和验收流程。

## 剧情与内容设计

**好玩第一，一切建立在好玩的基础上。** 当前大纲 v0.1 是写作与任务分解基线，游戏内容尚未实现、真实玩家试玩未进行，不以文本规模证明质量。

- [详细主线大纲](docs/narrative/STORY_OUTLINE.md)：世界、序章至尾声、关键行动、路线与结局。
- [人物与关系](docs/narrative/CHARACTERS.md)：核心人物、沈砾重逢后的双向成长、合作与分离。
- [地区与长期故事](docs/narrative/REGIONAL_STORIES.md)：六条有自身人物冲突和回访变化的地方线。

剧情文档包含完整剧透。“归来”是工作标题；叙事场景、游戏内容与技术验收仍需逐步落实。

## 本地启动

1. 用 Unity Hub 安装 **Unity 6000.6.8f1**。克隆本仓库，先切换到 `codex/dev`。
2. 此私有仓库已完整纳入 ORK 3.22.0 / Makinom 2.21.0 的插件、Gizmos、说明、源码压缩包及原始 `.meta`，克隆后无需再次导入原包。在仓库根目录用 Python 3 校验资源。Windows 上可将 `python` 换为 `py -3`。

   ```sh
   python tools/verify_ork.py
   ```

   校验清单见 `docs/ork-package-manifest.json`。如需用原包恢复缺失文件，仍可运行 `python tools/install_ork.py "/path/to/package.unitypackage"`；安装器校验固定 SHA-256，复用相同文件并拒绝覆盖不同内容。源码 ZIP 保持原样，不要将其中源码与 DLL 同时放入 Unity 编译路径。整合证据见 [ORK 包整合记录](docs/ork-integration.md)。
3. 在 Unity 打开工程后，到 `Edit > Project Settings > Editor > When entering play mode` 选择 `Reload Domain and Scene`，并把 Unity 保存的 `ProjectSettings/EditorSettings.asset` 纳入首次验收提交。
4. 打开 `Assets/Game/Scenes/Bootstrap.unity`。从 `Window > Gaming Is Love > ORK Framework` 打开框架编辑器。仅在尚无项目配置时首次保存设置。项目数据位于 `Assets/Gaming Is Love/_Data/Project.asset`。
5. 运行 `Tools > RPG > Wire ORK Bootstrap`，然后连续进入并退出 Play Mode 两次，每次检查 Console 和 ORK 初始化。开发者可提交自己的场景及框架配置数据。第一阶段验证启动与框架初始化，角色生成和交互见接力记录。

Unity 6 的 UGUI 2.0 已包含 TextMesh Pro。首次成功解析依赖后，提交 Unity 生成的 `Packages/packages-lock.json`，供其他工作环境复现。ORK 3.22.0 和 Makinom 2.21.0 使用现有的固定包，组合兼容性由下方验收记录确认。

### 批处理启动验收

接线并提交场景及游戏自有的 ORK 项目数据后，在干净的目标提交上使用已授权的 Unity 6000.6.8f1，关闭同工程的其他 Unity 实例。在仓库根目录运行以下命令，将 Unity 路径换成实际可执行文件路径。

```sh
git rev-parse HEAD
"/path/to/Unity" -batchmode -nographics -quit -projectPath "." -executeMethod Rpg.Editor.OrkBootstrapSetup.ValidateBatch -logFile "bootstrap-validation.log"
```

记录提交 SHA、退出码及日志。退出码为 0 且日志含 `RPG_BOOTSTRAP_VALIDATED` 才算批处理配置检查通过。Play Mode 启动是单独的验收项。

## 工作室开发流程

后续开发默认在普通 ChatGPT 聊天中使用 GitHub 插件，从 [对话入口](CHATGPT.md) 读取实时任务并提交。Codex 通过 `AGENTS.md`，Claude Code 通过 `/dev-story` 和 `/story-done` 使用同一套规则。

[18 个角色](production/team.md) · [当前状态](AI2-STATUS.md) · [冻结范围与任务](BACKLOG.md) · [工作室流程](production/workflow.md) · [上游许可](docs/CLAUDE_CODE_GAME_STUDIOS_LICENSE.txt)

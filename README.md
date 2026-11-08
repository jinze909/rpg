# RPG

Unity 2D 俯视角单机动作 RPG。ORK Framework 3.22.0 和 Makinom 2.21.0 管理 RPG 逻辑，项目采用 Claude Code Game Studios 的制作、实现和验收流程。

## 本地启动

1. 用 Unity Hub 安装 **Unity 2022.3.53f1**。克隆本仓库，从仓库根目录打开项目。
2. 在 Unity 尚未打开项目时，在仓库根目录用 Python 3 安装有授权的 ORK 包。Windows 上可将 `python` 换为 `py -3`。

   ```sh
   python tools/install_ork.py "/path/to/RPG Editor ORK Framework 3 2025 v3.22.0(8).unitypackage"
   ```

   安装会校验包的 SHA-256，完整写入包内的插件、Gizmos、说明、源码压缩包及原始 `.meta`。重复执行会复用相同文件，遇到不同内容则停止。
3. 在 Unity 打开 `Assets/Game/Scenes/Bootstrap.unity`。从 `Window > Gaming Is Love > ORK Framework` 打开框架编辑器，保存设置。项目数据位于 `Assets/Gaming Is Love/_Data/Project.asset`。
4. 运行 `Tools > RPG > Wire ORK Bootstrap`，然后进入 Play Mode 检查 Console。开发者可提交自己的场景及框架配置数据。第一阶段验证启动与框架初始化，角色生成和交互见接力记录。

开发分支为 `codex/dev`。当前状态见 [接力记录](production/session-state/active.md)。

## 工作室开发流程

后续开发默认在普通 ChatGPT 聊天中使用 GitHub 插件，从 [对话入口](CHATGPT.md) 读取实时任务并提交。Codex 通过 `AGENTS.md`，Claude Code 通过 `/dev-story` 和 `/story-done` 使用同一套规则。

[18 个角色](production/team.md) · [当前状态](AI2-STATUS.md) · [冻结范围与任务](BACKLOG.md) · [工作室流程](production/workflow.md) · [上游许可](docs/CLAUDE_CODE_GAME_STUDIOS_LICENSE.txt)

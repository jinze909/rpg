# 从 ChatGPT 对话接力

后续开发默认在普通 ChatGPT 聊天中进行。选择 GitHub 插件并授权访问 `jinze909/rpg`，在同一个对话读取、修改、验证并提交。直接使用当前对话提供的 GitHub 读写工具。

> 用 GitHub 读取 jinze909/rpg 的 codex/dev 实时 HEAD。按 CHATGPT.md 和 AGENTS.md 进入工作室流程，读取 AI2-STATUS.md、BACKLOG.md 和角色表，完成一个范围内的任务。修改、验证并提交到 codex/dev，更新接力状态。回复提交 SHA、同 SHA 验证证据、阻塞和下一步。

## 对话执行入口

1. 确认仓库和 `codex/dev` 的实时 HEAD，记下本轮基线 SHA。按同一个 SHA 读取 [AGENTS.md](AGENTS.md)、[状态](AI2-STATUS.md)、[任务清单](BACKLOG.md)、[工作流](production/workflow.md) 和 [角色表](production/team.md)。读取任务涉及的代码与配置时同样锁定基线 SHA。
2. 用 producer 决定本轮交付。按角色表戴上相关角色镜头。单个对话可依次执行各角色的职责。只在环境确实支持且用户授权时委派，汇报实际参与者。
3. 使用当前环境真实提供的能力修改文件。有 GitHub 写权限的对话可以基于当前提交写入一个包含多文件修改的提交，无需本地克隆。先审查完整差异，发布前重新确认远端 HEAD，使用快进更新。远端有并发修改时，以新 HEAD 重新读受影响文件并复核改动。如果本轮连接缺少某项必要权限或工具，交付基于准确 SHA 的可审查补丁，注明待执行步骤和具体错误。
4. 重新读取远端分支确认提交 SHA。查找精确 SHA 的验证记录，区分通过、失败、运行中、受阻和未运行。缺少 Unity 或完整 CI 结果时，在状态中留下待验证项。按 [同 SHA 规则](production/workflow.md#同-sha-验收) 记录证据。

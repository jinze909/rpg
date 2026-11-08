# 工作室开发流程

本流程适配自 [Claude Code Game Studios](https://github.com/Donchitos/Claude-Code-Game-Studios/tree/05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418) 的角色分层、任务开发、证据验收与接力做法。保留 [MIT 许可](../docs/CLAUDE_CODE_GAME_STUDIOS_LICENSE.txt)。以本仓库的 [18 个角色](team.md) 和 [冻结范围](../BACKLOG.md#冻结范围) 为准。

ChatGPT 从 [对话入口](../CHATGPT.md) 开始，Codex 从 `AGENTS.md` 开始，Claude Code 可用 `/dev-story [story-path]` 和 `/story-done [story-path]`。三个入口共用同一套角色、任务与证据。

## Prepare · 选任务

1. Producer 确认实时 `codex/dev` HEAD，读取 [AI2-STATUS](../AI2-STATUS.md)、[BACKLOG](../BACKLOG.md) 和相关 CI。
2. 选一个可验收的增量。记录目标、依赖、改动边界和验收办法。涉及体验或范围时用 creative-director，涉及架构、构建或 CI 时用 technical-director。QA lead 在动手前确定要收集的证据。
3. 具备条件时开始实际实现，把任务标为进行中。缺少运行环境时，推进可独立验证的工作并保留阻塞。

## Implement · 实现

1. 按 [角色表](team.md) 选择专家和负责人。优先使用 ORK 数据、Makinom Schematics 和 Unity 2D 原生表现层，并保留序列化引用与 `.meta`。
2. 涉及画面时由 art-director 判断是否标注 `ART_REVIEW_REQUIRED`。性能指标由 performance-analyst 解释趋势，保留采集条件和比较基线。
3. Lead-programmer 在每次提交前审查实际差异。能使用 Git 时运行 `git diff --check` 与 `git diff --cached --check`。通过 GitHub 对话写入时，审查基线和完整变更文件，重新确认远端 HEAD 后快进发布。

## Verify · 证据

| 变更类型 | 所需证据 |
| --- | --- |
| Unity 代码、场景、程序集或项目数据 | 锁定版本的 Unity 导入与编译。运行行为变化时验证受影响的玩家路径。 |
| 战斗、移动、交互、存档 | 相关 EditMode 或 PlayMode 测试，或可复现的运行验证。 |
| HUD、菜单、美术与动画 | Play Mode 观察，附相关状态截图或录屏。涉及输入时验证相应键鼠与手柄路径。 |
| P0-b 冒烟、P2 原型验收 | 记录脚本入口、实际结果、日志或 artifact。性能数据附环境和采集条件。 |
| 流程或文档 | 检查配置格式、引用与实际读写路径，在客户端可用时验证入口。 |

### 同 SHA 验收

- 每条证据记录所测的完整提交 SHA、检查命令或人工复现步骤、结果和可追溯链接。CI 按运行的提交 SHA 关联，只有分支名、PR 状态或旧提交的通过结果不能证明当前候选已通过。
- QA lead 分别标记通过、失败、运行中、受阻或未运行。全部必需证据针对同一个候选 SHA 且通过后，才将该候选的故事标为完成。未验收的实现可保留待验证状态。
- 验证可发生在发布候选之后。允许另提交纯状态记录，明确记录已验证的候选 SHA。状态提交自身不继承该 SHA 的绿色状态。候选实现改动后重新验证。`green/*` 标签只指向对应的已验证 SHA。

## Handoff · 接力

Producer 更新 `AI2-STATUS.md` 和 `BACKLOG.md` 中发生变化的状态，注明测试 SHA、证据、阻塞和一个下一步。用户授权发布时，确认远端分支指向本轮提交，并在回复中附提交与证据。保持 [长期基线](session-state/active.md) 简明。

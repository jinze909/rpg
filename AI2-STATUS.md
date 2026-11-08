# AI2 当前状态

- 工作分支为 `codex/dev`。每轮通过 GitHub 查询实时 HEAD。
- 当前故事为 [001 · ORK 启动验证](production/stories/001-ork-bootstrap.md)，属 P0，待验证。
- Unity 2022.3.53f1 的 2D 启动场景、ORK/Makinom 安装脚本和接线工具已入库。可用的本地证据包括包内 136 个文件的逐字节比对和重复安装检查。
- 启动接线会拒绝空配置、禁用的 Starter 与错误的数据来源。新增 [Unity 批处理检查](README.md#批处理启动验收)，可记录配置验收日志。
- Unity 导入、编译、重复接线、批处理及 Play Mode 尚无运行证据。下一步在具备授权包和编辑器的环境执行 001 的验收。
- [任务顺序及冻结范围](BACKLOG.md) · [工作室角色](production/team.md) · [验收与证据规则](production/workflow.md)

## 每轮更新

Producer 在此更新当前故事、最近交付、证据 SHA 及链接、未完成的检查和一个明确的下一步。项目长期背景见 [基线记录](production/session-state/active.md)。

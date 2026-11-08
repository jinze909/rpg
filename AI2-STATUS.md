# AI2 当前状态

- 工作分支为 `codex/dev`。每轮通过 GitHub 查询实时 HEAD。
- 当前故事为 [001 · ORK 启动验证](production/stories/001-ork-bootstrap.md)，属 P0，待验证。
- 工程锁定 Unity 6.6（6000.6.8f1）与随编辑器发布的 UGUI 2.0。当前框架仍用已提供的 ORK 3.22.0 / Makinom 2.21.0 固定包。此前已对安装脚本的 136 个文件做逐字节比对并验证重跑。
- 启动接线会拒绝空配置、禁用的 Starter 与错误的数据来源。新增 [Unity 批处理检查](README.md#批处理启动验收)，可记录配置验收日志。
- Unity 导入、编译、重复接线、批处理及 Play Mode 尚无运行证据。下一步在 Unity 6.6 环境执行 001，提交编辑器生成的包锁与重载设置，记录两次 Play Mode 的结果。
- [任务顺序及冻结范围](BACKLOG.md) · [工作室角色](production/team.md) · [验收与证据规则](production/workflow.md)

## 每轮更新

Producer 在此更新当前故事、最近交付、证据 SHA 及链接、未完成的检查和一个明确的下一步。项目长期背景见 [基线记录](production/session-state/active.md)。

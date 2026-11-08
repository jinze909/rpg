# AI2 当前状态

- 工作分支为 `codex/dev`。每轮通过 GitHub 查询实时 HEAD。
- 用户明确的最高产品原则是“好玩第一，一切都建立在好玩的基础上”，已写入 [开发规则](AGENTS.md) 和 [任务清单](BACKLOG.md)。
- 用户已同意“归来”主线并要求详细大纲入库。现有[主线大纲](docs/narrative/STORY_OUTLINE.md)、[人物关系](docs/narrative/CHARACTERS.md)、[地区与长期故事](docs/narrative/REGIONAL_STORIES.md) v0.1：救友在中段完成，后半共同冒险与成长，地方故事独立成立。它们是写作基线，未实现、未试玩；工作标题与具体扩写细节仍可改稿。
- 用户授权深入讨论并写一页玩法基线，本轮新增[基线v0.1](docs/gameplay/BASELINE.md)：区域探索与队伍短回合为方向，三人、固定个人顺序及镜头等仅作首轮控制；移动/固定阵位须公平对照，仍未实现、未试玩。
- 先前撤销的游戏设定继续作废。主角外观、美术尺度等未锁定，玩法的已定/暂定/待验证以[玩法基线](docs/gameplay/BASELINE.md)和 [BACKLOG](BACKLOG.md) 为准。
- 当前故事为 [001 · ORK 启动验证](production/stories/001-ork-bootstrap.md)，属 P0，待验证。
- 工程锁定 Unity 6.6（6000.6.8f1）与随编辑器发布的 UGUI 2.0。当前框架仍用已提供的 ORK 3.22.0 / Makinom 2.21.0 固定包。此前已对安装脚本的 136 个文件做逐字节比对并验证重跑。
- 框架包已完整整合到开发分支：73 个资源条目、136 个文件与原包逐字节一致，原始 GUID 保留；当时重复安装新增 0 个文件，`python tools/verify_ork.py` 与两个源码 ZIP 的 CRC 检查通过。详见 [整合证据](docs/ork-integration.md)。P0 仍待 Unity 验收。
- 启动接线会拒绝空配置、禁用的 Starter 与错误的数据来源。新增 [Unity 批处理检查](README.md#批处理启动验收)，可记录配置验收日志。
- Unity 导入、编译、重复接线、批处理及 Play Mode 尚无运行证据。下一步在 Unity 6.6 环境执行 001，提交编辑器生成的包锁与重载设置，记录两次 Play Mode 的结果。
- [任务顺序及冻结范围](BACKLOG.md) · [工作室角色](production/team.md) · [验收与证据规则](production/workflow.md)
- 剧情文档下一步：序章场景稿、一条完整地方线与M08—M11人物场的具体对白及行动；文档链接、因果与分支检查不代替真实玩家体验。Unity相关未运行项仍保持待验证。
- 玩法下一步：技术P0通过后制作声井救援代表环，分别观察人物/探索/战斗，并对照移动与固定阵位；首轮结论限于已记录控制条件，不自动冻结整个战斗系统。本轮未修改自动化任务prompt。
- 上轮剧情大纲文档验收（`15e35c39faf59a930dca55ca9d323677f14ed80b`）：叙事作者与独立一致性审稿完成；时间地理、重逢后因果、人物离开替代与结局边界审查通过；当时9份Markdown的53处本地链接、16个主线章节和6个地方线编号及空白检查通过，不代表游戏实现通过。
- 本轮玩法基线文档验收：设计、技术、叙事、UX与QA互评，状态边界及对照口径审查通过；7份Markdown的56处本地链接与空白检查通过。没有运行Unity或游戏测试，未实现、未试玩。

## 每轮更新

- 本轮技术P0继续：基于 `21ce4693323552858430f10db69b5ba40d3c1f2f` 准备 [Actions CI 草稿](docs/unity-p0-ci.md)，固定 Unity、GameCI 与测试框架，覆盖重复接线、两次完整 Play 周期和独立批处理进程。首次 `prepare` 仅生成可审查候选；提交生成配置后才可 `verify` 干净 SHA。
- 本地仍无 Unity Editor，当前云网络拒绝 `api.github.com`，Actions 运行记录与仓库授权配置未读到；用户已授权发布，本轮将候选提交到 `codex/dev`，由 push 事件请求首次 prepare。运行结果尚未取得。P0 保持待验证，不能把原包校验或静态审查当成运行通过。
- 下一步：追加放行 GitHub API，查询本次 push 对应的 Actions 运行并取得 prepare artifact，审查真实生成文件后提交并完成同 SHA 验证。

Producer 在此更新当前故事、最近交付、证据 SHA 及链接、未完成的检查和一个明确的下一步。项目长期背景见 [基线记录](production/session-state/active.md)。

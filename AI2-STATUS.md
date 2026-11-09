# AI2 当前状态

- 工作分支为 `codex/dev`。每轮通过 GitHub 查询实时 HEAD。
- 用户明确的最高产品原则是“好玩第一，一切都建立在好玩的基础上”，已写入 [开发规则](AGENTS.md) 和 [任务清单](BACKLOG.md)。
- 用户已同意“归来”主线并要求详细大纲入库。现有[主线大纲](docs/narrative/STORY_OUTLINE.md)、[人物关系](docs/narrative/CHARACTERS.md)、[地区与长期故事](docs/narrative/REGIONAL_STORIES.md) v0.1：救友在中段完成，后半共同冒险与成长，地方故事独立成立。它们是写作基线，未实现、未试玩；工作标题与具体扩写细节仍可改稿。
- 用户授权深入讨论并写一页玩法基线，本轮新增[基线v0.1](docs/gameplay/BASELINE.md)：区域探索与队伍短回合为方向，三人、固定个人顺序及镜头等仅作首轮控制；移动/固定阵位须公平对照，仍未实现、未试玩。
- 先前撤销的游戏设定继续作废。主角外观、美术尺度等未锁定，玩法的已定/暂定/待验证以[玩法基线](docs/gameplay/BASELINE.md)和 [BACKLOG](BACKLOG.md) 为准。
- 当前故事为 [002 · 声井声源试验](production/stories/002-soundwell-signal.md)。自动化验收通过，真人试玩待做。
- 工程锁定 Unity 6.6（6000.6.8f1）与随编辑器发布的 UGUI 2.0。当前框架仍用已提供的 ORK 3.22.0 / Makinom 2.21.0 固定包。此前已对安装脚本的 136 个文件做逐字节比对并验证重跑。
- 框架包已完整整合到开发分支：73 个资源条目、136 个文件与原包逐字节一致，原始 GUID 保留；当时重复安装新增 0 个文件，`python tools/verify_ork.py` 与两个源码 ZIP 的 CRC 检查通过。详见 [整合证据](docs/ork-integration.md)。[P0 已通过 Unity 验收](https://github.com/jinze909/rpg/actions/runs/42551491951/job/128948965800)。
- 启动接线会拒绝空配置、禁用的 Starter 与错误的数据来源。新增 [Unity 批处理检查](README.md#批处理启动验收)，可记录配置验收日志。
- P0 在 `6ea56a9746106559d32dbcd0456cfec17fce29d2` 的干净检出上通过 Unity 6000.6.8f1 导入、编译、重复接线、两次 Play Mode 和独立批处理。编辑器生成的项目配置和包锁已入库。[验证日志](https://github.com/jinze909/rpg/actions/runs/42551491951/job/128948965800)
- [任务顺序及冻结范围](BACKLOG.md) · [工作室角色](production/team.md) · [验收与证据规则](production/workflow.md)
- 剧情文档下一步：序章场景稿、一条完整地方线与M08—M11人物场的具体对白及行动；文档链接、因果与分支检查不代替真实玩家体验。Unity相关未运行项仍保持待验证。
- 玩法下一步先做 002 的小范围真人试玩，记录玩家能否自行发现声源与回撤规律，再加入短回合救援遭遇样本。移动／固定阵位和体验乐趣仍待对照。
- 上轮剧情大纲文档验收（`15e35c39faf59a930dca55ca9d323677f14ed80b`）：叙事作者与独立一致性审稿完成；时间地理、重逢后因果、人物离开替代与结局边界审查通过；当时9份Markdown的53处本地链接、16个主线章节和6个地方线编号及空白检查通过，不代表游戏实现通过。
- 本轮玩法基线文档验收：设计、技术、叙事、UX与QA互评，状态边界及对照口径审查通过；7份Markdown的56处本地链接与空白检查通过。没有运行Unity或游戏测试，未实现、未试玩。

## 每轮更新

- 本轮技术P0继续：基于 `21ce4693323552858430f10db69b5ba40d3c1f2f` 准备 [Actions CI 草稿](docs/unity-p0-ci.md)，固定 Unity、GameCI 与测试框架，覆盖重复接线、两次完整 Play 周期和独立批处理进程。首次 `prepare` 仅生成可审查候选；提交生成配置后才可 `verify` 干净 SHA。
- 网络现已放开并取得实际结果：[run 42520640550](https://github.com/jinze909/rpg/actions/runs/42520640550) 测试候选 `3c9502ad6b86a6c905ee8c5e6bdbc8600ff4347f`，失败于 GameCI CLI 参数 `Unknown argument: noCoverageEnabled`，Unity 尚未启动。当前首错是 Action/CLI 参数兼容，不继续把旧 API 网络拒绝当现状；ULF 沿用用户现有方案。
- 本轮联网研究并经叙事、战斗、探索、制作、玩家体验与独立创意审查，收敛[声井首样本设计](docs/gameplay/FIRST_PLAYABLE.md)及[002 声源试验](production/stories/002-soundwell-signal.md)。只定可开发范围，未实现、未运行、未真人试玩；移动/阵位和乐趣仍待对照，正式队友及美术未冻结。
- 002 实作和自动化验收完成。Unity 实测 SHA `e7312010b42c9a29e4bf65badd02a946ddd9381d`，从 Bootstrap 进入场景，验证移动与碰撞、摇铃追索、包铃搜索、回撤重试及场景重载，保存三张截图。[同 SHA CI](https://github.com/jinze909/rpg/actions/runs/42581703947)

Producer 在此更新当前故事、最近交付、证据 SHA 及链接、未完成的检查和一个明确的下一步。项目长期背景见 [基线记录](production/session-state/active.md)。

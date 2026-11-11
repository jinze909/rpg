# AI2 当前状态

- 工作分支为 `codex/dev`。每轮通过 GitHub 查询实时 HEAD。
- **当前最高接力指令（2026-11-09）：M1–M11 迁移优先于剧情/故事功能。** 最新规则取消固定阶段头长期保留：每个独立增量一个候选，合入后不复用旧候选，清理范围限本次及直接过时的候选；历史积压另作专项，详见 [阶段分支索引](docs/technical/MIGRATION_STAGE_BRANCHES.md) 和 [Issue #2](https://github.com/jinze909/rpg/issues/2)。M1 URP 2D 已完成；M2 Input System 核心已合入，当前活动候选 `codex/pending/m2-ugui-focus` SHA `4d263e0ce7a3cf9401a1e9dcafb13a74207901c6`，对应 [Run 42777305240](https://github.com/jinze909/rpg/actions/runs/42777305240) 在本次核查时运行中，**需重新查询最终结论，不能按旧绿色运行推断该 SHA 通过**。当前唯一下一步是解决这次 M2 CI 的实际结论并验收/合入，之后转 M3 Cinemachine 3。M3–M11 曾有空预留头，但这段 SHA/CI 属历史接力，不代表今日阶段状态或要求保留旧头。
- 用户明确的最高产品原则是“好玩第一，一切都建立在好玩的基础上”，已写入 [开发规则](AGENTS.md) 和 [任务清单](BACKLOG.md)。
- 用户已同意“归来”主线并要求详细大纲入库。现有[主线大纲](docs/narrative/STORY_OUTLINE.md)、[人物关系](docs/narrative/CHARACTERS.md)、[地区与长期故事](docs/narrative/REGIONAL_STORIES.md) v0.2：采用用户提供的 Claude 文稿，加入白水渡谜线、点名与受限听雾，保留中段救友、后半共同成长与独立地方故事。它们是写作基线，未实现、未试玩；工作标题与具体扩写细节仍可改稿。
- 用户授权深入讨论并写一页玩法基线，本轮新增[基线v0.1](docs/gameplay/BASELINE.md)：区域探索与队伍短回合为方向，三人、固定个人顺序及镜头等仅作首轮控制；移动/固定阵位须公平对照，仍未实现、未试玩。
- 先前撤销的游戏设定继续作废。主角外观、美术尺度等未锁定，玩法的已定/暂定/待验证以[玩法基线](docs/gameplay/BASELINE.md)和 [BACKLOG](BACKLOG.md) 为准。
- 当前开发为 [003 · 三人短回合声井救援](production/stories/003-soundwell-turn-rescue.md)，进行中而非完成。最新候选 `2eee996adaec6563fd379416e7ec6fad8e1b9a62` 已快进合入 `codex/dev`；同 SHA 的 [Run 42766341628](https://github.com/jinze909/rpg/actions/runs/42766341628) 通过 `passed-clean-candidate`（8/8 Editor tests、两次 Play Mode、独立 Batch）。该增量验证了 New Input System 键鼠/手柄动作路由、三人 ORK 回合战斗进入及 ORK HP 死亡/战斗结束后的闸门逻辑。故事仍未完成：Makinom UI System 仍为 `NoneUISystem`，自动战斗菜单关闭，默认攻击 Ability 未配置，因此玩家实际选择攻击/目标的完整战斗尚不可玩；两条完整救援策略和真人试玩仍待验收。
- 工程锁定 Unity 6.6（6000.6.8f1）与随编辑器发布的 UGUI（CI 解析为 2.6.0）。当前框架仍用已提供的 ORK 3.22.0 / Makinom 2.21.0 固定包。此前已对安装脚本的 136 个文件做逐字节比对并验证重跑。
- 框架包已完整整合到开发分支：73 个资源条目、136 个文件与原包逐字节一致，原始 GUID 保留；当时重复安装新增 0 个文件，`python tools/verify_ork.py` 与两个源码 ZIP 的 CRC 检查通过。详见 [整合证据](docs/ork-integration.md)。[P0 已通过 Unity 验收](https://github.com/jinze909/rpg/actions/runs/42551491951/job/128948965800)。
- 启动接线会拒绝空配置、禁用的 Starter 与错误的数据来源。新增 [Unity 批处理检查](README.md#批处理启动验收)，可记录配置验收日志。
- P0 在 `6ea56a9746106559d32dbcd0456cfec17fce29d2` 的干净检出上通过 Unity 6000.6.8f1 导入、编译、重复接线、两次 Play Mode 和独立批处理。编辑器生成的项目配置和包锁已入库。[验证日志](https://github.com/jinze909/rpg/actions/runs/42551491951/job/128948965800)
- [任务顺序及冻结范围](BACKLOG.md) · [工作室角色](production/team.md) · [验收与证据规则](production/workflow.md)
- 剧情文档下一步：打磨已补写的 S00—M00 场景稿，展开一条完整地方线与 M08—M11 连续人物场；文档链接、因果与分支检查不代替真实玩家体验。Unity相关未运行项仍保持待验证。
- 技术迁移当前接力：M1 URP 2D 已合入 `codex/dev`，最终资产 SHA `3a3465225636212effcb1d452deb6d7802bb0d16` 在 [Run 42744816128](https://github.com/jinze909/rpg/actions/runs/42744816128) 同 SHA 干净验收通过（6/6 Editor tests、双次 Play Mode、Soundwell ORK 战斗/信号回归）；合并提交为 `8c36229e011248d019f0ef4d8a9a162dd6354afe`。M2 游戏自有 Soundwell 输入动作适配已合入 `codex/dev`，候选 SHA `2eee996adaec6563fd379416e7ec6fad8e1b9a62` 在 [Run 42766341628](https://github.com/jinze909/rpg/actions/runs/42766341628) 通过 8/8 tests、双次 Play Mode、独立 Batch 与 `passed-clean-candidate`；使用 Input System 1.20.1，保留 `Active Input Handling = Both` 供 ORK 兼容，未修改 ORK 原厂 Input Key 资产。M2 剩余 UGUI 导航/焦点与更广泛 ORK 兼容验收。003 的实际战斗菜单、两条救援策略与真人试玩仍未完成。
- 上轮剧情大纲文档验收（`15e35c39faf59a930dca55ca9d323677f14ed80b`）：叙事作者与独立一致性审稿完成；时间地理、重逢后因果、人物离开替代与结局边界审查通过；当时9份Markdown的53处本地链接、16个主线章节和6个地方线编号及空白检查通过，不代表游戏实现通过。
- 本轮玩法基线文档验收：设计、技术、叙事、UX与QA互评，状态边界及对照口径审查通过；7份Markdown的56处本地链接与空白检查通过。没有运行Unity或游戏测试，未实现、未试玩。

## 每轮更新

- 2026-11-09 声井现有实现 `52bdaa77775cc32439099c435a3077a29157e28e` 在 Unity 6000.6.8f1 干净检出上通过 4/4 测试和独立批处理，结果为 `passed-clean-candidate`。[dev 同 SHA 验证](https://github.com/jinze909/rpg/actions/runs/42671061762/job/129347464989)。这轮推进规则调整只改指令与接力，003 尚无 Unity 结果。

- 2026-11-09 接续确认：`codex/dev` 上声井操作提示、返回入口与重入测试的实现候选 `82d0f99dc167b8425d04306fd685dedf3e2a8daa` 已在 Unity 6000.6.8f1 干净检出中通过 4/4 测试、独立批处理与 `passed-clean-candidate`。[dev 同 SHA Run 42586562395](https://github.com/jinze909/rpg/actions/runs/42586562395)。实体键鼠、扬声器、真人探索理解及乐趣仍待验证。仓库按用户最新指令保持 Public；原生常驻总指令已按任务保存版本纳入 `CHATGPT.md`。

- 002 复查修复了窄视口裁切、清响音效与追索状态脱节、重复加载入口。Unity 6000.6.8f1 在 `cc33cbd8402d7faf80083bc3a17661990128baaf` 的干净检出上通过 4 项测试与独立批处理。实体键盘、扬声器和真人试玩仍待验证。[同 SHA CI](https://github.com/jinze909/rpg/actions/runs/42584125951)

- 剧情 v0.2：整合用户提供的 Claude 文稿，新增[卖点与开场](docs/narrative/PITCH_AND_HOOK.md)、[写作准则](docs/narrative/NARRATIVE_GUIDE.md)，补齐[事实登记](docs/narrative/NARRATIVE_FACTS.md)与[S00—M00 场景稿](docs/narrative/SCENE_SCRIPT_S00_M00.md)。[整合记录](docs/narrative/REVISION_NOTES.md)说明来源与修订。叙事 S00 不等于技术 P0；名册 UI、对账和听雾操作未实现、未验证，现有队伍短回合和声井样本范围不变。本轮为文稿验收，保留下述技术／002 实现与同 SHA 证据。

- 本轮技术P0继续：基于 `21ce4693323552858430f10db69b5ba40d3c1f2f` 准备 [Actions CI 草稿](docs/unity-p0-ci.md)，固定 Unity、GameCI 与测试框架，覆盖重复接线、两次完整 Play 周期和独立批处理进程。首次 `prepare` 仅生成可审查候选；提交生成配置后才可 `verify` 干净 SHA。
- 网络现已放开并取得实际结果：[run 42520640550](https://github.com/jinze909/rpg/actions/runs/42520640550) 测试候选 `3c9502ad6b86a6c905ee8c5e6bdbc8600ff4347f`，失败于 GameCI CLI 参数 `Unknown argument: noCoverageEnabled`，Unity 尚未启动。当前首错是 Action/CLI 参数兼容，不继续把旧 API 网络拒绝当现状；ULF 沿用用户现有方案。
- 本轮联网研究并经叙事、战斗、探索、制作、玩家体验与独立创意审查，收敛[声井首样本设计](docs/gameplay/FIRST_PLAYABLE.md)及[002 声源试验](production/stories/002-soundwell-signal.md)。只定可开发范围，未实现、未运行、未真人试玩；移动/阵位和乐趣仍待对照，正式队友及美术未冻结。
- 002 实作和自动化验收完成。Unity 实测 SHA `e7312010b42c9a29e4bf65badd02a946ddd9381d`，从 Bootstrap 进入场景，验证移动与碰撞、摇铃追索、包铃搜索、回撤重试及场景重载，保存三张截图。[同 SHA CI](https://github.com/jinze909/rpg/actions/runs/42581703947)

Producer 在此更新当前故事、最近交付、证据 SHA 及链接、未完成的检查和一个明确的下一步。项目长期背景见 [基线记录](production/session-state/active.md)。


- 2026-11-10 交付：`codex/dev` 从 `8c36229e011248d019f0ef4d8a9a162dd6354afe` 快进至 `2eee996adaec6563fd379416e7ec6fad8e1b9a62`。同 SHA [Run 42766341628](https://github.com/jinze909/rpg/actions/runs/42766341628) 为 `passed-clean-candidate`；8/8 Editor tests 全部通过：Bootstrap wiring、URP 2D renderer、两次 Play 初始化、键盘/手柄动作上下文、三人 ORK 回合战斗与 Soundwell 信号流程。验收产物：[Unity P0 artifact](https://github.com/jinze909/rpg/actions/runs/42766341628/artifacts/13740231633)。

# 开发任务清单

**产品最高原则：好玩第一，一切都建立在好玩的基础上。** 内容体量与叙事追求为玩家体验服务；用玩家实际参与、探索、决策和情感投入评估设计，以试玩检验尚未证实的乐趣。系统数量、文本长度和技术完成不能替代体验验证。

| 阶段 | 本项目交付 | 当前状态 |
| --- | --- | --- |
| N0 | 建立“归来”[详细主线](docs/narrative/STORY_OUTLINE.md)、[人物关系](docs/narrative/CHARACTERS.md)与[地区故事](docs/narrative/REGIONAL_STORIES.md) v0.2，含点名、渡口谜线、五张归途中的脸及开场场景稿，供编剧与任务分解。 | 文档审查通过；未实现、未试玩 |
| D0 | 深度讨论并写成[一页玩法基线](docs/gameplay/BASELINE.md)，区分已定、暂定与待验证，供后续任务读取。 | 文档审查通过；未实现、未试玩 |
| P0 | 验证 Unity 工程和 ORK 启动。[001 · ORK 启动验证](production/stories/001-ork-bootstrap.md) | Unity 6000.6.8f1 干净提交验收通过。[CI](https://github.com/jinze909/rpg/actions/runs/42551491951/job/128948965800) |
| P0-b | 建立脚本化冒烟、同 SHA 验证和基础帧时间、P95、GC 趋势采样。 | 待开发 |
| G1 | 标题、菜单和 HUD 使用 UGUI，验证键鼠与手柄焦点及分辨率适配。世界渲染走 SpriteRenderer / Tilemap，并检查排序层。 | 待开发 |
| N1 | 打磨已有[S00—M00 场景稿](docs/narrative/SCENE_SCRIPT_S00_M00.md)，写完一条独立地区线及 M08—M11 连续人物场；检查具体行动、对白与因果。 | 开场初稿已整理，后续待编剧；剧情演出未实现、未试玩 |
| P2 | 按[首样本设计](docs/gameplay/FIRST_PLAYABLE.md)制作声井救援代表环，公平比较移动／固定阵位。 | [002 声源试验](production/stories/002-soundwell-signal.md)工程验收通过，真人试玩待做。当前根据用户最新优先级先推进 M2–M11 迁移，003 后续开发保留为待续接。 |

**用户新决定：当前必须优先持续完成 M1–M11 技术迁移；不得再默认转去推进剧情/故事 003。** M1–M11 仍按实际需求使用 `codex/pending/*` 候选，但**已完成阶段不再永久保留任何分支头**。每次候选实际成功合入 `codex/dev` 时检查全部 pending，按[清理规则](production/workflow.md#候选分支清理)即时删除可安全清理的阶段或故事候选；没有合入的轮次不执行全量扫描（见[历史索引](docs/technical/MIGRATION_STAGE_BRANCHES.md)）。先处理当前 CI/故障，再按 M 编号推进；当前任务状态见 [AI2-STATUS](AI2-STATUS.md)。

## 现代工作室技术路线（已建 GitHub 实施卡；迁移尚未完成）

**跨轮迁移接力已经立项：** [总追踪 Issue #2](https://github.com/jinze909/rpg/issues/2)，下设 14 张独立执行票 [#3—#16](https://github.com/jinze909/rpg/issues/3)。按总票与下表逐张实施：**先在当前 dev 上整合 Soundwell 的已通过候选，再做 [URP 2D #3](https://github.com/jinze909/rpg/issues/3)，然后做 [New Input System #4](https://github.com/jinze909/rpg/issues/4)**。其余有实际意义的迁移已分别开票，后续 AI 应沿同一编号推进，**不得因为换聊天就丢弃**。用户此前曾提前建立 M1–M11 空预留分支，但现已明确**取消永久保留要求**；阶段候选按需创建/延续，完成并经安全检查后清理。开票或开分支不等于迁移完成，也不得在一个候选中混装多项技术。ORK 原有输入文件保留，但游戏自有输入动作最终只允许 New Input System 一个运行执行者；内核 ORK 兼容由实际 Unity 结果决定。

**本轮技术先行证据：** Soundwell `codex/dev` 最终整合 SHA `0fb1389c9df1e50d41eef73551e25d0c391c1a43` 已在 [Run 42740645793](https://github.com/jinze909/rpg/actions/runs/42740645793) 通过 5/5、独立批处理与 `passed-clean-candidate`；M1 URP 2D 已在最终资产 SHA `3a3465225636212effcb1d452deb6d7802bb0d16` 的 [Run 42744816128](https://github.com/jinze909/rpg/actions/runs/42744816128) 通过 6/6 tests 并合入 dev。M2 Soundwell New Input System gameplay adapter 已在 `2eee996adaec6563fd379416e7ec6fad8e1b9a62` 合入 `codex/dev`，同 SHA [Run 42766341628](https://github.com/jinze909/rpg/actions/runs/42766341628) 通过 8/8 tests、双次 Play Mode、独立 Batch 与 `passed-clean-candidate`；[artifact](https://github.com/jinze909/rpg/actions/runs/42766341628/artifacts/13740231633)。003 的玩家战斗菜单/攻击、两种完整救援策略与真人试玩仍是后续验收，不可冒充整卡完成。

**硬限制：** 现有 ORK 3.22.0 / Makinom 2.21.0 **保持不变**，不升级/重购/改厂商文件或设置；UGUI 保留，不迁移 UI Toolkit；禁止未经另行授权的新付费插件、订阅、云服务和 CI 费用。统一执行规则与官方链接见 [现代 2D RPG 技术基线](docs/technical/MODERN_2D_RPG_STACK.md)。

| 技术卡 | 独立增量（只选有实际收益的官方/零新增购买方案） | 状态与门槛 |
| --- | --- | --- |
| M0 · 历史前提（已解决） | 修复 003 声井真实三人队伍候选的场景重载/玩家数量回归，恢复同 SHA 干净 CI，继续两条救援路径 | 已整合到 `codex/dev`：`0fb1389c9df1e50d41eef73551e25d0c391c1a43`；同 SHA Run 42740645793 通过 5/5、批处理与 `passed-clean-candidate`。003 两条策略仍未完成 |
| M1 · URP 2D | 单独候选：2D Renderer、项目管线、质量级继承、材质/相机/UGUI/场景回归 | **已合入** `codex/dev`：merge SHA `8c36229e011248d019f0ef4d8a9a162dd6354afe`；最终资产 SHA `3a3465225636212effcb1d452deb6d7802bb0d16`，Run 42744816128 `passed-clean-candidate`，6/6 tests；dev Run 42747121343 成功 |
| M2 · Input System | `com.unity.inputsystem`、`Both` 兼容、游戏自有 Input Actions/重绑定、键鼠手柄/UGUI 回归 | **核心适配已合入** `codex/dev`：SHA `2eee996adaec6563fd379416e7ec6fad8e1b9a62`；官方 1.20.1；Run 42766341628 同 SHA 通过 8/8 tests、双次 Play Mode、独立 Batch。Active Input Handling Both 保留供 ORK 兼容，ORK 原厂 Input Key 资产未改。剩余 UGUI 导航/焦点与更广泛 ORK 兼容验收，M2 尚未关闭 |
| M3 · Cinemachine 3 | 替代唯一自有摄像机控制路径，验证 2D 跟随、边界、各宽高比和战斗镜头 | 仅计划；不得同时运行双摄像机控制器 |
| M4 · 2D 动画 | 确定逐帧或骨骼方案后做可见角色待机/移动/战斗/受击一条闭环 | 仅计划；不强制骨骼 |
| M5 · Tilemap Extras | 真实区域的 Rule Tile、Tilemap、碰撞与排序 | 仅计划；地图需求成立再执行 |
| M6 · Audio Mixer | 音乐/环境/SFX 统一分组、快照、混音与实机检验 | 仅计划；内置免费工具 |
| M7 · Sprite Atlas V2 | 有足够精灵和实际 Draw Call/内存证据时建图集 | 按需，不是必装 |
| M8 · Pixel Perfect | 仅选择严格像素风时评估与相机/渲染组合 | 条件性 |
| M9 · Localization | 先定义与 ORK 既有文本管理不冲突的单一权威，再验证多语言 | 条件性，不改 ORK |
| M10 · Memory Profiler | 以复现场景/资源峰值/GC 为证据优化内存 | 触发式；CI 趋势不等于玩家设备基准 |
| M11 · Build Profiles | 有目标平台和复现构建后区分 Debug/Test/Release | 发布准备阶段，避免收费平台 |
| M12 · Addressables | 仅大型/远程资源管理需求成立后考虑 | 暂缓；目前保持直接引用 |
| 不列为必修 | DOTS/ECS、HDRP、付费 Asset Store 工具、商业云服务、ORK 升级 | 不采用，除非用户日后另作明确决定 |

**顺序（用户新优先级）:** 先处理阻塞当前迁移的 P2/003 编译、测试或关键玩家路径故障，然后优先完成 M2→M3→…→M11；独立剧情功能暂不抢占迁移主线。完成每个独立技术增量时按 [同 SHA 验收](production/workflow.md#同-sha-验收) 和新技术文档验证。文档清单不是授权一次安装上述全部包，更不能因文档完成而把技术卡标成绿色。

## 冻结范围

- 目标是制作成熟好玩的 2D RPG。用户已同意按“归来”主线扩写详细大纲；当前叙事基线见[主文](docs/narrative/STORY_OUTLINE.md)。故事须保留中段真正救回沈砾、后半共同冒险与双向成长，以及能独立成立的地区人物故事。大纲具体细节允许依据后续剧本与试玩改稿。
- 玩法采用区域探索与队伍短回合骨架，具体状态以[玩法基线](docs/gameplay/BASELINE.md)为准；移动/固定阵位、人数、行动预算与排序仍待实验，原型镜头和输入只作工作默认。
- 先前撤销的游戏设定不得从旧提交恢复。主角自定义范围、性别外观、美术尺度、恋爱系统及最终内容时长等未锁定；不得将工作人名地名理解为已完成的游戏资产。
- G1 的 UI 固定使用 UGUI。世界渲染采用 SpriteRenderer / Tilemap。框架职责见 [AGENTS](AGENTS.md)。
- 占位素材标注 `PLACEHOLDER`。需美术复核时标注 `ART_REVIEW_REQUIRED`，由 art-director 判断。美术规格随新方向制定。
- 影响冻结项的设计或迁移提案由对应总监记录影响和建议，经用户决定后再写入本清单。

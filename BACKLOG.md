# 开发任务清单

**CI 等待不等于全局开发暂停（2026-11-11）：** 任何一个或多个 `codex/pending/*` Unity Run 排队/运行，**只锁定那些 Run 依赖的候选最终 SHA 的验收/合入**，不得冻结 `codex/dev` 上其他互不依赖的故事、玩法、测试或资源增量。每次自动派发必须执行“CI 接力 A 轨 + 独立开发 B 轨”；旧 SHA 失败而更新 SHA 已提交排队时，不反复修旧失败、冻结新 SHA 并执行 B 轨。候选与文件查重后从最新 dev 新建/接续不同名独立候选，实际提交代码；没有可做任务必须逐项说明依赖或冲突，不能只报告 queued。完整规则见 [CHATGPT.md](CHATGPT.md#ci-排队运行是验收门禁不是开发停工令2026-11-11-用户再次明确) 和 [工作流程](production/workflow.md#ci-排队运行时必须继续独立开发)。

**真实进度更正（2026-11-10，`dev` `790266185...`）：** M1–M11 的工程贡献**全部 11/11 已实际进入 dev 提交历史**；不等于全部完成验收。**关闭 Issue：M1/M10/M11（3/11）；仍 open：M2–M9（8/11）**。旧文档的「M3–M11 未实施」「M8 尚未合入/Run 仍在运行」「当前必须先迁移再开发故事」等叙述已失效。各阶段最终 SHA、Unity CI、实际功能与剩余门槛以[逐项事实核查](docs/technical/MODERNIZATION_VERIFIED_STATUS.md)为准；后续须读取实时 HEAD 和最新 CI。


**产品最高原则：好玩第一，一切都建立在好玩的基础上。** 内容体量与叙事追求为玩家体验服务；用玩家实际参与、探索、决策和情感投入评估设计，以试玩检验尚未证实的乐趣。系统数量、文本长度和技术完成不能替代体验验证。

| 阶段 | 本项目交付 | 当前状态 |
| --- | --- | --- |
| N0 | 建立“归来”[详细主线](docs/narrative/STORY_OUTLINE.md)、[人物关系](docs/narrative/CHARACTERS.md)与[地区故事](docs/narrative/REGIONAL_STORIES.md) v0.2，含点名、渡口谜线、五张归途中的脸及开场场景稿，供编剧与任务分解。 | 文档审查通过；未实现、未试玩 |
| D0 | 深度讨论并写成[一页玩法基线](docs/gameplay/BASELINE.md)，区分已定、暂定与待验证，供后续任务读取。 | 文档审查通过；未实现、未试玩 |
| P0 | 验证 Unity 工程和 ORK 启动。[001 · ORK 启动验证](production/stories/001-ork-bootstrap.md) | Unity 6000.6.8f1 干净提交验收通过。[CI](https://github.com/jinze909/rpg/actions/runs/42551491951/job/128948965800) |
| P0-b | 建立脚本化冒烟、同 SHA 验证和基础帧时间、P95、GC 趋势采样。 | 已有声井冒烟、同 SHA 验证和 Editor 趋势采样；按场景补覆盖，不等同玩家设备性能验收 |
| G1 | 标题、菜单和 HUD 使用 UGUI，验证键鼠与手柄焦点及分辨率适配。世界渲染走 SpriteRenderer / Tilemap，并检查排序层。 | 已有入口/操作/HUD、输入焦点与 SpriteRenderer/Tilemap；完整菜单、各设备/分辨率覆盖按实际缺口补验 |
| N1 | 打磨已有[S00—M00 场景稿](docs/narrative/SCENE_SCRIPT_S00_M00.md)，写完一条独立地区线及 M08—M11 连续人物场；检查具体行动、对白与因果。 | 开场初稿已整理，后续待编剧；剧情演出未实现、未试玩 |
| P2 | 按[首样本设计](docs/gameplay/FIRST_PLAYABLE.md)制作声井救援代表环，公平比较移动／固定阵位。 | [002](production/stories/002-soundwell-signal.md) 与 [003 移动版代表环](production/stories/003-soundwell-turn-rescue.md)工程验收通过；完整固定阵位模式、后续成长/持久化及主观乐趣仍未验收。故事、序幕与技术卡按真实依赖推进。 |

**用户 2026-11-10 新决定：取消 M1–M11 高于玩法、剧情和故事 003 的强制优先级。** M1–M11 技术卡保留为可选择任务，与故事/玩法并列按价值、依赖、活动候选及真实 CI 结果确定开发顺序；任何 M 阶段均非故事开发的统一前置条件。已合入的阶段候选不再永久保留或复用；每次合入 `codex/dev` 后仅复核本次候选及其直接相关已完全取代的旧候选，按[清理规则](production/workflow.md#候选分支清理)及[自动清理 Actions](.github/workflows/close-merged-pending.yml)处理。正在运行的 CI 保持最终候选 SHA 不变并接续，失败时修复，不阻止在无依赖的独立候选上进行玩法、剧情或技术开发；实时任务状态以 [AI2-STATUS](AI2-STATUS.md) 和 GitHub 为准。

## M1–M11 现代化（工程增量已落地；8 个阶段仍待完整验收）

**跨轮现代化任务仍已立项，但不构成强制实施队列：** [总追踪 Issue #2](https://github.com/jinze909/rpg/issues/2) 及子票 [#3—#16](https://github.com/jinze909/rpg/issues/3) 留作独立任务与证据来源。只有技术依赖确实存在时才按依赖排序，并允许故事/玩法增量穿插；不得因为换聊天而丢弃正在接力的候选。原 M1–M11 空预留头只是历史记录，不再永久保留；每个候选只负责一次独立可验收增量，合入后安全关闭，后续内容从最新 dev 新建不同名称候选。开票/建分支不代表阶段完成，也不得混装多项技术或让 ORK 与新系统并行控制同一操作。

**历史技术证据（不代表当前候选）：** Soundwell `codex/dev` 最终整合 SHA `0fb1389c9df1e50d41eef73551e25d0c391c1a43` 已在 [Run 42740645793](https://github.com/jinze909/rpg/actions/runs/42740645793) 通过 5/5、独立批处理与 `passed-clean-candidate`；M1 URP 2D 已在最终资产 SHA `3a3465225636212effcb1d452deb6d7802bb0d16` 的 [Run 42744816128](https://github.com/jinze909/rpg/actions/runs/42744816128) 通过 6/6 tests 并合入 dev。M2 Soundwell New Input System gameplay adapter 已在 `2eee996adaec6563fd379416e7ec6fad8e1b9a62` 合入 `codex/dev`，同 SHA [Run 42766341628](https://github.com/jinze909/rpg/actions/runs/42766341628) 通过 8/8 tests、双次 Play Mode、独立 Batch 与 `passed-clean-candidate`；[artifact](https://github.com/jinze909/rpg/actions/runs/42766341628/artifacts/13740231633)。故事 003 已覆盖三人分别通过 ORK 开闸、保护、攻击，并从同一开局独立完成侧铃与正面护送。战中移动预览/取消/阻挡与同起点开闸分工已由[故事 003](production/stories/003-soundwell-turn-rescue.md)后续 Run 验证；完整固定阵位可玩版仍待后续独立切片。

**现行边界（2026-11-11）：** ORK/Makinom 框架和配置允许为明确需求修改，经隔离候选充分验证后合入，旧全面冻结撤销；UGUI 保留，零新增付费，单一 RPG 权威与许可约束继续有效。统一执行规则与官方链接见 [现代 2D RPG 技术基线](docs/technical/MODERN_2D_RPG_STACK.md)。

| 技术卡 | 独立增量（只选有实际收益的官方/零新增购买方案） | 状态与门槛 |
| --- | --- | --- |
| M0 · 历史前提（已解决） | 修复 003 声井真实三人队伍候选的场景重载/玩家数量回归，恢复同 SHA 干净 CI，继续两条救援路径 | 已整合到 `codex/dev`：`0fb1389c9df1e50d41eef73551e25d0c391c1a43`；同 SHA Run 42740645793 通过 5/5、批处理与 `passed-clean-candidate`。003 两路线现已工程验收通过，参见故事卡的新证据 |
| M1 · URP 2D | 单独候选：2D Renderer、项目管线、质量级继承、材质/相机/UGUI/场景回归 | **工程交付，Issue #3 closed**；最终资产 `3a346522...` [Run 42744816128](https://github.com/jinze909/rpg/actions/runs/42744816128) 同 SHA verify 成功 |
| M2 · Input System | `com.unity.inputsystem`、`Both` 兼容、游戏自有 Input Actions/重绑定、键鼠手柄/UGUI 回归 | **核心已合入，#4 open**；UI Toggle 及后续入口焦点修复已进入 dev；剩余设备/实际路径按最新代码与 Run 核对，不重做旧候选 |
| M3 · Cinemachine 3 | 替代唯一自有摄像机控制路径，验证 2D 跟随、边界、各宽高比和战斗镜头 | **已合入，#5 open**；唯一相机与跟随边界测试 `ca3652f...` [Run 42785942604](https://github.com/jinze909/rpg/actions/runs/42785942604) 通过，镜头截图/重载证据待关票 |
| M4 · 2D 动画 | 确定逐帧或骨骼方案后做可见角色待机/移动/战斗/受击一条闭环 | **已合入，#6 open**；保存占位 Animator/Idle/Move/Action/Hit，`f34a180...` [Run 42794568707](https://github.com/jinze909/rpg/actions/runs/42794568707) 通过；真实逐帧表现未完成 |
| M5 · Tilemap Extras | 真实区域的 Rule Tile、Tilemap、碰撞与排序 | **已合入，#7 open**；真实声井 RuleTile/Collider `44f37f4...` [Run 42802244276](https://github.com/jinze909/rpg/actions/runs/42802244276) 通过；完整玩家路径证据待核 |
| M6 · Audio Mixer | 音乐/环境/SFX 统一分组、快照、混音与实机检验 | **已合入，#8 open**；原生 mixer、音量 API/SFX 铃声接线 `7c5bb4a...` [Run 42803799190](https://github.com/jinze909/rpg/actions/runs/42803799190) 通过；真实听感未验 |
| M7 · Sprite Atlas V2 | 有足够精灵和实际 Draw Call/内存证据时建图集 | **已合入，#9 open**；原生 V2 图集 `01d7dda...` [Run 42864872330](https://github.com/jinze909/rpg/actions/runs/42864872330) 同 SHA 通过；缺 Draw Call/内存对照 |
| M8 · Pixel Perfect | 仅选择严格像素风时评估与相机/渲染组合 | **已合入，#10 open**；16 PPU / 320×180 相机；旧 M8 [Run 42877178888](https://github.com/jinze909/rpg/actions/runs/42877178888) 失败，后续 [dev Run 42935131913](https://github.com/jinze909/rpg/actions/runs/42935131913) 测试通过；视觉验收待审 |
| M9 · Localization | 先定义与 ORK 既有文本管理不冲突的单一权威，再验证多语言 | **已合入，#11 open**；EN/zh UI 表和运行时切换 [Run 42818630008](https://github.com/jinze909/rpg/actions/runs/42818630008) 与后续 dev CI 成功；全文案/字体截图待验 |
| M10 · Memory Profiler | 以复现场景/资源峰值/GC 为证据优化内存 | **工程交付，#12 closed**；Editor Profiler/GC 多阶段 JSON 指标 `d913c2d...` [Run 42820333763](https://github.com/jinze909/rpg/actions/runs/42820333763) 同 SHA 通过，非玩家机快照 |
| M11 · Build Profiles | 有目标平台和复现构建后区分 Debug/Test/Release | **工程交付，#13 closed**；Debug/Test Linux Profiles + Linux Player 构建 `12a94ed...` [Run 42830867419](https://github.com/jinze909/rpg/actions/runs/42830867419) 同 SHA 通过，Release 未验 |
| M12 · Addressables | 仅大型/远程资源管理需求成立后考虑 | 暂缓；目前保持直接引用 |
| 不列为必修 | DOTS/ECS、HDRP、付费 Asset Store 工具、商业云服务 | 未证明需求的技术暂缓；不新增费用。ORK 版本调整单独按框架验证规则判断 |

**选题原则（不再迁移优先）:** 结合实际玩家收益、当前故障、可独立验收条件选择 M1–M11、故事 003 或其他任务；技术迁移仅按确有必要的依赖顺序执行，不要求先全部迁移完再开发剧情或玩法。对正在受测的候选保持 SHA 不变，CI 失败须修复、通过须验收；同时允许推进独立候选。完成每个技术或玩法增量均按 [同 SHA 验收](production/workflow.md#同-sha-验收) 验证。技术清单不等于一次安装所有包的授权，文档完成也不等于技术卡通过。

## 冻结范围

- 目标是制作成熟好玩的 2D RPG。用户已同意按“归来”主线扩写详细大纲；当前叙事基线见[主文](docs/narrative/STORY_OUTLINE.md)。故事须保留中段真正救回沈砾、后半共同冒险与双向成长，以及能独立成立的地区人物故事。大纲具体细节允许依据后续剧本与试玩改稿。
- 玩法采用区域探索与队伍短回合骨架，具体状态以[玩法基线](docs/gameplay/BASELINE.md)为准；移动/固定阵位、人数、行动预算与排序仍待实验，原型镜头和输入只作工作默认。
- 先前撤销的游戏设定不得从旧提交恢复。主角自定义范围、性别外观、美术尺度、恋爱系统及最终内容时长等未锁定；不得将工作人名地名理解为已完成的游戏资产。
- G1 的 UI 固定使用 UGUI。世界渲染采用 SpriteRenderer / Tilemap。框架职责见 [AGENTS](AGENTS.md)。
- 占位素材标注 `PLACEHOLDER`。需美术复核时标注 `ART_REVIEW_REQUIRED`，由 art-director 判断。美术规格随新方向制定。
- 智能体记录设计/迁移影响、证据与可逆决定，依现行产品方向推进；不把例行确认或用户反馈设为开发前提。框架修改按[验证规则](production/workflow.md#框架修改与验证)，实际问题写入[问题记录](production/KNOWN_ISSUES.md)。

当前接力分别维护实现与智能体验证下一步；已验且可合入候选优先交付，无关治理随后。未得到真人反馈不阻止工程验收，也不等于乐趣已被证实。

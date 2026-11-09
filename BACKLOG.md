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
| P2 | 按[首样本设计](docs/gameplay/FIRST_PLAYABLE.md)制作声井救援代表环，公平比较移动／固定阵位。 | [002 声源试验](production/stories/002-soundwell-signal.md)已实现并通过 Play Mode 自动化。真人试玩与短回合救援样本待做。 |

Producer 按实际依赖和阻塞选下一项，每轮收敛到一个可验收结果。当前执行状态见 [AI2-STATUS](AI2-STATUS.md)。

## 冻结范围

- 目标是制作成熟好玩的 2D RPG。用户已同意按“归来”主线扩写详细大纲；当前叙事基线见[主文](docs/narrative/STORY_OUTLINE.md)。故事须保留中段真正救回沈砾、后半共同冒险与双向成长，以及能独立成立的地区人物故事。大纲具体细节允许依据后续剧本与试玩改稿。
- 玩法采用区域探索与队伍短回合骨架，具体状态以[玩法基线](docs/gameplay/BASELINE.md)为准；移动/固定阵位、人数、行动预算与排序仍待实验，原型镜头和输入只作工作默认。
- 先前撤销的游戏设定不得从旧提交恢复。主角自定义范围、性别外观、美术尺度、恋爱系统及最终内容时长等未锁定；不得将工作人名地名理解为已完成的游戏资产。
- G1 的 UI 固定使用 UGUI。世界渲染采用 SpriteRenderer / Tilemap。框架职责见 [AGENTS](AGENTS.md)。
- 占位素材标注 `PLACEHOLDER`。需美术复核时标注 `ART_REVIEW_REQUIRED`，由 art-director 判断。美术规格随新方向制定。
- 影响冻结项的设计或迁移提案由对应总监记录影响和建议，经用户决定后再写入本清单。

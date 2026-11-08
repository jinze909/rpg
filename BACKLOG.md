# 开发任务清单

| 阶段 | 本项目交付 | 当前状态 |
| --- | --- | --- |
| P0 | 验证 Unity 工程和 ORK 启动，建立可追溯的编译与构建证据。当前为 [001 · ORK 启动验证](production/stories/001-ork-bootstrap.md)。 | 待验证 |
| P0-b | 建立脚本化冒烟、同 SHA 验证和基础帧时间、P95、GC 趋势采样。 | 待开发 |
| G1 | 标题、菜单和 HUD 使用 UGUI，验证键鼠与手柄焦点及分辨率适配。世界渲染走 SpriteRenderer / Tilemap，并检查排序层。 | 待开发 |
| P2 | 实现可通关的 Thornwood 切片。Vanguard 进入遭遇，击败 Briar Beast，完成胜利结算。以脚本化通关记录验证。 | 待开发 |

Producer 按实际依赖和阻塞选下一项，每轮收敛到一个可验收结果。当前执行状态见 [AI2-STATUS](AI2-STATUS.md)。

## 冻结范围

- 目标是 Unity 6.6 的 2D 单机切片。主角为唯一金发主角，首个可玩角色使用 Vanguard。切片地点与 Boss 分别为 Thornwood 和 Briar Beast。
- G1 的 UI 固定使用 UGUI。世界渲染采用 SpriteRenderer / Tilemap。框架职责见 [AGENTS](AGENTS.md)。
- 占位素材标注 `PLACEHOLDER`。需美术复核时标注 `ART_REVIEW_REQUIRED`，由 art-director 判断。沿用 256 单元规范的素材由 technical-artist 核对脚底锚点 (128,240) 与 128 PPU。
- 影响冻结项的设计或迁移提案由对应总监记录影响和建议，经用户决定后再写入本清单。

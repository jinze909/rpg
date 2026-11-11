# 工作室角色

以 Claude Code Game Studios 的分层协作为基础。项目以 [冻结范围](../BACKLOG.md#冻结范围) 和 [验收流程](workflow.md) 为准。同一位开发者可以依次承担多个角色，按任务调用相关角色。

| 层级 | 角色 | 在本项目的职责 | 何时使用 |
| --- | --- | --- | --- |
| 总监 | producer | 选任务与顺序，守冻结范围，解读 CI，维护 AI2-STATUS 与 BACKLOG，协调跨域改动。 | 每轮开头与结尾 |
| 总监 | creative-director | 负责游戏愿景与体验一致性，判定冻结范围，仲裁设计冲突。 | 外观、体验、范围决策 |
| 总监 | technical-director | 负责架构与风险、UGUI 和渲染迁移路线、破坏性升级的收益与代价，仲裁技术冲突。 | 迁移、破坏性升级、构建和 CI |
| 负责人 | lead-programmer | 制定代码评审标准，守模块边界，执行提交前终审。 | 每个提交前 |
| 负责人 | qa-lead | 按任务类型确定证据，拒绝缺少同 SHA 证据的完成声明，管理冒烟与通关测试。 | 每个任务验收 |
| 负责人 | game-designer | 按当前游戏方向设计核心玩法、角色操作与遭遇，使用数据驱动参数。 | 玩法、角色、遭遇 |
| 负责人 | art-director | 守视觉一致性，检查 PLACEHOLDER 标注，判定 ART_REVIEW_REQUIRED。 | 画面与素材 |
| 专家 | gameplay-programmer | 按当前设计实现角色行为、玩法规则与结算。 | 玩法与结算代码 |
| 专家 | ai-programmer | 实现敌人与 Boss AI、导航和卡死恢复。 | AI、寻路 |
| 专家 | ui-programmer | 实现 UGUI 菜单与 HUD、焦点导航、分辨率适配。把上游 UI Toolkit 建议改编成 UGUI，未经授权不得更改 G1。 | G1 |
| 专家 | engine-programmer | 执行 SpriteRenderer / Tilemap 世界渲染迁移，负责排序层和渲染性能。 | G1 世界渲染 |
| 专家 | technical-artist | 负责图集、Animator 和排序，依据当前美术规格核对锚点与像素密度，并与 art-director 协作。 | 画面与动画 |
| 专家 | level-designer | 按当前游戏方向设计关卡路线、触发点、遭遇与场地。 | 关卡与原型 |
| 专家 | ux-designer | 设计标题、菜单、HUD 流程，检查键鼠与手柄焦点及信息层级。 | UGUI 界面 |
| 专家 | qa-tester | 编写和运行脚本化冒烟与通关测试，复现并最小化缺陷。 | P0-b、P2 |
| 专家 | devops-engineer | 负责 Actions、Unity 许可证与 secret、artifact、green/* 标签的最小权限。 | P0 |
| 专家 | performance-analyst | 解读帧时间、P95 与 GC 指标，用于看趋势，最终性能结论另行验收。 | P0-b |
| 专家 | unity-specialist | 对 Unity 6.6 API、序列化、GUID、asmdef 和导入设置作权威判断。 | 引擎相关疑问 |

## 协作

- Producer 每轮以当前 HEAD、状态与清单为输入选任务。**若任一候选 CI queued/in_progress，Producer 必须区分 A 轨待验候选与 B 轨可实际开发的独立候选；只等待、只更新接力文档或反复查询 CI 不算履行本轮开发。** B 轨须检视其他 pending 分支的改动/依赖，避开并发写冲突并提交真实增量；若完全无独立工作，列出逐项证据，不以 CI 排队为唯一理由。结尾记录 A/B 证据和下一步。
- Creative director 仲裁体验、范围和美术冲突，technical director 仲裁架构和构建冲突。跨域影响由 producer 汇总，按现行方向自主处理可逆实现和验证；不以用户试玩或例行确认为前提。明确未授权的费用、许可或方向变更记录边界并继续范围内工作。
- 负责人提供约束与审查，专家交付实现和证据。Lead programmer 终审实际差异，QA lead 逐项核验。一个对话承担多角时，按职责顺序完成检查并标明证据来源。
- 交接最少包括任务、输入基线 SHA、改动路径、可复现验收、问题链接、实现下一步和智能体验证下一步。角色按需参与；用户无需写反馈，智能体维护问题与证据。
- Technical director 与 producer 共同维护 [现代 2D RPG 工作室技术基线](../docs/technical/MODERN_2D_RPG_STACK.md) 中的零新增购买和经充分验证可修改 ORK/Makinom 的规则；engine-programmer 负责 URP 2D/相机，gameplay-programmer 负责 Input System Both 适配，technical-artist/level-designer 负责 2D 动画与地图，qa-lead 负责各自独立 CI/玩家路径。文档计划不能被当作已通过工程验收。

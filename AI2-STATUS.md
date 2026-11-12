# AI2 当前状态

本页只保留当前接力，不代替实时 GitHub 核对。声井当前已验工程基线为 `9f130460611f09032d7130761ed5b446251742c7`。更早证据见故事卡和[现代化事实核查](docs/technical/MODERNIZATION_VERIFIED_STATUS.md)。

- **当前目标**。[003 三人短回合声井救援](production/stories/003-soundwell-turn-rescue.md)进行中。已有侧铃引兽和真实 ORK 普攻胜利两条救援路径，均有护送到安全点的行为回归。
- **本轮交付**。战斗显示 ORK 当前可攻击队员和目标；主角攻击、受击反馈在 ORK 暂停缩放时仍会完成。中英文界面正确显示攻击暂不可用。攻击与撤退移到战斗画面底部，退回探索时恢复按钮布局。CI 报告逐项列出测试名单差异。
- **验收**。[候选 Run 43169901831](https://github.com/jinze909/rpg/actions/runs/43169901831) 在该 SHA 通过 20 项 Unity 测试、独立 Batch 与 Linux Player 构建，保存了战斗画面。接力文档与游戏验收提交分别追踪。
- **技术状态**。M1–M11 已有工程贡献，剩余范围与证据见[现代化事实核查](docs/technical/MODERNIZATION_VERIFIED_STATUS.md)。
- **实现下一步**。从最新 dev 接续保护或战中交互的小步，让队员在普攻之外有实际分工。先核对活动候选和同 SHA CI。
- **智能体验证下一步**。在相同开局与主要行动预算下对照两条救援路线，记录可达性、提示暴露、代价、人物回应和回撤重试。主观体验限制见[问题记录](production/KNOWN_ISSUES.md)。

每次合入更新发生变化的条目及 BACKLOG/故事状态，保留实际 SHA 和 Run；历史不在本页重复追加。

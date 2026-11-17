# AI2 当前状态

> 截至 2026-11-16 的交付核对；实时分支、PR 和 CI 以 GitHub 为准，旧候选 SHA 不代表当前活动工作。

- **唯一剧情写作基线**：《归来》v1.2，39 份 Markdown（含 29 份单章场景稿）已由 [PR #66](https://github.com/jinze909/rpg/pull/66) 入库，交付 SHA `b2f6f7dc560d366bd7801c43a560db9aae84fe1c`。v1.1 不再是并行剧情权威；v1.2 内标为待确认的创作提案仍须与已确定事实区分。**这是写作交付，不是新版剧情的 Unity 实现或验收。**
- **已实现的独立玩法**：故事 003 的移动版声井救援代表环，两条路线、真实三名 ORK 队员的开闸／防御／攻击、可取消的战中移动和失败重试已有同 SHA 工程回归；见[故事卡](production/stories/003-soundwell-turn-rescue.md)。完整固定阵位可玩对照、后续成长／持久化及主观乐趣尚未验收。
- **现有 Unity S00**：桥头 ORK 防守、胜利后第三声号钟和断桥、结尾返回控件已在实际游戏代码中实现，并经 [dev Unity ORK P0 Run 43991999900](https://github.com/jinze909/rpg/actions/runs/43991999900) 对 `591222ee949543f731b647ba35b5c8f2957f26f3` 验证。**它不是 v1.2 的完整叙事 P0 剧本**；现行 v1.2 写作稿不能被旧 S00 工程通过结果冒充已实现。
- **待整合的独立候选（不是 dev 已交付）**：桥头重试单次消费 [PR #64](https://github.com/jinze909/rpg/pull/64)，SHA `78db1809dbbe337e0ea498c43ddccd757cd29a92`，[Run 43993206310](https://github.com/jinze909/rpg/actions/runs/43993206310) 成功；超宽屏入口布局的最新整合候选 [PR #65](https://github.com/jinze909/rpg/pull/65)，SHA `75a12212a2b63d30976ce514f5f8ad1ac7b96b79`，[Run 43994385748](https://github.com/jinze909/rpg/actions/runs/43994385748) 成功。两者均基于先前 dev；因 v1.2 文档交付后 dev 已前移，必须在新 HEAD 整合并验证**新最终 SHA**，不得直接引用旧绿色结论交付。
- **下一步**：按 v1.2 为写作权威继续 P0／M00 连续性和落地评估；并接续 PR #64、#65 的最新 dev 整合与验收。名册对账、点名 UI、听雾操作及新版可玩 P0 仍须独立制作／验收，不因 39 份稿件已入库而标为完成。
- **智能体验证边界**：有真实输入、画面、音频或操作证据时报告适用范围；AI 自动通关和技术绿色不能证明真人乐趣，见[问题记录](production/KNOWN_ISSUES.md)。

历史工程细节与证据保留在故事卡和 Actions，不从旧接力记录恢复废弃剧情。

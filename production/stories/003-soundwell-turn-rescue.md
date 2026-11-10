# 003 · 三人短回合声井救援

状态：进行中。002 的真人试玩待做；003 已完成三人 ORK 队伍/单场 Turn Based Battle 接线及声井摇铃、包铃、撤退、重试自动回归，尚未完成两条独立救援策略的完整验收。设计依据见[首样本](../../docs/gameplay/FIRST_PLAYABLE.md)。

## 玩家得到什么

从现有声井入口进入一次移动版短回合救援。玩家指挥三名占位角色，看到借声兽的下一次威胁，能引兽离开撤离线，也能通过保护和攻击正面牵制，再将测路人带到同一个安全点。

## 实现范围

- 复用现有声井场地、两处命名铃位、闸门和撤离线。进入遭遇后按回合推进威胁，沿用三人固定个人顺序、移动额度和一次主要行动的原型默认。
- 先接通真实 ORK 战斗员、回合及行动结算，Makinom 接交互，Unity 和 UGUI 呈现状态。两种方案共用初始队伍、威胁、主要行动预算和救援目标。可撤回并恢复同一起点重试。
- 本卡交付移动版遭遇。固定阵位对照、成长与持久化留给后续切片，完整样本仍需验收这些结果。

## 工程验收

- 在最终候选的干净检出上，Unity 6000.6.8f1 导入编译通过，002 路径继续可用。
- Unity Play Mode 从实际入口操作三名角色。分别通过引兽和正面牵制完成救援，验证两条路径从同一起点出发，测路人到安全点才算成功。
- 验证威胁预告和结算一致，撤回后可从原始角色、威胁与声源状态重试。记录完整 SHA、Actions run 与玩家路径结果。首次玩家的理解和乐趣仍待真人试玩。


## 当前工程进展（2026-11-10）

- 已在 Unity 6000.6.8f1 的最终 SHA `0fb1389c9df1e50d41eef73551e25d0c391c1a43` 上通过 5/5 Editor tests、独立 Bootstrap batch 检查及 `passed-clean-candidate`：[Run 42740645793](https://github.com/jinze909/rpg/actions/runs/42740645793)。
- 已验证三名场景玩家注册为同一个 ORK PlayerGroup，借声兽注册为 EnemyGroup，三名玩家和敌人进入同一个 ORK Turn Based Battle；声井摇铃、包铃、撤退、重试的自动化路径通过。
- 未验证/未完成：从同一起点分别通过引兽撤离和正面牵制两条策略完成救援；战术差异、玩家理解与乐趣的真人试玩。不得将上述自动化结果视为整张 003 完成。


## 2026-11-10 Direct-combat route increment

- The existing gate-panel interaction now offers a direct route before the player commits to the bell route. The route starts the saved three-member ORK player group against the Soundwell beast in the scene's saved Turn Based battle system.
- During battle, the Soundwell exploration controls and trial HUD are suspended; the ORK battle UI owns navigation and action selection. The party's follow formation also pauses while its members are in battle.
- The same gate can now be opened by either a confirmed ORK victory or the existing side-bell lure condition. The selected approach is stored in Makinom variables; the ORK beast's `Status.IsDead` state, not a UI flag, is the direct-route victory condition.
- The editor integration test enters the direct route through the actual gate interaction, checks all four battle participants and a player turn, then exercises ORK's combatant death state to verify that only a resolved victory unlocks the gate.
- Still incomplete: a distinct protect/guard action during the direct battle, and a full player-input play-through of both rescue routes through surveyor escort and retreat. This increment does not close Story 003.

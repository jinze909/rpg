# M1–M11 迁移阶段历史索引（非永久保留的分支）

**真实进度更正（2026-11-10，`dev` `790266185...`）：** M1–M11 的工程贡献**全部 11/11 已实际进入 dev 提交历史**；不等于全部完成验收。**关闭 Issue：M1/M10/M11（3/11）；仍 open：M2–M9（8/11）**。旧文档的「M3–M11 未实施」「M8 尚未合入/Run 仍在运行」「当前必须先迁移再开发故事」等叙述已失效。各阶段最终 SHA、Unity CI、实际功能与剩余门槛以[逐项事实核查](MODERNIZATION_VERIFIED_STATUS.md)为准；后续须读取实时 HEAD 和最新 CI。


**仓库：** `jinze909/rpg` · **集成目标：** `codex/dev` · **唯一主追踪：** [GitHub Issue #2](https://github.com/jinze909/rpg/issues/2) · **技术验收规则：** [MODERN_2D_RPG_STACK.md](MODERN_2D_RPG_STACK.md)

2026-11-10 用户已取消 2026-11-09 的“M1–M11 最高优先于故事 003”旧约定。**本索引是历史/技术接力资料，不是必须先完成全部 M 阶段的开发队列。** 可以根据实时依赖和价值选择故事 003、其他玩法/剧情或技术迁移；只有存在真实前置依赖时才需要先完成相应阶段。CI 排队/运行时保持被测 SHA 不变并接续已有运行，仍可推进无依赖的独立候选；失败要查实修复，不能冒用旧绿灯。

## M1–M11 历史分支名称（仅需时保留）

本表是命名及历史状态记录，**不保证分支目前存在，也不代表当下验收状态**；每轮以实时 GitHub、Issue 和 Actions 为准。

| 阶段 | 历史名称（可能已清理） | 追踪票 | 当时状态 |
| --- | --- | --- | --- |
| M1 URP 2D | [`codex/pending/m1-urp2d`](https://github.com/jinze909/rpg/tree/codex/pending/m1-urp2d) | [#3](https://github.com/jinze909/rpg/issues/3) | **已合入，#3 closed**；同 SHA CI 通过 |
| M2 New Input System | [`codex/pending/m2-new-input-system`](https://github.com/jinze909/rpg/tree/codex/pending/m2-new-input-system) | [#4](https://github.com/jinze909/rpg/issues/4) | **核心已合入，#4 open**；UI Toggle 整合尚待入 dev |
| M3 Cinemachine 3 | [`codex/pending/m3-cinemachine3`](https://github.com/jinze909/rpg/tree/codex/pending/m3-cinemachine3) | [#5](https://github.com/jinze909/rpg/issues/5) | **已合入，#5 open**；镜头/截图验收待核 |
| M4 2D Animation | [`codex/pending/m4-2d-animation`](https://github.com/jinze909/rpg/tree/codex/pending/m4-2d-animation) | [#6](https://github.com/jinze909/rpg/issues/6) | **已合入占位 Clips/Animator，#6 open**；真实动画待验 |
| M5 Tilemap Extras | [`codex/pending/m5-tilemap-extras`](https://github.com/jinze909/rpg/tree/codex/pending/m5-tilemap-extras) | [#7](https://github.com/jinze909/rpg/issues/7) | **已合入实际墙 RuleTile/Collider，#7 open**；全路径待验 |
| M6 Audio Mixer | [`codex/pending/m6-audio-mixer`](https://github.com/jinze909/rpg/tree/codex/pending/m6-audio-mixer) | [#8](https://github.com/jinze909/rpg/issues/8) | **已合入 mixer/SFX 路由，#8 open**；真实听感待验 |
| M7 Sprite Atlas V2 | [`codex/pending/m7-sprite-atlas-v2`](https://github.com/jinze909/rpg/tree/codex/pending/m7-sprite-atlas-v2) | [#9](https://github.com/jinze909/rpg/issues/9) | **已合入原生 V2 图集，#9 open**；性能对照待补 |
| M8 Pixel Perfect | [`codex/pending/m8-pixel-perfect`](https://github.com/jinze909/rpg/tree/codex/pending/m8-pixel-perfect) | [#10](https://github.com/jinze909/rpg/issues/10) | **已合入像素相机，#10 open**；dev CI 成功，视觉待验 |
| M9 Localization | [`codex/pending/m9-localization`](https://github.com/jinze909/rpg/tree/codex/pending/m9-localization) | [#11](https://github.com/jinze909/rpg/issues/11) | **已合入 EN/zh 自有 UI，#11 open**；全文案待覆盖 |
| M10 Memory Profiler | [`codex/pending/m10-memory-profiler`](https://github.com/jinze909/rpg/tree/codex/pending/m10-memory-profiler) | [#12](https://github.com/jinze909/rpg/issues/12) | **已合入 Editor 多阶段指标，#12 closed** |
| M11 Build Profiles | [`codex/pending/m11-build-profiles`](https://github.com/jinze909/rpg/tree/codex/pending/m11-build-profiles) | [#13](https://github.com/jinze909/rpg/issues/13) | **已合入 Debug/Test Linux Profiles 和构建，#13 closed** |

**历史初始 HEAD（仅供追溯）：** M1 `c3e323a3e18e57396ef39289efb5c125be410481`、M2 `81a6fe97e347aff20cb2d7bc1135b5082b4f098e`；M3–M11 最初从旧 dev SHA `bf2474b455a9756ae8dd41c5a28f198554575bb4` 建立。**历史 SHA 绝不表示分支必须保留，也不是当前验收证据**。继续阶段工作时先核对实时 HEAD：未合入候选可接续，已合入旧头不能再续写，须从最新 dev 新建不同名称的候选。

## 历史候选与当时的下一步（非实时 CI 结论）

- **当时 M2 实际候选（历史记录）**：[`codex/pending/m2-ugui-focus`](https://github.com/jinze909/rpg/tree/codex/pending/m2-ugui-focus)，测试 SHA `4d263e0ce7a3cf9401a1e9dcafb13a74207901c6`，[Unity Run 42777305240](https://github.com/jinze909/rpg/actions/runs/42777305240) 最近查询仍在运行。下一轮**先重新查询状态**：失败查明步骤并修复；通过核对同 SHA 真实 Unity 结果、artifacts 和 `passed-clean-candidate`，再安全合入。不能把旧绿色 SHA 代替这次验收。
- **历史计划（非当前强制下一步）**：当时建议 M2 之后做 M3 Cinemachine 3；现在不需要依 M 编号推进，下一项按实时故事/技术价值选择。若 dev 前移导致正在受测的候选不能直接快进，则保留真实证据，将未交付差异转移到独立集成候选，并对该候选最终 SHA 重新验收，不强推或冒用旧结果。

## 跨轮接力与单增量候选生命周期

1. 先读取 `CHATGPT.md`、`AGENTS.md`、`AI2-STATUS.md`、`BACKLOG.md`、本索引及总票 #2；查询 `codex/dev` 与当前阶段的**实时完整 SHA** 和相关 CI。文档列出的 SHA 是历史定位，不是可替代实时核对的事实。
2. M2→M3→…→M11 只是历史编号，不代表全局优先顺序。若候选之间**确有技术依赖**，下游在上游通过实际 Unity 验证并安全进入 dev 后才能合入；与之无关的故事、玩法或其他迁移候选可独立推进。CI 正运行时不要修改被测 SHA，避免并行运行时权威冲突。
3. 只续接**尚未合入**的活动候选（含 CI/失败修复）；先确认远端 HEAD，若 dev 前移只能安全整合并重新验证，不强推。**已合入的旧候选永久关闭，不再接受新工作**；阶段或故事尚有后续增量时，从最新 dev 用不同名称新建候选。
4. M1–M11 不再永久保留阶段分支：每次候选真实合入 `codex/dev` 后，**仅检查本次候选及直接相关已完全取代的旧候选**，不对全部 pending 例行扫描。安全条件和禁止强推见[统一生命周期](../../production/workflow.md#候选分支清理)；有独有未交付工作者必须保留。
5. 各阶段验收必须记最终候选 SHA、CI Run、真实断言和截图/Artifacts（如适用）、最终合入 SHA 与独立下一步。失败继续修；无法实施的条件阶段在 Issue 中记录实证、理由与可再评估的触发门槛，不伪称已安装。
6. 充分验证后允许修改 ORK/Makinom 框架、配置和数据，按[统一验证规则](../../production/workflow.md#框架修改与验证)执行；保留 UGUI；零新增付费、零未经授权调度/可见性/计费变更；不强推。不把 Addressables、ECS/DOTS、HDRP 视为 M1–M11 当前迁移任务。

**本索引属于技术依赖与历史接力参考，不是迁移优先级命令或永久分支保留清单。** 用户已授权清理完成的 M1–M11 阶段头；这些阶段与故事增量由当前价值及真实依赖决定执行顺序。既有同 SHA CI 验收要求继续适用；最新状态由 Issue #2、故事卡与 GitHub Actions 决定。

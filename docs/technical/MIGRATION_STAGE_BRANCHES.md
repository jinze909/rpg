# M1–M11 迁移阶段分支索引（用户明确要求全部 pending 开头）

**仓库：** `jinze909/rpg` · **集成目标：** `codex/dev` · **唯一主追踪：** [GitHub Issue #2](https://github.com/jinze909/rpg/issues/2) · **技术验收规则：** [MODERN_2D_RPG_STACK.md](MODERN_2D_RPG_STACK.md)

2026-11-09 用户新决定：**将 M1–M11 现代 2D RPG 迁移作为后续 AI 接力的最高工程开发优先级**，优先于独立故事 003、剧情扩写、一般功能拓展；仅真实阻断迁移的编译、CI、用户路径回归先修复。每轮必须从当前阶段/候选/对应 Run 接着推进，不能因为 CI 仍在跑就跳到故事开发。CI 仍在运行时不改被测 SHA，可独立审查后续阶段，但不能让尚未验收的阶段进 `codex/dev`。

## 固定的 11 个 pending 阶段头

| 阶段 | 预留阶段分支 | 追踪票 | 实际状态 |
| --- | --- | --- | --- |
| M1 URP 2D | [`codex/pending/m1-urp2d`](https://github.com/jinze909/rpg/tree/codex/pending/m1-urp2d) | [#3](https://github.com/jinze909/rpg/issues/3) | **已完成并并入 dev**；旧分支保留历史，不重做 |
| M2 New Input System | [`codex/pending/m2-new-input-system`](https://github.com/jinze909/rpg/tree/codex/pending/m2-new-input-system) | [#4](https://github.com/jinze909/rpg/issues/4) | **当前阶段**；核心已并入 dev，UGUI/ORK 适配待终验 |
| M3 Cinemachine 3 | [`codex/pending/m3-cinemachine3`](https://github.com/jinze909/rpg/tree/codex/pending/m3-cinemachine3) | [#5](https://github.com/jinze909/rpg/issues/5) | 预留，未实施 |
| M4 2D Animation | [`codex/pending/m4-2d-animation`](https://github.com/jinze909/rpg/tree/codex/pending/m4-2d-animation) | [#6](https://github.com/jinze909/rpg/issues/6) | 预留，未实施 |
| M5 Tilemap Extras | [`codex/pending/m5-tilemap-extras`](https://github.com/jinze909/rpg/tree/codex/pending/m5-tilemap-extras) | [#7](https://github.com/jinze909/rpg/issues/7) | 预留，未实施 |
| M6 Audio Mixer | [`codex/pending/m6-audio-mixer`](https://github.com/jinze909/rpg/tree/codex/pending/m6-audio-mixer) | [#8](https://github.com/jinze909/rpg/issues/8) | 预留，未实施 |
| M7 Sprite Atlas V2 | [`codex/pending/m7-sprite-atlas-v2`](https://github.com/jinze909/rpg/tree/codex/pending/m7-sprite-atlas-v2) | [#9](https://github.com/jinze909/rpg/issues/9) | 预留；需要资源/性能证据 |
| M8 Pixel Perfect | [`codex/pending/m8-pixel-perfect`](https://github.com/jinze909/rpg/tree/codex/pending/m8-pixel-perfect) | [#10](https://github.com/jinze909/rpg/issues/10) | 预留；仅确定像素风后采用 |
| M9 Localization | [`codex/pending/m9-localization`](https://github.com/jinze909/rpg/tree/codex/pending/m9-localization) | [#11](https://github.com/jinze909/rpg/issues/11) | 预留；先解决文本权威 |
| M10 Memory Profiler | [`codex/pending/m10-memory-profiler`](https://github.com/jinze909/rpg/tree/codex/pending/m10-memory-profiler) | [#12](https://github.com/jinze909/rpg/issues/12) | 预留；需可复现测量 |
| M11 Build Profiles | [`codex/pending/m11-build-profiles`](https://github.com/jinze909/rpg/tree/codex/pending/m11-build-profiles) | [#13](https://github.com/jinze909/rpg/issues/13) | 预留；目标平台与构建验证 |

**保留原头：** M1 `c3e323a3e18e57396ef39289efb5c125be410481`、M2 `81a6fe97e347aff20cb2d7bc1135b5082b4f098e` 已有历史，未移动或覆盖。**新建头：** M3–M11 统一从建分支时 `codex/dev` SHA `bf2474b455a9756ae8dd41c5a28f198554575bb4` 创建，均为用户要求的预留分支，**无迁移提交、无 CI 验收**。这些基线会在开始各阶段时依安全快进规则更新，不应据此恢复旧工程状态。

## 当前活跃候选与唯一下一步

- **M2 实际工作候选**：[`codex/pending/m2-ugui-focus`](https://github.com/jinze909/rpg/tree/codex/pending/m2-ugui-focus)，测试 SHA `4d263e0ce7a3cf9401a1e9dcafb13a74207901c6`，[Unity Run 42777305240](https://github.com/jinze909/rpg/actions/runs/42777305240) 最近查询仍在运行。下一轮**先重新查询状态**：失败查明步骤并修复；通过核对同 SHA 真实 Unity 结果、artifacts 和 `passed-clean-candidate`，再安全合入。不能把旧绿色 SHA 代替这次验收。
- M2 通过并整合后开始 **M3 Cinemachine 3**。如果 `codex/dev` 由于独立文档提交前移，现有 M2 候选将不能直接快进合入：保留已通过的 SHA 证据，把实际差异转移到**新且可快进的集成候选**，再按新候选最终 SHA 重新运行必要验证，禁止强推或冒充旧证据适用于新 SHA。

## 每轮接力与保留规则

1. 先读取 `CHATGPT.md`、`AGENTS.md`、`AI2-STATUS.md`、`BACKLOG.md`、本索引及总票 #2；查询 `codex/dev` 与当前阶段的**实时完整 SHA** 和相关 CI。文档列出的 SHA 是历史定位，不是可替代实时核对的事实。
2. 迁移顺序按 M2→M3→M4→M5→M6→M7→M8→M9→M10→M11，前项在需要依赖时必须通过实际 Unity 结果并安全合入才执行后项。当前 Run 进行中不修改被测 HEAD，允许独立阶段准备，但不得制造两套运行时执行者。
3. 激活未修改的预留阶段分支前，检查其 head 是否为 `codex/dev` 祖先，仅用 expected SHA、`force=false` **快进到新 dev 基线**。有独有提交的旧阶段头不强推或覆盖，可额外建立 `codex/pending/mN-集成主题` 候选并保持原头以供追踪。
4. 11 个固定阶段头用户明确要求长期保留：**不得按常规 pending 清理策略自动删除**。临时集成/修复候选按 `production/workflow.md` 正常清理。
5. 各阶段验收必须记最终候选 SHA、CI Run、真实断言和截图/Artifacts（如适用）、最终合入 SHA 与独立下一步。失败继续修；无法实施的条件阶段在 Issue 中记录实证、理由与可再评估的触发门槛，不伪称已安装。
6. 不升级或修改 ORK 3.22.0 / Makinom 2.21.0 厂商文件或设置；保留 UGUI；零新增付费、零未经授权调度/可见性/计费变更；不强推。不把 Addressables、ECS/DOTS、HDRP 视为 M1–M11 当前迁移任务。

**本索引属于长期接力契约。** 更改 M1–M11 分支命名、迁移优先级或删除任一预留头须用户新的明确指令；阶段实际完成状态由同 SHA Unity 和 Issue #2 的实时证据确认。

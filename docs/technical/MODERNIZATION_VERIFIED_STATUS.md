# M1–M11 工程实施及验收事实核查

**核查日期：2026-11-10（美国太平洋时间）。基线：[`codex/dev` `790266185e064ac49557be5deac1bf152edaff8d`](https://github.com/jinze909/rpg/commit/790266185e064ac49557be5deac1bf152edaff8d)。** 此页是这次核对远端提交历史、仓库资产、真实 Unity Actions、Issues #3–#13 的结果。以后须重新读取实时 HEAD，不能把历史候选 SHA、旧 Run 的「运行中」当作实时事实。

## 严格口径

- **11/11：所有 M1–M11 至少有一个实际工程提交成为 `codex/dev` 祖先。** 仓库具备各域对应源码/资产和 Unity 测试，不能再把 M3–M11 概括为「仅计划」。
- **3/11 原票已关闭：M1、M10、M11。8/11 仍开放：M2–M9。** 提交进入 dev、局部测试绿色或关闭 Issue 是三种不同事实，不能推断全部需求与玩家体验均通过。
- 对 `prepare/publish/verify` CI，以 `verify-<最终完整SHA>` 产物及作业成功作为同 SHA 证据；`bootstrap` 直接运行的成功也要检查对应 SHA 的 artifact 和断言。Play/Editor 自动测试不代表真人试玩、主观听感、各宽高比截图已被审查或性能收益已量化。

| 阶段 / Issue | 已落地到 dev 的事实及 CI | 原任务未完成范围、票据状态 |
| --- | --- | --- |
| **M1 URP 2D** [#3](https://github.com/jinze909/rpg/issues/3) | 真实 2D Renderer/URP 设置，最终资产 `3a346522...` [Run 42744816128](https://github.com/jinze909/rpg/actions/runs/42744816128) prepare→publish→**最终 SHA verify 成功** | **closed**，该工程范围已完成 |
| **M2 New Input / UGUI** [#4](https://github.com/jinze909/rpg/issues/4) | 自有 InputActions/适配器、UGUI 已入 dev；`e0dd7cc...` [Run 42778774220](https://github.com/jinze909/rpg/actions/runs/42778774220) 成功。新的 UI Toggle 整合候选 `d667a9f...` [Run 42940261620](https://github.com/jinze909/rpg/actions/runs/42940261620) 成功，但此核查时**尚未合入 dev** | **open**：修复/整合当前 UI Toggle/焦点回归、补两条救援路径与实际键鼠手柄体验验收 |
| **M3 Cinemachine 3** [#5](https://github.com/jinze909/rpg/issues/5) | 唯一 Brain/Virtual Camera、镜头边界和比例测试，`ca3652f...` [Run 42785942604](https://github.com/jinze909/rpg/actions/runs/42785942604) 8/8 与 Play/Batch 成功 | **open**：完整场景中的多比例真实截图、战斗与重载镜头验收仍需逐项对照原票 |
| **M4 2D Animation** [#6](https://github.com/jinze909/rpg/issues/6) | 保存 Idle/Move/Action/Hit 动画 Clips 与 Animator，`f34a180...` [Run 42794568707](https://github.com/jinze909/rpg/actions/runs/42794568707) 最终 SHA verify 成功 | **open**：目前是占位角色的 Transform 曲线表现，不等于真实逐帧/方向/攻击受击质量完成 |
| **M5 Tilemap Extras** [#7](https://github.com/jinze909/rpg/issues/7) | 声井墙面的真实 RuleTile、TilemapRenderer、TilemapCollider2D，`44f37f4...` [Run 42802244276](https://github.com/jinze909/rpg/actions/runs/42802244276) 8/8 + Play/Batch 成功、物理射线断言 | **open**：完整救援/回撤/重载路线与相关截图、没有重复碰撞的端到端证据待归档确认 |
| **M6 Audio Mixer** [#8](https://github.com/jinze909/rpg/issues/8) | 原生 `SoundwellMixer.mixer`、Music/Ambient/SFX、铃声 SFX 路由和静音接口；`7c5bb4a...` [Run 42803799190](https://github.com/jinze909/rpg/actions/runs/42803799190) 最终 SHA verify 成功 | **open**：真实音频输出、可听静音/恢复、重载体验未由 CI 自动证明 |
| **M7 Sprite Atlas V2** [#9](https://github.com/jinze909/rpg/issues/9) | 保存 `SoundwellWalls.spriteatlasv2`、原 Sprite GUID 与 Packer V2；`01d7dda...` [Run 42864872330](https://github.com/jinze909/rpg/actions/runs/42864872330) 9/9、最终 SHA verify 成功 | **open**：尚无受控前后 Draw Call/纹理内存对照；不可宣称性能增益 |
| **M8 Pixel Perfect** [#10](https://github.com/jinze909/rpg/issues/10) | `SoundwellCameraRig` 采用 16 PPU、320×180、URP PixelPerfectCamera、CinemachinePixelPerfect，旧修复 `115c718...` 已是 dev 祖先；[dev Run 42935131913](https://github.com/jinze909/rpg/actions/runs/42935131913) 对实际 dev SHA `17ae583...` 通过 9/9、Play/Batch，含 `RPG_SOUNDWELL_PIXEL_PERFECT_VALIDATED` | **open**：旧专用 [Run 42877178888](https://github.com/jinze909/rpg/actions/runs/42877178888) **确实失败**，不能抹除；但「尚未合入」已过期。尚缺不同视口真实截图由智能体审查、抖动/裁切/全角色素材像素一致性验收 |
| **M9 Localization** [#11](https://github.com/jinze909/rpg/issues/11) | Unity EN/zh StringTables、CJK 字体和自有 HUD 切换，`d4ec33f...` [Run 42818630008](https://github.com/jinze909/rpg/actions/runs/42818630008) 成功；后续 dev Run 42935131913 含 `RPG_SOUNDWELL_LOCALIZATION_PLAY_VALIDATED` | **open**：首批 UI 已覆盖，未证明全体战斗/任务/剧情/ORK 文本迁移完毕；fallback、字体和截图继续核验 |
| **M10 Memory Profiler** [#12](https://github.com/jinze909/rpg/issues/12) | `SoundwellMemoryProbe.cs` 使用 Unity Profiler/GC 在真实 Editor Play 路径采多阶段指标并输出可消费 JSON，`d913c2d...` [Run 42820333763](https://github.com/jinze909/rpg/actions/runs/42820333763) 最终 SHA verify 成功 | **closed**：这是 Linux Editor 真实指标，**不是原生 .snap 或玩家设备测量** |
| **M11 Build Profiles** [#13](https://github.com/jinze909/rpg/issues/13) | 保存 Linux Debug/Test Build Profiles、验证实际 Linux Player 构建；`12a94ed...` [Run 42830867419](https://github.com/jinze909/rpg/actions/runs/42830867419) 最终 SHA verify，日志有 `RPG_SOUNDWELL_LINUX_PLAYER_BUILT` | **closed**：Release/其他平台尚未列为已验证 |

## 后续执行约束

1. 不重新迁移已在 dev 落地的工程；M2–M9 的 open 主要代表**验收/完整范围缺口**，按具体差异完成，不为了凑 11/11 关闭率而虚报。
2. 2026-11-10 用户已**取消「M1–M11 必须优先于故事 003」**。下一项可选故事、玩法或技术，依据实时收益、真实依赖和候选 CI；CI 运行时保持被测 SHA 不动，独立增量可继续。
3. 严格保留同最终 SHA 的 Unity CI、一个候选一次增量、合入后局部自动安全清理；历史分支名和旧 Issue 描述不得覆盖这里的有证据事实。

2026-11-11 执行规则：上述主观体验/媒体证据缺口由智能体记录和推进，不要求用户参与或提交反馈；没有证据不得宣称通过。框架与配置可经[充分验证](../../production/workflow.md#框架修改与验证)修改，旧 Issue 的冻结措辞不再适用。

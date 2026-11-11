# 现代 2D RPG 工作室技术基线（零付费新增依赖）

**真实进度更正（2026-11-10，`dev` `790266185...`）：** M1–M11 的工程贡献**全部 11/11 已实际进入 dev 提交历史**；不等于全部完成验收。**关闭 Issue：M1/M10/M11（3/11）；仍 open：M2–M9（8/11）**。旧文档的「M3–M11 未实施」「M8 尚未合入/Run 仍在运行」「当前必须先迁移再开发故事」等叙述已失效。各阶段最终 SHA、Unity CI、实际功能与剩余门槛以[逐项事实核查](MODERNIZATION_VERIFIED_STATUS.md)为准；后续须读取实时 HEAD 和最新 CI。


> **用户明确决定，2026-11-09。** 这是今后技术选型和升级的约束与实施路线，不是“已安装清单”或本次迁移完成声明。以 `codex/dev` 的实时 HEAD、Unity 实际导入结果和证据为准；首次仓库审查基线为 `790d9486dd8e163c278adefb9c25ae1be5821aa9`。

## 1. 不可突破的约束

1. **充分验证后允许修改框架及配置（2026-11-11）。** 当前 ORK 3.22.0 / Makinom 2.21.0 是可复现基线，代码、DLL、设置、数据和必要版本调整均可因明确需求在隔离候选处理，不再只读或禁止配置修改。按[统一验证规则](../../production/workflow.md#框架修改与验证)记录需求、原包/补丁来源、影响、回退与同最终 SHA 回归，充分验证后合入，无需逐次确认；保持单一权威、许可和零新增成本。
2. **新增购买成本为零。** 优先 Unity 当前编辑器许可内已包含功能、Unity Registry 官方已发布包、经核实可商用的免费/开源工具。禁止未经用户新授权的插件、素材、升级费、付费订阅、席位、云服务、付费构建或新收费依赖；检查许可、平台限制和 CI 配额。既有 ORK 许可仍适用，不以免费方案绕过许可。
3. **现有 Unity/UGUI 为基础，不迁移 UI Toolkit。** 当前 Unity `6000.6.8f1`、UGUI、SpriteRenderer/2D、Makinom Schematics + ORK 为权威。不要为了新技术无目的升级 Unity、拆毁现有玩法和剧情，或同时让两套系统长期竞争同一状态。
4. **现代化须为可玩性、可维护性、性能或交付服务。** 不按包的数量和“最新”排序。明确玩家收益、影响范围、替代/保留的旧路径、回滚办法、真实验收和资源开销。没有理由时不安装。
5. **一个改动域一个候选；保持 dev 可验证。** 先修当前阻塞、再逐项迁移；不把 URP、输入、镜头、动画和框架重构捆绑。项目设置、场景、资产、GUID、`.meta`、`packages-lock.json` 成对提交；同一最终 SHA 必须通过相关检查才整合。切换渲染/输入需复验 ORK 原有功能，需要修改 ORK 兼容行为时必须先完成框架修改验证。
6. 不创建或修改自动化调度、存储计费选项、云收费资源。保持现有仓库可见性及许可/凭据规则。除非用户另有明确授权，**以上约束优先于旧文档里泛称“ORM/ORK 更新”“使用最新框架”的提议**。

## 2. 工程状态来源

M1–M11 已有工程贡献，部分阶段仍有范围/验收缺口。逐阶段证据见[实施事实核查](MODERNIZATION_VERIFIED_STATUS.md)，当前交付及新增改动见[AI2-STATUS](../../AI2-STATUS.md)。实际版本、组件和场景读取本轮完整 SHA 的 manifest、锁文件、ProjectSettings 与资产，不能依据初始“未安装”清单重新迁移。

当前采用 Unity 6000.6.8f1、ORK/Makinom、URP 2D、UGUI、Input System 适配及单一镜头控制路径；其余已落地技术与剩余验收按上述记录核对，不在本文件复制另一份“最新候选”。

## 3. 技术路线：按价值与风险实施，官方免费优先

| 顺序 | 方案 / 官方包或内置能力 | 预期收益 | 采用触发条件与边界 |
| --- | --- | --- | --- |
| P0 | **修复当前游戏与 CI**（既有工具） | 保住 003 玩家路径、相机、UI、场景重载和 ORK 回归 | 阻塞必须先修；不借现代化绕开失败 |
| P1 | **URP 2D**（`com.unity.render-pipelines.universal`） | 现代 2D Renderer、灯光与后续图形扩展 | **独立迁移分支**；先保存当前 Built-in 证据/回退；用 2D Renderer，检查不兼容 Shader、材质和相机截图。转换器可能不可逆，不在 dev 直接点击批量转换；涉及 ORK 资产时按框架验证规则处理 |
| P1 | **Input System**（`com.unity.inputsystem`） | Actions、键鼠/手柄统一、重绑定、设备切换 | 先 `Active Input Handling = Both`，在游戏自有输入适配层渐进替换旧 `Input.*`。ORK/Makinom 旧路径必须仍可用；消除重复输入及焦点冲突，禁止直接切为 New-only |
| P1 | **Cinemachine 3**（`com.unity.cinemachine`） | 2D 跟随、边界、转场、剧情镜头 | 只允许一个摄像机控制者；替换游戏自有手写位置/缩放逻辑时回归截图、宽高比与战斗镜头；涉及 ORK 时按框架验证规则处理 |
| P1/P2 | **正式 2D 动画流水线**（Animator、Animation Clips；视需求采用 `com.unity.2d.animation`） | 待机/移动/攻击/受击、动画状态一致、减少占位感 | 美术先定逐帧或骨骼；优先制作一个角色闭环，验证 Pivot、帧尺寸、朝向、武器/技能对齐；不要强推骨骼以替代逐帧 |
| P2 | **Tilemap + Rule Tiles**（官方 `com.unity.2d.tilemap.extras`） | 地形自动拼接、碰撞/排序与可维护关卡 | 场景扩张时引入；优先做一张真实区域并验证碰撞、导航、障碍和保存重载 |
| P2 | **Audio Mixer / Snapshots**（Unity 内置） | 音乐、环境、SFX 分组与场景/战斗过渡 | 现有 AudioSource 仍可用；制作同一混音总线、静音和音量设置，检查实际播放器的听感，不引入付费音频插件 |
| P2/P3 | **Sprite Atlas V2**（Unity 现有精灵图集流程） | 精灵批处理、内存/纹理组织 | 精灵数和 draw call 有明确证据后使用；验证 Atlas/纹理压缩、SpriteRenderer 显示与资源加载，不为“现代”而空建图集 |
| 按美术风格 | **2D Pixel Perfect**（`com.unity.2d.pixel-perfect`） | 像素画面在常见分辨率下保持清晰和稳定 | **只在决定严格像素风时**启用；与 Cinemachine/URP 2D 的实际版本进行组合验证 |
| 中后期 | **Localization**（`com.unity.localization`） | 可测试的多语言 UI、对白和资源 | 先画清与ORK 文本/语言管理的权威边界；只能有一个最终文本来源，未解决冲突时不要加入双轨 |
| 按需 | **Memory Profiler**（`com.unity.memoryprofiler`）、Profiler/Frame Debugger（内置） | 内存快照、加载/GC 分析与性能追踪 | 性能确有问题时测量；记录 Unity 版本、平台与复现环境；已有 CI 指标不等于玩家设备基准 |
| 正式发布前 | **Build Profiles**（Unity 6 内置） | 分开 Debug/Test/Release 和不同平台的设置 | 只有目标平台与可复现构建存在时建档；检查构建所需模块/许可证/成本，不自动启用收费目标平台 |
| 暂缓 | **Addressables**（`com.unity.addressables`） | 大规模/远程资源、按需加载 | 当前固定且较小的 2D 游戏继续使用场景/直接引用。只在实际加载/内存/内容更新需求出现后，引入可验证的资源标签、加载释放与打包策略 |
| 暂缓 | ECS/DOTS、HDRP、额外云服务或付费资源 | 对当前短回合 2D RPG 未证明有净收益 | 不列为必装；新增仅在明确性能/平台指标和用户授权下评估 |

上表列的是**候选能力而非采购清单或立即全部安装**。使用 Unity Registry 中与 **6000.6.8f1** 兼容且已发布的版本；先由该 Unity Editor/Package Manager 确认依赖，再锁 `manifest.json` 与 `packages-lock.json`。**不能编造“最新兼容版本号”或因免费包而无条件升级现有依赖。**

## 4. 每一项现代化改造的完成定义

1. **前置检查：** 实时 `codex/dev` 干净可构建；确认当前候选状态，记录源 SHA、当前已有的资产与性能。正在失败的 003、丢失脚本、Console 编译错误先单独处理。
2. **范围与成本：** 给出该项对玩家的可见收益、要删除/代替的旧逻辑、零新增付费依赖的依据、许可和可能增量 CI 消耗；若不确定，保持未执行。
3. **隔离实现：** 每个领域独立 `codex/pending/*`；优先最小自有层改动；需要修改 `Assets/Gaming Is Love`、ORK/Makinom 设置或版本时按框架验证规则处理，不以目录禁止正常开发。改场景/材质/包需带 `.meta` 和 lockfile，保留已验证的回滚提交。
4. **QA 门槛：** Unity 6000.6.8f1 实际导入/编译；Bootstrap 启动与两次 Play、声井玩家路径、ORK 三人战斗及保存/重载（受影响时）不能回退；键鼠/手柄与 UGUI 焦点、不同分辨率、2D 渲染截图、音频听感按领域执行。自动截图、PlayMode 与智能体实际审查分别记证据；不派给用户，无法观测项写入问题记录。
5. **同 SHA 发布：** 具体测试命令、结果、Actions run 和 artifact 对应**同一个最终候选 SHA**。不得把测试跳过、零测试、未安装或候选之前的绿色 CI 记为完成。通过之后才安全快进/合并；有失败就继续修复候选。
6. **禁止重叠：** 新 Input 适配器与旧输入不得对同一操作重复响应；Cinemachine 与旧脚本不得同时控制镜头；UGUI/框架 UI 不双轨，文本/物品/任务/战斗/存档不产生平行权威。暂时兼容必须写明确移除条件。
7. **无费用变更：** 如需新付费插件、Unity Pro、平台许可、AI API、托管下载或额外 Actions 支出，记录具体外部阻塞，不实施收费方案，继续零新增费用替代方案。官方包的“可安装”不等于所有商业平台使用条件都免费。
8. **智能体玩家路径检查：** 现代化不等于玩法完成。智能体按故事卡检查两条救援策略、视觉反馈与实际响应；缺失的工具/主观体验证据记入问题记录，不要求用户参与，不将自动化等同乐趣实证。

## 5. 按价值、依赖与验收条件选择实施

**2026-11-10 用户已撤销 M1–M11 必须优先于故事/剧情/玩法的旧顺序。** 现代化阶段仍是可选工程任务，但不作为所有故事开发的前置或全局主线。根据玩家收益、实际缺陷、实时 CI 和技术依赖选题；正在运行的检查不得修改被测 SHA，失败需修复，同期允许推进独立候选。历史上曾为各阶段预留 `codex/pending/*`，参见[历史索引](MIGRATION_STAGE_BRANCHES.md)，但不再永久保留/续写已合入旧头。一个可验收增量一个候选，合入后局部清理。框架修改按充分验证规则；UGUI、零新增付费与同 SHA 验收保持。


- **已验收关闭：** M1 URP 2D、M10 Editor 多阶段内存采样、M11 Linux Debug/Test Profiles；并非只有 M1 落地。其余 M2–M9 也都有真实工程提交和 Unity 验证，但原票部分验收未完成。
- **工程状态而非实施队列：** New Input System、Cinemachine、Animation、Tilemap、Mixer、SpriteAtlas、Pixel Perfect、Localization 均已入 dev，相关 open Issue 只代表仍需完成原票验收；核对新 CI 与差异后才补做工作。
- **后续真实缺口（按需）：** M2 UI Toggle 整合/回归，M3 镜头截图，M4 真实角色动作，M5 完整救援/撤退路径，M6 听感，M7 性能，M8 多分辨率视觉，M9 文案覆盖。不能再把已落地的框架当作初次安装工作。
- **有充分证据后：** Sprite Atlas V2、Pixel Perfect（取决于美术）、Localization（先确认文本权威）、Memory Profiler、Build Profiles。
- **不强推：** Addressables、DOTS/ECS、HDRP；没有现阶段需求就不装。
- 既有用户决定：**ORK/Makinom 可经充分验证修改、无新增花费、UGUI 保留**。这是对任何旧的框架升级建议的明确覆盖。

## 6. 官方参考（先查与 Unity 6.6 当前兼容的版本）

- Unity 6 渲染管线配置与 Built-in 判定：https://docs.unity3d.com/6000.1/Documentation/Manual/srp-setting-render-pipeline-asset.html
- Unity Render Pipeline Converter（Built-in 2D → URP 2D；改动不可逆）：https://docs.unity3d.com/6000.0/Documentation/Manual/urp/features/rp-converter.html
- 新输入与旧 `Input` API：https://docs.unity3d.com/6000.0/ScriptReference/Input.html
- 输入处理 Both 设置：https://docs.unity3d.com/6000.0/Manual/UIE-Runtime-Event-System.html
- Cinemachine 官方包：https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.cinemachine.html
- Unity 2D 功能集与 Tilemap/Animation/Pixel Perfect：https://docs.unity3d.com/6000.0/Manual/2DFeature.html
- 已发布官方包清单：https://docs.unity3d.com/6000.0/Manual/pack-safe.html
- Sprite Atlas V2：https://docs.unity3d.com/6000.0/ScriptReference/SpritePackerMode.SpriteAtlasV2.html
- Audio Mixer Snapshots：https://docs.unity3d.com/6000.0/ScriptReference/Audio.AudioMixerSnapshot.html
- Memory Profiler：https://docs.unity3d.com/6000.0/Manual/com.unity.memoryprofiler.html
- Build Profiles：https://docs.unity3d.com/6000.0/Manual/create-build-profile.html
- Localization：https://docs.unity3d.com/6000.0/Manual/com.unity.localization.html
- Addressables 与直接资源引用如何选：https://docs.unity3d.com/6000.0/Manual/assets-managing-introduction.html
- Unity Registry 包安装与依赖解析：https://docs.unity3d.com/6000.0/Manual/upm-ui-install.html

参考是技术依据，不是现有依赖已安装或对应版本已获得项目级验证的证据。

# 现代 2D RPG 工作室技术基线（零付费新增依赖）

> **用户明确决定，2026-11-09。** 这是今后技术选型和升级的约束与实施路线，不是“已安装清单”或本次迁移完成声明。以 `codex/dev` 的实时 HEAD、Unity 实际导入结果和证据为准；首次仓库审查基线为 `790d9486dd8e163c278adefb9c25ae1be5821aa9`。

## 1. 不可突破的约束

1. **ORK/Makinom 冻结，不做版本升级，不动厂商部分。** 保持现有 **ORK Framework 3.22.0 + Makinom 2.21.0**、原始 DLL、源码 ZIP、`.meta`、GUID、插件目录、厂商设置与现有框架工程数据。不得因“更新/现代化”升级 ORK 3 小版本、改成 ORK 4、重新购买框架、重导入资产或编辑厂商源码；也不得偷偷引入平行战斗、任务、物品、存档系统。框架只读、游戏自己的适配层可在独立候选中改动和验证。**任何方案要求修改或重新授权 ORK/Makinom 时，停止该方案，不得自动执行。**
2. **新增购买成本为零。** 优先 Unity 当前编辑器许可内已包含功能、Unity Registry 官方已发布包、经核实可商用的免费/开源工具。禁止未经用户新授权的插件、素材、升级费、付费订阅、席位、云服务、付费构建或新收费依赖；检查许可、平台限制和 CI 配额。既有 ORK 许可是已有资产，不以“免费替换”推翻冻结。
3. **现有 Unity/UGUI 为基础，不迁移 UI Toolkit。** 当前 Unity `6000.6.8f1`、UGUI、SpriteRenderer/2D、Makinom Schematics + ORK 为权威。不要为了新技术无目的升级 Unity、拆毁现有玩法和剧情，或同时让两套系统长期竞争同一状态。
4. **现代化须为可玩性、可维护性、性能或交付服务。** 不按包的数量和“最新”排序。明确玩家收益、影响范围、替代/保留的旧路径、回滚办法、真实验收和资源开销。没有理由时不安装。
5. **一个改动域一个候选；保持 dev 可验证。** 先修当前阻塞、再逐项迁移；不把 URP、输入、镜头、动画和框架重构捆绑。项目设置、场景、资产、GUID、`.meta`、`packages-lock.json` 成对提交；同一最终 SHA 必须通过相关检查才整合。切换渲染/输入需复验 ORK 原有功能，不能修改 ORK 来追求兼容。
6. 不创建或修改自动化调度、存储计费选项、云收费资源。保持现有仓库可见性及许可/凭据规则。除非用户另有明确授权，**以上约束优先于旧文档里泛称“ORM/ORK 更新”“使用最新框架”的提议**。

## 2. 现状

| 领域 | 在审查基线上的事实 | 结论 |
| --- | --- | --- |
| 引擎 | `ProjectSettings/ProjectVersion.txt` 为 Unity `6000.6.8f1` | 固定，不主动改版 |
| RPG 数据与流程 | ORK 3.22.0 / Makinom 2.21.0；框架数据已由现有 Unity CI 验过 | **冻结** |
| 渲染 | Unity 6000.6.8f1；URP 17.6.0；GraphicsSettings 指向唯一项目 URP Asset，唯一 Renderer 为 Renderer2D；各 Quality override 清空 | **URP 2D 已在最终 SHA `3a3465225636212effcb1d452deb6d7802bb0d16` 通过 Unity 验收并合入 dev** |
| UI | 已使用 UGUI；禁用“顺手换 UI Toolkit”提案 | **继续 UGUI** |
| 输入 | M2 候选声明 `com.unity.inputsystem` 1.20.1；Active Input Handling 设为 Both；游戏自有 Input Actions 与 UI Input Module 正在替换 Soundwell 的旧 API；ORK 原厂 Input Key 资产保留 | **New Input System 迁移进行中，尚待最终 Unity 同 SHA 验收** |
| 摄像机 | `SoundwellView.cs` 手动设置 orthographic size、Camera 等；未声明 Cinemachine | 未采用 Cinemachine 3 |
| 2D 动画/地图 | 未见自有 `.anim`、Animator Controller、Tilemap 地图资产；仅基础 Sprite/Tilemap 模块 | 制作管线未建立 |
| 音频 | 场景代码使用 `AudioSource`，未见 Audio Mixer 资产 | 未采用 Mixer/Snapshots |
| 图集/像素优化 | 未见 `.spriteatlas` / `.spriteatlasv2`；2D Pixel Perfect 包未声明 | 视美术规格启用 |
| 本地化/资源加载 | 未声明 Unity Localization、Addressables | 尚未采用，不自动强制 |
| 性能/构建 | 已有 Unity Actions 回归、帧时间/GC 趋势摘要；未见专门 Build Profile、Memory Profiler 包资产 | 继续改进可追溯证据 |
| 正在开发 | 003 声井真实队伍/单场 ORK Turn Based Battle 已在 dev 验收；两种救援策略与真人试玩仍待完成。M2 输入迁移在独立候选中 | 玩法故事与输入技术候选分开验收 |

“未见”限定于该 SHA 的已提交文件，不代表开发者本机或未提交资源一定不存在。审核当前状态须重新读 HEAD、manifest、锁文件、ProjectSettings 与场景，而非照抄此表。

## 3. 技术路线：按价值与风险实施，官方免费优先

| 顺序 | 方案 / 官方包或内置能力 | 预期收益 | 采用触发条件与边界 |
| --- | --- | --- | --- |
| P0 | **修复当前游戏与 CI**（既有工具） | 保住 003 玩家路径、相机、UI、场景重载和 ORK 回归 | 阻塞必须先修；不借现代化绕开失败 |
| P1 | **URP 2D**（`com.unity.render-pipelines.universal`） | 现代 2D Renderer、灯光与后续图形扩展 | **独立迁移分支**；先保存当前 Built-in 证据/回退；用 2D Renderer，检查不兼容 Shader、材质和相机截图。转换器可能不可逆，不在 dev 直接点击批量转换；不修改 ORK 资产 |
| P1 | **Input System**（`com.unity.inputsystem`） | Actions、键鼠/手柄统一、重绑定、设备切换 | 先 `Active Input Handling = Both`，在游戏自有输入适配层渐进替换旧 `Input.*`。ORK/Makinom 旧路径必须仍可用；消除重复输入及焦点冲突，禁止直接切为 New-only |
| P1 | **Cinemachine 3**（`com.unity.cinemachine`） | 2D 跟随、边界、转场、剧情镜头 | 只允许一个摄像机控制者；替换游戏自有手写位置/缩放逻辑时回归截图、宽高比与战斗镜头；不改 ORK |
| P1/P2 | **正式 2D 动画流水线**（Animator、Animation Clips；视需求采用 `com.unity.2d.animation`） | 待机/移动/攻击/受击、动画状态一致、减少占位感 | 美术先定逐帧或骨骼；优先制作一个角色闭环，验证 Pivot、帧尺寸、朝向、武器/技能对齐；不要强推骨骼以替代逐帧 |
| P2 | **Tilemap + Rule Tiles**（官方 `com.unity.2d.tilemap.extras`） | 地形自动拼接、碰撞/排序与可维护关卡 | 场景扩张时引入；优先做一张真实区域并验证碰撞、导航、障碍和保存重载 |
| P2 | **Audio Mixer / Snapshots**（Unity 内置） | 音乐、环境、SFX 分组与场景/战斗过渡 | 现有 AudioSource 仍可用；制作同一混音总线、静音和音量设置，检查实际播放器的听感，不引入付费音频插件 |
| P2/P3 | **Sprite Atlas V2**（Unity 现有精灵图集流程） | 精灵批处理、内存/纹理组织 | 精灵数和 draw call 有明确证据后使用；验证 Atlas/纹理压缩、SpriteRenderer 显示与资源加载，不为“现代”而空建图集 |
| 按美术风格 | **2D Pixel Perfect**（`com.unity.2d.pixel-perfect`） | 像素画面在常见分辨率下保持清晰和稳定 | **只在决定严格像素风时**启用；与 Cinemachine/URP 2D 的实际版本进行组合验证 |
| 中后期 | **Localization**（`com.unity.localization`） | 可测试的多语言 UI、对白和资源 | 先画清与已冻结 ORK 文本/语言管理的权威边界；只能有一个最终文本来源，未解决冲突时不要加入双轨 |
| 按需 | **Memory Profiler**（`com.unity.memoryprofiler`）、Profiler/Frame Debugger（内置） | 内存快照、加载/GC 分析与性能追踪 | 性能确有问题时测量；记录 Unity 版本、平台与复现环境；已有 CI 指标不等于玩家设备基准 |
| 正式发布前 | **Build Profiles**（Unity 6 内置） | 分开 Debug/Test/Release 和不同平台的设置 | 只有目标平台与可复现构建存在时建档；检查构建所需模块/许可证/成本，不自动启用收费目标平台 |
| 暂缓 | **Addressables**（`com.unity.addressables`） | 大规模/远程资源、按需加载 | 当前固定且较小的 2D 游戏继续使用场景/直接引用。只在实际加载/内存/内容更新需求出现后，引入可验证的资源标签、加载释放与打包策略 |
| 暂缓 | ECS/DOTS、HDRP、额外云服务或付费资源 | 对当前短回合 2D RPG 未证明有净收益 | 不列为必装；新增仅在明确性能/平台指标和用户授权下评估 |

上表列的是**候选能力而非采购清单或立即全部安装**。使用 Unity Registry 中与 **6000.6.8f1** 兼容且已发布的版本；先由该 Unity Editor/Package Manager 确认依赖，再锁 `manifest.json` 与 `packages-lock.json`。**不能编造“最新兼容版本号”或因免费包而无条件升级现有依赖。**

## 4. 每一项现代化改造的完成定义

1. **前置检查：** 实时 `codex/dev` 干净可构建；确认当前候选状态，记录源 SHA、当前已有的资产与性能。正在失败的 003、丢失脚本、Console 编译错误先单独处理。
2. **范围与成本：** 给出该项对玩家的可见收益、要删除/代替的旧逻辑、零新增付费依赖的依据、许可和可能增量 CI 消耗；若不确定，保持未执行。
3. **隔离实现：** 每个领域独立 `codex/pending/*`；修改游戏自有文件，不触碰 `Assets/Gaming Is Love`、任何 ORK/Makinom 设置或版本。改场景/材质/包需带 `.meta` 和 lockfile，保留已验证的回滚提交。
4. **QA 门槛：** Unity 6000.6.8f1 实际导入/编译；Bootstrap 启动与两次 Play、声井玩家路径、ORK 三人战斗及保存/重载（受影响时）不能回退；键鼠/手柄与 UGUI 焦点、不同分辨率、2D 渲染截图、音频听感按领域执行。自动截图、PlayMode 与人工观察要分别记证据。
5. **同 SHA 发布：** 具体测试命令、结果、Actions run 和 artifact 对应**同一个最终候选 SHA**。不得把测试跳过、零测试、未安装或候选之前的绿色 CI 记为完成。通过之后才安全快进/合并；有失败就继续修复候选。
6. **禁止重叠：** 新 Input 适配器与旧输入不得对同一操作重复响应；Cinemachine 与旧脚本不得同时控制镜头；UGUI/框架 UI 不双轨，文本/物品/任务/战斗/存档不产生平行权威。暂时兼容必须写明确移除条件。
7. **无费用变更：** 如需新付费插件、Unity Pro、平台许可、AI API、托管下载或额外 Actions 支出，标记为“需要用户新批准”，不实施，不默认为现有授权。官方包的“可安装”不等于所有商业平台使用条件都免费。
8. **玩家检验：** 现代化不等于玩法完成。剧情 003 的两条救援策略、视觉可读性、实际响应和乐趣仍要按故事卡验收；真人试玩独立于工程 CI。

## 5. 优先级与实施接力

**2026-11-09 用户最新指令覆盖旧顺序：M1–M11 迁移为接下来开发的主线，先解决阻塞的 CI/真实回归，不因轮次中断而转为剧情故事优先。** 历史上曾为阶段预留 `codex/pending/*` 头，参见[阶段历史索引](MIGRATION_STAGE_BRANCHES.md)，但不再永久保留或复用已合入旧头。**一个可验收增量一个候选**；合入后仅局部清理，后续阶段/故事工作从最新 dev 创建新名称候选。预留分支不是迁移成功证据。阶段门槛、ARK/ORK 冻结与零费用限制不变。


- **已完成：** URP 2D 已独立迁移并通过最终资产 SHA 的 Unity 验收，合入 `codex/dev`。
- **当前：** New Input System 1.20.1 独立候选迁移，先 Both，游戏自有 Input Actions 替代旧 `Input.*` 执行路径，保留 ORK 原厂输入数据与兼容后端。
- **后续（相互独立）：** Cinemachine 3 → 第一套角色动画 → Tilemap/Audio Mixer。每项单独决定实施时机和验收；禁止同时迁移。
- **有充分证据后：** Sprite Atlas V2、Pixel Perfect（取决于美术）、Localization（先确认文本权威）、Memory Profiler、Build Profiles。
- **不强推：** Addressables、DOTS/ECS、HDRP；没有现阶段需求就不装。
- 既有用户决定：**ORK/Makinom 不动、无新增花费、UGUI 保留**。这是对任何旧的框架升级建议的明确覆盖。

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

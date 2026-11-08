# Unity / ORK P0 的 GitHub Actions 验收

CI 候选已推送，提交为 [`3c9502a`](https://github.com/jinze909/rpg/commit/3c9502ad6b86a6c905ee8c5e6bdbc8600ff4347f)，**待取得运行验证结果**，基于 `21ce4693323552858430f10db69b5ba40d3c1f2f`。本地尚无 Unity Editor；GitHub API 在当前云工作区被网络策略拒绝，因此未读取远端 secrets 名称或取得运行结果；已通过推送提交工作流启动请求，实际排队状态未确认。没有把静态检查记作编译或 Play Mode 通过。

## 验收内容

[工作流](../.github/workflows/unity-p0.yml)使用 Unity **6000.6.8f1**，固定 GameCI Action 提交、CLI `v0.1.72` 和 Linux 编辑器镜像 digest，不复用 Library 缓存。Unity Test Framework 固定 `1.4.6`，已核对其 Enter/Exit Play API；与此编辑器组合是否实际通过，须由本次 CI 确认。

三个 EditMode 测试依次检查：

1. 使用已保存的非空框架配置，运行接线菜单两次，验证唯一、启用且正确引用配置的 Starter。
2. 完整重载域与场景，进入 Play，检查 Makinom 和 ORK 初始化、Project 引用、帧推进，再退出。
3. 再次执行同样的完整 Play 周期。P0 只初始化框架，不启动尚未配置的游戏流程。

测试后的同一授权容器另外启动一次 Editor，执行 README 中的 `-batchmode -nographics -quit -executeMethod Rpg.Editor.OrkBootstrapSetup.ValidateBatch`。分别保存两次进程的退出码、测试 XML 和日志。测试内的方法调用不替代这个独立进程。

意外 Error / Exception / Assert 日志由 UTF 判失败。后置检查要求三个指定测试全部通过、两个 Play 标记、批处理退出 0 与 `RPG_BOOTSTRAP_VALIDATED`，拒绝空测试、跳过和缺失证据。

## 首次准备与干净提交验收

仓库尚未保存框架 Project、接线场景、EditorSettings 和包锁，所以 [p0-mode.txt](../production/ci/p0-mode.txt) 明确设为 `prepare`。首次准备只在缺少 Project 且没有其他游戏配置资产时，通过厂商编辑器 API 初始化各 Tab / ORK 扩展并调用真正的 `ProjectSaveHandler.SaveData()`。不会用手写 YAML 或空占位冒充框架配置；已有配置不被重建。

- **prepare**：允许首次保存默认配置与重载设置、接线。生成内容保留于 artifact，报告最多为 `prepared-candidate`。即使工作流成功，也只证明“源 SHA 加生成内容”通过，P0 保持待验证。
- **verify**：要求这些文件已纳入候选提交，先检查配置再执行接线，不补建缺失数据。测试后检查 `Assets`、`Packages`、`ProjectSettings` 中的全部 Git 变化；有变化即失败。Unity Action 成功且全部证据通过，才报告 `passed-clean-candidate`。

首次运行后，下载 `unity-ork-p0-prepare-<完整SHA>` artifact，审查 `validation.json`、`source-status.txt`、`candidate.patch` 与 `candidate-files.zip`。ZIP 保留所有新增或修改的源文件及 `.meta`；删除记录在 patch 与 JSON 中。包锁另存一份。ZIP 和 patch 包含相同的已跟踪修改，不可先覆盖 ZIP 再重复应用 patch。

将审查后的生成文件纳入提交，并将 `p0-mode.txt` 改为 `verify`，再运行新提交。最终按[同 SHA 规则](../production/workflow.md#同-sha-验收)记录证据；禁止用 prepare 的绿色状态创建验收标签。

## 触发与授权配置

工作流对 `codex/dev` 的相关路径推送运行，适合当前默认分支仍为 `main` 的仓库。也支持手动选择 `prepare` / `verify`；GitHub 的手动入口要求默认分支已包含该工作流。流水线没有自动提交、推送、部署或发布步骤，权限仅 `contents: read`。

复用仓库既有授权；不要把密钥写入代码或聊天。仓库变量 `UNITY_LICENSE_METHOD` 选择一个方案，未设置时为 `personal`：

| 方案 | GitHub Actions secrets |
| --- | --- |
| `personal` | `UNITY_EMAIL`、`UNITY_PASSWORD`，账户需具备有效授权并接受所需条款 |
| `serial` | `UNITY_EMAIL`、`UNITY_PASSWORD`、`UNITY_SERIAL` |
| `file` | `UNITY_LICENSE`，其有效授权及机器绑定必须适用于 runner |
| `floating` | `UNITY_LICENSING_SERVER`，runner 需能访问许可服务器 |

工作流只向 GameCI 步骤传选定方案的值。具体账户能否激活仍由 Unity 返回结果确认；服务账户 token 不代替 Editor 授权。日志和包含授权插件的 artifacts 仅留在当前私有仓库，保留 14 天。

## 当前环境与下一步

本地可以校验原包、工作流和收集脚本。由此处管理 Actions 还需在云环境网络设置中**追加** `api.github.com`，保留原有规则；Git 凭据已经存在，无需在聊天提供 token。GitHub runner 安装或拉取 Unity 不依赖此云工作区的 Unity 下载域名。

本轮已通过 `actionlint 1.7.7`、Bash / Python 语法、原包完整性和 17 项合成流程检查（包装器 5 项、证据收集器 12 项，覆盖零测试、缺失标记、失败退出和脏工作区等）。这些仅验证 CI 控制流程，不是 Unity 编译或运行证据；Lead programmer 与 QA 完成静态终审。

用户已授权推送这份经过审查的 CI 候选。取得首次 prepare 运行结果；随后导入真实生成文件，运行干净提交的 verify。在这两步完成前，[001](../production/stories/001-ork-bootstrap.md) 不标完成。

## 上游依据

- [GameCI Action 固定提交](https://github.com/game-ci/unity-test-runner/tree/32e57712352b500e17974b245a6dce9e11a73213)
- [CLI v0.1.72 的启动包装器透传](https://github.com/game-ci/cli/blob/7da0cea2f31799a4cbd79c1d452c254023f4f678/src/logic/unity/environment.ts#L34)与[测试命令](https://github.com/game-ci/cli/blob/7da0cea2f31799a4cbd79c1d452c254023f4f678/dist/platforms/ubuntu/steps/test.sh#L228)
- [UTF 1.4.6 包源码镜像](https://github.com/needle-mirror/com.unity.test-framework/tree/5cf7e50b4615eb68993cd41502c7e7bbe25c5860)：API / 重载行为依据，不是本项目实跑证据。

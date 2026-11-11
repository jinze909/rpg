# Unity / ORK 的 GitHub Actions 验收

本页描述现有流水线；实时运行以具体候选 SHA/Run 为准，不沿用早期网络或初始化阻塞。

## 准备与验证

[主工作流](../.github/workflows/unity-p0.yml)对 dev 和 pending 的实现/配置/CI相关路径运行；按分支分组，不把无关候选挤进全仓唯一 pending 槽。保留 workflow_dispatch 显式检查。

[Editor 工作流](../.github/workflows/unity-p0-editor.yml)固定 Unity 6000.6.8f1、GameCI CLI、镜像 digest 和依赖，使用干净检出，不将 Library 缓存当证据。

- `prepare`：在可审查候选中由 Unity 保存配置/资产/包锁，产物最多为 `prepared-candidate`。需要回写时 publish 以受控写权限写回原候选，随后显式检出最终 SHA 执行 verify；不依赖 Token push 自动再触发。
- `verify`：要求已提交配置存在，真实导入、编译、两次 Play、受影响故事测试与独立 batch 成功。收集器检查候选实际声明的必需测试、标记、退出码、画面/构建等产物及干净源文件状态，结果为 `passed-clean-candidate` 才可验收。
- `prepare` 成功、分支绿色或旧 SHA 结果均不替代最终 SHA 的验证。测试直接扣HP只证明结算后的逻辑，不能代替玩家 Attack/选目标。

## 精确证据复用

dev push 若与成功候选具有完全相同的最终 SHA、明确 verify 模式及同等执行条件，可用[证据复用检查](../tools/reuse_unity_evidence.py)验证仍可读取的可信 artifact，记录原始 Run 并跳过重复 Unity 执行。不得仅看 artifact 名称或绿色状态；证据不完整、过期、配置不一致或查询失败时回退正常 Unity。

手动请求、prepare 和候选首次验证照常执行。复用不替代分支独有验收，也不影响独立的分支清理工作流。旧证据缺少复用所需条件记录时保守补跑，不倒填历史验证。

## 授权与产物

沿用现有 UNITY_EMAIL / UNITY_PASSWORD / UNITY_LICENSE 等及 UNITY_LICENSE_METHOD；只向运行步骤传所选授权方式，不读取或打印秘密。不新增凭据、收费资源或定时调度。产物当前保留14天；失效后补跑，不把已删除日志当可读取证据。Public 仓库的产物与代码仍受第三方资产许可约束。

## 交付和问题

智能体核对完整 SHA、测试列表、Run、实际结果和画面/路径证据。真实运行、自动输入、智能体审查与主观假设分开记录；不要求用户操作 Unity、试玩或写反馈。缺陷、工具限制与无法证实的体验写入[问题记录](../production/KNOWN_ISSUES.md)，不伪称通过。

相关入口：[工作室流程](../production/workflow.md) · [框架修改与验证](../production/workflow.md#框架修改与验证) · [当前状态](../AI2-STATUS.md)

# ORK 3.22.0 包整合记录

## 输入与范围

- 开发基线：`03c6b954ef9a2bec347564edcdd29485d806d90e`（`codex/dev`）。已读取全部 43 个原有跟踪文件。
- 用户提供的 RAR：27,905,140 字节，含一个 Unity 包；外层解压成功，未执行包内程序。
- Unity 包：34,981,620 字节，SHA-256 为 `ba2879da647514dac8087531c69c58612aac42270fa4c8fce56d37ea3124eb35`，与现有安装器固定值一致。
- 按原始 `pathname` 完整恢复 73 个资源条目：63 个资源文件、73 个 `.meta`，合计 136 个文件。
- 包内 49 张 `preview.png` 和一个 `.icon.png` 属于导入器预览，不是项目资源。原始资产与元数据均已恢复。
- 两个源码 ZIP 原样保留，包括其中全部源码；没有将源码展开到 DLL 的编译路径，以免重复定义类型。

## 可复现验证

在仓库根目录执行：

```sh
git rev-parse HEAD
python tools/verify_ork.py
git diff --check
git diff --cached --check
```

本轮实际结果：

- 原安装器首次运行：`Verified 136 package files, installed 136.`
- 重复安装：`Verified 136 package files, installed 0.`
- 使用独立 tar 遍历与原包逐字节比较：136/136 一致；73 个条目的 GUID 与包内条目 ID 一致，无重复路径。
- 校验脚本：`Verified 136 package files, 78 unique asset GUIDs, 2 source ZIPs.`（GUID 数量包含游戏自有资产。）
- ORK 源码 ZIP：2,551 个条目；Makinom 源码 ZIP：1,742 个条目，均通过全部 ZIP CRC 检查。
- `docs/ork-package-manifest.json` 记录每个文件的路径、大小、SHA-256，供当前及后续提交重复核验。
- Git 暂存区的 136 个资源文件哈希也全部与原包一致；`.gitattributes` 禁止对厂商资源自动转换换行，保证跨平台检出仍保留原始字节。
- 工作区 `git diff --check` 通过。导入提交的完整 `git diff --cached --check` 报告厂商原文件自带的行末空格/空行；为保持原包字节不改写这些文件。排除这 136 个原始资源后，项目自有改动的空白检查通过。

## 评审与待验收项

本对话依次承担 producer、Unity 整合实现、lead-programmer 差异评审及 qa-lead 证据核对；没有委派子代理。
资源文件、原始 `.meta`、DLL 导入设置保持原样；游戏脚本、启动场景、项目设置及 UPM 依赖未修改。
`.gitignore` 仅解除本次依赖目录的忽略，仍忽略备份、下载及本地编辑器数据。
厂商说明保留于 `Assets/Gaming Is Love/Makinom 2/`，依赖仅用于此私有项目。

当前机器未找到 Unity Editor，也没有可连接的 Unity MCP。
Unity 6000.6.8f1 导入、编译、依赖解析、Starter 接线、批处理及两次 Play Mode 均**未运行**。
包内没有游戏自有的 `Project.asset`，尚不能声明 ORK 已在启动场景初始化。
故事 001 保持 Pending；下一步按 README 在指定 Unity 版本中保存项目数据、接线并完成验收。
文件完整性通过不代表 Unity 兼容性或玩家路径已经通过。

# Attention Guardian

Attention Guardian（中文工作名：托管时间）是一款未来事项托管工具。它替用户监控下一件不可错过的事情，让用户在交接时刻到来前安心专注于当前唯一任务。

> 当前状态：Attention Guardian v0.1.0 已在 GitHub 正式发布。Windows x64 安装包
> 已通过本机安装、空数据启动、卸载注册、隐私扫描和 SHA-256 一致性检查。

下载：[Attention Guardian v0.1.0](https://github.com/NOsauceburGer/Attention_Guardian/releases/tag/v0.1.0)

## 核心理念

普通任务工具告诉你“还有什么要做”，Attention Guardian 重点回答：

> 我现在可以放心专注到几点？

对于下一个固定事件：

```text
handoffTime =
    startTime
    - preparationDuration
    - travelDuration
    - safetyBuffer

safeUntil = handoffTime
```

## v0.1.0 范围

- Windows 桌面应用
- 当日待办与未来待办的独立添加流程和独立 SQLite 存储
- 当前唯一任务、无时间专注首页和用户主动开始
- 确定性的冲突检测、自动顺延、不可移动事件管理和跨日处理
- 拖拽/键盘重排、编辑、确认删除和可重复休息模板
- 到期未来待办、不可移动冲突、普通顺延和跨日顺延的应用内提醒
- 当前事件结束前五分钟的 Windows 系统通知和相邻任务自动切换
- 已完成事件退出活动界面但保留数据库历史记录
- 九类特殊排程样例与自动化验收

第一版不包含账号、云同步、第三方日历、AI 接入、社交功能或复杂统计。

已结束当日待办现在会标记完成并退出活动界面，数据库历史仍被保留。未来待办成功
规划后标记为 `planned`。v0.1.0 安装包、Tag 和 GitHub Release 已正式发布。

## 使用应用

普通用户请先阅读 [用户使用指南](USER_GUIDE.md)。指南包含：

- 如何从顶部抽屉进入添加事件和事件管理；
- 当日待办、未来待办与不可移动事件的区别；
- 自动顺延、冲突、跨日和五分钟前提醒的触发规则；
- 如何拖拽排序、编辑、删除和加入“休息”；
- 已完成、已删除和已安排记录如何保存；
- 通知不出现或应用无法启动时的排查步骤。

正式 Release 界面不显示内部“验收检查”入口；该入口只在 Debug 或显式开发者诊断
开关下可见。

## 技术方向

- C#
- .NET 10
- Avalonia UI
- MVVM
- SQLite
- 独立单元测试项目

架构与依赖规则见 [ARCHITECTURE.md](ARCHITECTURE.md)。

## 项目状态

当前完成情况和真实验证记录见 [PROJECT_DEVELOPMENT.md](PROJECT_DEVELOPMENT.md)，版本规划见 [ROADMAP.md](ROADMAP.md)。

## 开始开发

环境要求和构建命令见 [SETUP.md](SETUP.md)。

普通用户可运行生成的 Windows x64 安装程序：

```text
artifacts/v0.1.0-public-clean/AttentionGuardian-0.1.0-win-x64-setup.exe
```

开发者也可以从源码运行：

```powershell
dotnet run --project src/AttentionGuardian.Desktop
```

数据保存在 `%LOCALAPPDATA%\AttentionGuardian` 下两个带 schema 版本的 SQLite
数据库中。源码 Release 输出依赖 Microsoft Windows App Runtime 2.3.1 x64；正式
安装包必须自动部署该运行时。开发环境和启动说明见 [SETUP.md](SETUP.md)。

当前验证记录：158 项 Release 测试全部通过，格式检查通过，Release 构建 0 警告、
0 错误；Windows 测试通知已经在真实系统中成功交给通知中心。安装程序已在当前
Windows 用户环境完成安装并启动。安装包目前没有项目方 Authenticode 代码签名。

## 参与项目

- 贡献流程：[CONTRIBUTING.md](CONTRIBUTING.md)
- 行为规范：[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- 安全问题：[SECURITY.md](SECURITY.md)
- 使用支持：[SUPPORT.md](SUPPORT.md)

## 许可证

项目以 GNU General Public License v3.0 发布，完整条款见 [LICENSE](LICENSE)。

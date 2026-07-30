# 开发环境与启动

## 平台范围

本文件当前只记录已经存在并验证过的 C#/.NET/Windows 客户端环境与命令。Apple 客户端
尚未创建 `Package.swift`、Xcode 工程或 Swift 源码，因此这里不提供推测性的 Xcode、
Swift、Simulator 或签名步骤。

等 Apple 工程实际建立并运行验证后，再追加准确的 macOS/iOS 环境、版本与命令。不得从
`DESIGN.md` 或目标架构推导出尚未执行的安装和构建说明。当前 Apple 开发顺序见
[`HANDOFF.md`](HANDOFF.md) 与 [`ROADMAP.md`](ROADMAP.md)。

## Windows 通知运行时

源码构建使用 Microsoft Windows App SDK 2.3.1。直接运行未打包的 Release 输出前，
Windows 必须安装对应的 Microsoft Windows App Runtime x64；它包含通知所需的
Framework、Main、Singleton 和 DDLM 包。

如果验收页显示 `0x80040154 REGDB_E_CLASSNOTREG`，表示运行时组件没有注册，并不表示
用户关闭了通知权限。v0.1.0 的正式安装器必须自动部署该依赖，不应要求普通用户手工
处理。

## 当前环境基线

- 操作系统：Windows
- .NET SDK：10.0.302 已验证可用
- Git：已安装
- Windows v0.1.0 发布工作区曾使用 Git；当前这份 macOS 本地目录未检测到 `.git`，
  不能据此执行分支、提交或 worktree 操作
- Solution：`AttentionGuardian.slnx`
- 当前包含四个源项目与四个测试项目

记录日期：2026-07-27。

## 需要的软件

- .NET 10 SDK
- Git
- Visual Studio Community 或 VS Code
- Avalonia 模板与开发工具
- SQLite 查看工具（可选）

第一阶段不需要 Python、Node.js、Docker、云数据库或 AI 服务。

## 验证环境

```powershell
dotnet --version
git --version
dotnet new list avalonia
```

## 预期项目结构

```text
AttentionGuardian/
├── src/
│   ├── AttentionGuardian.Core/
│   ├── AttentionGuardian.Application/
│   ├── AttentionGuardian.Infrastructure/
│   └── AttentionGuardian.Desktop/
├── tests/
│   ├── AttentionGuardian.Core.Tests/
│   ├── AttentionGuardian.Application.Tests/
│   ├── AttentionGuardian.Infrastructure.Tests/
│   └── AttentionGuardian.Desktop.Tests/
├── docs/
├── .github/
└── AttentionGuardian.slnx
```

当前 .NET 10 与 Avalonia 工具链已验证可识别 `.slnx`，因此采用 `AttentionGuardian.slnx`。

## 预期开发命令

Solution 创建后，应使用实际文件名运行：

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/AttentionGuardian.Desktop
```

## 配置与敏感信息

- API 密钥不得写入源码或提交到 Git。
- 本地数据库、日志、构建产物和个人 IDE 配置不得提交。
- 第一版不使用任何真实 AI API。
- 正式数据库位于 `%LOCALAPPDATA%\AttentionGuardian`：
  `attention-guardian-scheduled.db` 与 `attention-guardian-future.db`。它们可能包含
  任务文字、时间安排和未来的完成历史，不得提交到 Git。

## 故障定位顺序

1. 环境：SDK 或工具是否安装。
2. 依赖：NuGet 包是否可恢复、版本是否兼容。
3. 配置：目标框架、项目引用、运行参数是否正确。
4. 代码：编译错误、异常或业务规则错误。
5. 架构：职责或依赖方向是否错误。

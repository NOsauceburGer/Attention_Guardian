# 参与贡献

感谢你关注 Attention Guardian。项目正在准备首个 v0.1.0 GitHub 发布，请先通过
Issue 对齐需求，再投入较大实现；不要把后续 AI/API 范围带入首版修复。

## 开始之前

1. 阅读 `README.md`、`AGENTS.md`、`ARCHITECTURE.md` 和 `PRODUCT_DECISIONS.md`。
2. 搜索现有 Issue，避免重复工作。
3. 对新增功能、架构变化或较大重构，先创建 Issue 并说明动机。
4. 安全漏洞不要公开提交 Issue，按 `SECURITY.md` 处理。

## 开发原则

- 保持产品聚焦，不把它改造成普通 Todo List。
- 核心业务规则与 Avalonia、SQLite 和 Windows 解耦。
- 采用小而清晰的 Pull Request。
- 不加入未使用的抽象或未来功能占位代码。
- 不提交密钥、数据库、日志、个人配置或构建产物。
- 实质性变更同步更新 `PROJECT_DEVELOPMENT.md`。
- 已完成事件必须保留为完成记录；活动列表隐藏不等于物理删除历史。

## 分支与提交

- 从最新默认分支创建短生命周期功能分支。
- 推荐分支名：`feature/...`、`fix/...`、`docs/...`、`refactor/...`。
- 提交信息使用简短祈使句，并只描述一个逻辑变化。
- 不在未经维护者同意的情况下重写共享分支历史。

## 测试

提交 Pull Request 前，应按实际工程运行：

```powershell
dotnet restore
dotnet test AttentionGuardian.slnx --configuration Release --no-restore
dotnet build AttentionGuardian.slnx --configuration Release --no-restore
```

如果某项无法执行，必须在 Pull Request 中说明原因，不得勾选为已完成。

## Pull Request 要求

- 说明问题、方案和范围。
- 链接相关 Issue。
- 列出实际运行的验证命令和结果。
- UI 变化提供截图或录屏。
- 标明已知限制和后续工作。
- 保持与任务无关的格式化或重构在范围之外。

## 评审标准

- 是否解决真实问题。
- 是否遵循架构依赖方向。
- 是否具有足够测试。
- 是否保持产品原则。
- 文档是否与实际状态一致。
- 是否引入不必要复杂度或安全风险。

## 许可证与贡献授权

提交贡献即表示你有权提交该内容，并同意该贡献按项目的 GPL-3.0 许可证发布。未来如考虑双许可证，项目会先建立独立、明确的贡献者授权机制，不会默认取得额外授权。

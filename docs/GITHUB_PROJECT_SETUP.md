# GitHub 项目创建清单

本清单用于把本地目录转为可维护的公开 GitHub 项目。执行者必须记录真实结果，不应因为文档存在就声称步骤已完成。

## 1. 本地仓库

- [ ] 确认目录中没有密钥、个人数据库或隐私文件。
- [ ] 创建适合 .NET、Avalonia 和本地 SQLite 的 `.gitignore`。
- [x] 添加完整 GPL-3.0 `LICENSE`。
- [x] 初始化 Git。
- [ ] 检查待提交文件。
- [ ] 创建首次提交。

## 2. GitHub 仓库

- [ ] 使用最终仓库名创建空远程仓库。
- [ ] 不让 GitHub 自动生成会与本地冲突的 README、许可证或 `.gitignore`。
- [ ] 添加 `origin` 并核对远程地址。
- [ ] 推送默认分支。
- [ ] 填写仓库描述、Topics 和网站地址（如有）。

推荐仓库标识：`AttentionGuardian` 或 `attention-guardian`。最终名称应由项目所有者在创建远程仓库前确认。

## 3. 社区健康

- [x] README
- [x] CONTRIBUTING
- [x] CODE_OF_CONDUCT
- [x] SECURITY
- [x] SUPPORT
- [x] 完整 LICENSE
- [x] Issue 模板
- [x] Pull Request 模板

公开后在 GitHub Insights 的 Community Standards 中检查识别结果。

## 4. 仓库设置

- [ ] 启用 Issues。
- [ ] 按需要启用 Discussions。
- [ ] 启用 Private Vulnerability Reporting。
- [ ] 配置默认分支。
- [ ] 禁止强制推送和删除默认分支。
- [ ] 要求 Pull Request 合并。
- [ ] 在 CI 可用后要求构建和测试检查通过。
- [ ] 根据协作人数决定是否要求审批。
- [ ] 启用 Dependabot alerts，并评估自动更新策略。

早期单人项目不应配置无法满足的强制审批人数，但仍应通过分支和 Pull Request 保留审查轨迹。

## 5. 标签与项目管理

建议从最小标签集开始：

- `bug`
- `enhancement`
- `documentation`
- `security`
- `architecture`
- `good first issue`
- `help wanted`
- `blocked`

不要一次创建复杂标签体系。只有在 Issue 数量增长后再增加优先级、模块和状态标签。

## 6. 自动化

- [ ] 增加 restore、Release build、四个测试项目和格式检查的 GitHub Actions。
- [ ] 固定 Actions 主版本或提交版本，降低供应链风险。
- [ ] 不在工作流中输出 Secret。
- [ ] 首次 Release 前建立 Windows 打包工作流。

## 7. 首次公开前

- [ ] README 与实际功能一致。
- [ ] 已完成当日待办保留为完成记录，且旧数据库迁移验证通过。
- [ ] SETUP 命令由新环境验证。
- [ ] PROJECT_DEVELOPMENT 记录真实构建与测试结果。
- [ ] 没有把规划或占位代码描述为已实现。
- [ ] 安全报告渠道真实可用。
- [ ] 发布包在干净 Windows 环境验证。

# Attention Guardian 文档分层设计

日期：2026-07-29  
状态：用户已批准方案 A

## 目标

在不移动现有文档路径、不开始 Swift 开发的前提下，消除 Windows 历史交接与 Apple
当前路线之间的冲突，让下一位 Agent 能在一次阅读中明确：

1. Windows v0.1.0 已发布，仍需保留；
2. Apple 客户端已确认 SwiftUI、Swift Core 与独立平台实现；
3. 产品规则和 Apple 视觉规范已经确认；
4. 当前尚未创建 Swift 工程；
5. 下一步只建立共享 JSON 测试向量；
6. 测试向量验证 C# 后，才开始 Swift Domain。

## 文档职责

- `AGENTS.md`：长期约束、跨平台边界、工作方式和当前唯一优先级。
- `HANDOFF.md`：短期交接快照，可整体替换，不保存历史。
- `PRODUCT.md`：跨平台产品目标与体验原则。
- `DESIGN.md`：Apple SwiftUI 与 Windows Avalonia 两套视觉语言。
- `ARCHITECTURE.md`：当前 Windows 架构、目标 Apple 架构及共同边界。
- `PRODUCT_DECISIONS.md`：只追加决策历史。
- `PROJECT_DEVELOPMENT.md`：只追加实际操作历史。
- `ROADMAP.md`：已发布版本、当前阶段和远期方向。
- `docs/CROSS_PLATFORM_RULES.md`：C# 与 Swift Core 的共同业务合同。
- `SETUP.md`：只记录真实可运行的环境和命令；Swift 工程创建后再添加 Apple 命令。
- `README.md`、`USER_GUIDE.md`：面向当前已发布 Windows 用户。
- `teaching.md`：本地只追加教学记录，不进入 Git。

## HANDOFF 设计

现有 `HANDOFF.md` 整体替换，不保留旧章节。历史事实由 `PROJECT_DEVELOPMENT.md`、
`PRODUCT_DECISIONS.md`、`CHANGELOG.md` 和 GitHub Release 保存。

新的 `HANDOFF.md` 只包含：

1. 当前一句话状态；
2. 已确认并不可误改的事实；
3. 当前未开始的内容；
4. 下一位 Agent 的唯一任务；
5. 明确实施顺序；
6. 本轮禁止事项；
7. 必读文件及其目的；
8. 验证与教学要求；
9. 已知工作区限制。

目标长度约 100–150 行，不复制产品规则、设计细节或历史时间线，只链接权威文档。

## AGENTS 设计

保留长期产品、数据、架构、质量与教学约束；把已完成的 Windows 发布任务从“当前优先级”
移出。新的当前顺序为：

1. 建立平台无关 JSON 测试向量格式；
2. 从现有 C# 测试提取首批向量；
3. 让 C# 测试读取并验证共享向量；
4. 创建 Apple Swift Package；
5. 以测试驱动实现 Swift Domain；
6. 再实现 Application、Infrastructure；
7. 最后进入 SwiftUI。

本轮只更新这段优先级和必要的平台措辞，不重写长期业务规则。

## 其他文档处理

- `ARCHITECTURE.md`：增加清晰的 Windows 当前架构与 Apple 目标架构入口，不删除历史细节。
- `ROADMAP.md`：把 Apple 工作拆成测试合同、Domain、Application、Infrastructure、UI
  五个有顺序的阶段。
- `README.md`：维持 Windows v0.1.0 用户入口，只增加 Apple 当前阶段链接。
- `SETUP.md`：明确当前命令只适用于 Windows/C#；不伪造尚不存在的 Swift 构建命令。
- `PRODUCT_DECISIONS.md`、`PROJECT_DEVELOPMENT.md`、`teaching.md`：只在末尾追加本次记录。
- `PRODUCT.md`、`DESIGN.md`、`USER_GUIDE.md`、`CHANGELOG.md`、`SECURITY.md`、
  `SUPPORT.md`：若没有事实冲突，本轮不改。

## 验证

- 搜索所有“下一位 Agent”“当前优先级”“下一检查点”，确认只有根入口表达当前任务。
- 搜索 Swift、macOS、iOS、Avalonia 和 Windows，确认平台归属明确。
- 检查所有新增相对链接存在。
- 确认没有 Swift 源码、测试向量、数据库 schema 或虚构构建结果。
- 确认 `teaching.md` 只在末尾追加。

## 非目标

- 不创建 Apple 工程或目录占位。
- 不编写共享 JSON 测试向量。
- 不修改 C#、Avalonia、SQLite 或发布产物。
- 不提交、推送、打 Tag 或发布。

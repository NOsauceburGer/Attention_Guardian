# Attention Guardian 当前工作交接

更新日期：2026-07-30  
用途：供下一位 Agent 快速接手当前阶段。本文件是当前快照，不保存实施时间线。除非用户再次
明确要求，否则禁止修改本文件。

## 一句话状态

Windows v0.1.0 已发布并保留；Apple Swift Domain、Application、双 SQLite Persistence、
系统通知适配、共享 SwiftUI 基础组件、原生 macOS 壳和“添加事件”真实写库闭环已经完成。
下一位 Agent 应继续 Apple UI 的功能内容，优先实现事件管理等尚未完成页面；当前 UI 不是终稿，
用户明确还会提出多轮视觉与交互修改。

## 用户当前最重要的 UI 要求

后续实现必须同时遵守以下要求：

1. `DESIGN.md` 仍是默认设计原则：calm ambient、低信息密度、原生 Material、克制 Liquid
   Glass、Capsule Button/Stepper、底部抽屉、Floating Tab Bar 和无焦虑动效。
2. 当前 UI 只是一版功能与结构基线，用户对视觉并不完全满意，之后会继续要求修改。不得把现有
   颜色、卡片比例、文字层级、间距或控件外观当成不可更改终稿。
3. 页面、Design Token、局部组件、表单 draft、App 组合和 Application 用例必须保持分层，
   让后续改样式或排列时不需要推翻数据库与业务规则。
4. macOS 开发的主要目的之一是服务 iOS 适配。macOS/iOS 共用
   `AttentionGuardianPresentation`，Mac 是同一套 iPhone 优先组件的宽屏适配，不建立另一套
   桌面 View。
5. 禁止使用绝对像素或截图坐标摆放元素。Presentation/Mac App 中不得使用 `.position`、
   截图式 `.offset`、固定屏幕尺寸、`UIScreen`、`NSScreen` 或以 `GeometryReader` 手算位置。
   使用安全区域、弹性容器、size class、Dynamic Type 和内容尺寸。
6. 可复用间距、圆角、最大可读宽度和 44 point 最小命中区可以作为 Design Token；这些是设计
   尺度，不是锁死元素位置。
7. macOS 使用原生窗口行为。白色标题栏材质和标题文字已经隐藏，ambient 背景延伸到窗口顶部；
   红黄绿交通灯必须继续使用系统控件，不得自绘。
8. ambient 背景必须持续缓慢流动，不得退回静态终稿。Reduce Motion 时冻结；未开始稍活跃，
   稳定专注更慢、更低饱和，状态切换只有一次克制脉冲。
9. 下一位 Agent 应先实现真实功能内容，再接受用户后续视觉校准；不得用多轮形式确认阻塞开发。

## 已完成的平台与分层

### Windows 已发布基线

- Windows 客户端继续使用 C#、.NET 10、Avalonia UI、MVVM 和 SQLite。
- v0.1.0 安装器、Tag、Release 和发布记录必须保留。
- 最近记录的 C# Release 全量结果：Core 67、Application 45、Infrastructure 17、
  Desktop 33、SharedVectors 14，共 176 项通过。
- 当前 Apple 工作没有修改 Windows 发布内容。

### 共享跨平台合同

- 共同业务规格：[`docs/CROSS_PLATFORM_RULES.md`](docs/CROSS_PLATFORM_RULES.md)。
- 共享向量：[`test-vectors/v1`](test-vectors/v1)。
- 覆盖矩阵：[`test-vectors/COVERAGE.md`](test-vectors/COVERAGE.md)。
- C# 与 Swift 直接读取同一批 JSON，不维护复制版业务预期。

### Apple Domain

Swift Package 位于 [`apple/AttentionGuardianDomain`](apple/AttentionGuardianDomain)。

`AttentionGuardianDomain` 已实现并通过共享向量：

- `ScheduledTodo`、固定偏移和 `[start, end)`；
- 当前事项选择、重叠时最新录入优先和稳定排序；
- 新增、级联顺延、空档停止、不可移动挡板、跨日和不可移动冲突；
- 管理重排、连续不可移动组、删除补位、编辑、休息和开始时间冲突试算；
- 五分钟前提醒资格；
- `LocalDate`、未来待办和 active/completed/deleted/planned 生命周期；
- 到期查询、相对日期、历史保留和冲突恢复。

### Apple Application

`AttentionGuardianApplication` 已完成：

- 可替换 `Clock`；
- `ScheduledTodoRepository` 与 `FutureTodoRepository` 协议；
- 两类新增用例；
- OpeningState；
- 管理加载、重排、编辑、删除、休息和开始时间修改编排；
- 未来待办确认删除；
- 规划未来待办及跨库 planned 失败幂等恢复；
- 提醒进程内 UUID 去重；
- 严格本地时间解析，拒绝夏令时不存在或重复的时刻；
- 完整 Application 生命周期与本地时间共享向量。

Domain/Application 不依赖 SQLite、SwiftUI 或系统通知实现。

### Apple Persistence

`AttentionGuardianPersistence` 已完成：

- 当日与未来待办两个独立 SQLite 文件和表；
- 两个 Repository actor，连接不跨 actor 共享；
- 两库独立 `PRAGMA user_version` v2；
- v0 建库、v1→v2 事务迁移、高版本拒绝和迁移失败回滚；
- 迁移前 SQLite online backup，失败时保留可读旧版本快照；
- 当日整组替换使用 `BEGIN IMMEDIATE`，缺失 active 标记 deleted；
- completed/deleted/planned 历史不物理擦除；
- UUID、epoch 小数秒、原始偏移、优先级、状态和完成时刻关闭重开无损往返；
- `ApplePersistenceContainer` 默认使用
  `Application Support/AttentionGuardian`，固定创建两个数据库；
- 真实跨库规划恢复和关闭重开集成测试。

### Apple Infrastructure

`AttentionGuardianInfrastructure` 已完成：

- `SystemClock`；
- `AppleNotificationCenter` 可替换端口；
- `UserNotificationCenterAdapter`；
- `AppleHandoffNotificationSender`；
- 授权、临时授权、未决定和拒绝状态；
- 只有明确用户操作才请求权限，提醒轮询不偷偷弹权限窗口；
- 通知同时包含当前事项和下一事项名称。

真实权限窗口、前台/最小化通知展示仍需正式 Xcode App 实机验收。

## 当前 SwiftUI 与 Mac App 状态

### 共享 Presentation

`AttentionGuardianPresentation` 当前包含：

- `DesignTokens.swift`：品牌色、间距、圆角、命中区、动效；
- `AmbientBackground.swift`：TimelineView 驱动的动态 Mesh/渐变降级；
- `GlassSurface.swift`：原生 Material Dashboard/Component 表面；
- `GlassNumberStepper.swift`：连续 Capsule 数字输入；
- `GlassPicker.swift`：共享菜单选择器；
- `AlertGlass.swift`：双击与辅助功能展开的折叠提醒；
- `FloatingBottomDrawer.swift`：底部隐藏抽屉和 Floating Capsule Tab Bar；
- `FocusDashboard.swift`：loading/empty/ready/focused 首页骨架；
- `AddEventFlow.swift`：类型选择和两类真实添加表单。

Apple Package 平台基线为 macOS 14 / iOS 17。macOS 15 / iOS 18 使用 Mesh Gradient；
较旧目标系统使用同色动态渐变降级。

### 原生 macOS 壳

`AttentionGuardianMacApp` 已完成：

- SwiftUI `WindowGroup` 原生 Scene；
- `hiddenTitleBar` + AppKit `fullSizeContentView`；
- 隐藏白色标题栏材质和标题文字；
- 系统原生关闭、最小化、全屏按钮仍在辅助功能树中；
- 背景拖动窗口；
- 启动时组合真实 Persistence、SystemClock 和 OpeningState；
- 本地数据错误显示普通用户文案，不暴露 SQLite 或路径。

本地临时验证 `.app` 位于 `/private/tmp/AttentionGuardianMacApp.app`，只用于当前机器快速查看；
它不是正式 Xcode App、安装包或发布产物，系统清理临时目录后可能消失。

### 已完成的“添加事件”功能闭环

入口：底部导航 → 添加事件。

第一屏严格只提供：

- “今天要做什么？”
- “这些事情之后做”

当日表单：

- 名称；
- 当天开始小时/分钟；
- 持续小时/分钟；
- 不可移动开关；
- 不提供结束时间；
- 使用 `LocalDateTimeResolver` 严格解析系统时区；
- 调用 `AddScheduledTodoUseCase` 写入真实当日数据库。

未来表单：

- 名称；
- 一天后/两天后；
- 年/月/日；
- 快捷日期同步具体日期；
- 手动日期取消快捷选中；
- 月份/年份变化收紧合法日期；
- 不可移动开关；
- 调用 `AddFutureTodoUseCase` 写入独立未来数据库。

保存成功返回首页并刷新 OpeningState。保存失败保留表单并显示普通用户错误。

Presentation 只输出 `ScheduledTodoDraft` / `FutureTodoDraft`，不认识 Repository；
Mac App 组合层负责 draft→Application Request，方便未来修改 UI。

## 当前实际验证证据

仓库根目录执行：

```bash
swift test --package-path apple/AttentionGuardianDomain
```

最近实际结果：

- 18 个 suite；
- 41 项测试；
- 0 项失败。

额外验证：

- `swift build --package-path apple/AttentionGuardianDomain --product AttentionGuardianMacApp`
  实际成功；
- Presentation/Mac App 源码绝对定位检查无结果；
- 真实临时 `.app` 检查确认透明顶部、原生交通灯和动态背景；
- 两帧间隔 2.5 秒的背景区域比较证明色场持续细微流动；
- 真实窗口冒烟检查确认类型选择和当日表单完整进入辅助功能树；
- 冒烟检查没有输入或保存测试事项。

SwiftPM 在受限沙箱内可能因用户级缓存权限失败。遇到 `Operation not permitted` 时应以同一
`swift build/test` 命令申请沙箱外执行，不得误报为产品失败。系统没有 `pkg-config` 时可能
打印 SQLite 查找警告，但 SDK 自带 SQLite，当前实际构建与测试成功。

## 下一位 Agent 的主线任务

继续 Apple UI 的真实功能内容，不要先花大量时间把当前视觉当成终稿。建议顺序：

1. 实现真实事件管理页，替换当前开发中承载面：
   - 加载活动当日待办；
   - 默认隐藏开始时间；
   - “显示开始时间 / 隐藏开始时间”页面状态；
   - 活动气泡列表；
   - 主动展开未来待办；
   - 删除确认与真实 Application 调用。
2. 再补管理高级交互：
   - 普通事项拖拽重排与键盘等价操作；
   - 不可移动连续组与冲突组；
   - 双击展开编辑；
   - 修改开始时间确认两种方案；
   - 休息模板。
3. 接入首页真实提醒：
   - 到期未来待办；
   - 规划与确认删除；
   - 不可移动冲突；
   - 顺延与跨日提示。
4. 建立正式 macOS/iOS Xcode App targets，加入 iOS 文件保护、签名配置和真机验证。
5. 用户会在功能推进过程中继续提出 UI 修改。应优先修改 Token/局部组件，保持已完成业务层不变。

### 下一步最小建议切片

先做事件管理的“加载 + 展示 + 删除”闭环：

- Mac App 调用 `ScheduleManagementUseCase.load()`；
- Presentation 接收纯展示模型；
- 当日活动事项按 Domain 顺序显示；
- 页面级开始时间开关默认关闭；
- 删除显示包含事项名称的确认；
- 确认后调用既有删除用例并刷新；
- 未来待办只有用户主动展开后才读取；
- 增加 Presentation 状态和 App 组合测试；
- 先跑最小测试，再跑完整 Swift。

不要在这一小步同时实现完整拖拽、冲突组编辑和休息模板，以便结果可审查、可修改。

## 尚未完成

- 真实事件管理 SwiftUI 页面；
- 管理页拖拽、编辑、开始时间确认、冲突组和休息模板 UI；
- 首页到期未来待办规划/删除提醒；
- 首页不可移动冲突与顺延提示；
- App 中的通知权限入口、轮询和应用内降级；
- 正式 macOS/iOS Xcode App targets；
- iOS 文件保护、签名、打包和发布；
- iPhone 真机、Dynamic Type、VoiceOver、Reduce Motion/Transparency 全矩阵验收；
- 用户后续提出的 UI 视觉和交互修改；
- Windows 单独授权的 PD-053 管理页同步；
- 全部主要功能完成后的交互式验收与诊断界面。

## 禁止事项

- 不重复创建 Swift Domain、Application 或 Persistence 规则。
- 不让 View 直接访问 SQLite、计算排程、选择当前事项或判断提醒资格。
- 不把两类待办放进同一数据库或混合表单。
- 不物理擦除 completed/deleted/planned 历史。
- 不为修 Mac 截图加入绝对坐标或一次性像素定位。
- 不自绘 macOS 红黄绿交通灯。
- 不把当前 UI 声称为用户已确认终稿。
- 不提前接入 AI、云同步、账号、网络 API 或社交功能。
- 不修改 Windows v0.1.0 Tag、Release、安装器或发布记录。
- 未获明确授权时不提交、推送、创建 Tag、Release 或发布。
- 未获用户再次明确要求时不修改本文件。

## 必读顺序

1. [`AGENTS.md`](AGENTS.md)
2. 本文件
3. [`DESIGN.md`](DESIGN.md)
4. [`ARCHITECTURE.md`](ARCHITECTURE.md)
5. [`docs/CROSS_PLATFORM_RULES.md`](docs/CROSS_PLATFORM_RULES.md)
6. [`apple/AttentionGuardianDomain/Package.swift`](apple/AttentionGuardianDomain/Package.swift)
7. `Sources/AttentionGuardianDomain`
8. `Sources/AttentionGuardianApplication`
9. `Sources/AttentionGuardianPersistence`
10. `Sources/AttentionGuardianInfrastructure`
11. `Sources/AttentionGuardianPresentation`
12. `Sources/AttentionGuardianMacApp`
13. 对应 Tests
14. `PROJECT_DEVELOPMENT.md` 最近 Apple 章节
15. `teaching.md` 开头规则与最近相关章节，只追加，绝不能提交

## 工作区说明

- 当前目录没有可用 `.git` 元数据，无法报告真实分支、提交或工作树差异。
- `teaching.md` 是本地私有只追加记录，绝不能上传。
- 用户偏好正常开发直接推进，不主动调用 Superpowers/brainstorming 形式流程。
- 纯 Swift UI 修改先运行相关 Swift 测试，再运行整个 Package。

# Apple Application 纵向切片设计

日期：2026-07-30  
状态：待用户复核书面规格

## 目标

在现有 `apple/AttentionGuardianDomain` Swift Package 中建立
`AttentionGuardianApplication` 层。Application 负责组织领域规则、仓库协议、可替换时钟、
跨数据库规划恢复、提醒进程内去重和本地日期时间解析，但不依赖 SQLite、SwiftUI、系统通知
发送或可运行 Apple App。

实现采用纵向切片：每个切片交付一个可独立测试的完整应用行为，而不是先横向创建大量尚未被
用例验证的抽象。

## 已有基础与边界

- `AttentionGuardianDomain` 已实现当前事项、自动顺延、管理操作、提醒资格和生命周期纯规则。
- `test-vectors/v1/application-lifecycle.json` 定义 Application 生命周期编排案例。
- `test-vectors/v1/resolve-local-date-time.json` 定义正常、无效和重复当地时间案例。
- Application 只能依赖 `AttentionGuardianDomain` 和 Foundation。
- Domain 不得反向依赖 Application。
- 本阶段不得创建 SQLite schema、数据库迁移、SwiftUI 页面、通知平台适配器或 Xcode App。
- 测试不得读取真实当前时间，所有时间输入必须来自固定 Clock。

## Package 结构

在现有 Package 中增加：

```text
Sources/
├── AttentionGuardianDomain/
└── AttentionGuardianApplication/
    ├── Ports/
    ├── Planning/
    ├── Opening/
    ├── Management/
    ├── Reminders/
    └── LocalTime/

Tests/
├── AttentionGuardianDomainTests/
└── AttentionGuardianApplicationTests/
    ├── Fakes/
    └── SharedVectors/
```

文件按职责拆分；不会为了目录外观创建没有实际消费者的空文件或空协议。

## 最小端口

### Clock

Clock 提供确定的当前 `Date`，并提供把该时刻转换成产品使用的本地 `LocalDate` 所需的时区。
Application 不直接调用 `Date()`、`Calendar.current` 或 `TimeZone.current`。测试使用固定时刻
和固定时区。

### Scheduled Todo Repository

当日待办仓库读取包含生命周期状态的完整记录，并接受完整计划替换或明确状态转换。协议表达
Application 所需的原子意图，不暴露表名、SQL、事务对象或数据库实体。

整组替换必须保持 completed/deleted 历史；具体 SQLite 事务和迁移留到 Persistence 阶段。

### Future Todo Repository

未来待办仓库负责读取活动记录、读取指定 UUID 的活动记录、保存新事项，以及幂等标记
`planned` 或 `deleted`。它不参与排程计算，也不调用当日待办仓库。

### Reminder Deduplication State

提醒去重使用进程内状态边界，按当前事项 UUID 记录已经产生过的提醒。退出进程后不要求保存。
首版默认提供内存实现，不定义数据库协议。

## 纵向切片

### 切片 1：新增当日待办

Application：

1. 从当日待办仓库读取完整记录。
2. 使用 Clock 的当前时刻完成到期活动事项。
3. 从剩余活动计划计算新的 `currentSelectionPriority`。
4. 未提供开始时刻时使用 Clock 的确定时刻；结束时刻只由开始时刻加正持续时间计算。
5. 创建 `ScheduledTodo` 并调用 Domain `ScheduleTrial`。
6. 将试算成功的完整活动计划与既有历史组合后整体写入仓库。
7. 返回完整计划、不可移动冲突和跨日标志。

试算、领域校验或仓库写入失败时，不产生部分 Application 写入。

### 切片 2：新增未来待办

输入必须恰好提供确定日期或至少一天后的相对天数。相对日期使用 Clock 的本地今天，经 Domain
换算成确定 `LocalDate` 后保存。保存对象保持 `active`，不读取或写入当日待办仓库。

### 切片 3：打开状态与管理加载

打开流程在一次调用内：

1. 完成所有 `end <= now` 的活动当日待办并保存完成时刻。
2. 从剩余活动计划选择当前事项。
3. 计算尚未结束的不可移动冲突。
4. 只在本次打开流程查询 `scheduledDate <= 今天` 的活动未来待办。

管理加载同样先完成到期事项，但只有用户主动展开未来待办时才读取未来待办仓库。

### 切片 4：管理操作

Application 为拖拽重排、连续不可移动组重排、编辑、删除、插入休息和修改开始时间提供用例。
每个用例先加载活动计划，调用既有 Domain 规则，再整体保存。需要用户确认的删除或开始时间
冲突只接受 UI 已明确给出的选择；取消时 UI 不调用写入用例。

### 切片 5：规划未来待办与幂等恢复

规划沿用源未来待办的 UUID 和标题：

1. 读取当日记录并检查同 UUID 的活动当日待办是否已存在。
2. 不存在时读取 active 源事项、执行当日计划试算并先整体写入当日仓库。
3. 当日写入成功后，幂等标记源未来待办为 `planned`。
4. 如果标记失败，向调用方报告失败，但保留已经完成的第一步。
5. 重试发现同 UUID 当日事项已存在时，只重试 `planned` 标记，绝不再次插入或顺延。

内存 fake 必须记录调用顺序、整组替换次数和首次标记失败，以证明恢复合同。

### 切片 6：提醒去重

Application 从当日仓库加载活动计划，以 Clock 当前时刻调用 Domain
`HandoffReminderPolicy`。只有 Domain 返回应提醒且当前事项 UUID 尚未记录时，才返回一条
待发送提醒并写入进程内去重状态。同一事项在同一进程只产生一次；Application 不发送系统通知。

### 切片 7：本地日期时间解析

解析器接受没有偏移量的当地日期时间和 IANA 时区标识，返回：

- `resolved`：唯一确定的带偏移时刻；
- `invalid`：夏令时跳跃导致当地时间不存在；
- `ambiguous`：夏令时回拨导致当地时间出现两次；
- 明确输入错误：日期格式或时区标识无效。

解析不得让 Foundation 自动替用户移动到邻近合法时间，也不得在重复时间中静默选择一个偏移。
Swift Application 测试读取并通过
`test-vectors/v1/resolve-local-date-time.json`。

### 切片 8：Application 共享生命周期向量

Swift Application 测试读取 `test-vectors/v1/application-lifecycle.json`，覆盖：

- 规划首次标记失败后的幂等重试；
- 未来待办确认与未确认删除；
- 自然完成保留历史；
- 打开状态包含逾期和今日未来待办；
- 用户删除与自然完成分离；
- 整组计划替换不擦除完成历史；
- 相对日期换算为确定日期。

## 错误与一致性

- 领域校验错误原样作为用例失败，不执行仓库写入。
- 找不到 active 源事项时，规划和编辑失败，不创建替代数据。
- 当日整组保存失败时，不标记未来待办 `planned`。
- `planned` 标记失败时不回滚已成功的当日计划；依赖 UUID 幂等重试恢复。
- 仓库 fake 只模拟端口语义，不复制 Domain 排程算法。
- Application 返回面向调用方的结构化状态，不生成 UI 文案。

## 测试策略

每个切片严格执行红—绿—重构：

1. 先用固定 Clock 和内存 fake 写一个失败测试。
2. 运行最小 Application 测试，确认因缺少目标行为而失败。
3. 实现使该测试通过的最小代码。
4. 重新运行该测试和 Application test target。
5. 切片完成后运行整个 Swift Package。

共享向量测试固定 UUID、时刻和时区，不依赖设备当前日期、地区设置或数据库。纯 Swift 修改不
重复运行 C# 全量测试，因为本设计不修改共享合同或 C# 代码。

## 完成条件

- Package 同时提供 `AttentionGuardianDomain` 和 `AttentionGuardianApplication` library。
- Domain target 不依赖 Application。
- Application 不依赖 SQLite、SwiftUI 或平台通知实现。
- Repository fake 能证明写入顺序、整组边界和规划幂等重试。
- Clock 完全可替换，测试不读取真实现在。
- Application 生命周期与本地时间共享向量全部通过。
- 先通过最小 Application 测试，再通过完整 `swift test`。
- `PROJECT_DEVELOPMENT.md` 和本地 `teaching.md` 只追加记录实际完成的操作与测试证据。
- 不修改 `HANDOFF.md`，不提交、推送、Tag、Release 或发布。

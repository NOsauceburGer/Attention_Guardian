# 架构说明

## 目标

架构服务于两个目标：

1. 保证核心时间规则可以独立测试和解释。
2. 让 UI、数据库和 Windows 通知可以替换，而不改写业务规则。

## 分层结构

```text
Desktop ───────────────┐
                       ▼
Application ────────> Core
     ▲
     │ implements
Infrastructure
```

### AttentionGuardian.Core

包含领域模型、值对象、时间计算和纯业务规则。

- 不依赖 Avalonia。
- 不依赖 SQLite。
- 不依赖 Windows API。
- 不执行文件、网络或数据库 I/O。

### AttentionGuardian.Application

包含用例、服务接口和应用级数据传输对象。

- 可以依赖 Core。
- 定义持久化、通知和时钟等能力的接口。
- 不依赖具体 SQLite 或 Windows 实现。
- 当前由 `FocusSessionService` 组织开始、刷新和清除单个专注会话；状态暂存于进程内存。
- `FocusSessionService` 可接收一个或多个固定事件，并通过 Core 选择下一事件；单事件桌面入口复用同一流程，不维护第二套规则。
- `LocalDateTimeResolver` 负责把本地日期时间和系统时区解析为确定时刻，并拒绝无效或重复的夏令时时间。
- `TodoPlanningService` 组织新的多待办流程：创建有时间待办、执行 Core 试算、仅提交成功计划、保存日期事项，以及生成一次打开界面状态。
- `IScheduledTodoRepository` 与 `IUnscheduledTodoRepository` 分别定义两类数据库能力，Application 不认识 SQLite 文件、表或 SQL。
- `ScheduledTodoSelector` 位于 Core，使用 `[start, end)` 匹配当前事件，跨零点事件在完整区间内仍可匹配。

### AttentionGuardian.Infrastructure

包含 Application 接口的具体实现。

- SQLite 数据访问。
- 本地设置。
- 平台通知。
- 时间和系统能力适配。
- `SqliteFocusSessionRepository` 实现 Application 的会话持久化接口。
- SQLite 文件位于用户本地应用数据目录，不放在源码仓库。
- Desktop 默认使用用户本地应用数据目录；设置
  `ATTENTION_GUARDIAN_DATA_DIRECTORY` 时只把两个数据库重定向到指定本地目录，
  用于隔离的真实窗口验收。该覆盖不改变 schema、仓库职责或跨库写入顺序。

### AttentionGuardian.Desktop

包含 Avalonia Views、ViewModels、样式和应用组合入口。

- 负责显示和用户交互。
- 不直接访问数据库。
- ViewModel 不承载核心时间计算。
- ViewModel 负责收集输入、调用 Application 用例并提供可绑定状态；窗口计时器只触发状态刷新。

## 核心时间规则

```text
handoffTime =
    startTime
    - preparationDuration
    - travelDuration
    - safetyBuffer
```

```text
safeUntil = nextFixedEvent.handoffTime
```

当当前时间达到或超过 `handoffTime` 时，应用进入交接状态。

多个固定事件按 `startTime` 选择：忽略已经开始的事件，在尚未开始或恰好开始的事件中选择开始时间最早者。事件恰好开始时仍被选中，以确保应用进入交接而不是错误显示“没有下一事项”。

若提前量会使交接时间超出 `DateTimeOffset` 支持的日期范围，领域模型在创建时立即拒绝该事件，不把异常推迟到页面显示或通知阶段。

## 时间策略

- Core 使用 `DateTimeOffset` 表达已经确定偏移量的真实时刻，使用 `TimeSpan` 表达准备、路程和安全缓冲。
- Core 不把未指定时区的 `DateTime` 当作固定事件，也不负责把用户输入的本地钟表时间解析成真实时刻。
- 本地日期时间与时区的转换位于 Application 与 Infrastructure 边界。第一版保存 Windows 时区标识，同时保留已经解析的 UTC 时刻和原始偏移量。
- 夏令时导致的无效本地时间必须拒绝并要求用户修改；重复时间必须让用户明确选择较早或较晚的偏移量，不静默猜测。
- SQLite 中的真实时刻统一保存为 UTC ISO 8601 文本，持续时间保存为整数秒；该格式属于 Infrastructure，不进入 Core。
- Core 的纯规则显式接收当前时间，不读取系统时钟。Application 需要当前时间时使用 .NET `TimeProvider`，测试可替换它。

时间扣减按真实经过时长计算。数据库实现前还需用集成测试确认序列化可以无损往返。

## 数据边界

领域模型、数据库实体和 UI 展示模型可以在确有差异时分离。不要为了形式提前复制所有模型，也不要让数据库字段直接成为整个系统的事实来源。

### 旧版单会话 SQLite 模型（遗留）

第一版只保存一条当前会话记录，固定主键为 `1`。保存字段包括当前任务、事件 UTC 开始时刻、原始偏移量，以及准备、路程、安全缓冲的整数秒。

- `safeUntil` 和当前状态不保存，因为它们可以由 Core 根据事件和当前时间重新计算。
- 写入使用 upsert，新的当前会话替换旧会话。
- 清除会话只删除当前记录，不删除整个数据库文件。
- 数据库连接只存在于单次操作期间；当前实现关闭连接池，确保文件生命周期明确。
- 数据库表由仓库首次操作时创建；正式扩展更多表前需要增加显式 schema 版本和迁移机制。

该数据库只服务早期最小体验版，不是当前多待办正式存储结构；退役或迁移方案仍需
在发布维护工作中明确。

### 目标双数据库边界

多待办持久化拆为两个独立 SQLite 数据库：

1. **当日待办数据库**：文件名 `attention-guardian-scheduled.db`，保存所有有明确开始时刻和持续时间的事件。“当日”是业务分类名，事件可以跨零点。该数据库支持排序、冲突检测结果和事务式整组排程更新。
2. **未来待办数据库**：文件名 `attention-guardian-future.db`，保存只有确定日期的事项，不保存虚构的开始或结束时刻，不参与排程或冲突检测。领域类型仍使用 `UnscheduledTodo`，但 UI 统一显示“未来待办”。

两个数据库分别维护单行 `schema_version` 表。未来待办库当前为 v1；当日待办库当前为
v2。首次打开空数据库时按顺序在同一事务中应用全部迁移，发现高于当前仓库支持范围的
版本时拒绝打开，避免旧程序误写新结构。v1 表分别为 `scheduled_todo` 和
`unscheduled_todo`，不得跨库创建另一类表。当日待办库 v2 增加非负整数
`current_selection_priority`：旧事件迁移后为 0，新录入事件取现有最大值加一。

未来待办采用保留记录的生命周期状态：`active`、`planned`、`deleted`。普通日期查询只返回 `active`；成功规划后改为 `planned`，确认删除后改为 `deleted`。阶段 A 已建立字段与约束，具体状态转换仓库方法和 Application 可恢复流程在阶段 B 接入。

Application 为两类存储定义不同接口，Infrastructure 分别实现。不得让 Core 依赖数据库文件，也不得让 UI 直接组合跨数据库写入。启动时由 Application 读取未来待办数据库，只执行一次“今天是否存在未规划事项”检查。

相对日期输入在 Application 边界使用当前日期换算成确定 `DateOnly` 后再保存；数据库不保存“两天后”这种会随时间变化的表达。

当前 Application 合同已经确定：

- 当日待办仓库读取整张时间表，并通过 `ReplaceAllAsync` 接收一份完整成功计划。该方法的 Infrastructure 实现必须在单个数据库事务内整体替换，不能逐条暴露半完成状态。
- 待办事项仓库按确定日期读取并逐项保存，不参与排程。
- `TodoPlanningService.AddScheduledTodoAsync` 在调用仓库前完成时间范围创建和 Core 试算；不可移动事件冲突作为带冲突组的完整计划写入。
- 未提供开始时间时使用 `TimeProvider` 的当前本地时刻；结束时刻由开始时刻加持续时间得到。
- 日历日期直接保存；相对日期必须恰好提供一种输入，并在保存前换算。
- `LoadOpeningStateAsync` 同时查询当前有时间事件和日期小于等于今天的活动未来待办集合。该方法表示一次应用打开流程；持续刷新当前时间时不得重复调用未来待办检查。

### 添加类型路由

Desktop 的添加入口先产生一个明确的类型选择，再调用对应 Application 用例：

```text
添加事件
├── 当日待办 → AddScheduledTodoAsync → IScheduledTodoRepository
└── 未来待办 → AddUnscheduledTodoAsync → IUnscheduledTodoRepository
```

类型路由属于 UI/Application 边界。Infrastructure 不根据字段是否为空猜测类型，两个仓库也不互相调用。用户尚未选择类型时，不创建领域对象、不读取表单专属数据，也不写入任何数据库。

### 到期未来待办状态转换

打开状态返回的不能只有布尔值；接入 UI 前需要扩展为日期已到的未来待办集合，使每条提醒具有稳定标识和名称。

```text
到期未来待办
├── 规划
│   ├── 打开当日待办页并预填名称
│   ├── 未保存返回 → 未来待办保持活动 → 继续提醒
│   └── 当日待办成功保存 → 原未来待办退出活动列表
└── 删除
    ├── 显示含事件名的确认弹窗
    ├── 取消 → 不修改
    └── 确认 → 原未来待办退出活动列表
```

规划成功涉及两个独立数据库，不能伪装成单个 SQLite 事务。Application 必须组织可恢复的顺序：

1. 先把当日待办计划完整写入当日待办数据库。
2. 写入成功后，再让对应未来待办退出活动列表。
3. 如果第一步失败，未来待办保持不变。
4. 如果第二步失败，必须保留可重试状态，避免重复创建当日待办；具体补偿与幂等策略在双数据库 schema/迁移设计时确定。

未来待办退出活动列表采用状态标记，不物理删除：成功规划标记为 `planned`，确认删除标记为 `deleted`，活动查询只返回 `active`。这保留了跨数据库第二步失败后的重试和诊断依据。

阶段 B 使用原未来待办 `Guid` 作为转换后当日待办的 `Guid`。规划流程先检查当日时间表是否已经存在该标识：不存在时读取 active 源事项、执行试算并保存整张计划；已存在时视为第一步已经完成，只重试 `planned` 状态更新。状态更新本身也允许重复写入同一目标状态。因此第二个数据库暂时失败后，重试不会创建重复事件或再次推动时间表。

确认删除只有在 Application 收到明确确认后才调用 `MarkDeletedAsync`；取消不访问写入方法。`planned` 与 `deleted` 不能互相转换，避免把已经规划的事项误当成用户删除。

## 排程变更边界

自动顺延采用“计算计划，再提交结果”的两阶段方式：

```text
读取当日时间表
→ 在内存中模拟顺延
→ 检查强制事件占用与跨日结果
→ 整体通过后一次性持久化
```

试算属于 Core/Application 的确定性规则，不在 ViewModel 或 SQL 中临时计算。Infrastructure 最终应使用 SQLite 事务一次性写入整个排程变更；发生冲突或写入失败时，原时间表保持不变。

### 可保存的不可移动冲突与管理重排

新的排程结果需要区分“存储失败”和“存在待管理冲突”。不可移动事件互相重叠不再是保存失败，而是包含完整时间表与冲突组的有效结果；SQLite 仍在单事务中保存全部事件。真正的写入异常才回滚。

管理重排由 Application 组织并调用 Core 的确定性计算：

1. 普通拖拽以原计划最早开始时刻为锚点重新排列。
2. 不可移动事件保持原时间，普通事件无法放入时越过阻挡事件。
3. 冲突或首尾相接的不可移动事件形成连续组；用户对组内顺序确认后，从组内最早开始时刻连续重排。
4. 删除以被删除事件原开始时刻为补位锚点。
5. 气泡编辑收起后一次性提交名称、持续时间和不可移动属性；离开未收起编辑时由 Desktop 询问是否保存。

ViewModel 只提交用户意图，不直接计算时间、不直接写 SQL。

### 专注显示与通知边界

Core/Application 可以继续使用完整时间范围判断当前事件和五分钟切换点，但专注 ViewModel 不提供任何可见时间文本。用户点击开始属于会话内展示许可，不修改排程数据。

五分钟前提醒由 Desktop 每五秒在应用进程内调度，并通过 Windows App SDK 发送系统
通知；进程退出后不保留后台任务。只有当前事件与下一非休息事件首尾相接且当前事件
持续不少于五分钟时调度。发送失败时不崩溃，本地验收页提供独立测试入口；窗口最小化
和不同 Windows 通知设置仍需纳入发布前实机矩阵。

Desktop 是未打包的 framework-dependent Windows 应用。`AppNotificationManager`
依赖 Windows App Runtime 的 Framework、Main、Singleton 与 DDLM 包；v0.1.0
安装器必须部署 Microsoft Windows App Runtime 2.3.1 x64。通知测试页保留底层异常
类型与 HRESULT，避免把运行时缺失错误误报为用户关闭通知权限。

提醒资格现由 Core 的 `HandoffReminderPolicy` 纯规则计算。它返回当前事件、首尾相接
的下一事件、五分钟前提醒时刻、当前是否已进入提醒窗口，以及不符合条件的明确原因。
提醒窗口使用 `[结束前五分钟, 当前事件结束)`；当前事件恰好五分钟时可从开始时刻
提醒，短于五分钟时不提醒。

Application 的 `HandoffReminderService` 每次从当日待办仓库读取计划，使用
`TimeProvider` 取得当前时刻，并在进程内按当前事件标识去重。同一事件在一次进程
生命周期中只产生一次待发送提醒；应用退出后不保留后台状态。Windows 通知发送、
最小化窗口验证和平台降级仍属于后续 Infrastructure/Desktop 接入。

跨日是合法结果，不作为计算错误。Application 返回跨日标识供 Desktop 显示可关闭提示；关闭提示不改变领域数据。

### 待办领域模型与区间语义

- `TodoItem` 保存所有待办共有的稳定标识、标题和强制属性。
- `ScheduledTodo` 表示具有明确开始与结束时刻的定时待办；所属日期由开始时刻的本地日期得出。
- `ScheduledTodo.CurrentSelectionPriority` 只用于多个事件同时覆盖当前时刻时的候选选择。
  新录入事件使用更大的值，因此不可移动事件重叠时新输入项优先成为当前事件；移动、
  编辑和自动顺延都保留该值，不把管理页位置误当成录入先后。
- `ScheduleConflictDetector` 从完整持久化时间表重新计算所有两两重叠的不可移动事件。
  首尾相接不算冲突；三个事件共同重叠时返回三个事件对。Application 打开状态必须
  携带该结果，不能只依赖刚保存时仍留在内存中的 `ScheduleTrialResult`。
- `UnscheduledTodo` 只保存指定日期，不伪造开始或结束时刻，也不参与具体时段匹配。
- `TimeRange` 使用左闭右开区间 `[start, end)`：开始时刻属于事件，结束时刻不属于事件；前一事件恰好结束、后一事件恰好开始时允许首尾相接。
- 时间区间必须具有正持续时间。顺延只改变开始与结束时刻，稳定标识、标题、强制属性和持续时间保持不变。

### 纯内存排程试算

`ScheduleTrial` 以拟新增定时待办为锚点，在内存中计算完整结果：

1. 与新增时段无关且已经结束的事件保持不变。
2. 与新增事件重叠的普通事件移动到新增事件之后，并继续级联推动紧邻的普通事件。
3. 遇到足够容纳顺延链条的空档后停止推动，后续事件保持不变。
4. 顺延链条与强制事件首尾相接是合法结果；真实占用强制事件区间时，把普通事件整体移动到该强制事件结束后，再继续级联。
5. 连续遇到多个强制事件时依次越过。普通事件与强制事件之间不产生冲突结果。
6. 新增加的不可移动事件与已有不可移动事件重叠时返回完整计划和冲突组，不自动选择优先级。
7. 任一事件的开始日期因顺延或越过强制事件进入更晚日期时，结果设置跨日标识。

试算结果和输入对象都是不可变的。Core 不负责把结果写入数据库；未来由 Application 决定是否提交，Infrastructure 使用 SQLite 事务保证整体写入。

## 最终验收与诊断边界

v0.1.0 之后增加本地交互式诊断界面。它消费与正式排程相同的 Core 结果和解释步骤，不在 UI 中复制第二套算法。

- 验收数据使用专门的样例库或内存数据，不默认读取用户真实待办。
- 诊断记录是一次试算的临时解释，不作为排程事实来源，也不替代自动化测试。
- Core/Application 可以产生结构化步骤，例如“重叠”“顺延”“越过强制事件”“强制事件互相冲突”“跨日”；Desktop 把它翻译成简短日常语言。
- 冲突结果只能显示“原计划未修改”，不能提供绕过规则的强制提交入口。
- 该功能不得引入远程控制、隐藏身份验证绕过或生产安全后门。

## 通知与恢复

- 通知实现位于 Infrastructure。
- 核心层只产生“何时应该交接”的确定结果。
- 通知不可用时，Desktop 提供应用内降级提醒。
- 重启后依据持久化状态和当前时间重新计算，不盲信旧倒计时。

## 测试策略

- Core：纯单元测试覆盖时间规则和边界条件。
- Application：用例测试使用内存替身，不依赖真实 SQLite。
- Infrastructure：针对 SQLite 和平台适配器的集成测试。
- Desktop：仅对关键交互增加必要测试，不用 UI 测试替代业务测试。
## 管理排程合同

管理页不直接修改领域对象或计算时间。`ScheduleManagement` 是纯 Core
服务，负责普通事件优先级重排、不可移动事件挡板、连续不可移动组、组内重排、
删除补位、编辑后的队列重算和“休息”插入。它只接收完整时间表并返回完整结果，
不依赖 UI、仓库或 SQLite。

`ScheduleManagementService` 位于 Application，负责加载完整计划、调用 Core
规则并通过 `IScheduledTodoRepository.ReplaceAllAsync` 整体保存。拖拽失败时
Core 返回实际落点与是否发生回落，Desktop 只负责显示
“这个事件没办法放在这里”，不得自行猜测落点。

管理服务通过可替换的 `TimeProvider` 判断活动边界。`TimeRange.End <= now` 的任务
必须标记为完成，并由活动查询排除；不得通过 `ReplaceAllAsync` 物理删除其数据库
记录。随后所有编辑、删除和重排只面对活动排程，避免完成历史干扰列表索引。v0.1.0
不提供已完成历史页，但完成状态和完成时刻仍须持久化，为后续经授权的分析保留依据。

当日待办数据库 v3 区分 `active`、`completed` 和用户主动删除状态，并保存确定的
完成时刻。打开应用、刷新当前任务和进入管理页都会先把到达结束边界的活动记录标记
完成。活动排程整组更新只把缺失的活动记录标记删除，再 upsert 新活动计划，不使用
整表物理删除。v1/v2 通过事务迁移到 v3；迁移失败回滚，高版本继续拒绝打开。

`LoadOpeningStateAsync` 仍从持久化时间表恢复冲突，但只对结束时刻晚于当前时刻的
任务执行 `ScheduleConflictDetector`。Desktop 必须把返回的 `MandatoryConflicts`
复制到可观察集合并通知 `HasConflicts`，不能只消费刚保存时的试算结果。

未来待办管理继续走独立的 `IUnscheduledTodoRepository`。管理页只有主动展开后
才调用 `LoadAllActiveAsync`；编辑使用 `UpdateActiveAsync`，该操作只能更新
`active` 记录，不能把已经 planned 或 deleted 的记录重新激活。

## Desktop ambient 渲染边界

持续背景由 Desktop 的 `AmbientFocusSurface` 负责，不进入 ViewModel 或 Core。
控件使用 Skia runtime effect 接收时间、专注混合值和一次性转换脉冲；ViewModel
只提供 `IsFocusStarted` 与当前任务标识，不计算帧、颜色或 shader 参数。窗口隐藏、
最小化、关闭或控件脱离视觉树时，Desktop 停止渲染计时器；平台绘制能力不可用时
保留下层静态渐变。该边界避免视觉实现污染排程规则，也便于后续独立调整性能。

一次性启动开场由同层的 `LaunchMotionSurface` 负责。它在窗口第一次 `Activated`
时启动，使时间轴从窗口真正进入前台后计算；数据初始化仍由 `Opened` 路径并行执行。
开场使用独立 Skia runtime effect 绘制中心扩散流体，并在末段把整层交叉淡出到
`AmbientFocusSurface`。`Guardian` 由五段内置 SVG/Bezier 单线路径和
`SKPathMeasure.GetSegment` 按真实路径长度绘制，不经过字体布局。开场层结束后隐藏
且关闭命中，不保留逐帧渲染。Desktop 测试同时检查 shader 编译和矢量路径解析，
避免运行时静默退化成黑场或缺失字迹。

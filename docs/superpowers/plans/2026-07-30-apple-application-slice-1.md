# Apple Application Slice 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Apple Application target, its minimal Clock and scheduled repository ports, and a tested use case that inserts one scheduled todo through the existing Domain trial and atomically replaces the complete record set.

**Architecture:** `AttentionGuardianApplication` depends only on `AttentionGuardianDomain` and Foundation. `AddScheduledTodoUseCase` reads lifecycle records through a repository port, gets the current instant through a Clock port, completes due records, constructs a proposed Domain todo, runs `ScheduleTrial.insert`, preserves non-active history with `TodoLifecycle.replaceActiveSchedule`, and performs one whole-record replacement.

**Tech Stack:** Swift 6.2, Swift Package Manager, Foundation, Swift Testing, existing `AttentionGuardianDomain`.

## Global Constraints

- Application may depend on `AttentionGuardianDomain` and Foundation only.
- `AttentionGuardianDomain` must not depend on Application.
- Do not add SQLite, SwiftUI, system notification delivery, an Xcode app, or platform persistence.
- Tests must use a fixed Clock and must not read the real current time.
- Start and end use determined `Date` instants; end is derived from start plus a strictly positive duration.
- Whole-schedule replacement must preserve completed/deleted history.
- Do not modify `HANDOFF.md`.
- Append actual implementation and verification evidence to `PROJECT_DEVELOPMENT.md` and local `teaching.md`.
- The workspace has no usable `.git`; do not invent commits, branch state, or diffs.

---

## File Structure

- Modify `apple/AttentionGuardianDomain/Package.swift`: expose the Application library and test target.
- Create `apple/AttentionGuardianDomain/Sources/AttentionGuardianApplication/Clock.swift`: replaceable current-instant port.
- Create `apple/AttentionGuardianDomain/Sources/AttentionGuardianApplication/ScheduledTodoRepository.swift`: whole-record repository port.
- Create `apple/AttentionGuardianDomain/Sources/AttentionGuardianApplication/AddScheduledTodo.swift`: request, result, validation error, and use case.
- Create `apple/AttentionGuardianDomain/Tests/AttentionGuardianApplicationTests/AddScheduledTodoTests.swift`: real use-case behavior tests with fixed inputs.
- Create `apple/AttentionGuardianDomain/Tests/AttentionGuardianApplicationTests/TestDoubles.swift`: fixed Clock and specific in-memory repository fake.
- Append `PROJECT_DEVELOPMENT.md`: actual slice result and exact test evidence.
- Append `teaching.md`: beginner-facing explanation and limitations.

### Task 1: Establish the Application target through a compile-time red test

**Files:**
- Modify: `apple/AttentionGuardianDomain/Package.swift`
- Create: `apple/AttentionGuardianDomain/Sources/AttentionGuardianApplication/Clock.swift`
- Create: `apple/AttentionGuardianDomain/Tests/AttentionGuardianApplicationTests/AddScheduledTodoTests.swift`

**Interfaces:**
- Consumes: existing `AttentionGuardianDomain.ScheduledTodoRecord`.
- Produces: a package product named `AttentionGuardianApplication` and a test target with the same dependency.

- [ ] **Step 1: Add package/test scaffolding without production implementation**

Update `Package.swift` so products and targets contain:

```swift
.library(
    name: "AttentionGuardianApplication",
    targets: ["AttentionGuardianApplication"])
```

```swift
.target(
    name: "AttentionGuardianApplication",
    dependencies: ["AttentionGuardianDomain"]),
.testTarget(
    name: "AttentionGuardianApplicationTests",
    dependencies: [
        "AttentionGuardianApplication",
        "AttentionGuardianDomain"
    ])
```

Create `AddScheduledTodoTests.swift` with the first public-API test:

```swift
import Foundation
import Testing
import AttentionGuardianDomain
@testable import AttentionGuardianApplication

@Suite("Add scheduled todo")
struct AddScheduledTodoTests {
    @Test
    func omittedStartUsesClockAndPersistsOneCompleteReplacement() async throws {
        let now = Date(timeIntervalSince1970: 1_775_000_000)
        let repository = ScheduledTodoRepositoryFake(records: [])
        let useCase = AddScheduledTodoUseCase(
            repository: repository,
            clock: FixedClock(now: now))
        let id = try #require(
            UUID(uuidString: "00000000-0000-0000-0000-000000000501"))

        let result = try await useCase.execute(
            AddScheduledTodoRequest(
                id: id,
                title: "第一项",
                start: nil,
                duration: 1_800,
                utcOffsetSeconds: 28_800,
                isMandatory: false))

        let saved = try #require(repository.replacements.only)
        #expect(saved.count == 1)
        #expect(saved[0].todo.id == id)
        #expect(saved[0].todo.start == now)
        #expect(saved[0].todo.end == now.addingTimeInterval(1_800))
        #expect(saved[0].todo.currentSelectionPriority == 0)
        #expect(result.scheduledTodos == saved.map(\.todo))
    }
}
```

Create `Sources/AttentionGuardianApplication/Clock.swift` containing only:

```swift
import Foundation
```

This is a module scaffold, not a production API; it lets SwiftPM compile the new target far enough to produce
the intended missing-API red.

In the same test file, add the lifecycle/priority test shown in Task 3 and all three Domain-output/zero-write
tests shown in Task 4 before any production implementation. The test doubles referenced by these tests
intentionally do not exist yet. The production changes the initial red set catches are using a real clock,
calculating the wrong end, failing to perform the whole replacement, calculating priority from history,
dropping historical records, ignoring an explicit start, persisting before a successful trial, swallowing
conflict metadata, or accepting a non-positive duration.

- [ ] **Step 2: Run the narrow test target and verify RED**

Run:

```bash
swift test --filter AddScheduledTodoTests
```

Expected: build failure because `AttentionGuardianApplication`, `AddScheduledTodoUseCase`, and the test doubles do not yet exist. A manifest syntax error is not an acceptable red; fix package syntax until the failure names missing slice APIs.

- [ ] **Step 3: Record the checkpoint without committing**

Do not run `git add` or `git commit`: this workspace has no usable `.git`, and the user has not authorized commits. Keep the red-test evidence in the session for the later development log.

### Task 2: Add the minimal ports and make the first behavior green

**Files:**
- Modify: `apple/AttentionGuardianDomain/Sources/AttentionGuardianApplication/Clock.swift`
- Create: `apple/AttentionGuardianDomain/Sources/AttentionGuardianApplication/ScheduledTodoRepository.swift`
- Create: `apple/AttentionGuardianDomain/Sources/AttentionGuardianApplication/AddScheduledTodo.swift`
- Create: `apple/AttentionGuardianDomain/Tests/AttentionGuardianApplicationTests/TestDoubles.swift`

**Interfaces:**
- Produces:
  - `public protocol Clock: Sendable { var now: Date { get } }`
  - `public protocol ScheduledTodoRepository: Sendable`
  - `func loadAll() async throws -> [ScheduledTodoRecord]`
  - `func replaceAll(_ records: [ScheduledTodoRecord]) async throws`
  - `public struct AddScheduledTodoRequest: Sendable`
  - `public struct AddScheduledTodoResult: Equatable, Sendable`
  - `public struct AddScheduledTodoUseCase: Sendable`
  - `func execute(_ request: AddScheduledTodoRequest) async throws -> AddScheduledTodoResult`

- [ ] **Step 1: Add specific test doubles**

Create:

```swift
import Foundation
import AttentionGuardianDomain
@testable import AttentionGuardianApplication

struct FixedClock: Clock {
    let now: Date
}

final class ScheduledTodoRepositoryFake:
    ScheduledTodoRepository,
    @unchecked Sendable
{
    var records: [ScheduledTodoRecord]
    private(set) var replacements: [[ScheduledTodoRecord]] = []
    var replacementError: (any Error)?

    init(records: [ScheduledTodoRecord]) {
        self.records = records
    }

    func loadAll() async throws -> [ScheduledTodoRecord] {
        records
    }

    func replaceAll(_ records: [ScheduledTodoRecord]) async throws {
        if let replacementError { throw replacementError }
        replacements.append(records)
        self.records = records
    }
}

extension Array {
    var only: Element? {
        count == 1 ? self[0] : nil
    }
}
```

The fake simulates only the Application port. It does not duplicate scheduling logic.

- [ ] **Step 2: Implement the two ports**

`Clock.swift`:

```swift
import Foundation

public protocol Clock: Sendable {
    var now: Date { get }
}
```

`ScheduledTodoRepository.swift`:

```swift
import AttentionGuardianDomain

public protocol ScheduledTodoRepository: Sendable {
    func loadAll() async throws -> [ScheduledTodoRecord]
    func replaceAll(_ records: [ScheduledTodoRecord]) async throws
}
```

- [ ] **Step 3: Implement the minimal use case**

`AddScheduledTodo.swift` starts with:

```swift
import Foundation
import AttentionGuardianDomain

public struct AddScheduledTodoRequest: Sendable {
    public let id: UUID
    public let title: String
    public let start: Date?
    public let duration: TimeInterval
    public let utcOffsetSeconds: Int
    public let isMandatory: Bool

    public init(
        id: UUID,
        title: String,
        start: Date?,
        duration: TimeInterval,
        utcOffsetSeconds: Int,
        isMandatory: Bool
    ) {
        self.id = id
        self.title = title
        self.start = start
        self.duration = duration
        self.utcOffsetSeconds = utcOffsetSeconds
        self.isMandatory = isMandatory
    }
}

public enum AddScheduledTodoError: Error, Equatable {
    case invalidDuration
    case currentSelectionPriorityOverflow
}

public struct AddScheduledTodoResult: Equatable, Sendable {
    public let scheduledTodos: [ScheduledTodo]
    public let conflicts: [ScheduleConflict]
    public let hasRolloverToNextDay: Bool
}

public struct AddScheduledTodoUseCase: Sendable {
    private let repository: any ScheduledTodoRepository
    private let clock: any Clock

    public init(
        repository: any ScheduledTodoRepository,
        clock: any Clock
    ) {
        self.repository = repository
        self.clock = clock
    }

    public func execute(
        _ request: AddScheduledTodoRequest
    ) async throws -> AddScheduledTodoResult {
        guard request.duration > 0 else {
            throw AddScheduledTodoError.invalidDuration
        }

        let records = try await repository.loadAll()
        let completed = TodoLifecycle.completeDue(records, at: clock.now)
        let active = completed
            .filter { $0.status == .active }
            .map(\.todo)
        let maximumPriority = active.map(\.currentSelectionPriority).max()
        guard maximumPriority != Int64.max else {
            throw AddScheduledTodoError.currentSelectionPriorityOverflow
        }
        let priority = maximumPriority.map { $0 + 1 } ?? 0
        let start = request.start ?? clock.now
        let proposed = try ScheduledTodo(
            id: request.id,
            title: request.title,
            start: start,
            end: start.addingTimeInterval(request.duration),
            utcOffsetSeconds: request.utcOffsetSeconds,
            isMandatory: request.isMandatory,
            currentSelectionPriority: priority)
        let trial = try ScheduleTrial.insert(proposed, into: active)
        let replacement = TodoLifecycle.replaceActiveSchedule(
            completed,
            with: trial.scheduledTodos)

        try await repository.replaceAll(replacement)
        return AddScheduledTodoResult(
            scheduledTodos: trial.scheduledTodos,
            conflicts: trial.conflicts,
            hasRolloverToNextDay: trial.hasRolloverToNextDay)
    }
}
```

- [ ] **Step 4: Run the narrow test and verify GREEN**

Run:

```bash
swift test --filter AddScheduledTodoTests.omittedStartUsesClockAndPersistsOneCompleteReplacement
```

Expected: the one test passes with no Swift concurrency warnings.

- [ ] **Step 5: Run both Swift targets**

Run:

```bash
swift test
```

Expected: existing five Domain tests plus the initial Application red set now pass.

### Task 3: Verify lifecycle history, priority, and explicit-start semantics

**Files:**
- Modify: `apple/AttentionGuardianDomain/Tests/AttentionGuardianApplicationTests/AddScheduledTodoTests.swift`
- Modify only if a red test proves necessary: `apple/AttentionGuardianDomain/Sources/AttentionGuardianApplication/AddScheduledTodo.swift`

**Interfaces:**
- Consumes: Task 2 public APIs.
- Produces: verified behavior that due records become completed, historical records survive replacement, explicit start overrides Clock, and priority is derived only from active todos.

- [ ] **Step 1: Confirm the lifecycle/priority test was part of the initial RED set**

The following test must already have been added in Task 1, before Task 2 production code:

```swift
@Test
func completesDueRecordsPreservesHistoryAndUsesNextActivePriority() async throws {
    let now = Date(timeIntervalSince1970: 1_775_010_000)
    let old = try ScheduledTodo(
        id: #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000511")),
        title: "已到期",
        start: now.addingTimeInterval(-7_200),
        end: now.addingTimeInterval(-3_600),
        currentSelectionPriority: 99)
    let active = try ScheduledTodo(
        id: #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000512")),
        title: "活动项",
        start: now.addingTimeInterval(3_600),
        end: now.addingTimeInterval(5_400),
        currentSelectionPriority: 4)
    let historical = try ScheduledTodo(
        id: #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000513")),
        title: "删除历史",
        start: now.addingTimeInterval(-20_000),
        end: now.addingTimeInterval(-19_000))
    let repository = ScheduledTodoRepositoryFake(records: [
        ScheduledTodoRecord(todo: old),
        ScheduledTodoRecord(todo: active),
        ScheduledTodoRecord(todo: historical, status: .deleted)
    ])
    let useCase = AddScheduledTodoUseCase(
        repository: repository,
        clock: FixedClock(now: now))
    let explicitStart = now.addingTimeInterval(7_200)

    _ = try await useCase.execute(
        AddScheduledTodoRequest(
            id: #require(UUID(uuidString:
                "00000000-0000-0000-0000-000000000514")),
            title: "新增",
            start: explicitStart,
            duration: 1_800,
            utcOffsetSeconds: 0,
            isMandatory: false))

    let saved = try #require(repository.replacements.only)
    let completed = try #require(saved.first {
        $0.todo.id == old.id
    })
    let deleted = try #require(saved.first {
        $0.todo.id == historical.id
    })
    let added = try #require(saved.first {
        $0.todo.title == "新增"
    })
    #expect(completed.status == .completed)
    #expect(completed.completedAt == now)
    #expect(deleted.status == .deleted)
    #expect(added.todo.start == explicitStart)
    #expect(added.todo.currentSelectionPriority == 5)
}
```

The production mutations caught are calculating priority from completed history, dropping deleted history,
failing to complete due records, and ignoring an explicit start.

- [ ] **Step 2: Run only the lifecycle/priority test after Task 2 and verify GREEN**

Run:

```bash
swift test --filter AddScheduledTodoTests.completesDueRecordsPreservesHistoryAndUsesNextActivePriority
```

Expected: pass. Its valid RED evidence was collected in Task 1 when the Application API did not exist.

- [ ] **Step 3: Make the smallest correction if the test exposes a defect**

Only change `AddScheduledTodoUseCase.execute`; do not add repository methods or persistence concepts. Ensure `completeDue` runs before filtering active records and `replaceActiveSchedule` receives the completed record set.

- [ ] **Step 4: Re-run the Application tests**

Run:

```bash
swift test --filter AddScheduledTodoTests
```

Expected: both tests pass.

### Task 4: Verify Domain trial output and all-or-nothing writes

**Files:**
- Modify: `apple/AttentionGuardianDomain/Tests/AttentionGuardianApplicationTests/AddScheduledTodoTests.swift`
- Modify: `apple/AttentionGuardianDomain/Tests/AttentionGuardianApplicationTests/TestDoubles.swift`
- Modify only if a red test proves necessary: `apple/AttentionGuardianDomain/Sources/AttentionGuardianApplication/AddScheduledTodo.swift`

**Interfaces:**
- Consumes: Task 2 use case and repository fake.
- Produces: verified rollover/conflict propagation and zero replacement on invalid input or trial failure.

- [ ] **Step 1: Confirm these tests were part of the initial RED set**

The following mandatory-conflict test must already have been added in Task 1:

```swift
@Test
func returnsMandatoryConflictFromDomainAndPersistsTheValidPlan() async throws {
    let start = Date(timeIntervalSince1970: 1_775_020_000)
    let existing = try ScheduledTodo(
        id: #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000521")),
        title: "固定 A",
        start: start,
        end: start.addingTimeInterval(3_600),
        isMandatory: true,
        currentSelectionPriority: 0)
    let repository = ScheduledTodoRepositoryFake(records: [
        ScheduledTodoRecord(todo: existing)
    ])
    let useCase = AddScheduledTodoUseCase(
        repository: repository,
        clock: FixedClock(now: start.addingTimeInterval(-3_600)))

    let result = try await useCase.execute(
        AddScheduledTodoRequest(
            id: #require(UUID(uuidString:
                "00000000-0000-0000-0000-000000000522")),
            title: "固定 B",
            start: start.addingTimeInterval(1_800),
            duration: 3_600,
            utcOffsetSeconds: 0,
            isMandatory: true))

    #expect(result.conflicts.count == 1)
    #expect(result.conflicts[0].mandatoryTodo.id == existing.id)
    #expect(repository.replacements.count == 1)
}
```

The following zero-write validation test must already have been added in Task 1:

```swift
@Test
func invalidDurationDoesNotWrite() async {
    let repository = ScheduledTodoRepositoryFake(records: [])
    let useCase = AddScheduledTodoUseCase(
        repository: repository,
        clock: FixedClock(now: Date(timeIntervalSince1970: 1_775_030_000)))

    await #expect(throws: AddScheduledTodoError.invalidDuration) {
        try await useCase.execute(
            AddScheduledTodoRequest(
                id: UUID(),
                title: "无效",
                start: nil,
                duration: 0,
                utcOffsetSeconds: 0,
                isMandatory: false))
    }
    #expect(repository.replacements.isEmpty)
}
```

The duplicate-ID trial failure test must also already exist from Task 1. It uses an existing active record and
the same request UUID, asserts `ScheduleTrialError.duplicateIdentifier`, and asserts an empty replacement list.

The tests catch replacing before trial success, swallowing conflict metadata, and persisting invalid input.

- [ ] **Step 2: Run the initial Domain-output/zero-write tests after Task 2 and verify GREEN**

Run:

```bash
swift test --filter AddScheduledTodoTests
```

Expected: pass. Their valid RED evidence was collected in Task 1 before the production APIs existed. Do not
weaken assertions if a behavior remains red.

- [ ] **Step 3: Apply only proven corrections**

If required:

- validate duration before `loadAll`;
- call `replaceAll` only after `ScheduleTrial.insert` succeeds;
- copy `trial.conflicts` and `trial.hasRolloverToNextDay` into the result.

Do not catch and translate `ScheduleTrialError.duplicateIdentifier`.

- [ ] **Step 4: Run the Application target**

Run:

```bash
swift test --filter AttentionGuardianApplicationTests
```

Expected: every slice-1 Application test passes.

- [ ] **Step 5: Run the whole package**

Run:

```bash
swift test
```

Expected: all existing Domain suites and all new Application tests pass with zero failures.

### Task 5: Document verified implementation without expanding scope

**Files:**
- Modify: `PROJECT_DEVELOPMENT.md`
- Modify: `teaching.md`

**Interfaces:**
- Consumes: actual red/green commands and final test output from Tasks 1–4.
- Produces: append-only historical evidence; no product API.

- [ ] **Step 1: Append the project development entry**

Append a dated section that states:

- Application library and test target were added;
- Clock and scheduled repository ports were added;
- `AddScheduledTodoUseCase` completes due records, derives active priority, calls Domain trial, preserves history, and makes one whole replacement;
- exact narrow and full `swift test` counts/output;
- no SQLite, SwiftUI, notification adapter, App, C# changes, or `HANDOFF.md` changes were made.

Do not write planned later slices as completed.

- [ ] **Step 2: Append the teaching entry**

Explain for a beginner:

- the problem solved;
- actual files and operations;
- why Clock and Repository are ports rather than database implementations;
- where Application sits between Domain and Persistence;
- how red/green tests proved the behavior;
- consequences of skipping the boundary;
- alternatives;
- what needs to be understood now;
- errors, sandbox limitations, Git absence, and unfinished slices.

- [ ] **Step 3: Run final verification after documentation**

Run:

```bash
swift test
```

Expected: unchanged successful result. Documentation changes must not alter build output.

- [ ] **Step 4: Inspect final scope**

Run:

```bash
find Sources/AttentionGuardianApplication Tests/AttentionGuardianApplicationTests -maxdepth 3 -type f -print
```

Expected: only the ports, add-scheduled use case, tests, and test doubles listed in this plan. Confirm no SQLite, SwiftUI, notification delivery, or app target exists.

- [ ] **Step 5: Stop at the slice boundary**

Report the files changed, exact tests run, and any limitations. Do not begin future-todo, opening, management, planning retry, reminders, or local-time parsing until slice 1 has been reviewed.

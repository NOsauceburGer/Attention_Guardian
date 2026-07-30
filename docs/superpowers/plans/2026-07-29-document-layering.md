# Document Layering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the existing documentation in place so the next Agent sees one current Apple migration path while the released Windows baseline and historical records remain intact.

**Architecture:** Root documents keep their current paths and receive one clear responsibility. `HANDOFF.md` becomes a replaceable current snapshot, `AGENTS.md` carries durable rules and the active order of work, and platform/detail documents remain authoritative through links rather than copied text.

**Tech Stack:** Markdown, `rg`, shell read-only validation, local append-only `teaching.md`.

## Global Constraints

- Keep every existing documentation path; do not move files into `docs/apple`, `docs/windows`, or `docs/shared`.
- Do not create Apple source directories, Swift packages, Swift files, JSON test vectors, database schemas, or build commands.
- Preserve Windows v0.1.0 source, documentation, release evidence, and validation history.
- Replace `HANDOFF.md` completely; do not preserve its historical timeline there.
- Only append to `PRODUCT_DECISIONS.md`, `PROJECT_DEVELOPMENT.md`, and `teaching.md`.
- Do not modify `PRODUCT.md`, `DESIGN.md`, `USER_GUIDE.md`, `CHANGELOG.md`, `SECURITY.md`, or `SUPPORT.md` unless validation finds a direct factual contradiction.
- Do not commit, push, tag, or publish. The current workspace does not contain a detectable `.git` directory.
- Do not claim a build or test result; this plan changes documentation only.

---

### Task 1: Replace HANDOFF with the current Apple migration snapshot

**Files:**
- Replace: `HANDOFF.md`
- Read: `docs/CROSS_PLATFORM_RULES.md`
- Read: `DESIGN.md`
- Read: `ROADMAP.md`

**Interfaces:**
- Consumes: confirmed Windows release status, PD-048 through PD-050, cross-platform rule specification, Apple SwiftUI design language.
- Produces: the single current entry point for the next Agent.

- [x] **Step 1: Replace the old historical handoff**

Write a concise handoff containing exactly these top-level sections:

```markdown
# Attention Guardian 当前工作交接

## 一句话状态
## 已确认且不得误改
## 当前尚未开始
## 下一位 Agent 的唯一任务
## 后续实施顺序
## 本轮禁止事项
## 必读文件
## 验证要求
## 工作区说明
```

The content must state:

- Windows v0.1.0 is released and retained.
- Apple uses SwiftUI, a shared Swift Core for macOS/iOS, and no MCP/.NET runtime bridge.
- `docs/CROSS_PLATFORM_RULES.md` and the Apple section of `DESIGN.md` are confirmed.
- No Swift package, Apple database, JSON vector, or SwiftUI prototype exists yet.
- The next task is only to define shared JSON test-vector format, extract initial vectors from C# tests, and make C# tests read them.
- Swift Domain starts only after C# verifies those vectors.
- Application, Infrastructure, SQLite, notifications, and SwiftUI remain out of the next task.
- `teaching.md` is local, ignored, and append-only.

- [x] **Step 2: Verify HANDOFF is a snapshot, not a history**

Run:

```bash
wc -l HANDOFF.md
rg -n '2026-07-2[5-8]|用户给下一对话|UI 大改|候选稿|发布前状态' HANDOFF.md
```

Expected:

- `HANDOFF.md` is approximately 100–150 lines.
- None of the old historical section labels are present.

### Task 2: Make AGENTS the durable rulebook with one current priority

**Files:**
- Modify: `AGENTS.md`
- Read: `docs/CROSS_PLATFORM_RULES.md`
- Read: `DESIGN.md`

**Interfaces:**
- Consumes: current cross-platform architecture and approved implementation order.
- Produces: durable Agent constraints plus the only active priority sequence.

- [x] **Step 1: Add a documentation responsibility map**

Add a concise section that defines:

- `HANDOFF.md` is replaceable current state.
- `PRODUCT_DECISIONS.md` is append-only decision history.
- `PROJECT_DEVELOPMENT.md` is append-only implementation history.
- `teaching.md` is local append-only learning history.
- `DESIGN.md` separates Apple SwiftUI and Windows Avalonia.
- `docs/CROSS_PLATFORM_RULES.md` is the C#/Swift behavior contract.

- [x] **Step 2: Replace the obsolete current priorities**

Replace the Windows release-preparation list with this ordered sequence:

```text
1. Define the versioned, platform-neutral JSON test-vector envelope and operation schemas.
2. Extract the first vectors from existing C# Core/Application tests.
3. Make C# tests consume and pass the shared vectors.
4. Create the Apple Swift Package structure only after the vectors are verified.
5. Implement AttentionGuardianDomain in Swift with test-first parity.
6. Implement Apple Application, then Persistence/Infrastructure.
7. Build SwiftUI Design Tokens and components only after domain and application boundaries pass.
```

Also state that the immediate next task stops after step 3.

- [x] **Step 3: Verify no completed Windows work remains active**

Run:

```bash
sed -n '/## 当前优先级/,$p' AGENTS.md
rg -n '修复“完成任务|首次提交|创建.*Tag|GitHub Release|v0.1.x 之后再' AGENTS.md
```

Expected:

- The active section points to shared test vectors.
- Completed Windows release work is not listed as current work.

### Task 3: Align architecture, roadmap, README, and setup entry points

**Files:**
- Modify: `ARCHITECTURE.md`
- Modify: `ROADMAP.md`
- Modify: `README.md`
- Modify: `SETUP.md`

**Interfaces:**
- Consumes: root documentation responsibility map and current Apple migration stage.
- Produces: platform-aware navigation without speculative Apple commands or source layout.

- [x] **Step 1: Add an architecture status map**

Near the top of `ARCHITECTURE.md`, add a short status block that distinguishes:

- Windows current/released: C# Core → Application → Infrastructure → Avalonia Desktop.
- Apple target/not yet scaffolded: Swift Domain → Swift Application → Swift Persistence → SwiftUI macOS/iOS.
- Shared contract: `docs/CROSS_PLATFORM_RULES.md` plus future shared test vectors.

Do not remove the detailed Windows architecture history.

- [x] **Step 2: Turn the Apple roadmap into ordered gates**

Replace the current flat Apple list with these gates:

1. Shared test-vector format and C# reader.
2. Swift `AttentionGuardianDomain`.
3. Swift Application protocols and use cases.
4. Apple Persistence/Infrastructure, migration, recovery, and notification adapters.
5. SwiftUI Design System and macOS/iOS applications.
6. Platform-specific accessibility, signing, packaging, and real-device acceptance.

Mark only the already confirmed specifications as complete; leave implementation gates incomplete.

- [x] **Step 3: Clarify README audience**

Keep Windows v0.1.0 download and user instructions unchanged. Add a short “Apple development status” paragraph linking:

- `HANDOFF.md`
- `docs/CROSS_PLATFORM_RULES.md`
- `DESIGN.md`
- `ROADMAP.md`

It must say Apple has no runnable client yet.

- [x] **Step 4: Scope SETUP to real commands**

Add an opening platform note:

- Existing commands and verified versions describe the current C#/.NET/Windows client.
- Xcode/Swift commands will be added only after an Apple project exists and is verified.
- Do not invent an Apple setup procedure from the design documents.

- [x] **Step 5: Validate links and platform claims**

Run:

```bash
for f in HANDOFF.md docs/CROSS_PLATFORM_RULES.md DESIGN.md ROADMAP.md; do test -f "$f" || exit 1; done
rg -n 'Apple|Swift|Windows|Avalonia' ARCHITECTURE.md ROADMAP.md README.md SETUP.md
rg -n 'swift build|xcodebuild|Package.swift' SETUP.md
```

Expected:

- All linked files exist.
- Platform status is explicit.
- No unverified Swift build command is present.

### Task 4: Record the layering decision and verify the full handoff

**Files:**
- Append: `PRODUCT_DECISIONS.md`
- Append: `PROJECT_DEVELOPMENT.md`
- Append: `teaching.md`
- Validate: all root documentation files

**Interfaces:**
- Consumes: completed Tasks 1–3.
- Produces: durable decision history, implementation history, beginner-facing explanation, and evidence that the next Agent sees one task.

- [x] **Step 1: Append a product/process decision**

Add the next sequential PD entry recording:

- Documentation paths stay stable.
- `HANDOFF.md` is replaceable and non-historical.
- Durable history lives in decision/development files.
- The current Apple implementation gate is shared test vectors.

- [x] **Step 2: Append the development record**

Record exactly which documents changed, that no source code or Swift scaffold was created, and which validation commands were run.

- [x] **Step 3: Append the teaching chapter**

Explain for a beginner:

- why a handoff differs from a history log;
- what was changed;
- why Windows remains useful as an oracle;
- where this sits in the development lifecycle;
- how to verify the next task;
- consequences of leaving contradictory priorities;
- alternatives considered;
- what the user needs to understand;
- limitations and unfinished work.

- [x] **Step 4: Run full documentation validation**

Run:

```bash
rg -n '^## (下一位|用户给下一|下一检查点|当前优先级)' \
  AGENTS.md HANDOFF.md PROJECT_DEVELOPMENT.md
rg -n '共享.*测试向量|JSON.*测试向量|Swift Domain' \
  AGENTS.md HANDOFF.md ARCHITECTURE.md ROADMAP.md
rg -n 'SwiftUI|Avalonia' DESIGN.md HANDOFF.md ARCHITECTURE.md
rg -n 'TBD|TODO|待补充|稍后填写' \
  AGENTS.md HANDOFF.md ARCHITECTURE.md ROADMAP.md README.md SETUP.md
find . -maxdepth 3 -type f \
  \( -name '*.swift' -o -name 'Package.swift' -o -name '*.json' \) -print
```

Expected:

- `AGENTS.md` and `HANDOFF.md` expose one current task: shared JSON test vectors.
- Windows and Apple platform ownership is explicit.
- No placeholders exist in the updated entry documents.
- No Swift source, Swift package manifest, or new JSON test vector has been created.

- [x] **Step 5: Report without claiming code verification**

Report:

- files changed;
- the next Agent’s one task;
- that Windows remains the released baseline;
- that no build/tests were run because no source code changed;
- that no commit was created because `.git` is absent and no commit was authorized.

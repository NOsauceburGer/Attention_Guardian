# Break Time Entity Implementation Plan

**Goal:** Replace the management break template card with one reusable circular
Liquid Glass time entity that morphs into a duration stepper and uses the same
spatial drag lifecycle as ordinary events.

**Architecture:** Keep duration as Presentation draft state and insertion in the
existing Application use case. Add a focused break-control state model beside
the existing spatial drag model, render the control as one SwiftUI view with a
circle-to-capsule morph in a fixed bottom-center highest-depth layer, reveal
stepper content only after geometry settles, and route pointer/touch drag
updates through the existing in-app spatial drag infrastructure instead of
native drag snapshots.

**Tech Stack:** Swift 6, SwiftUI, Swift Testing, native macOS/iOS 26 Clear
Liquid Glass with `ultraThinMaterial` fallback, Spring animations, existing
AttentionGuardian Presentation/Application layers.

## Global Constraints

- Use no absolute screen coordinates, `.position`, screenshot `.offset`,
  `UIScreen`, `NSScreen`, or layout-calculating `GeometryReader`.
- Do not calculate schedule time in SwiftUI.
- Preserve Reduce Motion and Reduce Transparency fallbacks.
- Use `glassEffect(.clear.interactive())` on macOS/iOS 26 for the resting
  entity and Lens Core; use `.ultraThinMaterial` only on older systems.
- Never simulate glass transparency by lowering the whole view opacity.
- Use the shared 64 point management Capsule height and 32 point continuous
  corner radius for collapsed events, queue-end insertion and the break stepper.
- The parent drag gesture must coexist with stepper buttons; clicks below the
  drag threshold must reach both minus and plus controls.
- Keep the entity at the safe-area bottom center on wide and compact layouts;
  do not create a right-side template column.
- Complete shape morphing before stepper controls fade in; reverse that order
  when collapsing.
- Keep the template reusable and inserted records named `休息`.
- Do not modify `HANDOFF.md`.
- Commit only after automated verification and real UI acceptance.

## Task 1: Break control state and interaction contract

**Files:**
- Modify: `apple/AttentionGuardianDomain/Tests/AttentionGuardianPresentationTests/AdaptiveLayoutTests.swift`
- Modify: `apple/AttentionGuardianDomain/Sources/AttentionGuardianPresentation/ManagementSurface.swift`

- [ ] Write a failing test proving a double activation changes a collapsed
  break control into its expanded state and a second activation collapses it.
- [ ] Run the focused Presentation test and confirm the missing state contract
  causes the expected failure.
- [ ] Add the minimal explicit break-control state model.
- [ ] Run the focused test and confirm it passes.

## Task 2: Circle-to-capsule Liquid Glass component

**Files:**
- Create: `apple/AttentionGuardianDomain/Sources/AttentionGuardianPresentation/BreakTimeEntity.swift`
- Modify: `apple/AttentionGuardianDomain/Sources/AttentionGuardianPresentation/ManagementSurface.swift`
- Modify: `apple/AttentionGuardianDomain/Tests/AttentionGuardianPresentationTests/AdaptiveLayoutTests.swift`

- [ ] Write a failing test for the component's semantic accessibility contract:
  collapsed state exposes `休息，20 分钟`; expanded state exposes duration
  adjustment and collapse actions without losing the rest identity.
- [ ] Implement one shape-morphing view using `thickMaterial`, restrained
  specular overlays, ambient glow, soft shadow, and a continuous stepper.
- [ ] Add double-click/tap Spring motion, one transient liquid ripple, and
  Reduce Motion/Transparency fallbacks.
- [ ] Run focused Presentation tests.

## Task 3: Reuse the ordinary-event spatial drag lifecycle

**Files:**
- Modify: `apple/AttentionGuardianDomain/Sources/AttentionGuardianPresentation/BreakTimeEntity.swift`
- Modify: `apple/AttentionGuardianDomain/Sources/AttentionGuardianPresentation/ManagementSurface.swift`
- Modify: `apple/AttentionGuardianDomain/Tests/AttentionGuardianPresentationTests/SpatialScheduleDragTests.swift`

- [ ] Write a failing test proving a reusable break drag can progress through
  press, lift, drag, magnetize, settle and return to idle without consuming the
  source template.
- [ ] Route macOS pointer and iOS touch input through an in-app break drag
  session that shares ordinary-event thresholds, anchoring and target previews.
- [ ] Render the moving break entity in the same overlay as ordinary-event Lens
  Core bubbles; remove the native `.draggable` preview path.
- [ ] Preserve click/keyboard insertion at queue end as the accessible fallback.
- [ ] Run focused drag and Presentation tests.

## Task 4: Verification, records and acceptance

**Files:**
- Modify: `PROJECT_DEVELOPMENT.md`
- Append only: `teaching.md`

- [ ] Run `swift test --package-path apple/AttentionGuardianDomain`.
- [ ] Run `git diff --check` and the repository forbidden-layout scan.
- [ ] Rebuild and relaunch the standard validation application.
- [ ] Verify collapsed/expanded visuals, duration changes, repeated insertion,
  drag-before-event, queue-end drop, Reduce Motion, and persistence through
  relaunch.
- [ ] Record verified evidence and any remaining physical-pointer limitation.
- [ ] Commit the complete break-time-entity feature once.

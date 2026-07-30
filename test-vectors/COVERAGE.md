# Shared Vector Coverage

This matrix is the gate defined by `docs/CROSS_PLATFORM_RULES.md` section 9. The future Swift Domain
must execute the same case identifiers. A checked row means the case is stored in JSON and currently
executed by `AttentionGuardian.SharedVectors.Tests`; it does not mean a Swift implementation exists.

| Rule | Shared vector cases |
| --- | --- |
| 1. `[start, end)` and touching boundary | `inclusive-start`, `exclusive-end`, `touching-boundary-selects-next` |
| 2. Cross-midnight current and end boundary | `cross-midnight`, `exclusive-end` |
| 3. Normal insertion, cascade, gap stop, duration | `cascade-stops-at-gap`, `cascade-across-two-normal-todos` |
| 4. Skip one and multiple mandatory blockers | `skip-one-mandatory-preserves-duration`, `skip-multiple-mandatory-and-rollover` |
| 5. Mandatory collision saved and latest input selected | `mandatory-overlap-is-saved`, `latest-conflict-priority` |
| 6. Rollover updates date and flag | `skip-multiple-mandatory-and-rollover` |
| 7. Normal reorder, blocker fallback, mandatory group reorder | `normal-reorder-without-blocker`, `normal-reorder-falls-behind-mandatory`, `mandatory-continuous-group-reorders-from-group-start` |
| 8. Delete, edit, start-time conflict choices, and break constraints | `delete-pulls-next-forward`, `edit-duration-rebuilds-following-schedule`, `edit-start-moves-existing-after-edited`, `edit-start-truncates-only-first-existing`, `edit-start-rejects-mandatory-occupant`, `insert-break-keeps-reserved-title`, `break-cannot-be-renamed`, `break-can-be-mandatory` |
| 9. Due future todos, planning recovery, confirmed delete | `opening-includes-overdue-and-due-future-todos`, `planning-retry-is-idempotent`, `unconfirmed-delete-keeps-active`, `confirmed-delete-is-distinct` |
| 10. Completion/deletion separation and retained history | `natural-completion-preserves-history`, `user-delete-is-not-natural-completion`, `completed-history-survives-plan-replacement` |
| 11. Reminder eligibility and every reason | `eligible-at-five-minute-boundary`, `no-current-todo`, `current-too-short`, `no-adjacent-next`, `next-is-break`, `outside-window` |
| 12. Invalid and ambiguous DST input | `invalid-spring-forward-time`, `ambiguous-fall-back-time` |

Additional normative input coverage:

- `relative-future-date-becomes-exact-date`
- `normal-new-york-time`

The nine interactive acceptance-scenario topics in `AcceptanceScenarioRunner` are also represented:
normal cascading, multiple mandatory blockers, mandatory collision, rollover, gap stop, touching
boundary, cross-midnight current selection, eligible reminder, and break/gap reminder rejection.

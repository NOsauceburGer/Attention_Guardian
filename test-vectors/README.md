# Attention Guardian Shared Test Vectors

These files are the executable behavior contract shared by the Windows C# Core and the future
Apple Swift Domain. They do not define UI, persistence, notification delivery, or platform APIs.

## Version 1

Each operation file uses this envelope:

```json
{
  "schemaVersion": 1,
  "operation": "selectCurrent",
  "cases": []
}
```

- `schemaVersion` is an integer. A reader must reject versions it does not support.
- `operation` selects one input/output schema and is never inferred from optional fields.
- `cases[].id` is a stable lower-kebab-case diagnostic identifier.
- UUID values are lowercase canonical strings.
- Instants are ISO 8601 strings with an explicit numeric offset.
- Local dates use `YYYY-MM-DD`.
- Durations use positive integer seconds.
- Scheduled outputs are ordered by `start`, then `end`, then canonical UUID string.
- Conflict pairs are ordered by proposed UUID and mandatory UUID.
- Tests must not use the system clock, random UUIDs, or database ordering.

`v1/schema.json` is the machine-readable JSON Schema for the envelope, common values, and every
operation introduced in v1. Adding cases does not change the schema version. Changing field meaning,
removing a field, or adding an incompatible operation shape requires a new version directory.


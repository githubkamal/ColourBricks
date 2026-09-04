# ADR 0002 — RBAC model and the seeded permission matrix

- **Status:** Accepted
- **Date:** 2026-09-02
- **Context task:** P0-T05

## Context

Plan §9 fixes the mechanism: permissions are `module.action` strings, roles hold
permissions, users hold one role plus optional per-project grants, authorisation is a
policy handler + `[HasPermission("...")]`, and **no code compares a role name**.

The data is less fixed. BRD §3 lists 27 modules; §62 gives an **example** 9-row
granular matrix ("Actual permissions will be configurable by the Administrator");
§60 names five roles but only spells out Administrator ("full") and Accounts Team
(via §61 enabled/disabled modules and §63 the access list). Project Manager,
Management and Data Entry User get one-line descriptions only.

## Decision

1. **Catalogue = 27 modules × 8 actions = 216 `Permission` rows.** Modules are BRD §3
   normalised to `snake_case` keys (`vendors`, `bank_reconciliation`, `reports`, … —
   matching plan §9's own examples). Actions: view, add, edit, delete, approve,
   reconcile, export, print.
2. **One checked-in fixture is the source of truth:**
   `backend/src/ColourBricks.Infrastructure/Identity/Permissions/permission-matrix.json`
   (embedded resource). The `IdentitySeeder` seeds from it; the
   `PermissionSeed_MatchesBrdMatrix` test loads the *same* file and asserts the DB
   grants match it exactly. Editing the matrix is a one-file change.
3. **Administrator** → `"*"` (every permission). Flattened into the JWT as a single
   `permissions: "*"` claim so a 216-entry token never blows the ~4 KB cookie limit;
   other roles get a space-delimited `permissions` claim.
4. **Accounts Team** → transcribed from BRD §61 + §62 + §63: full view/add/edit +
   export/print on the financial modules, `approve` on projects/expenses/payments,
   `reconcile` on bank_reconciliation, view/export/print on reports, **no `delete`
   anywhere**, nothing on users/roles/permissions/admin_configuration/audit_trail.
5. **Project Manager, Management, Data Entry User** → conservative defaults derived
   from §60's one-liners, flagged `"derived": true` in the fixture. Project Manager =
   project-operational + scoped to assigned projects (§64); Management = read + export
   only; Data Entry User = view/add on the transaction-entry modules. **These need
   client confirmation** (review.md Q12; gap 6 re: the undefined `approve` semantics).
   Low risk to change later — the matrix is one file and Admin-configurable at runtime
   (P1-T09).
6. **Project scoping** (`IProjectScopeFilter`): a user with zero `UserProjectAccess`
   rows is unrestricted; one or more rows restrict them to those project ids. Resolved
   once in the data layer, not per controller.

## Consequences

- `approve` permissions are seeded but enforce nothing yet — there is no approval
  workflow in the BRD (gap 6). They are placeholders on the matrix.
- Permissions are baked into the access token at login/refresh, so a role change
  takes effect on the **next token refresh** (≤ access-token lifetime, 15 min), not
  instantly. Plan §9 accepts this ("re-issued on refresh").
- Adding a module later = add it to the fixture's `modules` list; the seeder creates
  the 8 new permission rows and the test enforces the count.

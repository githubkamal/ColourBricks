# Colour Bricks — Engineering Plan

**Source of truth for requirements:** `Colour_Bricks_BRD_v1_2.md` (v1.2, 02 Sep 2026)
**Companion documents:** `tasks.md` (executable task list), `review.md` (feedback, risks, open questions)

---

## 1. How to use these documents

`plan.md` is the standing context. Any Claude session working on this codebase should read it first and treat it as binding: it defines the stack, the domain model, the naming and layering conventions, and the definition of done.

`tasks.md` is the work queue. Each task is self-contained and carries its own scope, acceptance criteria, validation steps and test list.

**Prompt template for a new session:**

```
Read plan.md, then execute Task P4-T05 from tasks.md.

Rules:
- Follow every convention in plan.md sections 4-11. Do not invent new patterns.
- Do not touch files outside the task's declared scope.
- Do not start any task listed in "Depends on" that is not already marked [x].
- Write the tests named in the task's Tests block before you write the feature code.
- Finish by running the Validation block and pasting the output.
- Update the task's checkbox and add a one-line completion note in tasks.md.

Reference BRD sections: 35, 37, 39.
```

If a task turns out to be underspecified, the session should stop and ask rather than guess. Financial logic guessed wrong is worse than financial logic not written.

---

## 2. Product in one paragraph

A construction project financial management system for a single company (Colour Bricks) running multiple concurrent building projects. It tracks money in and money out per project, keeps vendor and subcontractor balances, imports bank statements and reconciles them against what was already recorded, allocates one consolidated payment across several projects, and reports profitability against budget. The hard part is not CRUD. The hard part is that the same rupee must never be counted twice, and that project balances, vendor balances and bank balances must always agree.

**Scale assumptions** (confirm with client, see `review.md` Q1): 5-20 concurrent projects, under 20 users, low thousands of transactions per month, single company, INR only, no GST filing integration.

---

## 3. Tech stack

| Layer | Choice | Notes |
|---|---|---|
| Backend | .NET (LTS), ASP.NET Core Web API | Pin the exact SDK in `global.json`. Use whichever LTS you have installed; do not mix. |
| ORM | EF Core + `Pomelo.EntityFrameworkCore.MySql` | Pomelo's major version must match the EF Core major version. Check NuGet for the current pairing before pinning. |
| Database | MySQL 8 | Dev via XAMPP. **Read section 3.1 — this is a trap.** |
| Frontend | Next.js (App Router), TypeScript strict | React Server Components for read pages, client components for forms and grids. |
| UI kit | Tailwind CSS + shadcn/ui | |
| Data fetching | TanStack Query | Server state only. No Redux. |
| Forms | React Hook Form + Zod | Zod schemas shared with API contract types. |
| Tables | TanStack Table | Every list screen in this app is a filterable, sortable, paginated grid. |
| Charts | Recharts | Dashboards only. |
| Validation (API) | FluentValidation | |
| Logging | Serilog, structured, to console + rolling file | |
| Auth | JWT access token + rotating refresh token, httpOnly cookies | |
| Backend tests | xUnit, FluentAssertions, Testcontainers (MySQL), Respawn | |
| Frontend tests | Vitest + React Testing Library | |
| E2E | Playwright | |
| Migrations | EF Core migrations, checked into source | |

### 3.1 The XAMPP problem — read this before Task P0-T02

**XAMPP does not ship MySQL. It ships MariaDB**, while still labelling it "MySQL" in the control panel. MariaDB and MySQL 8 have diverged enough to matter here:

- MySQL 8's default collation `utf8mb4_0900_ai_ci` does not exist in MariaDB. **Set `utf8mb4` / `utf8mb4_unicode_ci` explicitly on every table** so schemas are portable.
- MariaDB has no native `JSON` column type; `JSON` is an alias for `LONGTEXT`. EF Core's JSON column mapping and owned-entity-as-JSON will behave differently. **Avoid JSON columns for anything you need to query.** The audit trail old/new values are the one acceptable exception, stored as text.
- `SELECT ... FOR UPDATE SKIP LOCKED` needs MariaDB 10.6+. The allocation engine uses row locking; verify your bundled version.
- Pomelo's feature detection reads the server version string. Always use `ServerVersion.AutoDetect(connectionString)`, never a hardcoded `MySqlServerVersion`.

**Decision:** develop against XAMPP for convenience if you want, but **run integration tests against real MySQL 8 via Testcontainers**, and deploy to real MySQL 8. Task P0-T09 sets this up. Do not let a MariaDB-only schema reach production.

If you'd rather avoid the drift entirely, run `docker run -p 3306:3306 mysql:8` for dev and keep XAMPP only for phpMyAdmin.

---

## 4. Repository layout

```
colour-bricks/
├── global.json
├── Directory.Build.props            # shared LangVersion, Nullable, TreatWarningsAsErrors
├── backend/
│   ├── ColourBricks.sln
│   ├── src/
│   │   ├── ColourBricks.Api/            # controllers, DI wiring, middleware, Program.cs
│   │   ├── ColourBricks.Application/    # use cases, DTOs, validators, interfaces
│   │   ├── ColourBricks.Domain/         # entities, value objects, domain services, enums
│   │   └── ColourBricks.Infrastructure/ # EF Core, repositories, file storage, importers
│   └── tests/
│       ├── ColourBricks.UnitTests/
│       └── ColourBricks.IntegrationTests/
├── frontend/
│   ├── src/app/(auth)/                  # login
│   ├── src/app/(app)/                   # authenticated shell
│   │   ├── projects/ vendors/ labour/ materials/
│   │   ├── donations/ loans/ accounts/ reconciliation/
│   │   ├── expenses/ reports/ admin/
│   ├── src/components/                  # shared UI
│   ├── src/features/<module>/           # module-scoped components, hooks, schemas
│   ├── src/lib/                         # api client, formatters, auth
│   └── tests/e2e/
├── docs/
│   ├── Colour_Bricks_BRD_v1_2.md
│   ├── plan.md
│   ├── tasks.md
│   ├── review.md
│   └── adr/                             # one file per significant decision
└── db/
    └── seed/                            # seed scripts for masters and demo data
```

**Backend layering rule:** `Api → Application → Domain`, `Infrastructure → Application/Domain`. Domain references nothing. No EF Core types in Domain or Api.

**Frontend rule:** everything module-specific lives in `src/features/<module>/`. `src/components/` is for things used by three or more modules.

---

## 5. Core domain model

This is the most important section in the document. Get this wrong and every phase after Phase 2 fights it.

### 5.1 The central idea: separate obligation from settlement

BRD sections 22 and 37 both say the same thing in different words. Encode it in the schema, not in developer discipline.

- An **Obligation** is money owed. A vendor purchase, a subcontractor's agreed work value, a temple donation allocation, a client's contract value, an EMI instalment. Creating one records an expense (or receivable) and increases an outstanding balance.
- A **Settlement** is money moving. A vendor payment, a client receipt, a labour payment. It reduces an outstanding balance and changes a cash/bank balance. **It never creates an expense.**
- An **Allocation** links a settlement to one or more obligations and to one or more projects.

A payment recorded with no matching obligation is a *vendor advance* (BRD 25), which is an obligation running the other way.

### 5.2 Entity sketch

```
Project           ProjectId, Code (unique), Name, ClientId, Status, ContractValue,
                  EstimatedCost, StartDate, ExpectedEndDate, ActualEndDate, ManagerId

Party             PartyId, Type (Vendor|Subcontractor|Client|Temple|Lender),
                  Name, NormalisedName, Category, Phone, Email, Address,
                  GstNumber, BankDetails, IsActive
                  -- one table for all counterparties; a hardware shop can also
                     be a subcontractor. Do not make four near-identical tables.

Obligation        ObligationId, Type, ProjectId, PartyId, Date, Amount,
                  Description, SourceDocumentRef, Status, CreatedBy
ObligationLine    for purchase line items: ItemId, Qty, Unit, Rate, TaxAmount, LineTotal

Settlement        SettlementId, Direction (In|Out), PartyId (nullable),
                  Date, Amount, PaymentModeId, AccountId, ReferenceNo,
                  Description, Status (Active|Reversed), ReversalOfId, CreatedBy

Allocation        AllocationId, SettlementId, ObligationId (nullable),
                  ProjectId, Amount, Method (Fifo|Manual|Auto), CreatedBy, CreatedAt
                  -- nullable ObligationId supports "pay against project, not a
                     specific invoice" and advances.

LedgerEntry       LedgerEntryId, EntryDate, ProjectId (nullable), AccountId (nullable),
                  PartyId (nullable), CategoryId, Debit, Credit,
                  SourceType, SourceId, IsReversal, CreatedAt
                  -- append-only. Never UPDATE, never DELETE.

ImportBatch       ImportBatchId, AccountId, FileName, UploadedBy, UploadedAt,
                  Status (Draft|Committed|Discarded), row counts by outcome
                  -- P4-T01: an upload lands as a Draft batch of StagedBankRow for
                     review; only a Commit promotes survivors to BankTransaction.

StagedBankRow     StagedBankRowId, ImportBatchId, SourceLineNo, ValueDate, Narration,
                  Debit, Credit, Balance, BankReference, ParseState (Parsed|Error),
                  ParseError, DuplicateOfBankTransactionId
StagedBankRowAllocation  StagedBankRowId, ProjectId, Amount
                  -- debit rows: 1..n, must sum to Debit. credit rows: exactly 1
                     (BRD §34 / rule 46 — a credit is never split across projects).

BankTransaction   BankTransactionId, ImportBatchId, AccountId, ValueDate, Narration,
                  Debit, Credit, BankReference, RunningBalance, RowHash (unique),
                  Status (Pending|InReview|Reconciled|Excluded|InternalTransfer),
                  ExclusionReason, ReconciledBy, ReconciledAt
                  -- created only at commit, always Pending. Rows the accountant
                     removed at review are dropped with no fingerprint kept.
BankTransactionProjectHint  BankTransactionId, ProjectId, Amount
                  -- the import-review mapping, carried as a hint only; it pre-fills
                     the reconciliation screen. Commit creates no Settlement /
                     Allocation / ledger rows — reconciliation (P4-T05..T07) does.

ReconciliationLink  LinkId, BankTransactionId, SettlementId, MatchConfidence,
                    MatchMethod (Auto|Suggested|Manual), CreatedBy, CreatedAt,
                    UnlinkedAt, UnlinkedBy, UnlinkReason
```

### 5.3 Balances are derived, never stored

BRD rule 53 is explicit: totals must come from linked transaction records. So:

- Vendor outstanding = `SUM(obligations where PartyId=X) - SUM(allocations to those obligations)`
- Project outstanding = same, scoped by ProjectId
- Bank balance = `opening + SUM(credits) - SUM(debits)` from LedgerEntry
- Project actual cost = `SUM(LedgerEntry.Debit where ProjectId=X and Category is a cost category)`

**Do not add an `OutstandingAmount` column that you keep in sync with UPDATE statements.** It will drift, and you will spend a week finding out why Project C says ₹30,000 and the vendor statement says ₹28,500.

If a report gets slow (it will, around Phase 8), add a *materialised* snapshot table that is rebuilt from ledger entries by a job, plus a nightly consistency check that compares snapshot to derived. Never make the snapshot authoritative. Task P9-T02 covers this.

### 5.4 Money

- `decimal` in C#, `DECIMAL(18,2)` in MySQL, configured with `.HasPrecision(18, 2)`.
- Never `float`, `double`, or JavaScript `number` for arithmetic. On the frontend, format for display only; never compute totals client-side that the server also computes.
- Currency is INR, single-currency. Do not build multi-currency.
- Display with `Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' })`. The BRD is written in lakhs and crores; the client will expect `₹1,15,000` grouping, not `₹115,000`.

**Rounding rule for split allocations.** When dividing an amount across N targets: compute `floor(amount / N)` to 2 decimal places for the first N-1 targets, and give the final target the remainder so the parts sum exactly to the whole. Applies to equal common-expense distribution (BRD 45) and percentage allocation (BRD 46). Implement once in `Domain/Services/AmountSplitter.cs` and unit test it against `₹100,000 / 3` and `₹100 / 7`.

### 5.5 Dates

- Business dates (transaction date, purchase date, work date, EMI date) are `DateOnly` in C# and `DATE` in MySQL. No time, no timezone.
- Audit timestamps are `DateTimeOffset` stored UTC.
- On the frontend, treat business dates as `YYYY-MM-DD` strings until display. Do not pass them through `new Date()` and back; that is how a 1st-of-month entry becomes the 31st.
- Financial year is April to March (Indian convention). Report date presets must include "This FY" and "Previous FY" alongside the calendar presets in BRD 57.

### 5.6 Nothing is deleted

Financial records are cancelled, reversed or voided (BRD rule 32, BRD 33). Concretely:

- `LedgerEntry` is append-only. A reversal writes a new mirrored entry with `IsReversal = true` and a link to the original.
- `Settlement` and `Obligation` get `Status = Reversed` plus a reversal record. The row stays.
- `BankTransaction` gets `Status = Excluded` with a reason. It stays so the import de-duplicator still sees it (BRD 33, rule 51).
- Masters (`Project`, `Party`, `Item`) get `IsActive = false`. Hard delete only if zero referencing rows, and only for Admin.

Add a global EF Core `SaveChanges` guard that throws if any entity of type `LedgerEntry` is in `Deleted` or `Modified` state.

---

## 6. Database conventions

- Table names `PascalCase` singular (`Project`, `LedgerEntry`). Column names `PascalCase`.
- Every table: `Id` (BIGINT AUTO_INCREMENT PK), `CreatedAtUtc`, `CreatedByUserId`, `UpdatedAtUtc`, `UpdatedByUserId`, `ConcurrencyStamp` (CHAR(36)).
- **Optimistic concurrency:** MySQL has no `rowversion`. Use a `ConcurrencyStamp` GUID column configured with `.IsConcurrencyToken()` and reassign it in the `SaveChanges` interceptor. Do not rely on `[Timestamp]`.
- Charset `utf8mb4`, collation `utf8mb4_unicode_ci`, set explicitly per table.
- All FKs indexed. Composite index on every `(ProjectId, Date)` and `(PartyId, Date)` pair used by reports.
- Unique constraints that the business actually needs: `Project.Code`, `Party.NormalisedName + Type`, `Item.NormalisedName`, `BankTransaction.RowHash`.
- Enums stored as `TINYINT`, mapped in C#. Do not store enum names as strings.
- One migration per task, named `<TaskId>_<Description>`, e.g. `P2T03_AddVendorPurchase`.

**Duplicate prevention for masters** (BRD 13, 15, rule 17): store a `NormalisedName` column (lowercase, whitespace collapsed, punctuation stripped) with a unique index, and on create, warn on close matches using a Levenshtein or trigram check before inserting. Warn, do not block — "ABC Cement" and "ABC Cements" may genuinely be two suppliers.

---

## 7. API conventions

- Base path `/api/v1/`. Version in the URL.
- REST-ish, resource-oriented: `GET /api/v1/projects`, `POST /api/v1/vendor-payments`, `POST /api/v1/bank-transactions/{id}/reconcile`.
- Errors return RFC 9457 `application/problem+json`. Validation failures return 400 with an `errors` dictionary keyed by field.
- List endpoints share one envelope:

```json
{ "items": [], "page": 1, "pageSize": 50, "totalCount": 1234, "totalPages": 25 }
```

- List endpoints share one query contract: `?page=&pageSize=&sortBy=&sortDir=&search=&dateFrom=&dateTo=&projectId=&...`. Build this once (Task P8-T01) and reuse it for every report.
- Every write endpoint that moves money accepts an `Idempotency-Key` header and rejects a repeat within 24 hours. Bank reconciliation and allocation are double-click magnets.
- Long-running operations (statement import, allocation run) return `202` with a job id and expose `GET /api/v1/jobs/{id}`.
- OpenAPI generated and committed; frontend types generated from it so contracts cannot silently drift.

---

## 8. Frontend conventions

### 8.1 Structure

- Server Components for list and detail pages; fetch on the server, stream the shell.
- Client Components for forms, grids with local filter state, and the reconciliation screen.
- One `apiClient` in `src/lib/api.ts` that attaches auth, unwraps the list envelope, and maps `problem+json` to typed errors.
- Route groups mirror the navigation in BRD 68 exactly. The client reviewed that navigation; do not reorganise it.

### 8.2 Design direction

This is a back-office accounting tool used daily by two or three people who care about one thing: is the number right. Design for density and legibility, not for a landing page.

- **Palette:** a neutral base (paper white `#FCFCFA`, ink `#1A1D21`, rule `#E2E1DC`) with exactly three semantic colours that only ever mean one thing: credit/positive `#1F7A4D`, debit/negative `#B03A2E`, needs-attention/unreconciled `#B8860B`. Never use these three decoratively. If a number is green it is money in, always.
- **Type:** one family. A humanist sans for UI (Inter or IBM Plex Sans), with **tabular figures enabled** on every numeric column (`font-variant-numeric: tabular-nums`). Amounts right-aligned, always two decimals, always grouped `en-IN`. Misaligned digits in a ledger are a real usability defect, not a nitpick.
- **Layout:** the grid is the product. Sticky header row, sticky total row, frozen first column on wide tables, row density toggle (comfortable/compact), and column visibility control on every report (BRD 57 asks for it).
- **Restraint:** no card-chopping of dense data, no gradient washes, no animated entrances. The one place to spend visual effort is the reconciliation screen (BRD 39), where the match confidence, the amount difference and the status need to be readable at a glance across a hundred rows.
- **Copy:** buttons say what happens. "Reconcile", "Post payment", "Exclude from queue". The toast after "Reconcile" says "Reconciled". Empty states say what to do next, error states say what went wrong and how to fix it.

### 8.3 Non-negotiables

- Every destructive or financial action shows a confirmation summarising the effect in words and numbers before it commits.
- Every amount input uses a masked numeric field that rejects letters and shows the grouped value below as you type.
- Keyboard-first data entry: tab order follows visual order, Enter submits, Escape cancels, no mouse required to add a purchase line.
- Optimistic updates are banned on financial writes. Wait for the server.

---

## 9. Security and RBAC

BRD 58-64 describes `User → Role → Module → Action`, optionally scoped to projects.

- **Permissions are strings** of the form `module.action`, e.g. `vendors.edit`, `bank_reconciliation.reconcile`, `reports.export`. Seed the full matrix from BRD 62.
- Roles hold permissions; users hold one role plus optional per-project grants. Permissions are flattened into claims at login and re-issued on refresh.
- Authorisation is enforced with a policy handler and an attribute: `[HasPermission("bank_reconciliation.reconcile")]`. **Never check role names in code.** The client will add roles.
- **Project scoping** is applied in the data layer, not per-controller. An `IProjectScopeFilter` injected into query handlers appends `WHERE ProjectId IN (@allowed)` for scoped users. One place to get right, one place to test.
- The frontend hides menu items the user lacks permission for (BRD 61 requires this), but the frontend is not the enforcement boundary. Every endpoint checks independently.
- Passwords hashed with ASP.NET Core Identity's hasher (PBKDF2) or Argon2id. Refresh tokens rotate on use and are revoked on reuse detection.
- File uploads: validate extension and content type, cap size, store outside the web root with a generated filename, serve through an authorised endpoint. Never serve `/uploads/` statically.

---

## 10. Audit trail

BRD 65 requires user, timestamp, module, action, record id, old value, new value, plus reconciliation-specific fields.

Implement as an EF Core `SaveChangesInterceptor` that inspects the ChangeTracker for entities marked `[Auditable]`, serialises changed properties only, and writes an `AuditLog` row in the same transaction. Explicit domain events supplement it for actions that are not simple property changes: reconcile, unreconcile, exclude, allocation override, permission change.

Audit rows are never editable and never deleted. This is the record you will need when the client asks why Project D's outstanding changed.

---

## 11. Testing strategy and definition of done

### Test pyramid for this app

| Level | What it covers | Runs against |
|---|---|---|
| Unit | Allocation algorithms, amount splitting, EMI amortisation, match scoring, outstanding calculations, validators | In-memory, no DB |
| Integration | Every endpoint, every EF query, migrations, concurrency, permission enforcement | Real MySQL 8 via Testcontainers, reset with Respawn |
| Component (FE) | Forms, grids, amount inputs, allocation editor | Vitest + RTL, MSW for API |
| E2E | The five money-critical journeys (see below) | Playwright against a seeded stack |

**The five journeys that must always have passing E2E tests:**
1. Record a purchase, pay part of it, check vendor and project outstanding both moved by the same amount.
2. Make one consolidated vendor payment across four projects, verify FIFO allocation matches BRD 20 exactly.
3. Import a statement twice, verify no duplicate rows.
4. Reconcile a bank debit against an existing manual payment, verify total expense did not double (BRD 37).
5. Allocate a common expense equally across three ongoing projects, verify the parts sum to the whole to the paisa.

### Definition of done for any task

- [ ] Acceptance criteria in the task all satisfied
- [ ] Named tests written and passing
- [ ] Migration created, applied, and reversible (`Down` tested)
- [ ] No new compiler warnings; `TreatWarningsAsErrors` still passes
- [ ] Permission checks on every new endpoint
- [ ] Audit logging on every new financial write
- [ ] Frontend: keyboard navigable, loading and error states present, amounts formatted `en-IN`
- [ ] Validation block from the task run, output pasted into the completion note
- [ ] `tasks.md` checkbox updated

### Data integrity checks (run in CI from Phase 3 onward)

Implement BRD 38's control table as automated assertions over the seeded test database:

```
Bank:          opening + credits − debits = closing
Vendor:        obligations − allocations = vendor outstanding
Project:       project payables − project payments = project outstanding
Client:        contract value − receipts = client outstanding
Multi-project: bank debit = SUM(project allocations)
```

Any failure fails the build. These five assertions are the safety net for the entire application.

---

## 12. Local environment setup

```bash
# 1. Database — either XAMPP (start MySQL module) or:
docker run --name cb-mysql -e MYSQL_ROOT_PASSWORD=dev -p 3306:3306 -d mysql:8

# 2. Create schema
mysql -u root -p -e "CREATE DATABASE colourbricks CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

# 3. Backend
cd backend
dotnet restore
dotnet ef database update -p src/ColourBricks.Infrastructure -s src/ColourBricks.Api
dotnet run --project src/ColourBricks.Api        # https://localhost:7001

# 4. Frontend
cd frontend
npm install
npm run dev                                       # http://localhost:3000
```

Secrets go in `dotnet user-secrets` and `.env.local`. Neither is committed. `appsettings.Development.json` holds only non-secret defaults.

---

## 13. Phase roadmap

The BRD's own phasing (section 69) puts almost everything in Phase 1, including bank reconciliation. That is too large to sequence or test. The plan below preserves the BRD's delivery intent but splits Phase 1 into buildable increments. Client-facing milestones map as follows:

| Plan phase | Contents | Maps to BRD phase |
|---|---|---|
| **P0 — Foundation** | Solution, DB, auth, permissions, audit, test harness, app shell | prerequisite |
| **P1 — Masters** | Projects, parties, items, teams, accounts, payment modes, temples, users/roles UI | BRD 1 |
| **P2 — Core transactions** | Ledger core, income, purchases, expenses, labour, donations, custom work, attachments | BRD 1 |
| **P3 — Payments & allocation** | Outstanding engine, payments, FIFO multi-project allocation, advances, override | BRD 1 + 4 |
| **P4 — Bank import & reconciliation** | Import, dedupe, matching, credit/debit allocation, internal transfers, control reports | BRD 1 (new) |
| **P5 — Budget, ledger, P&L, dashboards** | Budget vs actual, project ledger, P&L, project and company dashboards | BRD 1 |
| **P6 — Common expenses** | Personal/office/savings, equal/percentage/manual allocation, history | BRD 2 |
| **P7 — Loans & EMI** | Loan master, amortisation, EMI payments, alerts, reports | BRD 3 |
| **P8 — Reporting & analytics** | Report framework, all report families, Excel/PDF export | BRD 5 |
| **P9 — Hardening & release** | Notifications, performance, security review, integrity suite, deployment, UAT | all |

**Recommended first client demo:** end of P3. At that point the system does something the client cannot do in Excel, which is multi-project vendor allocation with correct outstanding on both sides. Demoing after P1 shows only empty forms and invites scope churn.

---

## 14. Glossary

| Term | Meaning here |
|---|---|
| Obligation | Money owed but not yet paid. Purchase, agreed work value, donation allocation, EMI due. |
| Settlement | Money actually moving. Payment out, receipt in. |
| Allocation | The link that says how much of a settlement applies to which obligation and project. |
| Outstanding | Obligations minus allocations, for a party or a project. |
| Reconciled | A bank statement line has been confirmed to correspond to a recorded settlement. |
| Excluded | A bank statement line the accountant has removed from the queue, kept for audit. |
| Pending | Imported but not yet reviewed. |
| Advance | A settlement that exceeds the outstanding, carried as a credit against future obligations. |
| Common expense | Personal, office or savings expense held at company level and distributed to ongoing projects. |
| FIFO | Oldest outstanding settled first. The default allocation method. |

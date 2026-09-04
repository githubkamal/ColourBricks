# Colour Bricks — Review, Open Questions and Best Practices

My reading of `Colour_Bricks_BRD_v1_2.md` as an engineer who has to build it.

---

## 1. What the BRD gets right

Worth saying, because it shapes how much of the plan is design versus discovery.

- **Section 22 and Section 37 are the whole ballgame, and the BRD knows it.** Separating "purchase creates an expense" from "payment settles it" is the single distinction that most small accounting builds get wrong. It's stated twice, with worked numbers.
- **Section 20's allocation table is a complete test fixture.** Four projects, exact before/allocated/after figures. That is a spec you can assert against, and I've made it a required test in P3-T03.
- **Section 38's control table** is a genuine internal-audit mindset. Five formulas that must always hold. I've promoted them from a report into an automated check that runs in CI (P3-T07), because a control you only look at when someone complains is not a control.
- **Rule 53 rules out cached balances.** "Reports must derive totals from linked transaction records." That one line prevents an entire category of drift bugs.
- **Rule 32 and Section 33** choose reversal over deletion. Correct, and rare in a first-draft BRD.
- **Section 68's navigation** has clearly been reviewed by someone who will use the app. Build it as written.

The reconciliation additions in v1.2 (sections 30-39) are notably better specified than the v1.1 material. Whoever wrote them has done bank reconciliation before.

---

## 2. Open questions to put to the client

These are the places where I would guess if forced, and guessing would be expensive. Numbered so tasks can reference them.

**Q1 — Scale and concurrency.** How many projects run at once, how many users, roughly how many transactions a month, and how many years of history need to be migrated? The plan assumes 5-20 projects, under 20 users, low thousands of transactions monthly. If it's ten times that, the reporting design changes.

**Q2 — GST.** The Item Master has a Tax/GST field and the Vendor Master has a GST number, but nothing in the BRD does anything with them. Is this a placeholder, or does the client expect input-credit tracking, GSTR reconciliation, or GST-inclusive/exclusive handling on purchases? This is the difference between a nullable column and a module. **Assumption in the plan: capture only, no GST logic.**

**Q3 — Revenue recognition basis.** Section 43 says profit is revenue minus cost, but never defines revenue. Is a project's revenue its contract value from day one, or only the receipts collected so far? These give very different profit numbers mid-project, and the client's mental model matters more than the accounting theory. **Assumption: contract value, with a toggle (P5-T04).**

**Q4 — Savings.** Section 44 groups Savings with Personal and Office expenses, and Section 45 allocates all three to projects as costs. Savings is not an expense; it's a transfer to a different pot. Allocating it to projects makes every project look less profitable than it is. Does the client actually want savings to reduce project profit, or should it be tracked at company level only? **This one has real reporting consequences and should be asked before P6.**

**Q5 — TDS.** Construction subcontractor payments in India typically attract TDS under 194C. The BRD does not mention it anywhere. If the client deducts TDS, the payment amount and the settlement amount differ, and the difference is a liability. Adding that later touches every payment screen. Ask now.

**Q6 — Subcontractor advances.** Section 25 handles vendor advances explicitly. Nothing says whether a subcontractor can be paid ahead of agreed work value. In practice, on Indian construction sites, they routinely are. **Current assumption: rejected (P2-T05). Confirm — this is likely wrong.**

**Q7 — Retention / retainage.** Also standard in construction: a percentage held back from each subcontractor payment and released after a defect liability period. Not mentioned. If the client does this, it's a Phase 2 feature, not an afterthought.

**Q8 — Existing data.** What is the client using today? Almost certainly Excel workbooks. Getting a copy early determines the shape of the migration in P9-T04 and, more importantly, reveals the fields they actually use that never made it into the BRD.

**Q9 — Deployment target.** Windows Server with IIS, a Linux VPS, or cloud? Who administers it? Where do attachments live and who backs them up? This affects P9-T06 and should not be decided in the last week.

**Q10 — Bank statement formats.** Get real (anonymised) statement exports from every bank the client uses, before starting P4. Indian bank CSV exports are wildly inconsistent: some use separate Debit and Credit columns, some a single signed column, some append `Cr`/`Dr` to the amount, some put four junk header rows above the data, and date formats vary by bank and sometimes by export setting. The parser design in P4-T02 depends entirely on what you actually see.

**Q11 — Multi-company.** Colour Bricks is one company today. Is a second entity plausible? If yes, adding a `CompanyId` discriminator now costs a day. Adding it in year two costs a month.

**Q12 — Who is the real user?** Section 63 gives Accounts a long list of permissions. Is there an actual accounts person, or is this the owner doing everything? If it's the owner on a phone at a site, the UI priorities change substantially.

**Q13 — Import review: credit split. RESOLVED 2026-09-03.** Credit (money in from a client) maps to **exactly one project** (BRD §34 / rule 46). Debit (money out from the company, potentially covering several vendors across several projects) maps to **one or more projects with per-project amounts** (BRD §35). Built this way in P4-T01. Open sub-point for P4-T07: the client described a debit as reaching *multiple vendors*, whereas BRD §35 models "one bank transaction → one vendor payment → many project allocations". If a single bank debit really needs per-vendor attribution at reconciliation, P4-T07's panel needs a vendor dimension, not just a project one.

**Q14 — Import review: non-project rows. RESOLVED 2026-09-03.** No special disposition tags. Every extracted row is shown in the review table and the accountant either maps it to project(s) or deletes it — nothing else. P4-T01 already works this way. Consequence to handle in **P4-T08**: an internal transfer deleted at review is never recorded, so the paired-transfer feature must instead let the accountant mark a transfer *after* commit (both legs committed), or add an "internal transfer" action on the review row at that point. Decide when P4-T08 is picked up.

---

## 3. Gaps and inconsistencies in the BRD

Things I would raise in a review meeting.

1. **Client is not a first-class entity.** Section 4 has "Client Name" as a text field on the project, but sections 34 and 38 talk about "client received amount" and "client outstanding" as if clients are tracked records. They need to be. The plan makes Client a `Party` type (P1-T02). Flag this as a change to the BRD.

2. **Phase 1 in section 69 is not a phase.** It contains nineteen items including full RBAC, bank reconciliation and multi-project allocation. That is the whole application minus loans. It cannot be estimated, sequenced or tested as a unit. `plan.md` §13 splits it into P0-P5 while preserving the same delivery content. Get the client to agree to the sub-milestones so there is something to demo before month six.

3. **Reconciliation is in Phase 1 but depends on Phase 4.** Section 69 puts "Multi-project debit allocation via reconciliation" in Phase 1, while "Advanced Vendor Payment Allocation" including FIFO is Phase 4. Section 35 explicitly says the reconciliation screen should propose allocations using FIFO. You cannot ship the first without the second. The plan resolves this by building allocation (P3) before reconciliation (P4).

4. **Section 5's expense breakdown includes "Loan/EMI" and "Savings Allocation" as project expenses.** Loan principal is not an expense, and savings is not an expense (see Q4). Only loan *interest* is. If the dashboard shows principal repayment as project cost, every project's profit will be understated and the numbers will not tie to anything.

5. **Section 45 allocates common expenses to "ongoing projects" without a period definition.** Allocated monthly? Per expense? On demand? What happens to an expense incurred in a month where a project was ongoing for only ten days? The plan assumes a per-period allocation run with the eligible set fixed at run time (P6-T03), but the client should confirm.

6. **No approval workflow, despite an Approve permission.** Section 62's matrix has an Approve column for Projects, Expenses and Payments, but nothing in the BRD describes what approval means or blocks. Either define it (does an unapproved payment post to the ledger?) or drop the column.

7. **Section 33's "In Review" status has no defined behaviour.** Who sets it, what does it prevent, does it time out? Currently it appears to be Pending with a flag.

8. **Nothing about opening balances.** The system will go live mid-project with existing vendor outstandings and part-built projects. Section 4 has no "cost incurred before system start" field. Handled in P9-T04, but it belongs in the BRD.

9. **Notification triggers have no thresholds or recipients.** Section 66 lists ten triggers. Who receives each, and at what threshold? "Project profitability falls below threshold" needs a number.

10. **"Delete where permitted" appears in sections 59 and 60** while rule 32 says prefer cancellation. Define which entities are ever hard-deletable. The plan says: financial records never, masters only with zero references.

---

## 4. Risks, ranked

| # | Risk | Why it matters | Mitigation |
|---|---|---|---|
| 1 | Allocation and reconciliation logic is subtly wrong | The client will discover it months later via a wrong outstanding figure and lose trust in every number | P3-T07 integrity controls in CI; every BRD worked example is a required test |
| 2 | Cached balance columns creep in for performance | Drift between vendor and project outstanding; unfixable without a rebuild | `plan.md` §5.3 forbids them; snapshots only with a nightly reconciliation check |
| 3 | Bank statement formats differ from what was assumed | P4 blows its estimate | Get real files before starting P4 (Q10); per-bank mapping profiles from the start |
| 4 | Scope creep during Phase 1's six-month stretch | No demo until late, so feedback arrives after the code is written | Sub-milestones per `plan.md` §13; demo at end of P3 |
| 5 | GST or TDS turns out to be required | Touches every purchase and payment screen | Ask Q2 and Q5 in week one |
| 6 | MariaDB-vs-MySQL drift from XAMPP development | Works locally, fails on the production server | `plan.md` §3.1; integration tests on real MySQL 8 |
| 7 | Rounding differences in split allocations | ₹0.01 discrepancies that make the control reports fail | `AmountSplitter` (P0-T07) with property tests |
| 8 | Concurrent allocation over-allocating a vendor | Negative outstanding, corrupted balances | Row locking in P3-T03, with a concurrency test |
| 9 | Migration of existing data is worse than expected | Delays go-live regardless of code readiness | Start P9-T04's dry-run tooling early; get the data at Q8 |
| 10 | Reports slow down as history accumulates | Usable at demo, unusable in year two | P9-T02 with a three-year volume dataset |

---

## 5. Best practices specific to this application

Not general advice. Things that matter here because of what this app is.

### Financial correctness

- **One posting service.** Every ledger write goes through `ILedgerPostingService`. If a second write path exists, the controls will eventually fail and you will not know where the entry came from. Make `DbSet<LedgerEntry>` inaccessible outside Infrastructure.
- **Every worked example in the BRD becomes a test.** Sections 16, 18, 20, 21, 25, 26, 32, 34, 35, 36, 40, 41, 42, 45. These are not illustrations. They are the acceptance criteria the client will check on day one, and they are already written in a form you can assert against.
- **Assert both sides of every balance change.** A payment that reduces vendor outstanding by ₹50,000 must reduce the bank by ₹50,000 and change project expense by ₹0. Test all three in one assertion, not the one you were thinking about.
- **Property-test the splitters.** Random amount, random target count, assert parts sum to the whole and no part is negative. Ten lines of test that catch a class of bug that is otherwise found by an accountant.
- **Never compute a total in JavaScript that the server also computes.** Two implementations of the same arithmetic will disagree eventually, and the user will believe the one on screen.

### Domain modelling

- **One `Party` table, not four.** Vendors, subcontractors, clients, temples and lenders share 90% of their fields, and in this business a party is genuinely more than one thing. Four near-identical tables means four search components, four duplicate checks and four report joins.
- **Nullable `ObligationId` on allocations.** It buys you advances, on-account payments and reconciliation-created settlements without a schema change.
- **Model the reconciliation *link* as its own row.** A bank transaction pointing at a settlement id is not enough. You need who matched it, when, how confident, and the unlink history, all of which BRD §65 asks for.
- **Store `NormalisedName` on every master.** Duplicate prevention (rules 16, 17) is unimplementable without it, and adding it after 5,000 vendor rows exist means a cleanup project.

### Sequencing

- **Build the integrity controls in Phase 3, not Phase 8.** They are a report in the BRD. Treat them as a test harness. Every phase after P3 is safer because the controls run on every commit.
- **Build the report framework before any report** (P8-T01). There are roughly forty reports in the BRD across sections 49-57. Forty bespoke filter implementations is a maintenance disaster; one framework and forty definitions is a week.
- **Demo at the end of P3, not P1.** Multi-project vendor allocation is the thing the client cannot do in Excel. Empty master screens invite scope discussion; a correct ₹1,00,000 split across four projects ends it.

### Indian domain specifics

- Format currency `en-IN`. `₹1,15,000` not `₹115,000`. Getting this wrong on every screen reads as a foreign product.
- Financial year is April to March. Report presets need "This FY" and "Previous FY" alongside the calendar ranges in §57.
- Date parsing is `dd/MM/yyyy`. A bank statement row reading `03/04/2026` is 3 April. Parse it as 4 March and you will silently misdate an entire month of transactions.
- Amounts in statements arrive with lakh-grouped commas and sometimes trailing `Cr`/`Dr`. Handle both in the parser.

### Operational

- **Get the client's real Excel workbooks in week one.** They contain the fields the BRD forgot, in the vocabulary the client actually uses. Half your naming decisions get made for you.
- **Test the restore, not the backup.** A backup you have never restored is a hypothesis (P9-T05).
- **Traceability matrix from the start.** BRD §70 has 54 numbered rules. Maintaining the rule-to-test map as you go (P9-T07) turns UAT from an argument into a checklist.

### Things to deliberately not build

- Multi-currency. Single company, single currency, INR.
- Microservices. This is a modular monolith. Two developers and twenty users do not need service boundaries.
- A generic workflow engine for the Approve permission. Define what approval means first (gap 6), then build the narrowest thing that does it.
- Real-time updates. Nobody needs to watch the ledger tick. Refresh on action is fine.
- A mobile app, unless Q12 says the owner works from site on a phone. Then make the web app work well at 390px instead of building a second client.

---

## 6. Suggested sequence for the first two weeks

1. Send Q1-Q12 to the client. Q2, Q3, Q4, Q5 and Q10 are blocking.
2. Collect real bank statement exports and the existing Excel workbooks.
3. Execute P0-T01 through P0-T09. The foundation does not depend on any open question.
4. Start P1-T01 and P1-T02. Project and Party are stable regardless of the answers.
5. Reconvene on the BRD gaps in section 3 above and issue v1.3 before P2 begins.

Phase 2 onward depends on the answers. Phase 0 and most of Phase 1 do not, so there is no reason to wait.

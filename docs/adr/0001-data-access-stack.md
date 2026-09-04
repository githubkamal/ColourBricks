# ADR 0001 — Data-access stack and local database

- **Status:** Accepted
- **Date:** 2026-09-02
- **Context task:** P0-T02

## Context

Plan §3 pins EF Core + `Pomelo.EntityFrameworkCore.MySql` and states *"Pomelo's major
version must match the EF Core major version — check NuGet for the current pairing
before pinning."* Plan §3.1 additionally says to run integration tests against real
MySQL 8 via Testcontainers and to deploy to real MySQL 8.

At implementation time:

- The .NET 10 SDK resolves **EF Core 10** by default.
- `Pomelo.EntityFrameworkCore.MySql` latest release is **9.0.0**, which targets
  **EF Core 9** (`Microsoft.EntityFrameworkCore.Relational [9.0.0, 9.0.999]`). No
  EF Core 10 build of Pomelo exists, stable or preview.
- The environment has **no Docker daemon**, and the project owner directed:
  *"use mysql through xampp running on port 3306"* and *"do not use docker at all,
  even for production."*

## Decision

1. **Pin the data stack to EF Core 9.** `Pomelo.EntityFrameworkCore.MySql` `9.0.0`,
   `Microsoft.EntityFrameworkCore.Design` `9.0.19` in `ColourBricks.Infrastructure`
   and `ColourBricks.Api`. This satisfies plan §3's major-version-match rule with the
   only stable pairing available. EF Core 9 runs on the .NET 10 runtime.
2. **Pin `dotnet-ef` to `9.0.19`** via `backend/.config/dotnet-tools.json` so the CLI
   matches the runtime. Run `dotnet tool restore` before `dotnet ef …`.
3. **Local + CI + production database is XAMPP's MySQL module (MariaDB 10.4.32) on
   `localhost:3306`.** Dev DB `colourbricks`, test DB `colourbricks_test`, user `root`,
   empty password. This replaces plan §3.1's Testcontainers/Docker approach.
4. `ServerVersion.AutoDetect(connectionString)` is used everywhere (never a hardcoded
   `MySqlServerVersion`), so the same code works against MariaDB now and MySQL 8 later.
5. Charset/collation is forced to `utf8mb4` / `utf8mb4_unicode_ci` on every table and
   column via `HasCharSet(..., DelegationModes.ApplyToAll)` so schemas stay portable
   and never emit `utf8mb4_0900_ai_ci`.

## Consequences / open risks

- **`SELECT … FOR UPDATE SKIP LOCKED` needs MariaDB 10.6+ (plan §3.1).** 10.4.32 does
  not support it. The P3-T03 allocation engine's row-locking design must use an
  alternative (plain `FOR UPDATE`, `GET_LOCK`, or app-level serialization). Flag when
  P3-T03 is picked up.
- **CI no longer exercises real MySQL 8**, so MySQL-8-vs-MariaDB divergence
  (`JSON` type, `0900` collations, reserved words) is not caught automatically.
  Mitigated by explicit collation config and by avoiding queryable `JSON` columns.
- **`dotnet-ef` is a version behind the SDK.** Revisit this ADR when Pomelo publishes
  an EF Core 10 release; upgrading is then a package-version bump plus a migration
  snapshot regen.
- **EF Core packages are pinned to `9.0.0` exactly** (not `9.0.x`). Pomelo `9.0.0`
  NREs in `RelationalTypeMappingSource.FindMappingWithConversion` against some later
  9.0.x patches. `.config/dotnet-tools.json`, `ColourBricks.Infrastructure` and
  `ColourBricks.Api` all pin `9.0.0`.

## Pomelo 9.0.0 model-mapping limitations (found in P0-T03)

Pomelo `9.0.0` throws `NullReferenceException` deep in `RelationalTypeMappingSource`
(`FindCollectionMapping`) when a **string** property is given either:

- an explicit `HasColumnType("char(36)")` / `HasColumnType("longtext")`, or
- `IsFixedLength()` (which resolves to a `char` column).

Both patterns crash `dotnet ef` and the app at model-build time. Workarounds in use:

- `BaseEntity.ConcurrencyStamp` is configured with `IsConcurrencyToken` + `MaxLength(36)`
  only, so it maps to **`varchar(36)`**, not the `CHAR(36)` named in plan.md §6. Same
  GUID, same behaviour; revisit if a Pomelo release fixes fixed-length strings.
- Long text columns are left unannotated; Pomelo maps unbounded `string` to `LONGTEXT`
  by default, which is what we want.
- A stale `AppDbContextModelSnapshot.cs` re-triggers the NRE on every `dotnet ef`
  command, so if you delete a migration `.cs` you must also revert the snapshot.
- A `System.Guid` property maps to `char(36)` fine — the NRE is specific to `string`
  properties with an explicit char/text column type or `IsFixedLength`.

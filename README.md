# Colour Bricks

Construction project financial management system for a single company running multiple
concurrent building projects. See [`docs/plan.md`](docs/plan.md) for the engineering plan
and [`docs/tasks.md`](docs/tasks.md) for the work queue.

## Repository layout

```
colour-bricks/
├── global.json                 # pinned .NET SDK
├── Directory.Build.props        # shared LangVersion, Nullable, TreatWarningsAsErrors
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
├── frontend/                    # Next.js (App Router), TypeScript strict, Tailwind, shadcn/ui
├── docs/
│   └── adr/                     # one file per significant decision
└── db/
    └── seed/                    # seed scripts for masters and demo data
```

**Backend layering:** `Api → Application → Domain`, `Infrastructure → Application/Domain`.
`Domain` references nothing outside the BCL. No EF Core types in `Domain` or `Api`.
`Api` references `Infrastructure` only as the composition root (DI wiring in `Program.cs`).

## Prerequisites

- .NET SDK per [`global.json`](global.json) (10.0.400+)
- Node.js 20+ and npm
- XAMPP's MySQL module (MariaDB) on `localhost:3306` — dev DB `colourbricks`, test DB
  `colourbricks_test`, user `root`, empty password. See
  [`docs/adr/0001-data-access-stack.md`](docs/adr/0001-data-access-stack.md) for why
  the data stack is EF Core 9 + Pomelo 9 and why Docker/Testcontainers are not used.

## Local environment setup

```bash
# 1. Database — start the MySQL module in the XAMPP control panel (MariaDB on :3306)

# 2. Create schemas (mysql client ships in C:\xampp\mysql\bin)
mysql -u root -e "CREATE DATABASE colourbricks CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysql -u root -e "CREATE DATABASE colourbricks_test CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

# 3. Backend
cd backend
dotnet restore
dotnet tool restore                              # pins dotnet-ef (see ADR 0001)
dotnet ef database update -p src/ColourBricks.Infrastructure -s src/ColourBricks.Api
# Serve http too so the dev frontend can talk to it (Auth:CookieSecure is false in dev):
dotnet run --project src/ColourBricks.Api --urls "http://localhost:5095;https://localhost:7001"

# 4. Frontend
cd frontend
npm install
npm run dev                                       # http://localhost:3000
```

The frontend calls the API at `http://localhost:5095/api/v1` by default; override with
`NEXT_PUBLIC_API_BASE_URL` in `frontend/.env.local` (see `frontend/.env.example`).
Seeded admin: `admin@colourbricks.local` / `Admin!23456`.

Secrets go in `dotnet user-secrets` and `.env.local`. Neither is committed.
`appsettings.Development.json` holds only non-secret dev defaults — including a
throwaway `Jwt:SigningKey` and the `Auth:Seed` administrator
(`admin@colourbricks.local` / `Admin!23456`). **In any non-dev environment override
`Jwt:SigningKey` (≥ 32 bytes)** via environment variables or user-secrets; the app
refuses to start without a valid signing key. **`Auth:Seed` has no base/production
config at all** (only `appsettings.Development.json` sets it) — a production deploy
seeds *no* administrator account by default, on purpose.

### Creating table schemas in production

Local setup applies migrations with `dotnet ef database update` (step 3 above), which
needs the .NET SDK, `dotnet-ef`, and this source tree on the machine running it, and
applies changes live. For production, two other options avoid one or more of those:

**Migrations bundle** — a self-contained executable that only needs a connection
string, no SDK or source on the target machine:

```bash
cd backend
dotnet ef migrations bundle -p src/ColourBricks.Infrastructure -s src/ColourBricks.Api -o efbundle
# Copy efbundle to the target machine, then:
./efbundle --connection "<production connection string>"
```

**Raw SQL** — [`backend/scripts/schema.sql`](backend/scripts/schema.sql) is an
idempotent script (safe to re-run; each migration checks `__EFMigrationsHistory`
before applying itself), committed to the repo, for a DBA to review and run with any
MySQL client — no .NET tooling involved at all on the production side:

```bash
mysql -u <user> -p <production-db-name> < backend/scripts/schema.sql
```

**It's a checked-in file that must stay in sync with the migrations, not a
generated-on-demand artifact — regenerate and commit it in the same change as any
migration that adds or alters a table:**

```powershell
# Windows
backend\scripts\generate-schema-sql.ps1
```

```bash
# macOS/Linux
backend/scripts/generate-schema-sql.sh
```

Both overwrite `backend/scripts/schema.sql` in place. Verified end-to-end: generating
it, applying it to a fresh database, and confirming the resulting table count matches
a database updated the normal way.

### Creating the first production administrator

Since production seeds no admin, use `tools/ColourBricks.SeedAdmin` once to create
one (or to reset a locked-out admin's password later — it's idempotent, safe to
re-run). It reads the same config the API does (`appsettings.{ASPNETCORE_ENVIRONMENT}.json`
+ environment variables), so point it at production the same way you'd point the API.

Run it via the wrapper script — prompts for anything you don't pass:

```powershell
# Windows
backend\scripts\seed-admin-production.ps1 -Email you@yourcompany.com -ConnectionString "<production connection string>"
```

```bash
# macOS/Linux
backend/scripts/seed-admin-production.sh you@yourcompany.com "<production connection string>"
```

Or invoke the tool directly:

```bash
cd backend
ASPNETCORE_ENVIRONMENT=Production ConnectionStrings__Default="<production connection string>" \
  dotnet run --project tools/ColourBricks.SeedAdmin -- --email you@yourcompany.com
```

Omit `--password` to get a strong one generated and printed once (copy it immediately —
it's shown only in that terminal output, never stored anywhere in plaintext); pass
`--password "..."` to set a specific one instead. Sign in and change the password
right away either way. This can be run from any machine that can reach the
production database — it doesn't have to run on the production server itself.

**Prefer raw SQL instead of a live DB connection from this tool?** Add `--sql` and it
prints a ready-to-run MySQL statement instead of writing to the database directly — no
connection string needed to *generate* it. Hand the output to a DBA, or run it yourself
with any MySQL client:

```bash
dotnet run --project tools/ColourBricks.SeedAdmin -- --email you@yourcompany.com --sql > seed-admin.sql
mysql -u <user> -p <production-db-name> < seed-admin.sql
```

The password hash still comes from the real PBKDF2 hasher (MySQL itself can't compute
one) — this mode only changes *how* the result reaches the database. The statement
upserts by email and resolves the Administrator role by name at run time, so it needs
the RBAC catalogue already seeded (true for any database the API has started against
at least once). Omitting `--password` prints the generated password as a SQL comment
at the end of the output — copy it before running the statement.

## Build, lint and test

```bash
# Backend  (integration tests reset colourbricks_test with Respawn before each test)
cd backend
dotnet build -warnaserror
dotnet test

# Frontend
cd frontend
npm run lint          # eslint, --max-warnings 0
npm run typecheck
npm run format:check
npm run build
npm run test          # vitest (jsdom + RTL + MSW)
npm run test:e2e      # playwright — starts the API + Next dev, needs `npx playwright install chromium` once
```

CI (`.github/workflows/ci.yml`) runs all of the above against a `mariadb:10.4` service
container on every push and PR.

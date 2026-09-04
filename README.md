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
`Jwt:SigningKey` (≥ 32 bytes) and `Auth:Seed:Password`** via environment variables or
user-secrets; the app refuses to start without a valid signing key.

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

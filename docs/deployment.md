# Deployment guide — Ubuntu 24.04 LTS VPS

This describes how Colour Bricks is deployed to a single Ubuntu 24.04 VPS, with no
Docker and no change to the EF Core/MariaDB stack (see
[`docs/adr/0001-data-access-stack.md`](adr/0001-data-access-stack.md) — Docker was
explicitly rejected for this project). Companion files live under
[`deploy/`](../deploy):

```
deploy/
├── systemd/colourbricks-api.service   # runs the .NET API
├── systemd/colourbricks-web.service   # runs `next start`
├── nginx/colourbricks.conf            # reverse proxy + TLS termination points
└── scripts/
    ├── server-setup.sh                # one-time VPS bootstrap
    └── deploy.sh                      # runs on the VPS on every deploy
```

## Architecture

```
Internet
   │
   ▼
 Nginx (80/443, certbot)
   ├── app.yourdomain.com  →  127.0.0.1:3000   (Next.js, `next start`)
   └── api.yourdomain.com  →  127.0.0.1:5095   (ASP.NET Core, Kestrel)
                                    │
                                    ▼
                              MariaDB (127.0.0.1:3306)
```

Both apps run as systemd services under a dedicated `colourbricks` OS user, deployed
with a releases/current symlink layout (Capistrano-style) so a deploy is an atomic
symlink swap and a bad release can be rolled back instantly:

```
/opt/colourbricks/
├── api/
│   ├── current -> releases/20260905141200-abc1234
│   └── releases/20260905141200-abc1234/   # dotnet publish output + efbundle
├── web/
│   ├── current -> releases/20260905141200-abc1234
│   └── releases/20260905141200-abc1234/   # .next/, public/, package.json, package-lock.json, node_modules/
└── shared/
    ├── api.env       # secrets/config for the API (EnvironmentFile=)
    ├── web.env       # secrets/config for the web app
    ├── storage/      # attachment files (Storage:Root) — persists across releases
    └── logs/         # Serilog file sink — persists across releases
```

`shared/storage` and `shared/logs` are symlinked into each new API release so
uploaded attachments and log history survive every deploy.

**Frontend build model, as requested:** the Next.js app is built once on the CI
runner (`npm run build`), and only `.next/`, `public/`, `package.json` and
`package-lock.json` are shipped to the server. The server never runs `next build`;
it only runs `npm ci --omit=dev` to materialise `node_modules` for the platform it
will actually run on (Ubuntu), then `next start`.

**Backend build model:** the API is published on CI as a framework-dependent
`linux-x64` build (`dotnet publish`), so the server needs the ASP.NET Core **runtime**
only — no SDK. Database migrations ship as a self-contained
[`dotnet ef migrations bundle`](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying#bundles)
executable, so applying migrations on the server doesn't need the SDK or the
`dotnet-ef` tool either.

No Docker, no Prisma anywhere in this pipeline.

---

## 1. One-time VPS setup

SSH into a fresh Ubuntu 24.04 box as a sudo-capable user and run:

```bash
git clone <your-repo-url> /tmp/colourbricks-setup
cd /tmp/colourbricks-setup
chmod +x deploy/scripts/server-setup.sh
./deploy/scripts/server-setup.sh
```

This installs: the ASP.NET Core 10 runtime (Microsoft's apt feed), Node.js 24
(NodeSource), MariaDB server, Nginx, certbot, and ufw; creates the `colourbricks`
system user; and lays out `/opt/colourbricks`. Read the script before running it —
it prints the manual follow-ups below when it finishes.

### 1.1 Database

```bash
sudo mysql_secure_installation
sudo mysql -u root -p <<'SQL'
CREATE DATABASE colourbricks CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'colourbricks_app'@'localhost' IDENTIFIED BY '<strong-password>';
GRANT ALL PRIVILEGES ON colourbricks.* TO 'colourbricks_app'@'localhost';
FLUSH PRIVILEGES;
SQL
```

Use a real generated password here — this is production, not the empty-password
XAMPP dev setup described in the repo [`README.md`](../README.md).

### 1.2 Nginx + TLS

```bash
sudo cp deploy/nginx/colourbricks.conf /etc/nginx/sites-available/colourbricks
sudo $EDITOR /etc/nginx/sites-available/colourbricks   # set your real domains
sudo ln -s /etc/nginx/sites-available/colourbricks /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d app.yourdomain.com -d api.yourdomain.com
```

### 1.3 Config/secrets files

Create `/opt/colourbricks/shared/api.env` (owned `colourbricks:colourbricks`, mode
`600`) — these map to ASP.NET Core's `__`-delimited configuration keys:

```ini
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5095
ConnectionStrings__Default=Server=localhost;Port=3306;Database=colourbricks;User ID=colourbricks_app;Password=<strong-password>;TreatTinyAsBoolean=false;AllowUserVariables=true;UseAffectedRows=false
Jwt__SigningKey=<random 48+ byte secret, e.g. `openssl rand -base64 48`>
Auth__CookieSecure=true
Cors__FrontendOrigins__0=https://app.yourdomain.com
Storage__Root=/opt/colourbricks/shared/storage
```

**No `Auth__Seed__*` keys** — production intentionally seeds no administrator (see
[`README.md`](../README.md#creating-the-first-production-administrator)). Create the
first admin after the first deploy with the `SeedAdmin` tool instead (§2 below).

Create `/opt/colourbricks/shared/web.env` (same ownership/mode). The port is set in
the systemd unit's `ExecStart` (§1.4), not here — this file just needs:

```ini
NODE_ENV=production
```

> **Important — `NEXT_PUBLIC_API_BASE_URL` is a build-time value.** Next.js inlines
> `NEXT_PUBLIC_*` variables into the JS bundle at `next build` time, not at server
> start. Since the build happens in CI (see below), set it as a **GitHub Actions
> repository variable** (`Settings → Secrets and variables → Actions → Variables`),
> e.g. `https://api.yourdomain.com/api/v1` — not in `web.env` on the server, where it
> would have no effect.

```bash
sudo chown colourbricks:colourbricks /opt/colourbricks/shared/*.env
sudo chmod 600 /opt/colourbricks/shared/*.env
```

### 1.4 systemd units

```bash
sudo cp deploy/systemd/colourbricks-api.service deploy/systemd/colourbricks-web.service /etc/systemd/system/
sudo systemctl daemon-reload
```

Don't `enable --now` yet — there's no release in `current` until the first deploy.

### 1.5 Deploy script + SSH access for CI

```bash
sudo cp deploy/scripts/deploy.sh /opt/colourbricks/deploy.sh
sudo chown colourbricks:colourbricks /opt/colourbricks/deploy.sh
sudo chmod 750 /opt/colourbricks/deploy.sh
```

Generate a dedicated deploy keypair (don't reuse your personal key) and authorize it
for whichever sudo-capable user GitHub Actions will SSH in as:

```bash
ssh-keygen -t ed25519 -f colourbricks-deploy-key -C "github-actions-deploy" -N ""
# append colourbricks-deploy-key.pub to that user's ~/.ssh/authorized_keys on the VPS
```

That user needs passwordless `sudo systemctl restart colourbricks-api colourbricks-web`
(add a narrow `/etc/sudoers.d/colourbricks-deploy` rule for just those two commands —
don't hand it blanket sudo) and write access to `/opt/colourbricks/{api,web}/releases`.

---

## 2. First deployment (manual, to bootstrap `current`)

Before wiring up CI, do one deploy by hand so `current` exists and the services can
start:

```bash
# On your machine:
cd frontend && npm ci && NEXT_PUBLIC_API_BASE_URL=https://api.yourdomain.com/api/v1 npm run build && cd ..
cd backend && dotnet tool restore
dotnet publish src/ColourBricks.Api -c Release -r linux-x64 --self-contained false -o ../stage/api
dotnet ef migrations bundle -p src/ColourBricks.Infrastructure -s src/ColourBricks.Api \
  -r linux-x64 --self-contained --configuration Release -o ../stage/api/efbundle --force
cd ..
mkdir -p stage/web
cp -r frontend/.next stage/web/.next
cp -r frontend/public stage/web/public
cp frontend/package.json frontend/package-lock.json stage/web/

REL=$(date +%Y%m%d%H%M%S)-manual
ssh deploy@yourserver "mkdir -p /opt/colourbricks/api/releases/$REL /opt/colourbricks/web/releases/$REL"
rsync -az stage/api/ deploy@yourserver:/opt/colourbricks/api/releases/$REL/
rsync -az stage/web/ deploy@yourserver:/opt/colourbricks/web/releases/$REL/
ssh deploy@yourserver "/opt/colourbricks/deploy.sh $REL"
ssh deploy@yourserver "sudo systemctl enable colourbricks-api colourbricks-web"
```

`deploy.sh` runs `efbundle` for you as part of that — see the note in §3 about
`COLOURBRICKS_MIGRATIONS_CONNECTION` if you ever run it by hand instead.

Then check:

```bash
curl -I https://api.yourdomain.com/health
curl -I https://app.yourdomain.com/
```

Production seeds no administrator — create the first one now with the `SeedAdmin`
tool from your own machine (it just needs to reach the production database; tunnel
over SSH if it's not publicly exposed, e.g. `ssh -L 13306:127.0.0.1:3306 deploy@yourserver`):

```bash
cd backend
ASPNETCORE_ENVIRONMENT=Production \
ConnectionStrings__Default="Server=127.0.0.1;Port=13306;Database=colourbricks;User ID=colourbricks_app;Password=<strong-password>;TreatTinyAsBoolean=false;AllowUserVariables=true;UseAffectedRows=false" \
dotnet run --project tools/ColourBricks.SeedAdmin -- --email you@yourcompany.com
```

Copy the generated password from the output, sign in, and change it immediately.

---

## 3. CI/CD pipeline

[`​.github/workflows/ci.yml`](../.github/workflows/ci.yml) already runs on every push
and PR: backend build/test/integration/data-integrity checks against a MariaDB
service container, and frontend lint/typecheck/format/build/unit tests, plus an
end-to-end job. Nothing about that changes.

[`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) adds the deploy
step: it triggers via `workflow_run` on the `CI` workflow completing successfully on
`main`, so a deploy can never start until every existing check has passed. It then:

1. Builds the frontend on the runner (`npm run build`, with
   `NEXT_PUBLIC_API_BASE_URL` from a repo variable) and stages `.next/`, `public/`,
   `package.json`, `package-lock.json`.
2. Publishes the API (`dotnet publish -r linux-x64 --self-contained false`) and
   builds an `efbundle` migrations bundle.
3. `rsync`s both artifacts to a new timestamped release directory on the VPS over
   SSH.
4. Runs `/opt/colourbricks/deploy.sh <release>` on the server, which installs
   production `node_modules` (`npm ci --omit=dev`), applies pending migrations via
   `efbundle`, flips the `current` symlinks, restarts both systemd services, health
   -checks `/health` and `/`, and **rolls back automatically** (repoints the symlinks
   to the previous release and restarts) if either health check fails.
5. Prunes old releases, keeping the last 5, for easy manual rollback.

> **`efbundle --connection` doesn't work the way it looks like it should.** It does
> not override the app's `AppDbContextFactory` (used at design time to detect the
> MySQL server version) — running it with `--connection "<real string>"` still
> tried to connect as `root` with an empty password and failed. The factory reads
> `COLOURBRICKS_MIGRATIONS_CONNECTION` from the environment, so `deploy.sh` exports
> that instead of using the flag. Found by hitting it live on the first production
> deploy — if you ever run `efbundle` by hand, export the env var, don't rely on
> `--connection`.

### Required repository configuration

**Secrets** (`Settings → Secrets and variables → Actions → Secrets`):

| Name              | Value                                              |
|-------------------|-----------------------------------------------------|
| `DEPLOY_SSH_KEY`  | private half of the deploy keypair from step 1.5    |
| `DEPLOY_HOST`     | VPS hostname or IP                                  |
| `DEPLOY_USER`     | the sudo-capable SSH user for deploys (`root` on this deployment — see §5) |
| `DEPLOY_PORT`     | SSH port, if not 22 (optional)                      |

**Variables** (`Settings → Secrets and variables → Actions → Variables`):

| Name                        | Value                                    |
|-----------------------------|-------------------------------------------|
| `NEXT_PUBLIC_API_BASE_URL`  | `https://api.yourdomain.com/api/v1`       |

### Manual rollback

Every release directory stays on disk (last 5). To roll back by hand:

```bash
ssh deploy@yourserver
ls -1t /opt/colourbricks/api/releases   # find the previous good timestamp
ln -sfn /opt/colourbricks/api/releases/<prev> /opt/colourbricks/api/current
ln -sfn /opt/colourbricks/web/releases/<prev> /opt/colourbricks/web/current
sudo systemctl restart colourbricks-api colourbricks-web
```

Rolling back does **not** undo a database migration — if a bad release added a
breaking migration, that needs a manually written down-migration or bundle, not just
a symlink flip.

---

## 4. Operations notes

- **Logs:** the API writes to `/opt/colourbricks/shared/logs/colourbricks-*.log`
  (14-day retention, already configured in `Program.cs`) and also to
  `journalctl -u colourbricks-api`. The web app's stdout/stderr goes to
  `journalctl -u colourbricks-web`.
- **Backups:** MariaDB isn't backed up by anything in this repo. At minimum, cron a
  nightly `mysqldump colourbricks | gzip > /opt/colourbricks/backups/$(date +%F).sql.gz`
  and ship it off-box.
- **Attachments:** live under `/opt/colourbricks/shared/storage`, outside any
  release directory, so they survive deploys — back this up alongside the database.
- **Scaling beyond one box:** this guide assumes a single VPS. If you outgrow it,
  the release/symlink/systemd model still works per-host; you'd add a load balancer
  and point `deploy.sh` at each host in turn.

---

## 5. Deploying to a shared box (this deployment's actual setup)

Colour Bricks currently runs on `62.72.58.99`, a box that already hosted other apps
(other Nginx sites, a MySQL 8 server, Node services on other ports, a Frappe/ERPNext
install, Docker containers for an unrelated project). The generic steps above assume
a clean box; here's what actually differs there, in case you're adding another app
to a shared box too or need to reason about this one:

- **Ports 3000, 3001 and 3002 were already taken** by other Next.js apps, so this
  app's web service runs on **3003** instead — see the comment in
  [`deploy/systemd/colourbricks-web.service`](../deploy/systemd/colourbricks-web.service).
  `ss -tlnp` first on any shared box to find a free port.
- **MySQL 8.0 was already installed and running** (not MariaDB) — no new database
  server was installed, just a new `colourbricks` database and `colourbricks_app`
  user inside the existing instance. Pomelo/EF Core's `ServerVersion.AutoDetect`
  makes this transparent; nothing in the app cares whether the backing server is
  MariaDB or real MySQL 8.
- **Node.js 22 (already installed for other apps) was reused** rather than
  installing Node 24 — `next start` only needs a Node new enough for Next.js 16
  (≥20), so there was no reason to touch the box's existing Node install and risk
  the other apps on it.
- **The web/API services bind to `127.0.0.1` only**, and Nginx does all the public
  proxying, so no new firewall ports were opened.
- **`Type=notify` in the API systemd unit doesn't work** — the app never calls
  `sd_notify(READY=1)` (no `Microsoft.Extensions.Hosting.Systemd`/`UseSystemd()`
  wiring in `Program.cs`), so systemd waited the full start timeout and killed it.
  Fixed to `Type=simple` in [`colourbricks-api.service`](../deploy/systemd/colourbricks-api.service).
- **CI deploys as `root`** on this box, matching how the other apps already deployed
  there are set up (their deploy keys are also in root's `authorized_keys`) — there
  was no separate low-privilege deploy account to fit into, so introducing one would
  have been inconsistent with the box's existing operational model rather than safer.
- Live values for this deployment: `app.colourbricks.livewiresdigitalsolutions.com`
  and `api.colourbricks.livewiresdigitalsolutions.com`, both proxied by
  [`deploy/nginx/colourbricks.conf`](../deploy/nginx/colourbricks.conf). TLS is
  pending until those DNS records point at `62.72.58.99` — until then both sites are
  HTTP-only; re-run `certbot --nginx -d app... -d api...` once DNS resolves, then
  rebuild the frontend with `NEXT_PUBLIC_API_BASE_URL` switched to `https://` and
  redeploy (that value is baked in at build time — see the callout in §1.3).

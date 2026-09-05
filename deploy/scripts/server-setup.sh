#!/usr/bin/env bash
# One-time VPS bootstrap for Ubuntu 24.04 LTS. Run once as a sudo-capable user.
#
# IMPORTANT — this only installs pieces that are actually missing, and never
# touches firewall rules or existing services blindly. Colour Bricks has been run
# on a SHARED box (already hosting other apps: Nginx sites, a MySQL 8 server,
# Node apps on other ports, Docker containers) at least once — see "Deploying to
# a shared box" in docs/deployment.md. Read this script before running it, and on
# a non-empty box prefer running the sections you actually need by hand instead.
set -euo pipefail

echo "== apt update =="
sudo apt-get update -y

echo "== base packages =="
sudo apt-get install -y curl wget gnupg2 ca-certificates lsb-release ufw unzip

echo "== deploy user =="
if ! id -u colourbricks >/dev/null 2>&1; then
  sudo useradd --system --create-home --shell /usr/sbin/nologin colourbricks
fi

echo "== directory layout =="
sudo mkdir -p /opt/colourbricks/{api,web}/releases
sudo mkdir -p /opt/colourbricks/shared/{storage,logs}
sudo chown -R colourbricks:colourbricks /opt/colourbricks

echo "== .NET 10 ASP.NET Core runtime =="
if dotnet --list-runtimes 2>/dev/null | grep -q 'Microsoft.AspNetCore.App 10\.'; then
  echo "already installed, skipping"
elif apt-cache show aspnetcore-runtime-10.0 >/dev/null 2>&1; then
  # Ubuntu 24.04's own repos have carried .NET 10 packages directly — no need
  # for Microsoft's apt feed. Confirm with `apt-cache policy aspnetcore-runtime-10.0`.
  sudo apt-get install -y aspnetcore-runtime-10.0
else
  echo "== falling back to Microsoft's apt feed for Ubuntu 24.04 'noble' =="
  wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
  sudo dpkg -i /tmp/packages-microsoft-prod.deb
  rm /tmp/packages-microsoft-prod.deb
  sudo apt-get update -y
  sudo apt-get install -y aspnetcore-runtime-10.0
fi

echo "== Node.js =="
if command -v node >/dev/null 2>&1; then
  echo "node $(node --version) already installed, skipping (Next.js 16 needs >=20 — check this is enough)"
else
  curl -fsSL https://deb.nodesource.com/setup_24.x | sudo -E bash -
  sudo apt-get install -y nodejs
fi

echo "== MySQL/MariaDB =="
if command -v mysql >/dev/null 2>&1 && sudo mysql -u root -e "SELECT 1" >/dev/null 2>&1; then
  echo "a MySQL-compatible server is already running and root-accessible via socket auth, skipping install"
  echo "Create the app database/user by hand:"
  echo "  CREATE DATABASE colourbricks CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
  echo "  CREATE USER 'colourbricks_app'@'localhost' IDENTIFIED BY '<strong-password>';"
  echo "  GRANT ALL PRIVILEGES ON colourbricks.* TO 'colourbricks_app'@'localhost';"
  echo "  FLUSH PRIVILEGES;"
else
  echo "== installing MariaDB server (see docs/adr/0001) =="
  sudo apt-get install -y mariadb-server
  sudo systemctl enable --now mariadb
  echo "Run 'sudo mysql_secure_installation' interactively, then create the app database/user (see above)."
fi

echo "== Nginx + certbot =="
if command -v nginx >/dev/null 2>&1; then
  echo "nginx already installed, skipping"
else
  sudo apt-get install -y nginx
fi
if command -v certbot >/dev/null 2>&1; then
  echo "certbot already installed, skipping"
else
  sudo apt-get install -y certbot python3-certbot-nginx
fi
sudo systemctl enable --now nginx

echo "== firewall =="
echo "ufw is left as-is on a shared box — add rules by hand if needed, e.g.:"
echo "  sudo ufw allow OpenSSH"
echo "  sudo ufw allow 'Nginx Full'"
echo "This deployment binds the app/API to 127.0.0.1 and lets Nginx do the"
echo "public-facing proxying, so no new inbound ports need opening for them."

cat <<'EOF'

Next steps (manual, one-time):
  1. Check for port conflicts first: `ss -tlnp` and `ls /etc/nginx/sites-enabled/`.
     Pick a free port for the Next.js app (this deployment uses 3003 because
     3000-3002 were already taken) and edit deploy/systemd/colourbricks-web.service
     and deploy/nginx/colourbricks.conf to match if you pick something else.
  2. Copy deploy/nginx/colourbricks.conf to /etc/nginx/sites-available/, edit the
     server_names/ports to match your domains, symlink into sites-enabled,
     `nginx -t`, reload, then once DNS is live:
       sudo certbot --nginx -d app.yourdomain.com -d api.yourdomain.com
  3. Create /opt/colourbricks/shared/api.env and /opt/colourbricks/shared/web.env
     (see docs/deployment.md for the required keys). Lock them down:
       sudo chown colourbricks:colourbricks /opt/colourbricks/shared/*.env
       sudo chmod 600 /opt/colourbricks/shared/*.env
  4. Copy deploy/systemd/*.service to /etc/systemd/system/, `daemon-reload`, but do
     NOT enable/start them until the first release has been deployed (see deploy.sh).
  5. Copy deploy/scripts/deploy.sh to /opt/colourbricks/deploy.sh (owned by
     colourbricks, mode 750) — CI invokes this on every deploy.
  6. Add the CI deploy public key to the authorized_keys of whichever user
     GitHub Actions will SSH in as. On a box already managed this way for other
     apps, following the existing convention (often root, if that's how the
     other apps' deploy keys are set up) is simpler than introducing a new model.
EOF

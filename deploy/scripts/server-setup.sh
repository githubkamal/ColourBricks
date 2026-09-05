#!/usr/bin/env bash
# One-time VPS bootstrap for Ubuntu 24.04 LTS. Run once as a sudo-capable user.
# Idempotent-ish: safe to re-run, but review before running on an existing box.
set -euo pipefail

echo "== apt update/upgrade =="
sudo apt-get update -y
sudo apt-get upgrade -y

echo "== base packages =="
sudo apt-get install -y curl wget gnupg2 ca-certificates lsb-release ufw rsync unzip

echo "== deploy user =="
if ! id -u colourbricks >/dev/null 2>&1; then
  sudo useradd --create-home --shell /usr/sbin/nologin colourbricks
fi

echo "== directory layout =="
sudo mkdir -p /opt/colourbricks/{api,web}/releases
sudo mkdir -p /opt/colourbricks/shared/{storage,logs}
sudo chown -R colourbricks:colourbricks /opt/colourbricks

echo "== .NET 10 ASP.NET Core runtime (Microsoft feed for Ubuntu 24.04 'noble') =="
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
sudo dpkg -i /tmp/packages-microsoft-prod.deb
rm /tmp/packages-microsoft-prod.deb
sudo apt-get update -y
sudo apt-get install -y aspnetcore-runtime-10.0

echo "== Node.js 24 (NodeSource) =="
curl -fsSL https://deb.nodesource.com/setup_24.x | sudo -E bash -
sudo apt-get install -y nodejs

echo "== MariaDB server (matches Pomelo/EF Core provider — see docs/adr/0001) =="
sudo apt-get install -y mariadb-server
sudo systemctl enable --now mariadb
echo "Run 'sudo mysql_secure_installation' interactively, then create the app database/user:"
echo "  CREATE DATABASE colourbricks CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
echo "  CREATE USER 'colourbricks_app'@'localhost' IDENTIFIED BY '<strong-password>';"
echo "  GRANT ALL PRIVILEGES ON colourbricks.* TO 'colourbricks_app'@'localhost';"
echo "  FLUSH PRIVILEGES;"

echo "== Nginx + certbot =="
sudo apt-get install -y nginx certbot python3-certbot-nginx
sudo systemctl enable --now nginx

echo "== firewall =="
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx Full'
sudo ufw --force enable

cat <<'EOF'

Next steps (manual, one-time):
  1. Copy deploy/nginx/colourbricks.conf to /etc/nginx/sites-available/, edit the
     server_names, symlink into sites-enabled, `nginx -t`, reload, then run certbot:
       sudo certbot --nginx -d app.yourdomain.com -d api.yourdomain.com
  2. Create /opt/colourbricks/shared/api.env and /opt/colourbricks/shared/web.env
     (see docs/deployment.md for the required keys). Lock them down:
       sudo chown colourbricks:colourbricks /opt/colourbricks/shared/*.env
       sudo chmod 600 /opt/colourbricks/shared/*.env
  3. Copy deploy/systemd/*.service to /etc/systemd/system/, `daemon-reload`, but do
     NOT enable/start them until the first release has been deployed (see deploy.sh).
  4. Add the CI deploy public key to /home/<ssh-user>/.ssh/authorized_keys for the
     user GitHub Actions will SSH in as (does not have to be `colourbricks`; a sudo
     user that can write under /opt/colourbricks and run systemctl is fine).
EOF

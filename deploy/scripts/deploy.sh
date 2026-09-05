#!/usr/bin/env bash
# Runs ON THE VPS. Invoked by the GitHub Actions deploy job over SSH after it has
# rsynced a new release into:
#   /opt/colourbricks/api/releases/<RELEASE>/   (dotnet publish output + efbundle)
#   /opt/colourbricks/web/releases/<RELEASE>/   (.next, public, package.json, package-lock.json)
#
# Usage: deploy.sh <RELEASE>
# Exits non-zero (and rolls back) if the post-deploy health checks fail.
set -euo pipefail

RELEASE="${1:?usage: deploy.sh <release-timestamp>}"
BASE=/opt/colourbricks
SHARED="$BASE/shared"
API_REL="$BASE/api/releases/$RELEASE"
WEB_REL="$BASE/web/releases/$RELEASE"
KEEP_RELEASES=5

log() { echo "[deploy $RELEASE] $*"; }

[ -d "$API_REL" ] || { echo "missing $API_REL"; exit 1; }
[ -d "$WEB_REL" ] || { echo "missing $WEB_REL"; exit 1; }

PREV_API=$(readlink -f "$BASE/api/current" || true)
PREV_WEB=$(readlink -f "$BASE/web/current" || true)

rollback() {
  log "FAILED — rolling back"
  if [ -n "${PREV_API:-}" ]; then ln -sfn "$PREV_API" "$BASE/api/current"; fi
  if [ -n "${PREV_WEB:-}" ]; then ln -sfn "$PREV_WEB" "$BASE/web/current"; fi
  sudo systemctl restart colourbricks-api colourbricks-web || true
  exit 1
}
trap rollback ERR

log "linking persistent storage/logs into the new API release"
mkdir -p "$SHARED/storage" "$SHARED/logs"
ln -sfn "$SHARED/storage" "$API_REL/storage"
ln -sfn "$SHARED/logs" "$API_REL/logs"

log "installing production node_modules for web release (npm ci --omit=dev)"
( cd "$WEB_REL" && npm ci --omit=dev )

if [ -x "$API_REL/efbundle" ]; then
  log "applying EF Core migrations via efbundle"
  # shellcheck disable=SC1091
  source "$SHARED/api.env"
  # efbundle's own --connection flag does NOT override the app's design-time
  # DbContext factory (AppDbContextFactory) — confirmed the hard way. That
  # factory reads COLOURBRICKS_MIGRATIONS_CONNECTION, so export it instead.
  export COLOURBRICKS_MIGRATIONS_CONNECTION="$ConnectionStrings__Default"
  "$API_REL/efbundle"
else
  log "no efbundle found in release, skipping migrations"
fi

log "switching 'current' symlinks"
ln -sfn "$API_REL" "$BASE/api/current"
ln -sfn "$WEB_REL" "$BASE/web/current"

log "restarting services"
sudo systemctl restart colourbricks-api
sleep 3
sudo systemctl restart colourbricks-web
sleep 3

log "health check: API"
for i in 1 2 3 4 5; do
  if curl -fsS http://127.0.0.1:5095/health >/dev/null; then break; fi
  [ "$i" -eq 5 ] && { echo "API health check failed"; false; }
  sleep 3
done

log "health check: Web"
for i in 1 2 3 4 5; do
  if curl -fsS http://127.0.0.1:3003/ >/dev/null; then break; fi
  [ "$i" -eq 5 ] && { echo "Web health check failed"; false; }
  sleep 3
done

trap - ERR
log "OK — pruning old releases (keeping last $KEEP_RELEASES)"
( cd "$BASE/api/releases" && ls -1t | tail -n +$((KEEP_RELEASES + 1)) | xargs -r rm -rf )
( cd "$BASE/web/releases" && ls -1t | tail -n +$((KEEP_RELEASES + 1)) | xargs -r rm -rf )

log "done"

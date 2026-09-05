#!/usr/bin/env bash
# Creates (or resets the password of) a production Administrator account by
# running the ColourBricks.SeedAdmin tool with the right environment set.
# Safe to re-run later if an admin is ever locked out.
#
# Usage:
#   ./seed-admin-production.sh you@yourcompany.com "server=...;database=...;user=...;password=...;"
#   ./seed-admin-production.sh you@yourcompany.com "..." "a specific password"
#
#   # Print raw SQL instead of writing to a database (no connection string needed):
#   ./seed-admin-production.sh you@yourcompany.com --sql
#
# Prompts for anything not passed as an argument. Omit the password argument
# to have a strong one generated and printed once by the tool.
set -euo pipefail

EMAIL="${1:-}"
CONNECTION_STRING="${2:-}"
PASSWORD="${3:-}"

if [ -z "$EMAIL" ]; then
  read -rp "Administrator email: " EMAIL
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if [ "$CONNECTION_STRING" = "--sql" ]; then
  if [ -n "$PASSWORD" ]; then
    dotnet run --project tools/ColourBricks.SeedAdmin -- --email "$EMAIL" --password "$PASSWORD" --sql
  else
    dotnet run --project tools/ColourBricks.SeedAdmin -- --email "$EMAIL" --sql
  fi
  exit 0
fi

if [ -z "$CONNECTION_STRING" ]; then
  read -rp "Production connection string (ConnectionStrings:Default): " CONNECTION_STRING
fi

export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__Default="$CONNECTION_STRING"

if [ -n "$PASSWORD" ]; then
  dotnet run --project tools/ColourBricks.SeedAdmin -- --email "$EMAIL" --password "$PASSWORD"
else
  dotnet run --project tools/ColourBricks.SeedAdmin -- --email "$EMAIL"
fi

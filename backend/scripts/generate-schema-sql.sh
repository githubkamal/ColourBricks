#!/usr/bin/env bash
# Generates a raw, idempotent SQL script that creates every table/index/constraint
# in the current EF Core migration history — for handing to a DBA, or running
# with any MySQL client against production instead of running
# `dotnet ef database update` there directly.
#
# schema.sql is committed to the repo and must stay in sync with the migrations —
# re-run this and commit the result in the same change as any migration that adds
# or alters a table.
#
# Usage:
#   ./generate-schema-sql.sh                # writes schema.sql next to this script
#   ./generate-schema-sql.sh /path/out.sql
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUT_FILE="${1:-$SCRIPT_DIR/schema.sql}"

cd "$REPO_ROOT"
dotnet ef migrations script --idempotent \
  -p src/ColourBricks.Infrastructure \
  -s src/ColourBricks.Api \
  -o "$OUT_FILE"

echo "Wrote $OUT_FILE"
echo "Apply with: mysql -u <user> -p <database> < \"$OUT_FILE\""

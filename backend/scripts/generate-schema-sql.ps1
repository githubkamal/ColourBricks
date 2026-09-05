#Requires -Version 5.1
<#
Generates a raw, idempotent SQL script that creates every table/index/constraint
in the current EF Core migration history (all of them, from scratch, or just
the missing ones against a database that already has some applied) — for
handing to a DBA, or running with any MySQL client against production instead
of running `dotnet ef database update` there directly.

schema.sql is committed to the repo and must stay in sync with the migrations —
re-run this and commit the result in the same change as any migration that adds
or alters a table.

Usage:
  .\generate-schema-sql.ps1                      # writes schema.sql next to this script
  .\generate-schema-sql.ps1 -OutFile C:\out.sql
#>
param(
    [string]$OutFile = (Join-Path $PSScriptRoot "schema.sql")
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    dotnet ef migrations script --idempotent `
        -p src/ColourBricks.Infrastructure `
        -s src/ColourBricks.Api `
        -o $OutFile
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($exitCode -eq 0) {
    Write-Host "Wrote $OutFile"
    Write-Host "Apply with: mysql -u <user> -p <database> < `"$OutFile`""
}

exit $exitCode

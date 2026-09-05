#Requires -Version 5.1
<#
Creates (or resets the password of) a production Administrator account by
running the ColourBricks.SeedAdmin tool with the right environment set.
Safe to re-run later if an admin is ever locked out.

Usage:
  .\seed-admin-production.ps1 -Email you@yourcompany.com -ConnectionString "server=...;database=...;user=...;password=...;"
  .\seed-admin-production.ps1 -Email you@yourcompany.com -ConnectionString "..." -Password "a specific one"

  # Print raw SQL instead of writing to a database (no -ConnectionString needed):
  .\seed-admin-production.ps1 -Email you@yourcompany.com -Sql

Prompts for anything not passed as a parameter. Omit -Password to have a
strong one generated and printed once by the tool.
#>
param(
    [string]$Email,
    [string]$ConnectionString,
    [string]$Password,
    [string]$Name,
    [switch]$Sql
)

$ErrorActionPreference = "Stop"

if (-not $Email) {
    $Email = Read-Host "Administrator email"
}
if (-not $Sql -and -not $ConnectionString) {
    $ConnectionString = Read-Host "Production connection string (ConnectionStrings:Default)"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $toolArgs = @("--email", $Email)
    if ($Password) { $toolArgs += @("--password", $Password) }
    if ($Name) { $toolArgs += @("--name", $Name) }

    if ($Sql) {
        $toolArgs += "--sql"
        dotnet run --project tools/ColourBricks.SeedAdmin -- @toolArgs
        $exitCode = $LASTEXITCODE
    }
    else {
        $env:ASPNETCORE_ENVIRONMENT = "Production"
        $env:ConnectionStrings__Default = $ConnectionString
        dotnet run --project tools/ColourBricks.SeedAdmin -- @toolArgs
        $exitCode = $LASTEXITCODE
    }
}
finally {
    Remove-Item Env:\ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
    Remove-Item Env:\ConnectionStrings__Default -ErrorAction SilentlyContinue
    Pop-Location
}

exit $exitCode

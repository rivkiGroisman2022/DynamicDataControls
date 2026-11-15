# ==========================================================
# Builds the DataControlsDemo database from scratch by running
# every *.sql file in this folder in name order.
# Usage:   .\setup-database.ps1                       (default server: .\SQLEXPRESS)
#          .\setup-database.ps1 -Server "localhost"   (another SQL Server instance)
# The server here must match "Database:ConnectionString" in backend/src/DataControls.Api/appsettings.json
# ==========================================================
param(
    [string]$Server = ".\SQLEXPRESS"
)

$ErrorActionPreference = "Stop"
$scripts = Get-ChildItem -Path $PSScriptRoot -Filter "*.sql" | Sort-Object Name

foreach ($script in $scripts) {
    Write-Host "Running $($script.Name)..." -ForegroundColor Cyan
    # -E = Windows Authentication | -b = stop on error | -f 65001 = UTF-8 (Hebrew text)
    sqlcmd -S $Server -E -b -f 65001 -i $script.FullName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED on $($script.Name)" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Database DataControlsDemo is ready." -ForegroundColor Green

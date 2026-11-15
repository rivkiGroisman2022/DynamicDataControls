# ==========================================================
# Starts the three processes of the system, each in its own window:
#   API (5080), AI service (8000), frontend (4200).
# Prerequisites: the database was created (database\setup-database.ps1),
# ai-service\.venv exists and frontend\node_modules was installed - see README.md.
# ==========================================================
$root = $PSScriptRoot

Start-Process powershell -ArgumentList "-NoExit", "-Command",
    "Set-Location '$root\backend'; dotnet run --project src/DataControls.Api --launch-profile http"

Start-Process powershell -ArgumentList "-NoExit", "-Command",
    "Set-Location '$root\ai-service'; .venv\Scripts\python -m uvicorn app.main:app --port 8000"

Start-Process powershell -ArgumentList "-NoExit", "-Command",
    "Set-Location '$root\frontend'; npm start"

Write-Host "Starting... the browser will open at http://localhost:4200" -ForegroundColor Green

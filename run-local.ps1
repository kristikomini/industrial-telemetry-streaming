<#
.SYNOPSIS
    Run the entire Motor Valley Sentinel stack natively, with no Docker.

.DESCRIPTION
    Starts all four application services wired to their zero-install substitutes:
      - Kafka     -> direct HTTP between producer, processor, and backend
      - PostgreSQL-> SQLite  (a single motorvalley.db file)
      - Redis     -> in-process memory cache
    Each service launches in its own PowerShell window so you can read its logs and
    stop it with Ctrl+C. Requires only the .NET 8 SDK, Python 3.10+, and Node.js 18+.

.PARAMETER Machines
    Number of simulated machines the producer runs (default 120).

.PARAMETER Source
    Where readings come from: 'sim' (built-in random walk, default) or 'opcua' (read live
    values from an OPC UA server such as the Prosys Simulation Server). With 'opcua', set
    $env:OPCUA_ENDPOINT first if your server is not on the Prosys default endpoint.

.EXAMPLE
    .\run-local.ps1
    .\run-local.ps1 -Machines 40
    .\run-local.ps1 -Source opcua
#>
param(
    [int]$Machines = 120,
    [ValidateSet('sim', 'opcua')]
    [string]$Source = 'sim'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Require-Command($name, $hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Write-Error "Required tool '$name' was not found on PATH. $hint"
        exit 1
    }
}

Write-Host "Checking prerequisites..." -ForegroundColor Cyan
Require-Command dotnet "Install the .NET 8 SDK from https://dotnet.microsoft.com/download"
Require-Command node   "Install Node.js 18+ from https://nodejs.org/"
Require-Command npm    "Install Node.js 18+ (npm ships with it) from https://nodejs.org/"

$pythonCmd = Get-Command python -ErrorAction SilentlyContinue
if (-not $pythonCmd) { $pythonCmd = Get-Command python3 -ErrorAction SilentlyContinue }
if (-not $pythonCmd) {
    Write-Error "Required tool 'python' was not found on PATH. Install Python 3.10+ from https://python.org/"
    exit 1
}
$python = $pythonCmd.Source

# --- Python virtual environment (shared by producer + processor) ------------------
$venv = Join-Path $root ".venv-local"
$venvPython = Join-Path $venv "Scripts\python.exe"
if (-not (Test-Path $venvPython)) {
    Write-Host "Creating Python virtual environment (.venv-local)..." -ForegroundColor Cyan
    & $python -m venv $venv
}
Write-Host "Installing Python dependencies..." -ForegroundColor Cyan
& $venvPython -m pip install --quiet --upgrade pip
& $venvPython -m pip install --quiet -r (Join-Path $root "processor\requirements.txt") -r (Join-Path $root "producer\requirements.txt")

# --- Frontend dependencies --------------------------------------------------------
if (-not (Test-Path (Join-Path $root "frontend\node_modules"))) {
    Write-Host "Installing frontend dependencies (npm install)..." -ForegroundColor Cyan
    Push-Location (Join-Path $root "frontend")
    npm install
    Pop-Location
}

# --- Launch each service in its own window ----------------------------------------
function Start-MvsService($title, $workDir, $command) {
    $inner = "`$host.UI.RawUI.WindowTitle = '$title'; Set-Location '$workDir'; $command"
    Start-Process powershell -ArgumentList '-NoExit', '-Command', $inner | Out-Null
}

Write-Host "`nStarting services..." -ForegroundColor Green

# 1) Backend: SQLite + in-memory cache + Kafka disabled (via the Local launch profile).
Start-MvsService "MVS backend" (Join-Path $root "backend\MotorValley.Backend") `
    "dotnet run --launch-profile Local"
Write-Host "  backend    -> http://localhost:5000/swagger"
Start-Sleep -Seconds 8

# 2) Processor: HTTP transport, forwards alerts to the backend.
Start-MvsService "MVS processor" (Join-Path $root "processor") `
    "`$env:TRANSPORT='http'; `$env:BACKEND_URL='http://localhost:5000'; & '$venvPython' -m uvicorn processor:app --host 0.0.0.0 --port 8000"
Write-Host "  processor  -> http://localhost:8000/health"
Start-Sleep -Seconds 4

# 3) Producer: HTTP transport, POSTs readings to the processor. SOURCE selects sim vs OPC UA;
#    OPCUA_* env vars set in this shell are inherited by the new window.
Start-MvsService "MVS producer" (Join-Path $root "producer") `
    "`$env:SOURCE='$Source'; `$env:TRANSPORT='http'; `$env:PROCESSOR_URL='http://localhost:8000'; `$env:NUM_MACHINES='$Machines'; & '$venvPython' producer.py"
Write-Host "  producer   -> $Machines machines (source: $Source)"

# 4) Frontend: Vite dev server, proxies /api and /hubs to the backend.
Start-MvsService "MVS frontend" (Join-Path $root "frontend") `
    "npm run dev"
Write-Host "  dashboard  -> http://localhost:5173"

Write-Host "`nAll services launched in separate windows. Close a window or press Ctrl+C in it to stop that service." -ForegroundColor Green
Write-Host "Give it a minute - machines drift by a random walk, so alerts build rather than appear instantly." -ForegroundColor DarkGray

#!/usr/bin/env bash
#
# Run the entire Motor Valley Sentinel stack natively, with no Docker.
#
# Wires each service to its zero-install substitute:
#   Kafka      -> direct HTTP between producer, processor, and backend
#   PostgreSQL -> SQLite (a single motorvalley.db file)
#   Redis      -> in-process memory cache
#
# Requires only the .NET 10 SDK, Python 3.10+, and Node.js 18+.
# All services run as background jobs of this script; Ctrl+C stops the whole stack.
#
# Usage:  ./run-local.sh [NUM_MACHINES]     (default 120)
#
# Data source is chosen with the SOURCE env var:
#   SOURCE=sim    built-in random walk (default)
#   SOURCE=opcua  read live values from an OPC UA server (set OPCUA_ENDPOINT if not Prosys default)
# e.g.  SOURCE=opcua ./run-local.sh 40

set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
machines="${1:-120}"
source_mode="${SOURCE:-sim}"

need() { command -v "$1" >/dev/null 2>&1 || { echo "Required tool '$1' not found on PATH. $2"; exit 1; }; }
echo "Checking prerequisites..."
need dotnet "Install the .NET 10 SDK from https://dotnet.microsoft.com/download"
need node   "Install Node.js 18+ from https://nodejs.org/"
need npm    "Install Node.js 18+ (npm ships with it) from https://nodejs.org/"
python_bin="$(command -v python3 || command -v python || true)"
[ -n "$python_bin" ] || { echo "Required tool 'python' not found. Install Python 3.10+."; exit 1; }

# --- Python virtual environment (shared by producer + processor) ---
venv="$root/.venv-local"
if [ ! -x "$venv/bin/python" ]; then
  echo "Creating Python virtual environment (.venv-local)..."
  "$python_bin" -m venv "$venv"
fi
venv_py="$venv/bin/python"
echo "Installing Python dependencies..."
"$venv_py" -m pip install --quiet --upgrade pip
"$venv_py" -m pip install --quiet -r "$root/processor/requirements.txt" -r "$root/producer/requirements.txt"

# --- Frontend dependencies ---
if [ ! -d "$root/frontend/node_modules" ]; then
  echo "Installing frontend dependencies (npm install)..."
  (cd "$root/frontend" && npm install)
fi

pids=()
cleanup() { echo; echo "Stopping services..."; for p in "${pids[@]}"; do kill "$p" 2>/dev/null || true; done; }
trap cleanup EXIT INT TERM

echo
echo "Starting services..."

# 1) Backend: SQLite + in-memory cache + Kafka disabled (via the Local launch profile).
( cd "$root/backend/MotorValley.Backend" && dotnet run --launch-profile Local ) &
pids+=($!)
echo "  backend    -> http://localhost:5000/swagger"
sleep 8

# 2) Processor: HTTP transport, forwards alerts to the backend.
( cd "$root/processor" && TRANSPORT=http BACKEND_URL=http://localhost:5000 \
    "$venv_py" -m uvicorn processor:app --host 0.0.0.0 --port 8000 ) &
pids+=($!)
echo "  processor  -> http://localhost:8000/health"
sleep 4

# 3) Producer: HTTP transport, POSTs readings to the processor. SOURCE selects sim vs OPC UA;
#    OPCUA_* env vars exported before running this script are passed through.
( cd "$root/producer" && SOURCE="$source_mode" TRANSPORT=http PROCESSOR_URL=http://localhost:8000 NUM_MACHINES="$machines" \
    "$venv_py" producer.py ) &
pids+=($!)
echo "  producer   -> $machines machines (source: $source_mode)"

# 4) Frontend: Vite dev server, proxies /api and /hubs to the backend.
( cd "$root/frontend" && npm run dev ) &
pids+=($!)
echo "  dashboard  -> http://localhost:5173"

echo
echo "All services running. Press Ctrl+C to stop the whole stack."
wait

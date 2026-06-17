#!/usr/bin/env bash
# Post-create script for the Planalyzer devcontainer.
# 1. dotnet restore
# 2. installa sqlcmd (mssql-tools18) per poter eseguire il seed
# 3. aspetta il sidecar SQL Server
# 4. esegue il seed di un DB demo "PlanalyzerDemo"
set -euo pipefail

cd "/workspaces/${PWD##*/}" 2>/dev/null || cd "$(dirname "$0")/.."

echo "==> dotnet restore"
dotnet restore Planalyzer.sln

echo "==> install sqlcmd (mssql-tools18)"
if ! command -v sqlcmd >/dev/null 2>&1 && ! [ -x /opt/mssql-tools18/bin/sqlcmd ]; then
  sudo mkdir -p /etc/apt/keyrings
  curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | sudo gpg --dearmor -o /etc/apt/keyrings/microsoft.gpg
  # Per Ubuntu 22.04 (devcontainer di base); per 20.04 cambia il suffisso.
  ubuntu_version="$(. /etc/os-release && echo "$VERSION_ID")"
  curl -fsSL "https://packages.microsoft.com/config/ubuntu/${ubuntu_version}/prod.list" \
    | sudo tee /etc/apt/sources.list.d/mssql-release.list >/dev/null
  sudo apt-get update -y
  sudo ACCEPT_EULA=Y DEBIAN_FRONTEND=noninteractive apt-get install -y mssql-tools18 unixodbc-dev
fi
SQLCMD=/opt/mssql-tools18/bin/sqlcmd

echo "==> attendo SQL Server (host=db, porta 1433)"
for i in {1..60}; do
  if "$SQLCMD" -S db,1433 -U sa -P "${MSSQL_SA_PASSWORD}" -C -l 3 -Q "SELECT 1" >/dev/null 2>&1; then
    echo "    SQL Server pronto."
    break
  fi
  sleep 2
done

echo "==> seed di PlanalyzerDemo"
"$SQLCMD" -S db,1433 -U sa -P "${MSSQL_SA_PASSWORD}" -C -l 30 -i .devcontainer/seed.sql

cat <<EOM

============================================================
 Planalyzer Codespace pronto.

 Connection string demo (gia' nella env PLANALYZER_DEMO_CONN):
   ${PLANALYZER_DEMO_CONN:-Server=db,1433;Database=PlanalyzerDemo;User Id=sa;Password=Planalyzer_Dev_2026!;TrustServerCertificate=True;Encrypt=False}

 Comandi rapidi:
   dotnet run --project src/Planalyzer.Web      # Web UI -> http://localhost:5057
   dotnet run --project src/Planalyzer.Cli -- validate --sql-file .devcontainer/sample-queries.sql
   dotnet test                                  # esegue gli xUnit

 Vedi .devcontainer/README.md per il giro completo.
============================================================
EOM

# Devcontainer / Codespaces

## Cosa contiene

| File | Cosa fa |
|---|---|
| `devcontainer.json` | Definisce il Codespace: usa `docker-compose.yml`, espone la porta `5057` con auto-open browser, imposta `MSSQL_SA_PASSWORD` e `PLANALYZER_DEMO_CONN`, installa le estensioni VS Code C# + MSSQL. |
| `docker-compose.yml` | Due servizi: `app` (.NET 8 SDK, dove sviluppi) e `db` (SQL Server 2022 Developer). Connessi via rete `planalyzer-net`. |
| `post-create.sh` | `dotnet restore` + installa `sqlcmd` + aspetta `db` + esegue `seed.sql`. |
| `seed.sql` | Crea `PlanalyzerDemo`: Customers (1k), Orders (50k), OrderLines (150k), una scalar UDF di demo. |
| `sample-queries.sql` | Esempi di query certificate, non certificate, Critical. Da incollare nella UI per provare. |

## Come avviare

1. Su GitHub, **Code → Codespaces → Create codespace on this branch**.
2. Aspetta che parta il post-create (1–3 minuti, scarica le image e fa il seed).
3. In terminale dentro il Codespace:
   ```bash
   dotnet run --project src/Planalyzer.Web
   ```
4. Codespaces fa il port-forward automatico di `5057`. Apri il browser sulla URL forwardata (te la apre da solo grazie a `onAutoForward: openBrowser`).
5. Vai sul tab **Esegui & history** e incolla nel campo *Connection string*:
   ```
   Server=db,1433;Database=PlanalyzerDemo;User Id=sa;Password=Planalyzer_Dev_2026!;TrustServerCertificate=True;Encrypt=False
   ```
   (è anche disponibile come env `PLANALYZER_DEMO_CONN`).

## Provare in CLI

```bash
# Validare una query (exit code 2 se Critical)
dotnet run --project src/Planalyzer.Cli -- validate --sql-file .devcontainer/sample-queries.sql

# Eseguire una query con history + analisi piano
dotnet run --project src/Planalyzer.Cli -- run \
   --conn "$PLANALYZER_DEMO_CONN" \
   --slug orders-by-date \
   --sql "SELECT TOP 1000 OrderId, Total FROM dbo.Orders WHERE OrderDate >= DATEADD(DAY,-30,SYSUTCDATETIME()) ORDER BY Total DESC"

# Vedere la storia
dotnet run --project src/Planalyzer.Cli -- history --slug orders-by-date

# Test
dotnet test
```

## Troubleshooting

### 502 sulla URL forwardata di Codespaces

Significa che il forwarder non riesce a connettersi al processo. Cause comuni:

1. **L'app non è in esecuzione.** Lancia: `dotnet run --project src/Planalyzer.Web`. Aspetta `Now listening on: http://0.0.0.0:5057`. Solo allora la URL forwardata risponde.
2. **L'app è bindata solo su `localhost`.** Il bind cross-platform DEVE essere `0.0.0.0:5057`. La `launchSettings.json` di default è già così; se hai usato un profilo diverso, forza l'override:
   ```bash
   ASPNETCORE_URLS=http://0.0.0.0:5057 dotnet run --project src/Planalyzer.Web --no-launch-profile
   ```
3. **Port `5057` non forwardato.** Apri il pannello *Ports* di VS Code; se manca, **Forward a Port → 5057**, visibilità **Private**.

Verifica veloce dal terminale del Codespace:
```bash
curl -sS http://127.0.0.1:5057/api/health
# atteso: {"status":"ok","time":"..."}
```

## Note

- La password `Planalyzer_Dev_2026!` è solo per il container dev locale dentro il Codespace. Non è esposta sulla rete pubblica: SQL Server è accessibile **solo** dal container `app` tramite la rete privata `planalyzer-net` (non è nei `forwardPorts`).
- L'image `mcr.microsoft.com/mssql/server:2022-latest` vuole ≥2 GB di RAM. Il template Codespaces 4-core ne ha 8 GB: ok.
- Se il seed fallisce (SQL Server non pronto in tempo), puoi rilanciarlo manualmente:
   ```bash
   /opt/mssql-tools18/bin/sqlcmd -S db,1433 -U sa -P "$MSSQL_SA_PASSWORD" -C -i .devcontainer/seed.sql
   ```

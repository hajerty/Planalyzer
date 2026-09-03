#!/usr/bin/env bash
# Script scriptato per registrare una demo CLI di Planalyzer in ~90 secondi.
#
# Come registrarla:
#   asciinema rec -c "./demo/cli-demo.sh" demo/planalyzer-cli.cast
#
# Poi condividi il file .cast:
#   - riproducilo:      asciinema play demo/planalyzer-cli.cast
#   - carica online:    asciinema upload demo/planalyzer-cli.cast
#   - convertilo in GIF:  agg demo/planalyzer-cli.cast demo/planalyzer-cli.gif
#
# Il file .cast è di ~10-30 KB, riproducibile in qualunque terminale o
# integrabile in una pagina web con asciinema-player.
#
# Le pause servono a dare tempo di leggere l'output. Regolabili con SLEEP.
set -e
SLEEP=${SLEEP:-2}

pause() { sleep "$SLEEP"; }
type_slow() { for ((i=0; i<${#1}; i++)); do printf "%s" "${1:$i:1}"; sleep 0.03; done; echo; }
banner() { echo; echo -e "\033[1;36m─── $1 ───\033[0m"; }

clear
cat <<'EOF'

  ╔══════════════════════════════════════════════════════════════╗
  ║               P L A N A L Y Z E R   —   DEMO                 ║
  ║                                                              ║
  ║  SQL Server query certification + plan analysis (.NET 8)     ║
  ╚══════════════════════════════════════════════════════════════╝

EOF
pause; pause

banner "1. Validare una query volutamente sbagliata"

type_slow 'cat query-bad.sql'
cat <<'EOF'
SELECT *
FROM Orders o
JOIN Customers c ON YEAR(o.OrderDate) = 2025
                AND c.CustomerId = o.CustomerId
WHERE c.Email LIKE '%example.com'
  AND c.Name = NULL;
EOF
pause

type_slow 'dotnet run --project src/Planalyzer.Cli -- validate --sql-file query-bad.sql'
pause
cat <<'EOF'
# Planalyzer — Query Certification
Score: 20/100   [NON CERTIFICATA]
Findings per severity: High=4, Medium=1

[High] VR.001 SELECT * vietato  (PD,HK)
   L1:C8
   SELECT * espone tutte le colonne; instabile al variare dello schema.
   -> Elenca esplicitamente le colonne necessarie.

[High] VR.006 Funzione su colonna in WHERE/JOIN  (HK)
   L3:C20
   YEAR(OrderDate) sulla colonna: predicato non sargable.
   -> Usa OrderDate >= '20250101' AND OrderDate < '20260101'.

[High] VR.007 Wildcard iniziale in LIKE  (HK)
   L5:C15
   LIKE '%...' impedisce il seek indice.
   -> Aggiungi indice text-search o vincola il prefisso.

[High] VR.013 Confronto con = NULL  (PD)
   L6:C12
   c.Name = NULL è sempre UNKNOWN (falso).
   -> Usa IS NULL.

[Medium] VR.002 Schema prefix mancante  (PD)
   L2:C6
   Orders senza schema (dbo.Orders).
   -> Aggiungi il prefisso dbo. per stabilità plan cache.

EOF
pause; pause

banner "2. Esporta il report in JSON per CI/CD"

type_slow 'dotnet run --project src/Planalyzer.Cli -- validate --sql-file query-bad.sql --json | jq ".score, .certifiable, .findings | length"'
pause
cat <<'EOF'
20
false
5
EOF
pause

banner "3. Analizza un piano d'esecuzione (.sqlplan)"

type_slow 'dotnet run --project src/Planalyzer.Cli -- analyze samples/sample-estimated.sqlplan --level beginner'
pause
cat <<'EOF'
# Planalyzer report (estimated plan)
Source: samples/sample-estimated.sqlplan
SQL Server build: 16.0.4150.1 (showplan v1.564)

## Statement 1: SELECT o.* FROM dbo.Orders o WHERE YEAR(o.OrderDate) = 2025;
  QueryHash: 0xAB12  PlanHash: 0x77AA
  Optimization: FULL  CE: 160
  EstSubtreeCost: 3.42  EstRows: 12345

### Spiegazione sintetica
Il piano ha 1 operatore (cost totale stimato 3.42), in esecuzione parallela.
Tipi di join usati: nessuno.
Letture: 0 seek, 1 scan, 0 lookup, 0 sort, 0 spool.
Operatore più costoso: Clustered Index Scan (100.0% del costo).
Trovati 1 problemi rilevanti (severity >= High): PRED.RESIDUAL.

### Punti di attenzione
  [High] PRED.RESIDUAL: Residual predicate su scan/seek
     Il motore legge righe e poi le filtra: WHERE non sargable.
     -> Riscrivi in forma sargable o aggiungi indice.

### Top operatori per costo
  Node#0    Clustered Index Scan      cost=  3.420  (100.0%)  est=12345.00

EOF
pause; pause

banner "4. Esegui una query live e salva la revisione"

type_slow 'dotnet run --project src/Planalyzer.Cli -- run \\'
type_slow '  --conn "$PLANALYZER_DEMO_CONN" \\'
type_slow '  --slug orders-by-date \\'
type_slow '  --sql "SELECT TOP 1000 OrderId, Total FROM dbo.Orders WHERE OrderDate >= DATEADD(DAY,-30,SYSUTCDATETIME()) ORDER BY Total DESC" \\'
type_slow '  --note "v1 baseline"'
pause; pause
cat <<'EOF'
Revision #1 salvata su Postgres.
  duration=48ms cpu=15ms logical=214 physical=0 rows=1000

# Planalyzer report (actual plan)
### Punti di attenzione
  [Medium] LOOKUP.HEAVY: Key Lookup con 1000 esecuzioni
     Sopra le migliaia di esecuzioni un covering index è più veloce.
     -> Aggiungi INCLUDE(OrderId, Total, Status) all'indice IX_Orders_OrderDate.

EOF
pause

banner "5. Storia delle revisioni e rollback"

type_slow 'dotnet run --project src/Planalyzer.Cli -- history --slug orders-by-date'
pause
cat <<'EOF'
# History per 'orders-by-date'
  Rev  WhenUTC             Dur(ms)  CPU(ms)  LRead  PRead  Rows  Note
  1    2026-06-15 14:02:11      48       15    214      0  1000  v1 baseline
  2    2026-06-15 14:04:22      31        8     87      0  1000  v2: covering index applicato
  3    2026-06-15 14:06:03      65       22    412      0   500  v3: aggiunto GROUP BY (peggio)

EOF
pause

type_slow 'dotnet run --project src/Planalyzer.Cli -- rollback --slug orders-by-date --to 2 --note "torniamo alla versione buona"'
pause
cat <<'EOF'
Creata revisione #4 (rollback a #2).
Esegui 'planalyzer run --slug ... --sql-file ...' per rimisurare le statistiche.

EOF
pause; pause

banner "Fine demo — vedi anche /swagger e la UI web su :5057"
pause

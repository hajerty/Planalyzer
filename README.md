# Planalyzer

Tool .NET 8 (console + librerie) per l'analisi dei **piani esecuzione SQL Server**.
Pensato sia per neofiti (sintesi guidata, spiegazione testuale, riferimenti)
sia per DBA esperti (dump completo delle proprietà di ogni operatore: niente
troncamenti come in SSMS o nei tooltip).

Il knowledge base interno codifica le euristiche e i pattern documentati
pubblicamente sul sito **sqlserverfast.com** di Hugo Kornelis (Plan Operators
series, "Why is my plan slow?", articoli su sargability, parameter sniffing,
spill, parallelismo, ecc.). Ogni finding riporta un riferimento alla pagina di
origine quando applicabile.

> **Disclaimer.** I contenuti di sqlserverfast.com sono di Hugo Kornelis. Questo
> tool li rielabora come riferimenti consultabili e li usa come base per le
> regole euristiche; non li copia integralmente. I link vengono inclusi in
> chiaro nel report, così l'utente può approfondire alla fonte.

## Requisiti

- .NET 8 SDK
- (opzionale) un'istanza SQL Server raggiungibile per la modalità live

## Build

```bash
dotnet restore
dotnet build -c Release
```

L'eseguibile sarà `src/Planalyzer.Cli/bin/Release/net8.0/planalyzer`.

## Comandi disponibili

### `analyze` — analisi di uno o più piani

Riceve in input uno o più file XML (.sqlplan).

```bash
planalyzer analyze samples/sample-estimated.sqlplan
planalyzer analyze p1.sqlplan p2.sqlplan --level expert --out report.txt
```

Output:

- **Spiegazione sintetica** del piano: tipologia di join, scan/seek/lookup,
  operatore più costoso, problemi rilevanti.
- **Punti di attenzione**: lista delle anomalie con severity, spiegazione,
  raccomandazione e link al riferimento (sqlserverfast.com).
- **Top operatori per costo** con NodeId, costo %, righe stimate/attuali,
  oggetti toccati.
- (Solo `--level expert`) **Dettaglio completo proprietà operatori**: ogni
  attributo XML, predicato, residual, scalar definitions, statistiche per
  thread — *senza troncamento*.

### `compare` — stimato vs effettivo

```bash
planalyzer compare --estimated est.sqlplan --actual act.sqlplan
```

Confronta:

- forma del piano (operatori per NodeId);
- cardinalità: per ogni operatore mostra Estimated rows vs Actual rows/exec
  e il rapporto di skew;
- warning aggiuntivi rilevati solo nell'esecuzione reale (spill, skew di CE,
  parallelismo sbilanciato).

### `multidb` — confronto multi-DB con sintesi

Confronta più piani per la stessa query (idealmente con stesso `QueryHash`)
eseguita su database diversi (es. ambienti, tenant, shard):

```bash
planalyzer multidb \
  --plan prod=prod.sqlplan \
  --plan staging=stg.sqlplan \
  --plan dev=dev.sqlplan
```

Produce:

- riepilogo per variante (cost, join, scan/seek/lookup, spill, parallelismo);
- `QueryHash` status (uguale su tutti? differente?);
- **raccomandazioni comuni** (presenti nella maggioranza dei DB) — applicabili
  ovunque con beneficio;
- **raccomandazioni divergenti** (solo su alcune varianti);
- **proposta di intervento sintetica** in commenti SQL.

### `run` — esecuzione live + history

Esegue la query, cattura il piano (estimated o actual), salva la **revisione**
(con statistiche IO/TIME) in un DB SQLite locale, poi analizza il piano.

```bash
planalyzer run \
  --conn "Server=.;Database=Northwind;Trusted_Connection=True;TrustServerCertificate=True" \
  --slug orders-by-date \
  --sql-file query.sql \
  --note "v1: prima versione" \
  --mode actual --level beginner \
  --history planalyzer.db
```

Ogni esecuzione crea una nuova **revision** con:

- testo SQL della query;
- piano XML reale o stimato;
- durata, CPU, logical reads, physical reads, righe restituite;
- output testuale di `STATISTICS IO` e `STATISTICS TIME`;
- nota dell'utente.

### `history` — visualizzazione cronologia

```bash
planalyzer history --history planalyzer.db                # elenca slug
planalyzer history --history planalyzer.db --slug orders-by-date
```

### `rollback` — torna a una revisione precedente

```bash
planalyzer rollback --slug orders-by-date --to 3 --note "il rewrite era peggio"
```

Il rollback **non sovrascrive** la storia: crea una nuova revisione con il SQL
della revisione di destinazione e una nota; le statistiche di esecuzione
torneranno al successivo `planalyzer run`.

## Architettura

```
Planalyzer.Core
  ├─ Model/          modello del piano (operatori, warning, runtime info)
  ├─ Parsing/        ShowplanParser: legge .sqlplan, preserva tutte le proprietà
  ├─ Knowledge/      OperatorReference + Heuristics (regole/anti-pattern)
  └─ Analysis/       PlanAnalyzer + TextRenderer (sintetico/dettagliato)
Planalyzer.Comparison
  ├─ EstimatedVsActual.cs
  └─ MultiDbComparer.cs
Planalyzer.Execution
  ├─ SqlPlanRunner.cs       esegue la query catturando plan + STATISTICS IO/TIME
  ├─ QueryHistoryStore.cs   storage SQLite delle revisioni
  └─ QueryWorkbench.cs      facade run/list/rollback
Planalyzer.Cli              entry point CLI con subcommand
tests/Planalyzer.Tests      xUnit
```

## Mappa euristiche → riferimenti

| Codice            | Cosa rileva                                | Riferimento                                                   |
|-------------------|---------------------------------------------|---------------------------------------------------------------|
| `WARN.SpillToTempDb`, `WARN.HashSpillDetails`, `WARN.SortSpillDetails` | spill in tempdb | sqlserverfast.com (Plan Operators: Hash Match, Sort) |
| `CE.SKEW`         | stima cardinalità errata >10x               | sqlserverfast.com (Cardinality Estimation series)             |
| `PRED.RESIDUAL`   | predicato non sargable applicato dopo Scan/Seek | sqlserverfast.com (Sargability)                          |
| `CONV.IMPLICIT`   | `CONVERT_IMPLICIT` in Compute Scalar        | sqlserverfast.com (Implicit conversion)                       |
| `LOOKUP.HEAVY`    | Key/RID Lookup con molte esecuzioni         | sqlserverfast.com (Key Lookup, tipping point)                 |
| `JOIN.NL_LARGE_OUTER` | Nested Loops su outer molto grande      | sqlserverfast.com (Nested Loops)                              |
| `SPOOL.IN_LOOP` / `SPOOL.EAGER` | spool che surroga indici / Halloween | sqlserverfast.com (Spool, Eager Spool)                  |
| `PARALLEL.SKEW`   | distribuzione parallela sbilanciata          | sqlserverfast.com (Parallelism)                               |
| `INDEX.MISSING`   | hint di indice mancante dell'optimizer       | (annotazione del piano stesso)                                |
| `OPT.EARLY_ABORT` | optimizer si è fermato prima                 | sqlserverfast.com (Optimizer)                                 |
| `OPT.LEGACY_CE`   | CE versione legacy                           | sqlserverfast.com (CE versions)                               |
| `UDF.SCALAR`      | scalar UDF non inlinable                     | sqlserverfast.com (Scalar UDF Inlining)                       |

## Beginner vs Expert

- **Beginner (`--level beginner`)** — il default. Spiegazione narrativa, top
  operatori per costo, finding con severity e raccomandazione in linguaggio
  naturale. Adatto a chi sta imparando a leggere un piano.
- **Expert (`--level expert`)** — aggiunge il dump completo dell'albero degli
  operatori con *tutte* le proprietà (Predicate, ResidualPredicate, HashKeysBuild,
  ProbeResidual, OutputList, RunTimeInformation per thread, ...). È il punto
  per cui la tooltip di SSMS spesso *non basta*: qui niente è troncato.

## Estendere

- nuove regole: aggiungi un metodo in `Heuristics.CheckOperator` o
  `AnalyzeStatement` ed emetti un `Finding` con un codice univoco.
- nuova spiegazione operatore: aggiungi una entry in
  `OperatorReference.Map`.
- altra modalità di output (JSON/HTML): aggiungi un renderer accanto a
  `TextRenderer`.

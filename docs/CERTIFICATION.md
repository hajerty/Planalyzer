# Query Certification — convenzioni interne

Documento di coordinamento del modulo *Planalyzer.Validation*. Le regole qui
catalogate definiscono cosa significa "query certificata" per il team. Sono
ispirate al corpus pubblico di **Hugo Kornelis** (sqlserverfast.com) e a
**Pinal Dave** (sqlauthority.com), oltre alle best practice generali T-SQL
(Microsoft docs, Erland Sommarskog, Itzik Ben-Gan).

Filosofia. Il direttore tecnico ha ragione: il problema spesso è a monte. Una
query "grande utente" o "cloud lento" è quasi sempre una query che ha rotto
una o più di queste regole. La certificazione **non blocca** — segnala con
severity e codice. La promozione in produzione richiede zero `Critical` e
nessun `High` non giustificato in commento (`-- justify: ...`).

## Severità

| Severity   | Significato                                  | Effetto su `certifiable` |
|------------|----------------------------------------------|--------------------------|
| `Critical` | rischio dati / sicurezza / regression sicura | `false`                  |
| `High`     | quasi sempre causa lentezza o bug latente    | `false` salvo justify    |
| `Medium`   | anti-pattern, da valutare                    | non blocca               |
| `Low`      | smell stilistico/manutenzione                | non blocca               |
| `Info`     | metrica o suggerimento                       | non blocca               |

Score = `100 - (Critical*40 + High*15 + Medium*5 + Low*1)` (clampato a 0).
Certifiable = `Critical == 0 && Highs_non_giustificati == 0`.

## Soglie configurabili (default)

| Soglia                       | Default | Note                                |
|------------------------------|---------|-------------------------------------|
| `MaxOutputColumns`           | 30      | colonne nella SELECT-list           |
| `MaxJoinedTables`            | 7       | l'ottimizzatore inizia a soffrire   |
| `MaxDerivedColumns`          | 8       | CASE/CONVERT/CONCAT/expressions     |
| `MaxEstimatedRows`           | 100000  | senza TOP/limit deve essere bounded |
| `MaxTablesWithoutAlias`      | 1       | da 2 tabelle in poi alias obbligatorio |
| `MaxStatementLines`          | 200     | statement gigante = unmaintainable  |

Configurazione via `ValidationOptions` (lato API) o `validation.json`.

## Catalogo regole

Codici stabili: cambiare la regola di una `VR.NNN` significa mantenerne il
significato. Aggiungere nuove regole = nuovo numero.

| Codice    | Titolo                          | Sev   | Fonte | Note |
|-----------|---------------------------------|-------|-------|------|
| `VR.001` | `SELECT *`                      | High  | PD,HK | Mai in produzione: tipi nascosti, plan invalidation. |
| `VR.002` | Schema prefix mancante          | Medium| PD    | `dbo.Foo` non `Foo` (resolution e cache plan). |
| `VR.003` | Troppe colonne output           | Medium| BP    | `> MaxOutputColumns`. |
| `VR.004` | Troppe tabelle in JOIN          | High  | HK    | `> MaxJoinedTables`: optimizer timeout/early-abort. |
| `VR.005` | `WITH(NOLOCK)` senza commento   | High  | PD    | Dirty read non documentato. |
| `VR.006` | Funzione su colonna in WHERE    | High  | HK    | Non-sargable: `YEAR(d)=2025`, `LOWER(c)='x'`. |
| `VR.007` | Wildcard iniziale in LIKE       | High  | HK    | `LIKE '%x'` impedisce seek. |
| `VR.008` | Cursore esplicito               | High  | PD,HK | RBAR vs set-based. |
| `VR.009` | Scalar UDF in WHERE/SELECT      | High  | HK    | Pre-2019 non inlinable -> riga per riga. |
| `VR.010` | UPDATE/DELETE senza WHERE       | Critical | BP | Quasi sempre incidente. |
| `VR.011` | Probabile implicit conversion   | High  | HK    | Parametro tipato vs colonna diversa. |
| `VR.012` | Sintassi join obsoleta          | High  | BP    | `FROM A,B WHERE A.x=B.y`. |
| `VR.013` | Confronto con `= NULL`          | High  | PD    | Deve essere `IS NULL`. |
| `VR.014` | SELECT senza TOP o limite       | Medium| BP    | Risultato potenzialmente illimitato. |
| `VR.015` | `SELECT DISTINCT` non commentato| Low   | PD    | Spesso copre un join errato. |
| `VR.016` | Troppe colonne calcolate        | Medium| BP    | `> MaxDerivedColumns`. |
| `VR.017` | SP senza `SET NOCOUNT ON`       | Medium| PD    | Round-trip inutili. |
| `VR.018` | `EXEC` con concatenazione       | Critical | PD | SQL injection / plan cache pollution. |
| `VR.019` | Table variable in join grande   | Medium| HK    | Stima 1 riga -> piani sbagliati. |
| `VR.020` | Tipo `DATETIME`                  | Low   | BP    | Preferire `DATETIME2`. |
| `VR.021` | 2+ tabelle senza alias          | Low   | PD    | Ambiguità di refactor. |
| `VR.022` | `ORDER BY` ordinale             | Low   | PD    | `ORDER BY 1,2` fragile. |
| `VR.023` | `WHILE` su singola riga         | High  | HK    | RBAR camuffato. |
| `VR.024` | `;` di fine statement mancante  | Info  | BP    | `THROW`, CTE richiedono terminatore. |
| `VR.025` | Index hint senza commento       | Medium| HK    | `WITH(INDEX(...))` fragile. |
| `VR.026` | `GOTO`                           | Medium| BP    | Quasi sempre evitabile. |
| `VR.027` | `sp_executesql` senza parametri | High  | PD    | Vanifica il punto del prepared. |
| `VR.028` | `CASE` senza `ELSE`             | Low   | BP    | NULL silente, bug latente. |
| `VR.029` | DDL dentro transazione esplicita| Medium| BP    | Lock metadata prolungati. |
| `VR.030` | Cross join implicito            | High  | BP    | `FROM A,B` senza correlazione. |
| `VR.031` | Predicato con `OR` di colonne   | Medium| HK    | Spesso forza index scan. |
| `VR.032` | `NOT IN` / `NOT EXISTS` confusi | Medium| HK    | NULL semantics di NOT IN. |
| `VR.033` | `COUNT(DISTINCT)` su grande set | Medium| HK    | Hash aggregate spill. |
| `VR.034` | Parametri ricerca opzionali     | Medium| HK    | Pattern `(@p IS NULL OR col=@p)` -> RECOMPILE. |

Fonte: `HK` = Hugo Kornelis, `PD` = Pinal Dave, `BP` = best practice generale.

## Schema dei finding

```csharp
public sealed record ValidationFinding(
    string RuleId,        // "VR.001"
    string Title,
    Severity Severity,
    string Source,        // "HK" | "PD" | "BP"
    string Message,       // localizzato, non troncato
    int? Line, int? Column,
    string? Snippet,
    string? Justification,// se il dev ha scritto -- justify: ... sopra
    string? FixHint);     // suggerimento concreto

public sealed record ValidationReport(
    List<ValidationFinding> Findings,
    int Score, bool Certifiable,
    Dictionary<string,int> CountsBySeverity,
    string SqlNormalized);
```

## Giustificazione inline

Una riga `-- justify: VR.005 lettura cache stale ammessa per dashboard`
sopra lo statement disinnesca la severity `High` per quella regola, lasciando
il finding ma marcando `Justification` non null. Critical non si giustifica
mai.

## Visitor pattern

Ogni regola = una classe `IValidationRule` con metodo
`IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)`.
Il `ValidationContext` contiene la `ValidationOptions` corrente e una mappa
linea->justify estratta dai commenti.

## API (riassunto, dettaglio in API.md)

`POST /api/validate { sql, options? } -> ValidationReport`

using Planalyzer.Core.Model;

namespace Planalyzer.Core.Knowledge;

public enum Severity { Info, Low, Medium, High, Critical }

public sealed record Finding(
    Severity Severity,
    string Code,
    string Title,
    string Explanation,
    string Recommendation,
    int? NodeId = null,
    string? Operator = null,
    Dictionary<string, string>? Evidence = null,
    string? Reference = null);

/// <summary>
/// Catalogue of heuristics distilled from the public Hugo Kornelis writings on
/// sqlserverfast.com (Plan Operators series, "Why is my plan slow?" patterns)
/// plus widely accepted SQL Server troubleshooting practice. Each rule emits
/// at most one Finding per operator/statement.
/// </summary>
public static class Heuristics
{
    public static IEnumerable<Finding> Analyze(ParsedPlan plan)
    {
        foreach (var stmt in plan.Statements)
        {
            if (stmt.Root is null) continue;
            foreach (var f in AnalyzeStatement(stmt, plan.IsActual))
                yield return f;
        }
    }

    public static IEnumerable<Finding> AnalyzeStatement(PlanStatement stmt, bool isActual)
    {
        if (stmt.Root is null) yield break;

        // Statement-level checks.
        if (stmt.StatementOptmEarlyAbortReason is { } reason && !string.IsNullOrEmpty(reason))
            yield return new Finding(Severity.Medium, "OPT.EARLY_ABORT",
                "Optimizer early-abort",
                $"L'ottimizzatore si è fermato per: {reason}.",
                "Significa che il piano potrebbe NON essere quello migliore. Verifica statistiche, riformula la query, valuta hint mirati.",
                Reference: "https://sqlserverfast.com/?s=early+abort");

        if (stmt.CardinalityEstimationModelVersion is int cev && cev < 120)
            yield return new Finding(Severity.Low, "OPT.LEGACY_CE",
                "Legacy Cardinality Estimator",
                $"CardinalityEstimationModelVersion={cev} (<120).",
                "Stai usando il vecchio CE: spesso giustificato per regression, ma valuta se il nuovo CE migliora le stime con i dati attuali.");

        // Per-operator checks.
        foreach (var op in stmt.Root.Walk())
        {
            foreach (var f in CheckOperator(op, isActual))
                yield return f;
        }

        // Missing indexes (always reported).
        foreach (var mi in stmt.MissingIndexes.OrderByDescending(m => m.Impact))
        {
            var ddl = mi.CreateIndexStatement();
            yield return new Finding(
                mi.Impact >= 80 ? Severity.High : mi.Impact >= 40 ? Severity.Medium : Severity.Low,
                "INDEX.MISSING",
                $"Missing index suggerito (Impact={mi.Impact:0.##})",
                $"L'ottimizzatore ha annotato un indice mancante su {mi.Schema}.{mi.Table}.",
                $"DDL proposta:\n{ddl}\nValuta sempre prima di crearlo: write-amplification, sovrapposizione con indici esistenti, % di selettività reale.",
                Evidence: new()
                {
                    ["Equality"] = string.Join(", ", mi.EqualityColumns),
                    ["Inequality"] = string.Join(", ", mi.InequalityColumns),
                    ["Include"] = string.Join(", ", mi.IncludeColumns),
                });
        }
    }

    private static IEnumerable<Finding> CheckOperator(PlanOperator op, bool isActual)
    {
        // -- Plan-emitted warnings -------------------------------------------------
        foreach (var w in op.Warnings)
        {
            var sev = w.Kind switch
            {
                "SpillToTempDb" => Severity.High,
                "HashSpillDetails" => Severity.High,
                "SortSpillDetails" => Severity.High,
                "ColumnsWithNoStatistics" => Severity.Medium,
                "PlanAffectingConvert" => Severity.High,
                "Wait" => Severity.Low,
                _ => Severity.Medium,
            };
            yield return new Finding(sev, $"WARN.{w.Kind}",
                $"Warning nel piano: {w.Kind}",
                ExplainPlanWarning(w),
                RecommendPlanWarning(w),
                NodeId: op.NodeId,
                Operator: op.PhysicalOp,
                Evidence: w.Properties.ToDictionary(kv => kv.Key, kv => kv.Value),
                Reference: "https://sqlserverfast.com/?s=" + w.Kind);
        }

        // -- Cardinality skew (only meaningful on actual plans) -------------------
        if (isActual && op.ActualRows is double a && op.ActualExecutions is double e && e > 0)
        {
            // Compare estimated rows-per-execution to actual rows-per-execution.
            var actualPerExec = a / e;
            var est = op.EstimateRows;
            var skew = SkewRatio(est, actualPerExec);
            if (skew >= 10 && (a >= 1000 || est >= 1000))
            {
                var sev = skew >= 100 ? Severity.High : Severity.Medium;
                yield return new Finding(sev, "CE.SKEW",
                    $"Stima cardinalità errata di ~{skew:0}x su {op.PhysicalOp}",
                    $"EstimateRows={est:0.##} vs ActualRows/exec={actualPerExec:0.##} (executions={e:0}).",
                    "Cause comuni: statistiche obsolete, parameter sniffing, predicato non sargable, table variable, MSTVF, OPTION(RECOMPILE) potrebbe aiutare.",
                    NodeId: op.NodeId, Operator: op.PhysicalOp,
                    Reference: "https://sqlserverfast.com/?s=cardinality+estimation");
            }
        }

        // -- Residual / non-sargable predicate on Scan/Seek -----------------------
        if ((op.PhysicalOp.Contains("Scan", StringComparison.OrdinalIgnoreCase)
             || op.PhysicalOp.Contains("Seek", StringComparison.OrdinalIgnoreCase))
            && op.Properties.TryGetValue("Predicate", out var predRaw)
            && !string.IsNullOrWhiteSpace(predRaw))
        {
            yield return new Finding(Severity.Medium, "PRED.RESIDUAL",
                "Residual predicate su scan/seek",
                "Il motore legge righe e poi le filtra: spesso indica WHERE non sargable (funzioni sulla colonna, ISNULL, conversioni, LIKE '%x').",
                "Riscrivi il predicato in forma sargable o aggiungi un indice mirato. Confronta EstimatedRowsRead vs ActualRows: se RowsRead >> Rows il filtro è applicato dopo la lettura.",
                NodeId: op.NodeId, Operator: op.PhysicalOp,
                Evidence: new() { ["Predicate"] = Trim(predRaw) },
                Reference: "https://sqlserverfast.com/blog/hugo/2019/08/sargability-on-cast/");
        }

        // -- Implicit conversion in scalar -----------------------------------------
        if (op.PhysicalOp.Equals("Compute Scalar", StringComparison.OrdinalIgnoreCase)
            && op.Properties.TryGetValue("DefinedValues", out var defv)
            && defv.Contains("CONVERT_IMPLICIT", StringComparison.OrdinalIgnoreCase))
        {
            yield return new Finding(Severity.High, "CONV.IMPLICIT",
                "Implicit conversion (CONVERT_IMPLICIT)",
                "Una conversione implicita può rendere il predicato non sargable e generare scan invece di seek; può anche cambiare le stime e introdurre errori di precisione.",
                "Allinea i tipi (es. parametro NVARCHAR vs colonna VARCHAR), oppure converti il valore di confronto, mai la colonna.",
                NodeId: op.NodeId, Operator: op.PhysicalOp,
                Evidence: new() { ["DefinedValues"] = Trim(defv) },
                Reference: "https://sqlserverfast.com/blog/hugo/2019/08/sargability-on-cast/");
        }

        // -- Key Lookup excessive ---------------------------------------------------
        if (op.PhysicalOp.Equals("Key Lookup", StringComparison.OrdinalIgnoreCase) ||
            op.PhysicalOp.Equals("RID Lookup", StringComparison.OrdinalIgnoreCase))
        {
            var execs = op.ActualExecutions ?? op.EstimateRows;
            if (execs >= 1000)
            {
                yield return new Finding(Severity.Medium, "LOOKUP.HEAVY",
                    $"{op.PhysicalOp} con {execs:0} esecuzioni",
                    "Ogni lookup è un I/O random nel cluster/heap. Sopra alcune migliaia di esecuzioni un covering index è quasi sempre più veloce.",
                    "Aggiungi le colonne mancanti come INCLUDE all'indice non-cluster usato dal Seek a monte, o crea un indice covering.",
                    NodeId: op.NodeId, Operator: op.PhysicalOp);
            }
        }

        // -- Nested Loops with large outer ----------------------------------------
        if (op.PhysicalOp.Equals("Nested Loops", StringComparison.OrdinalIgnoreCase))
        {
            var outer = op.Children.FirstOrDefault();
            if (outer is not null && (outer.ActualRows ?? outer.EstimateRows) >= 50_000)
            {
                yield return new Finding(Severity.Medium, "JOIN.NL_LARGE_OUTER",
                    "Nested Loops su outer molto grande",
                    "Un NL paga il costo per ogni riga esterna. Con grandi volumi è quasi sempre peggio di Hash o Merge.",
                    "Verifica se il costo è gonfiato da CE skew. Se il volume è davvero grande, valuta hash (OPTION(HASH JOIN)) o un indice che permetta MERGE.",
                    NodeId: op.NodeId, Operator: op.PhysicalOp);
            }
        }

        // -- Table/Index Spool inside loop ----------------------------------------
        if ((op.PhysicalOp.Contains("Spool", StringComparison.OrdinalIgnoreCase))
            && op.Parent?.PhysicalOp.Equals("Nested Loops", StringComparison.OrdinalIgnoreCase) == true)
        {
            yield return new Finding(Severity.Medium, "SPOOL.IN_LOOP",
                "Spool dentro Nested Loops",
                "Tipico segnale che SQL sta materializzando un indice volante in tempdb perché manca un indice fisico.",
                "Crea l'indice giusto sulle colonne di join/predicate del lato interno.",
                NodeId: op.NodeId, Operator: op.PhysicalOp,
                Reference: "https://sqlserverfast.com/epr/index-spool-eager-spool/");
        }

        // -- Eager Spool on DML ---------------------------------------------------
        if (op.PhysicalOp.Equals("Eager Spool", StringComparison.OrdinalIgnoreCase))
        {
            yield return new Finding(Severity.Low, "SPOOL.EAGER",
                "Eager Spool",
                "Materializzazione completa in tempdb. In DML è spesso Halloween protection; altrimenti può indicare un piano sub-ottimale.",
                "Verifica se è davvero necessario (in UPDATE/DELETE su colonna chiave dell'indice usato per la lettura, sì).",
                NodeId: op.NodeId, Operator: op.PhysicalOp,
                Reference: "https://sqlserverfast.com/epr/eager-spool/");
        }

        // -- Parallel skew --------------------------------------------------------
        if (op.RuntimeInfo.Count > 1)
        {
            var rows = op.RuntimeInfo.Select(r => r.ActualRows).ToArray();
            var max = rows.Max();
            var avg = rows.Average();
            if (max > 0 && avg > 0 && max / avg >= 4 && rows.Length >= 2)
            {
                yield return new Finding(Severity.Medium, "PARALLEL.SKEW",
                    $"Distribuzione parallela sbilanciata su {op.PhysicalOp}",
                    $"Thread max={max:0}, media={avg:0.##}: pochi thread fanno tutto il lavoro.",
                    "Spesso l'hash key di Repartition è poco selettiva o le stime errate. Controlla la chiave di distribuzione e le statistiche.",
                    NodeId: op.NodeId, Operator: op.PhysicalOp,
                    Evidence: new() { ["RowsPerThread"] = string.Join(",", rows.Select(r => r.ToString("0"))) },
                    Reference: "https://sqlserverfast.com/?s=parallel+skew");
            }
        }

        // -- Sort warning candidate (memory overflow heuristic) -------------------
        if (op.PhysicalOp.Equals("Sort", StringComparison.OrdinalIgnoreCase) && (op.ActualRows ?? 0) >= 100_000)
        {
            yield return new Finding(Severity.Low, "SORT.LARGE",
                "Sort di grandi dimensioni",
                "Il Sort è blocking e consuma memory grant. Se il grant è insufficiente spilla in tempdb.",
                "Valuta indice ordinato che soddisfi l'ORDER BY o GROUP BY a monte.",
                NodeId: op.NodeId, Operator: op.PhysicalOp);
        }

        // -- Scalar UDF inlining check (visible as 'UserDefinedFunction') ---------
        if (op.Properties.TryGetValue("ScalarOperator", out var so) && so.Contains("UserDefinedFunction", StringComparison.OrdinalIgnoreCase))
        {
            yield return new Finding(Severity.Medium, "UDF.SCALAR",
                "Scalar UDF rilevata",
                "Le scalar UDF non inlined forzano esecuzione riga-per-riga e nascondono i costi reali nel piano.",
                "Compatibility level >=150 + Inline=ON; se non inlinabile, riscrivi come iTVF.",
                NodeId: op.NodeId, Operator: op.PhysicalOp,
                Reference: "https://sqlserverfast.com/?s=scalar+udf");
        }
    }

    private static double SkewRatio(double est, double actual)
    {
        var e = Math.Max(est, 1);
        var a = Math.Max(actual, 1);
        return Math.Max(e / a, a / e);
    }

    private static string ExplainPlanWarning(PlanWarning w) => w.Kind switch
    {
        "SpillToTempDb" => "Operatore con memory grant insufficiente: dati materializzati in tempdb (lento, I/O extra).",
        "HashSpillDetails" => "Hash table non è entrata in memoria; SQL ha riversato in tempdb (a livelli, ognuno costa).",
        "SortSpillDetails" => "Il Sort non è entrato in memoria; è andato in tempdb a uno o più livelli.",
        "ColumnsWithNoStatistics" => "Colonne usate nel piano non hanno statistiche: stime random.",
        "PlanAffectingConvert" => "Una conversione (esplicita o implicita) ha cambiato la cardinalità o impedito il seek.",
        "Wait" => "Wait stat raccolto durante l'esecuzione; spesso ininfluente, ma può indicare contention.",
        _ => $"Warning '{w.Kind}': leggi i dettagli nelle proprietà.",
    };

    private static string RecommendPlanWarning(PlanWarning w) => w.Kind switch
    {
        "SpillToTempDb" or "HashSpillDetails" or "SortSpillDetails" =>
            "Verifica statistiche / cardinalità sulla cardinalità del lato build. Se confermato, rivedi MAXDOP, memory grant feedback, o spezza la query.",
        "ColumnsWithNoStatistics" => "Crea statistiche manuali (CREATE STATISTICS) o lascia che AUTO_CREATE_STATISTICS le faccia, e poi UPDATE STATISTICS.",
        "PlanAffectingConvert" => "Allinea i tipi: parametro/colonna devono avere stesso tipo/colation.",
        _ => "Consulta la sezione di sqlserverfast.com per il warning specifico.",
    };

    private static string Trim(string s, int max = 600) => s.Length <= max ? s : s.Substring(0, max) + "…";
}

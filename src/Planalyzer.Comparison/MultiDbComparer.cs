using System.Text;
using Planalyzer.Core.Knowledge;
using Planalyzer.Core.Model;

namespace Planalyzer.Comparison;

public sealed record DbVariant(string Label, ParsedPlan Plan);

public sealed record VariantSummary(
    string Label,
    string? QueryHash,
    string? PlanHash,
    double SubtreeCost,
    double EstRows,
    double? ActualRows,
    string DominantJoin,
    int NumScans,
    int NumSeeks,
    int NumLookups,
    int NumSpills,
    bool Parallel,
    List<Finding> Findings);

public sealed record SynthesisRecommendation(string Code, string Description, Severity Severity, int OccurrencesAcrossDbs);

public sealed record MultiDbReport(
    List<VariantSummary> Variants,
    string QueryHashStatus,            // "stesso" o "differente"
    List<SynthesisRecommendation> CommonRecommendations,
    List<SynthesisRecommendation> DivergentRecommendations,
    string SqlSuggestion,
    string Summary);

public static class MultiDbComparer
{
    /// <summary>
    /// Confronta più piani che si presume coprano la stessa query (idealmente
    /// stesso QueryHash) eseguita su DB diversi (es. ambienti, tenant, shard)
    /// e produce una sintesi delle raccomandazioni: ciò che migliora la query
    /// in modo trasversale, applicabile ovunque.
    /// </summary>
    public static MultiDbReport Compare(IEnumerable<DbVariant> variants)
    {
        var list = variants.ToList();
        if (list.Count < 2) throw new ArgumentException("Servono almeno 2 piani.", nameof(variants));

        var summaries = new List<VariantSummary>();
        var allFindings = new List<(string Label, Finding F)>();

        string? referenceHash = null;
        bool sameHash = true;
        foreach (var v in list)
        {
            var stmt = v.Plan.Statements.FirstOrDefault();
            if (stmt is null) continue;

            referenceHash ??= stmt.QueryHash;
            if (stmt.QueryHash != null && referenceHash != null && stmt.QueryHash != referenceHash)
                sameHash = false;

            var ops = stmt.Root?.Walk().ToList() ?? new();
            var findings = Heuristics.AnalyzeStatement(stmt, v.Plan.IsActual).ToList();

            summaries.Add(new VariantSummary(
                v.Label,
                stmt.QueryHash,
                stmt.QueryPlanHash,
                stmt.StatementSubTreeCost ?? stmt.Root?.EstimatedTotalSubtreeCost ?? 0,
                stmt.StatementEstRows ?? 0,
                stmt.Root?.ActualRows,
                ops.Where(o => o.PhysicalOp.Contains("Join", StringComparison.OrdinalIgnoreCase)
                              || o.PhysicalOp == "Hash Match")
                   .Select(o => o.PhysicalOp).Distinct().FirstOrDefault() ?? "(none)",
                ops.Count(o => o.PhysicalOp.Contains("Scan", StringComparison.OrdinalIgnoreCase)),
                ops.Count(o => o.PhysicalOp.Contains("Seek", StringComparison.OrdinalIgnoreCase)),
                ops.Count(o => o.PhysicalOp.Contains("Lookup", StringComparison.OrdinalIgnoreCase)),
                ops.SelectMany(o => o.Warnings)
                   .Count(w => w.Kind.Contains("Spill", StringComparison.OrdinalIgnoreCase)),
                ops.Any(o => o.Parallel),
                findings));

            foreach (var f in findings) allFindings.Add((v.Label, f));
        }

        // Group findings by Code: if a code appears across all (or majority of) variants,
        // it becomes a "common recommendation" - addressing it benefits everyone.
        // If it only appears in some, it goes to "divergent" and may only need targeted fix.
        var groups = allFindings.GroupBy(t => t.F.Code).ToList();
        var common = new List<SynthesisRecommendation>();
        var divergent = new List<SynthesisRecommendation>();
        int total = list.Count;

        foreach (var g in groups)
        {
            var distinctLabels = g.Select(x => x.Label).Distinct().Count();
            var sample = g.Select(x => x.F).First();
            var rec = new SynthesisRecommendation(
                g.Key,
                $"{sample.Title} — {sample.Recommendation}",
                g.Max(x => x.F.Severity),
                distinctLabels);
            if (distinctLabels * 2 > total)
                common.Add(rec);
            else
                divergent.Add(rec);
        }

        var sql = BuildPortableSqlSuggestion(common, summaries);

        var sb = new StringBuilder();
        sb.AppendLine($"Sono stati analizzati {total} piani.");
        sb.AppendLine(sameHash
            ? $"QueryHash identico ({referenceHash}) in tutte le varianti: i piani analizzati eseguono la stessa query."
            : "Attenzione: i QueryHash differiscono fra le varianti — il confronto è ancora utile ma le query non sono identiche.");
        sb.AppendLine();
        sb.AppendLine($"Costi stimati: min={summaries.Min(s => s.SubtreeCost):0.###} max={summaries.Max(s => s.SubtreeCost):0.###} mediana={Median(summaries.Select(s => s.SubtreeCost)):0.###}");
        sb.AppendLine($"Raccomandazioni comuni (presenti nella maggioranza dei DB): {common.Count}");
        sb.AppendLine($"Raccomandazioni divergenti (solo su alcuni DB): {divergent.Count}");

        return new MultiDbReport(
            summaries,
            sameHash ? "stesso" : "differente",
            common.OrderByDescending(c => c.Severity).ThenByDescending(c => c.OccurrencesAcrossDbs).ToList(),
            divergent.OrderByDescending(c => c.Severity).ToList(),
            sql,
            sb.ToString());
    }

    private static string BuildPortableSqlSuggestion(List<SynthesisRecommendation> common, List<VariantSummary> variants)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- Sintesi: modifiche applicabili a TUTTI i DB analizzati");
        sb.AppendLine("-- (azioni che risolvono i problemi comuni a più varianti)");
        sb.AppendLine();

        if (common.Any(c => c.Code == "INDEX.MISSING"))
            sb.AppendLine("-- 1) Indici mancanti suggeriti dall'ottimizzatore in più DB.");
        if (common.Any(c => c.Code == "PRED.RESIDUAL"))
            sb.AppendLine("-- 2) Riscrivere il WHERE in forma sargable (rimuovere funzioni sulla colonna, ISNULL, conversioni implicite).");
        if (common.Any(c => c.Code == "CONV.IMPLICIT"))
            sb.AppendLine("-- 3) Allineare i tipi: parametri/colonne devono avere lo stesso tipo (NVARCHAR vs VARCHAR).");
        if (common.Any(c => c.Code == "LOOKUP.HEAVY"))
            sb.AppendLine("-- 4) Aggiungere INCLUDE all'indice per evitare Key/RID Lookup.");
        if (common.Any(c => c.Code == "JOIN.NL_LARGE_OUTER"))
            sb.AppendLine("-- 5) Ricorrere a HASH/MERGE join su grandi volumi: rivedere indici o usare hint.");
        if (common.Any(c => c.Code == "UDF.SCALAR"))
            sb.AppendLine("-- 6) Scalar UDF non inlinable: passare a iTVF.");
        if (common.Any(c => c.Code.StartsWith("WARN.")))
            sb.AppendLine("-- 7) Spill rilevati: aggiornare statistiche e ripensare la cardinalità (memory grant).");

        if (sb.Length < 200)
            sb.AppendLine("-- Nessuna anti-pattern comune trovata: probabilmente le differenze sono guidate da volumi/dati.");

        sb.AppendLine();
        sb.AppendLine("-- Costi per variante (riferimento):");
        foreach (var s in variants)
            sb.AppendLine($"--   {s.Label}: cost={s.SubtreeCost:0.###} scans={s.NumScans} seeks={s.NumSeeks} lookups={s.NumLookups} spills={s.NumSpills} parallel={s.Parallel}");
        return sb.ToString();
    }

    private static double Median(IEnumerable<double> values)
    {
        var arr = values.OrderBy(x => x).ToArray();
        if (arr.Length == 0) return 0;
        return arr.Length % 2 == 1 ? arr[arr.Length / 2] : (arr[arr.Length / 2 - 1] + arr[arr.Length / 2]) / 2.0;
    }

    public static string Render(MultiDbReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Confronto multi-DB");
        sb.AppendLine();
        sb.AppendLine(r.Summary);
        sb.AppendLine();
        sb.AppendLine("## Per variante");
        sb.AppendLine($"  {"Label",-20}{"Cost",10}{"Join",-18}{"Scan",6}{"Seek",6}{"Look",6}{"Spill",6}  ParCfg");
        foreach (var v in r.Variants)
            sb.AppendLine($"  {v.Label,-20}{v.SubtreeCost,10:0.###}{v.DominantJoin,-18}{v.NumScans,6}{v.NumSeeks,6}{v.NumLookups,6}{v.NumSpills,6}  {(v.Parallel ? "yes" : "no")}");
        sb.AppendLine();

        sb.AppendLine("## Raccomandazioni COMUNI (applicabili ovunque con beneficio)");
        if (r.CommonRecommendations.Count == 0) sb.AppendLine("  Nessuna.");
        foreach (var c in r.CommonRecommendations)
            sb.AppendLine($"  [{c.Severity}] {c.Code} (presente in {c.OccurrencesAcrossDbs} DB): {c.Description}");
        sb.AppendLine();

        sb.AppendLine("## Raccomandazioni DIVERGENTI (solo su alcuni DB)");
        if (r.DivergentRecommendations.Count == 0) sb.AppendLine("  Nessuna.");
        foreach (var c in r.DivergentRecommendations)
            sb.AppendLine($"  [{c.Severity}] {c.Code} (presente in {c.OccurrencesAcrossDbs} DB): {c.Description}");
        sb.AppendLine();

        sb.AppendLine("## Proposta di intervento (sintesi)");
        sb.AppendLine(r.SqlSuggestion);
        return sb.ToString();
    }
}

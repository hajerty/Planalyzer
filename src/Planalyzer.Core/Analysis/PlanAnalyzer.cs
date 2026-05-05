using System.Text;
using Planalyzer.Core.Knowledge;
using Planalyzer.Core.Model;

namespace Planalyzer.Core.Analysis;

public sealed record AnalysisReport(
    ParsedPlan Plan,
    List<StatementReport> Statements);

public sealed record StatementReport(
    PlanStatement Statement,
    List<Finding> Findings,
    List<OperatorBrief> HotOperators);

public sealed record OperatorBrief(
    int NodeId,
    string PhysicalOp,
    string LogicalOp,
    double EstimateRows,
    double? ActualRows,
    double Cost,
    double CostPercent,
    string ObjectsTouched);

public enum AudienceLevel { Beginner, Expert }

public static class PlanAnalyzer
{
    public static AnalysisReport Analyze(ParsedPlan plan)
    {
        var statementReports = new List<StatementReport>();
        foreach (var stmt in plan.Statements)
        {
            var findings = Heuristics.AnalyzeStatement(stmt, plan.IsActual).ToList();
            var hot = HotOperators(stmt);
            statementReports.Add(new StatementReport(stmt, findings, hot));
        }
        return new AnalysisReport(plan, statementReports);
    }

    private static List<OperatorBrief> HotOperators(PlanStatement stmt)
    {
        if (stmt.Root is null) return new();
        var totalCost = stmt.StatementSubTreeCost ?? stmt.Root.EstimatedTotalSubtreeCost;
        if (totalCost <= 0) totalCost = stmt.Root.EstimatedTotalSubtreeCost;
        var ops = stmt.Root.Walk()
            .Select(o => new OperatorBrief(
                o.NodeId,
                o.PhysicalOp,
                o.LogicalOp,
                o.EstimateRows,
                o.ActualRows,
                o.EstimatedTotalSubtreeCost,
                totalCost > 0 ? o.EstimatedTotalSubtreeCost / totalCost * 100.0 : 0,
                string.Join(", ", o.Objects.Select(x => x.ToString()))))
            .OrderByDescending(o => o.Cost)
            .Take(10)
            .ToList();
        return ops;
    }
}

public static class TextRenderer
{
    public static string Render(AnalysisReport rep, AudienceLevel level)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Planalyzer report ({(rep.Plan.IsActual ? "actual" : "estimated")} plan)");
        if (!string.IsNullOrEmpty(rep.Plan.SourcePath))
            sb.AppendLine($"Source: {rep.Plan.SourcePath}");
        sb.AppendLine($"SQL Server build: {rep.Plan.Build} (showplan v{rep.Plan.Version})");
        sb.AppendLine();

        for (int i = 0; i < rep.Statements.Count; i++)
        {
            var s = rep.Statements[i];
            sb.AppendLine($"## Statement {i + 1}: {Truncate(s.Statement.StatementText, 200)}");
            sb.AppendLine($"  QueryHash: {s.Statement.QueryHash}  PlanHash: {s.Statement.QueryPlanHash}");
            sb.AppendLine($"  Optimization: {s.Statement.StatementOptmLevel}  CE: {s.Statement.CardinalityEstimationModelVersion}  RetrievedFromCache: {s.Statement.RetrievedFromCache}");
            sb.AppendLine($"  EstSubtreeCost: {s.Statement.StatementSubTreeCost:0.###}  EstRows: {s.Statement.StatementEstRows:0.##}");
            sb.AppendLine();

            // Synthetic explanation (beginner-friendly).
            sb.AppendLine("### Spiegazione sintetica");
            sb.AppendLine(SynthesizeStatement(s));
            sb.AppendLine();

            // Top findings ordered by severity.
            sb.AppendLine("### Punti di attenzione");
            if (s.Findings.Count == 0)
                sb.AppendLine("  Nessun problema rilevato dalle euristiche disponibili.");
            else
            {
                foreach (var f in s.Findings.OrderByDescending(f => f.Severity))
                {
                    sb.AppendLine($"  [{f.Severity}] {f.Code}: {f.Title}");
                    sb.AppendLine($"     {f.Explanation}");
                    sb.AppendLine($"     -> {f.Recommendation}");
                    if (f.NodeId is not null)
                        sb.AppendLine($"     Operator: {f.Operator} (NodeId={f.NodeId})");
                    if (level == AudienceLevel.Expert && f.Evidence is not null)
                    {
                        foreach (var (k, v) in f.Evidence)
                            sb.AppendLine($"        {k}: {Truncate(v, 400)}");
                    }
                    if (!string.IsNullOrEmpty(f.Reference))
                        sb.AppendLine($"     Riferimento: {f.Reference}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("### Top operatori per costo");
            foreach (var o in s.HotOperators)
            {
                var actual = o.ActualRows is null ? "" : $" actual={o.ActualRows:0.##}";
                sb.AppendLine($"  Node#{o.NodeId,-4} {o.PhysicalOp,-26} cost={o.Cost,8:0.###}  ({o.CostPercent,5:0.0}%)  est={o.EstimateRows:0.##}{actual}  {o.ObjectsTouched}");
            }
            sb.AppendLine();

            if (level == AudienceLevel.Expert)
            {
                sb.AppendLine("### Dettaglio completo proprietà operatori");
                sb.AppendLine("(Tutte le proprietà, senza troncamento alla SSMS.)");
                if (s.Statement.Root is not null)
                    DumpOperator(sb, s.Statement.Root, depth: 0);
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private static void DumpOperator(StringBuilder sb, PlanOperator op, int depth)
    {
        var pad = new string(' ', depth * 2);
        sb.AppendLine($"{pad}- Node#{op.NodeId} {op.PhysicalOp} ({op.LogicalOp})  est={op.EstimateRows:0.##} actual={op.ActualRows} cost={op.EstimatedTotalSubtreeCost:0.###}");
        foreach (var obj in op.Objects)
            sb.AppendLine($"{pad}    object: {obj}");
        foreach (var (k, v) in op.Properties.OrderBy(kv => kv.Key))
        {
            // Skip noise that's already shown above.
            if (k is "PhysicalOp" or "LogicalOp" or "NodeId" or "EstimateRows"
                  or "EstimatedTotalSubtreeCost" or "EstimateCPU" or "EstimateIO") continue;
            sb.AppendLine($"{pad}    {k}: {v}");
        }
        if (op.Warnings.Count > 0)
        {
            foreach (var w in op.Warnings)
                sb.AppendLine($"{pad}    !! warning: {w.Kind} {string.Join(",", w.Properties.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
        if (op.RuntimeInfo.Count > 0)
        {
            sb.AppendLine($"{pad}    runtime ({op.RuntimeInfo.Count} thread/i):");
            foreach (var r in op.RuntimeInfo)
                sb.AppendLine($"{pad}      thread#{r.Thread} rows={r.ActualRows:0} reads={r.ActualLogicalReads} cpu={r.ActualCPUms}ms elapsed={r.ActualElapsedms}ms");
        }
        foreach (var c in op.Children) DumpOperator(sb, c, depth + 1);
    }

    private static string SynthesizeStatement(StatementReport s)
    {
        if (s.Statement.Root is null) return "(nessun root operator)";
        var ops = s.Statement.Root.Walk().ToList();
        var totalCost = s.Statement.StatementSubTreeCost ?? s.Statement.Root.EstimatedTotalSubtreeCost;

        var joinKinds = ops.Where(o => o.LogicalOp.EndsWith("Join", StringComparison.OrdinalIgnoreCase)
                                       || o.PhysicalOp.Contains("Join", StringComparison.OrdinalIgnoreCase)
                                       || o.PhysicalOp.Equals("Hash Match", StringComparison.OrdinalIgnoreCase))
                           .Select(o => o.PhysicalOp).Distinct().ToList();

        var scans = ops.Count(o => o.PhysicalOp.Contains("Scan", StringComparison.OrdinalIgnoreCase));
        var seeks = ops.Count(o => o.PhysicalOp.Contains("Seek", StringComparison.OrdinalIgnoreCase));
        var lookups = ops.Count(o => o.PhysicalOp.Contains("Lookup", StringComparison.OrdinalIgnoreCase));
        var sorts = ops.Count(o => o.PhysicalOp.Equals("Sort", StringComparison.OrdinalIgnoreCase));
        var spools = ops.Count(o => o.PhysicalOp.Contains("Spool", StringComparison.OrdinalIgnoreCase));
        var parallel = ops.Any(o => o.Parallel);

        var sb = new StringBuilder();
        sb.Append($"Il piano ha {ops.Count} operatori (cost totale stimato {totalCost:0.###})");
        if (parallel) sb.Append(", in esecuzione parallela");
        sb.AppendLine(".");

        if (joinKinds.Count > 0)
            sb.AppendLine($"Tipi di join usati: {string.Join(", ", joinKinds)}.");
        sb.AppendLine($"Letture: {seeks} seek, {scans} scan, {lookups} lookup, {sorts} sort, {spools} spool.");

        var heaviest = s.HotOperators.FirstOrDefault();
        if (heaviest is not null)
            sb.AppendLine($"Operatore più costoso: {heaviest.PhysicalOp} ({heaviest.CostPercent:0.0}% del costo totale) su {heaviest.ObjectsTouched}.");

        var critical = s.Findings.Where(f => f.Severity >= Severity.High).ToList();
        if (critical.Count > 0)
            sb.AppendLine($"Trovati {critical.Count} problemi rilevanti (severity >= High): {string.Join("; ", critical.Take(3).Select(f => f.Code))}.");
        else
            sb.AppendLine("Nessun problema rilevante (>=High) rilevato dalle euristiche.");

        return sb.ToString();
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}

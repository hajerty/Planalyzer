using System.Text;
using Planalyzer.Core.Knowledge;
using Planalyzer.Core.Model;

namespace Planalyzer.Comparison;

public sealed record EstActDiff(
    int? NodeIdEstimated,
    int? NodeIdActual,
    string PhysicalOp,
    string LogicalOp,
    double EstRows,
    double ActRows,
    double SkewRatio,
    string Note,
    string? ObjectsTouched);

public sealed record EstActReport(
    string EstimatedSource,
    string ActualSource,
    bool ShapesMatch,
    string ShapeDifference,
    List<EstActDiff> Operators,
    List<Finding> ExtraFindings);

public static class EstimatedVsActual
{
    public static EstActReport Compare(ParsedPlan estimated, ParsedPlan actual)
    {
        if (estimated.IsActual) throw new ArgumentException("Il primo plan dovrebbe essere quello stimato.", nameof(estimated));
        if (!actual.IsActual) throw new ArgumentException("Il secondo plan dovrebbe essere quello effettivo.", nameof(actual));

        var estStmt = estimated.Statements.FirstOrDefault();
        var actStmt = actual.Statements.FirstOrDefault();

        if (estStmt?.Root is null || actStmt?.Root is null)
        {
            return new EstActReport(estimated.SourcePath ?? "(estimated)",
                                    actual.SourcePath ?? "(actual)",
                                    false,
                                    "Uno dei due piani non ha root operator.",
                                    new(), new());
        }

        // Compare structural shape based on the (NodeId, PhysicalOp) signature.
        var estOps = estStmt.Root.Walk().ToDictionary(o => o.NodeId, o => o);
        var actOps = actStmt.Root.Walk().ToDictionary(o => o.NodeId, o => o);

        var allIds = estOps.Keys.Union(actOps.Keys).OrderBy(i => i).ToList();
        var diffs = new List<EstActDiff>();
        var shapeIssues = new List<string>();

        foreach (var id in allIds)
        {
            estOps.TryGetValue(id, out var e);
            actOps.TryGetValue(id, out var a);

            if (e is null)
            {
                shapeIssues.Add($"Operatore Node#{id} ({a!.PhysicalOp}) presente solo nell'actual.");
                continue;
            }
            if (a is null)
            {
                shapeIssues.Add($"Operatore Node#{id} ({e.PhysicalOp}) presente solo nell'estimated.");
                continue;
            }
            if (!e.PhysicalOp.Equals(a.PhysicalOp, StringComparison.OrdinalIgnoreCase))
                shapeIssues.Add($"Node#{id}: estimated={e.PhysicalOp} vs actual={a.PhysicalOp}.");

            double act = a.ActualRows ?? 0;
            double exec = a.ActualExecutions ?? 0;
            double actPerExec = exec > 0 ? act / exec : act;
            double est = e.EstimateRows;
            double skew = SkewRatio(est, actPerExec);

            string note = skew >= 100 ? "Skew >100x"
                        : skew >= 10 ? "Skew >10x"
                        : skew >= 2  ? "Skew moderato"
                        : "ok";

            diffs.Add(new EstActDiff(
                id, id,
                a.PhysicalOp,
                a.LogicalOp,
                est,
                actPerExec,
                skew,
                note,
                string.Join(", ", a.Objects.Select(x => x.ToString()))));
        }

        // Merge plan-level findings already raised on the actual plan into ExtraFindings.
        var extras = Heuristics.AnalyzeStatement(actStmt, isActual: true)
            .Where(f => f.Code is "WARN.SpillToTempDb" or "WARN.HashSpillDetails" or "WARN.SortSpillDetails"
                                or "CE.SKEW" or "PARALLEL.SKEW")
            .ToList();

        var shapesMatch = shapeIssues.Count == 0;
        return new EstActReport(
            estimated.SourcePath ?? "(estimated)",
            actual.SourcePath ?? "(actual)",
            shapesMatch,
            shapesMatch ? "stessa forma" : string.Join("\n", shapeIssues),
            diffs,
            extras);
    }

    public static string Render(EstActReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Confronto Estimated vs Actual");
        sb.AppendLine($"  Estimated: {r.EstimatedSource}");
        sb.AppendLine($"  Actual:    {r.ActualSource}");
        sb.AppendLine($"  Shape:     {(r.ShapesMatch ? "uguale" : "DIFFERENTE")}");
        if (!r.ShapesMatch) sb.AppendLine("  " + r.ShapeDifference.Replace("\n", "\n  "));
        sb.AppendLine();

        sb.AppendLine("## Differenze cardinalità (per operatore)");
        sb.AppendLine($"  {"Node",-6}{"Operator",-26}{"EstRows",12}{"ActRows/exec",16}{"Skew",10}  Note  Objects");
        foreach (var d in r.Operators.OrderByDescending(d => d.SkewRatio))
        {
            sb.AppendLine($"  {("#" + d.NodeIdEstimated),-6}{d.PhysicalOp,-26}{d.EstRows,12:0.##}{d.ActRows,16:0.##}{d.SkewRatio,10:0.##}  {d.Note}  {d.ObjectsTouched}");
        }
        sb.AppendLine();

        sb.AppendLine("## Punti di attenzione aggiuntivi (presenti nell'actual)");
        if (r.ExtraFindings.Count == 0) sb.AppendLine("  Nessuno.");
        foreach (var f in r.ExtraFindings.OrderByDescending(f => f.Severity))
        {
            sb.AppendLine($"  [{f.Severity}] {f.Code} (Node#{f.NodeId} {f.Operator}): {f.Title}");
            sb.AppendLine($"     {f.Explanation}");
            sb.AppendLine($"     -> {f.Recommendation}");
        }
        return sb.ToString();
    }

    private static double SkewRatio(double est, double actual)
    {
        var e = Math.Max(est, 1);
        var a = Math.Max(actual, 1);
        return Math.Max(e / a, a / e);
    }
}

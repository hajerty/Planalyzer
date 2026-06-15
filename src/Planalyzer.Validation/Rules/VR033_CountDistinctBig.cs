using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.033 — COUNT(DISTINCT col) (HK).
/// Hash aggregate can spill to tempdb on large sets; alternatives include
/// approximate counting (APPROX_COUNT_DISTINCT) or pre-aggregation.
/// Without row estimates we can only warn at parse time.
/// Heuristic: any COUNT(DISTINCT ...) triggers a Medium finding.
/// </summary>
public sealed class VR033_CountDistinctBig : RuleBase
{
    public override string RuleId => "VR.033";
    public override string Title => "COUNT(DISTINCT) su grande set";
    public override Severity DefaultSeverity => Severity.Medium;
    public override string Source => "HK";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR033_CountDistinctBig _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR033_CountDistinctBig r) => _rule = r;

        public override void Visit(FunctionCall node)
        {
            var name = node.FunctionName?.Value;
            if (!string.Equals(name, "COUNT", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "COUNT_BIG", StringComparison.OrdinalIgnoreCase)) return;
            if (node.UniqueRowFilter != UniqueRowFilter.Distinct) return;
            Findings.Add(_rule.Finding(
                "COUNT(DISTINCT): hash aggregate, possibile spill su grandi set.",
                node,
                "Valuta APPROX_COUNT_DISTINCT() o pre-aggregazione."));
        }
    }
}

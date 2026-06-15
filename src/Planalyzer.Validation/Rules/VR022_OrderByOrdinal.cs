using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.022 — ORDER BY with positional ordinals: ORDER BY 1, 2 (PD).
/// Fragile to select-list changes.
/// </summary>
public sealed class VR022_OrderByOrdinal : RuleBase
{
    public override string RuleId => "VR.022";
    public override string Title => "ORDER BY con ordinali";
    public override Severity DefaultSeverity => Severity.Low;
    public override string Source => "PD";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR022_OrderByOrdinal _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR022_OrderByOrdinal r) => _rule = r;

        public override void Visit(OrderByClause node)
        {
            foreach (var el in node.OrderByElements)
            {
                if (el.Expression is IntegerLiteral)
                {
                    Findings.Add(_rule.Finding(
                        "ORDER BY usa un ordinale: fragile al cambiare della SELECT-list.",
                        el,
                        "Specifica nomi di colonna o espressioni esplicite."));
                }
            }
        }
    }
}

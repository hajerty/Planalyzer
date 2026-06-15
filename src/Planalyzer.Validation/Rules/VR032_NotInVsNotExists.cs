using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.032 — NOT IN with a subquery (HK).
/// NULL in the subquery result makes the whole predicate UNKNOWN, returning
/// empty results. NOT EXISTS is almost always the right replacement.
/// </summary>
public sealed class VR032_NotInVsNotExists : RuleBase
{
    public override string RuleId => "VR.032";
    public override string Title => "NOT IN con subquery";
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
        private readonly VR032_NotInVsNotExists _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR032_NotInVsNotExists r) => _rule = r;

        public override void Visit(InPredicate node)
        {
            if (!node.NotDefined) return;
            if (node.Subquery is null) return; // NOT IN (1,2,3) is OK if list is known non-null
            Findings.Add(_rule.Finding(
                "NOT IN con subquery: semantica fragile su NULL.",
                node,
                "Usa NOT EXISTS (...) o LEFT JOIN ... IS NULL."));
        }
    }
}

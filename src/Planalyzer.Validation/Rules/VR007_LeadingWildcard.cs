using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.007 — LIKE with a leading wildcard '%' or '_' (HK).
/// Such patterns cannot use a regular B-Tree index seek.
/// </summary>
public sealed class VR007_LeadingWildcard : RuleBase
{
    public override string RuleId => "VR.007";
    public override string Title => "LIKE con wildcard iniziale";
    public override Severity DefaultSeverity => Severity.High;
    public override string Source => "HK";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR007_LeadingWildcard _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR007_LeadingWildcard r) => _rule = r;

        public override void Visit(LikePredicate node)
        {
            if (node.SecondExpression is StringLiteral sl && !string.IsNullOrEmpty(sl.Value))
            {
                var first = sl.Value[0];
                if (first == '%' || first == '_')
                {
                    Findings.Add(_rule.Finding(
                        $"LIKE '{Truncate(sl.Value)}' inizia con wildcard: niente index seek.",
                        node,
                        "Considera full-text search o riformula evitando '%' iniziale."));
                }
            }
        }

        private static string Truncate(string s) => s.Length <= 30 ? s : s.Substring(0, 30) + "…";
    }
}

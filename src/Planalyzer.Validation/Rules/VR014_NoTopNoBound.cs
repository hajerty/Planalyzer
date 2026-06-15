using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.014 — SELECT without TOP / OFFSET-FETCH / WHERE (BP).
/// Unbounded result set: dangerous on growing tables.
/// We only flag top-level SELECTs (no INSERT...SELECT, no subqueries).
/// </summary>
public sealed class VR014_NoTopNoBound : RuleBase
{
    public override string RuleId => "VR.014";
    public override string Title => "SELECT senza TOP né WHERE";
    public override Severity DefaultSeverity => Severity.Medium;
    public override string Source => "BP";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR014_NoTopNoBound _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR014_NoTopNoBound r) => _rule = r;

        public override void Visit(SelectStatement node)
        {
            if (node.QueryExpression is not QuerySpecification qs) return;
            if (qs.FromClause is null) return; // SELECT @x = 1, no-from queries
            bool hasTop = qs.TopRowFilter is not null;
            bool hasWhere = qs.WhereClause is not null;
            bool hasOffset = node.QueryExpression is QuerySpecification && qs.OffsetClause is not null;
            if (!hasTop && !hasWhere && !hasOffset)
            {
                Findings.Add(_rule.Finding(
                    "SELECT senza TOP, OFFSET/FETCH o WHERE: risultato potenzialmente illimitato.",
                    node,
                    "Aggiungi un limite o un predicato selettivo."));
            }
        }
    }
}

using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.030 — Implicit cross join: FROM A, B without a correlating predicate (BP).
/// Heuristic: comma-style FROM with N tables and WHERE clause that does NOT
/// reference at least N-1 join-shaped comparisons.
/// </summary>
public sealed class VR030_ImplicitCrossJoin : RuleBase
{
    public override string RuleId => "VR.030";
    public override string Title => "Cross join implicito";
    public override Severity DefaultSeverity => Severity.High;
    public override string Source => "BP";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR030_ImplicitCrossJoin _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR030_ImplicitCrossJoin r) => _rule = r;

        public override void Visit(QuerySpecification node)
        {
            if (node.FromClause is null) return;
            var tables = node.FromClause.TableReferences;
            if (tables == null || tables.Count < 2) return;
            // Comma-style detected? VR.012 already flags it; here we additionally
            // verify there isn't a correlating predicate.
            if (node.WhereClause is null)
            {
                Findings.Add(_rule.Finding(
                    "FROM con più tabelle e nessuna WHERE: cross join implicito garantito.",
                    node.FromClause,
                    "Aggiungi una JOIN ... ON con predicato di correlazione."));
                return;
            }
            int joinPredicates = CountJoinPredicates(node.WhereClause.SearchCondition);
            if (joinPredicates < tables.Count - 1)
            {
                Findings.Add(_rule.Finding(
                    $"FROM con {tables.Count} tabelle ma solo {joinPredicates} predicati di correlazione.",
                    node.FromClause,
                    "Aggiungi i predicati mancanti o passa a JOIN ... ON espliciti."));
            }
        }

        private static int CountJoinPredicates(BooleanExpression? e) => e switch
        {
            BooleanBinaryExpression bbe =>
                CountJoinPredicates(bbe.FirstExpression) + CountJoinPredicates(bbe.SecondExpression),
            BooleanParenthesisExpression bpe => CountJoinPredicates(bpe.Expression),
            BooleanComparisonExpression bce =>
                (bce.FirstExpression is ColumnReferenceExpression
                 && bce.SecondExpression is ColumnReferenceExpression) ? 1 : 0,
            _ => 0
        };
    }
}

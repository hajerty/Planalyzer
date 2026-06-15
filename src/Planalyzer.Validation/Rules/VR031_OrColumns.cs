using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.031 — OR between predicates on different columns (HK).
/// Frequently forces an index scan; UNION ALL of two sargable seeks is often
/// faster.
/// </summary>
public sealed class VR031_OrColumns : RuleBase
{
    public override string RuleId => "VR.031";
    public override string Title => "OR su colonne diverse";
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
        private readonly VR031_OrColumns _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR031_OrColumns r) => _rule = r;

        public override void Visit(WhereClause node) => Walk(node.SearchCondition);

        private void Walk(BooleanExpression? e)
        {
            if (e is BooleanBinaryExpression bbe && bbe.BinaryExpressionType == BooleanBinaryExpressionType.Or)
            {
                var left = FirstColumn(bbe.FirstExpression);
                var right = FirstColumn(bbe.SecondExpression);
                if (left is not null && right is not null && !SameColumn(left, right))
                {
                    Findings.Add(_rule.Finding(
                        $"OR tra colonne diverse ({left} vs {right}): può forzare scan.",
                        bbe,
                        "Valuta UNION ALL di due query sargable."));
                }
                Walk(bbe.FirstExpression);
                Walk(bbe.SecondExpression);
            }
            else if (e is BooleanBinaryExpression and2)
            {
                Walk(and2.FirstExpression); Walk(and2.SecondExpression);
            }
            else if (e is BooleanParenthesisExpression bpe) Walk(bpe.Expression);
        }

        private static string? FirstColumn(BooleanExpression? e) => e switch
        {
            BooleanComparisonExpression bce when bce.FirstExpression is ColumnReferenceExpression c =>
                c.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value,
            BooleanComparisonExpression bce2 when bce2.SecondExpression is ColumnReferenceExpression c2 =>
                c2.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value,
            BooleanParenthesisExpression bpe => FirstColumn(bpe.Expression),
            InPredicate ip when ip.Expression is ColumnReferenceExpression c3 =>
                c3.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value,
            LikePredicate lp when lp.FirstExpression is ColumnReferenceExpression c4 =>
                c4.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value,
            _ => null
        };

        private static bool SameColumn(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}

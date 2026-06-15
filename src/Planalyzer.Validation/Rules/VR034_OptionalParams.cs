using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.034 — "Catch-all" optional parameters pattern (HK).
/// Predicates like (@p IS NULL OR col = @p) generate plans that are bad for
/// every actual parameter value. Without OPTION(RECOMPILE) they cache one
/// shape forever. Heuristic: a BooleanBinaryExpression OR where one side is
/// `<var> IS NULL` and the other compares the same variable to a column.
/// </summary>
public sealed class VR034_OptionalParams : RuleBase
{
    public override string RuleId => "VR.034";
    public override string Title => "Parametri opzionali (catch-all)";
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
        private readonly VR034_OptionalParams _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR034_OptionalParams r) => _rule = r;

        public override void Visit(BooleanBinaryExpression node)
        {
            if (node.BinaryExpressionType != BooleanBinaryExpressionType.Or) return;
            var (var1, isNullSide) = TryIsNullVar(node.FirstExpression);
            var (var2, cmpSide) = TryComparisonVar(node.SecondExpression);
            if (isNullSide && cmpSide && var1 is not null && var2 is not null
                && string.Equals(var1, var2, StringComparison.OrdinalIgnoreCase))
            {
                Emit(node, var1);
                return;
            }
            // Swap order
            (var1, isNullSide) = TryIsNullVar(node.SecondExpression);
            (var2, cmpSide) = TryComparisonVar(node.FirstExpression);
            if (isNullSide && cmpSide && var1 is not null && var2 is not null
                && string.Equals(var1, var2, StringComparison.OrdinalIgnoreCase))
            {
                Emit(node, var1);
            }
        }

        private void Emit(TSqlFragment at, string varName) =>
            Findings.Add(_rule.Finding(
                $"Pattern catch-all su {varName}: (@p IS NULL OR col=@p) genera piani non ottimali.",
                at,
                "Aggiungi OPTION(RECOMPILE) o usa dynamic SQL parametrizzato."));

        private static (string? var, bool ok) TryIsNullVar(BooleanExpression? e)
        {
            if (e is BooleanIsNullExpression nx && !nx.IsNot && nx.Expression is VariableReference vr)
                return (vr.Name, true);
            if (e is BooleanParenthesisExpression bpe) return TryIsNullVar(bpe.Expression);
            return (null, false);
        }

        private static (string? var, bool ok) TryComparisonVar(BooleanExpression? e)
        {
            if (e is BooleanComparisonExpression bce)
            {
                if (bce.FirstExpression is VariableReference v1) return (v1.Name, true);
                if (bce.SecondExpression is VariableReference v2) return (v2.Name, true);
            }
            if (e is BooleanParenthesisExpression bpe) return TryComparisonVar(bpe.Expression);
            return (null, false);
        }
    }
}

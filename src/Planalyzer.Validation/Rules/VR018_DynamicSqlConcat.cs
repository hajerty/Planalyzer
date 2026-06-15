using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.018 — EXEC with a concatenated string argument (PD).
/// Dynamic SQL by concatenation = SQL injection + plan cache pollution.
/// </summary>
public sealed class VR018_DynamicSqlConcat : RuleBase
{
    public override string RuleId => "VR.018";
    public override string Title => "EXEC con concatenazione di stringhe";
    public override Severity DefaultSeverity => Severity.Critical;
    public override string Source => "PD";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR018_DynamicSqlConcat _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR018_DynamicSqlConcat r) => _rule = r;

        public override void Visit(ExecuteStatement node)
        {
            var spec = node.ExecuteSpecification;
            if (spec is null) return;

            if (spec.ExecutableEntity is ExecutableStringList esl)
            {
                foreach (var s in esl.Strings)
                {
                    // s is a ValueExpression; ContainsConcat handles scalar nodes.
                    if (s is ScalarExpression se && ContainsConcat(se))
                    {
                        Findings.Add(_rule.Finding(
                            "EXEC con concatenazione di stringhe: rischio SQL injection.",
                            node,
                            "Usa sp_executesql con parametri tipizzati."));
                        return;
                    }
                }
            }
            // EXEC sp_executesql @stmt = N'...' + @x — flag too.
            if (spec.ExecutableEntity is ExecutableProcedureReference epr
                && epr.ProcedureReference?.ProcedureReference?.Name?.BaseIdentifier?.Value is { } procName
                && string.Equals(procName, "sp_executesql", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var p in epr.Parameters)
                {
                    if (ContainsConcat(p.ParameterValue))
                    {
                        Findings.Add(_rule.Finding(
                            "sp_executesql con stringa concatenata: passa la dinamica come parametro.",
                            node,
                            "Costruisci lo statement con placeholder e passa i valori come parametri."));
                        return;
                    }
                }
            }
        }

        private static bool ContainsConcat(ScalarExpression? e) => e switch
        {
            BinaryExpression be when be.BinaryExpressionType == BinaryExpressionType.Add =>
                IsStringy(be.FirstExpression) || IsStringy(be.SecondExpression)
                || ContainsConcat(be.FirstExpression) || ContainsConcat(be.SecondExpression),
            ParenthesisExpression pe => ContainsConcat(pe.Expression),
            _ => false
        };

        private static bool IsStringy(ScalarExpression e) =>
            e is StringLiteral or VariableReference || e is BinaryExpression;
    }
}

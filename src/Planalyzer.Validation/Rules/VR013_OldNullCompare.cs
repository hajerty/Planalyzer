using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.013 — `= NULL` / `&lt;&gt; NULL` instead of IS [NOT] NULL (PD).
/// With ANSI_NULLS ON (default) these always evaluate to UNKNOWN — silent bug.
/// </summary>
public sealed class VR013_OldNullCompare : RuleBase
{
    public override string RuleId => "VR.013";
    public override string Title => "Confronto con = NULL";
    public override Severity DefaultSeverity => Severity.High;
    public override string Source => "PD";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR013_OldNullCompare _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR013_OldNullCompare r) => _rule = r;

        public override void Visit(BooleanComparisonExpression node)
        {
            if (node.FirstExpression is NullLiteral || node.SecondExpression is NullLiteral)
            {
                Findings.Add(_rule.Finding(
                    "Confronto con NULL tramite '=' o '<>': sotto ANSI_NULLS sempre UNKNOWN.",
                    node,
                    "Usa IS NULL / IS NOT NULL."));
            }
        }
    }
}

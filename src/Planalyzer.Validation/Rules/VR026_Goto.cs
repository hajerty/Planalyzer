using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.026 — GOTO statement (BP).
/// Almost always replaceable with structured control flow.
/// </summary>
public sealed class VR026_Goto : RuleBase
{
    public override string RuleId => "VR.026";
    public override string Title => "GOTO";
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
        private readonly VR026_Goto _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR026_Goto r) => _rule = r;

        public override void Visit(GoToStatement node)
        {
            Findings.Add(_rule.Finding(
                "GOTO: ostacola la leggibilità e la verifica.",
                node,
                "Sostituisci con TRY/CATCH, IF/ELSE o early-return."));
        }
    }
}

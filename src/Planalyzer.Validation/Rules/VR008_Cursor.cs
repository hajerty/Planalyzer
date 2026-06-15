using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.008 — Explicit cursor (PD, HK).
/// RBAR processing — almost always replaceable with set-based logic.
/// </summary>
public sealed class VR008_Cursor : RuleBase
{
    public override string RuleId => "VR.008";
    public override string Title => "Cursore esplicito";
    public override Severity DefaultSeverity => Severity.High;
    public override string Source => "PD,HK";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR008_Cursor _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR008_Cursor r) => _rule = r;

        public override void Visit(DeclareCursorStatement node)
        {
            Findings.Add(_rule.Finding(
                "Cursore esplicito: RBAR. Considera una riscrittura set-based.",
                node,
                "Sostituisci con JOIN / window function / MERGE."));
        }
    }
}

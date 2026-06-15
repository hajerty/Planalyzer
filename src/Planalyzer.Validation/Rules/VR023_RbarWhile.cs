using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.023 — WHILE loop that looks like RBAR (HK).
/// We flag any WHILE statement: in practice almost all WHILE in T-SQL
/// are RBAR-shaped. The exceptions (queue dispatch, batched DML) should
/// carry an inline justify.
/// </summary>
public sealed class VR023_RbarWhile : RuleBase
{
    public override string RuleId => "VR.023";
    public override string Title => "WHILE su singola riga (RBAR)";
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
        private readonly VR023_RbarWhile _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR023_RbarWhile r) => _rule = r;

        public override void Visit(WhileStatement node)
        {
            Findings.Add(_rule.Finding(
                "Ciclo WHILE: spesso RBAR. Verifica fattibilità di una riscrittura set-based.",
                node,
                "Considera CTE ricorsiva o operazione singola; altrimenti aggiungi justify."));
        }
    }
}

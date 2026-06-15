using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.015 — SELECT DISTINCT used without an inline justify (PD).
/// Often masks a wrong/missing JOIN predicate that produces duplicates.
/// </summary>
public sealed class VR015_SelectDistinct : RuleBase
{
    public override string RuleId => "VR.015";
    public override string Title => "SELECT DISTINCT non commentato";
    public override Severity DefaultSeverity => Severity.Low;
    public override string Source => "PD";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR015_SelectDistinct _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR015_SelectDistinct r) => _rule = r;

        public override void Visit(QuerySpecification node)
        {
            if (node.UniqueRowFilter == UniqueRowFilter.Distinct)
            {
                Findings.Add(_rule.Finding(
                    "SELECT DISTINCT: spesso nasconde un join che produce duplicati.",
                    node,
                    "Verifica le join condition; aggiungi `-- justify: VR.015 motivo` se voluto."));
            }
        }
    }
}

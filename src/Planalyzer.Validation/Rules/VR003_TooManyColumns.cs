using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.003 — Select-list with too many output columns (BP).
/// Threshold from <see cref="ValidationOptions.MaxOutputColumns"/>.
/// </summary>
public sealed class VR003_TooManyColumns : RuleBase
{
    public override string RuleId => "VR.003";
    public override string Title => "Troppe colonne in output";
    public override Severity DefaultSeverity => Severity.Medium;
    public override string Source => "BP";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this, ctx.Options.MaxOutputColumns);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR003_TooManyColumns _rule;
        private readonly int _max;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR003_TooManyColumns r, int max) { _rule = r; _max = max; }

        public override void Visit(QuerySpecification node)
        {
            int n = node.SelectElements?.Count ?? 0;
            if (n > _max)
            {
                Findings.Add(_rule.Finding(
                    $"La SELECT-list ha {n} elementi (soglia {_max}).",
                    node,
                    "Restringi le colonne richieste o spezza la query."));
            }
        }
    }
}

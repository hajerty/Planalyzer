using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.001 — SELECT * forbidden in production code.
/// Hugo Kornelis + Pinal Dave: hidden columns, plan invalidation on schema
/// change, network bloat. Allow only inside EXISTS(SELECT *) which is a
/// well-known harmless idiom.
/// </summary>
public sealed class VR001_SelectStar : RuleBase
{
    public override string RuleId => "VR.001";
    public override string Title => "SELECT * vietato";
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
        private readonly VR001_SelectStar _rule;
        private int _existsDepth;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR001_SelectStar r) => _rule = r;

        // Track depth into EXISTS subqueries so we suppress SELECT * inside
        // them (well-known harmless idiom).
        public override void ExplicitVisit(ExistsPredicate node)
        {
            _existsDepth++;
            base.ExplicitVisit(node);
            _existsDepth--;
        }

        public override void Visit(SelectStarExpression node)
        {
            if (_existsDepth > 0) return;
            Findings.Add(_rule.Finding(
                "SELECT * espone tutte le colonne; instabile al variare dello schema.",
                node,
                "Elenca esplicitamente le colonne necessarie."));
        }
    }
}

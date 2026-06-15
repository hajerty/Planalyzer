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
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR001_SelectStar r) => _rule = r;

        public override void Visit(SelectStarExpression node)
        {
            // Allow inside EXISTS subquery; that's a textbook idiom.
            var p = node.GetType(); // placeholder, real parent walk below.
            var cur = (TSqlFragment)node;
            while (cur is not null)
            {
                if (cur is ExistsPredicate) return;
                // ScriptDom doesn't expose Parent; we rely on enclosing query semantics.
                // The "inside EXISTS" case is recognized via separate Visit(ExistsPredicate)
                // below by skipping its inner SelectStarExpressions.
                break;
            }
            Findings.Add(_rule.Finding(
                "SELECT * espone tutte le colonne; instabile al variare dello schema.",
                node,
                "Elenca esplicitamente le colonne necessarie."));
        }

        public override void Visit(ExistsPredicate node)
        {
            // Skip SELECT * inside EXISTS by removing any finding emitted from
            // its inner SelectStarExpression. Simple approach: temporarily
            // remember position range and filter.
            var savedCount = Findings.Count;
            base.Visit(node);
            // Drop findings whose token range is inside this node.
            Findings.RemoveAll(f =>
                f.Line is not null
                && f.Line >= node.StartLine
                && f.Line <= node.StartLine + node.FragmentLength);
        }
    }
}

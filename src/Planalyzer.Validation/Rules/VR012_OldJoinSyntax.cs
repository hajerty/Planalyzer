using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.012 — Old ANSI-89 join syntax: FROM A, B WHERE A.x = B.y (BP).
/// Hard to spot OUTER vs INNER intent; cross-join risk on missed predicate.
/// </summary>
public sealed class VR012_OldJoinSyntax : RuleBase
{
    public override string RuleId => "VR.012";
    public override string Title => "Sintassi JOIN obsoleta (virgola)";
    public override Severity DefaultSeverity => Severity.High;
    public override string Source => "BP";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR012_OldJoinSyntax _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR012_OldJoinSyntax r) => _rule = r;

        public override void Visit(FromClause node)
        {
            // Multiple TableReferences as siblings = comma-style join.
            if (node.TableReferences is { Count: >= 2 })
            {
                Findings.Add(_rule.Finding(
                    "Più tabelle nella FROM separate da virgola: sintassi obsoleta.",
                    node,
                    "Riscrivi con INNER/LEFT/RIGHT JOIN ... ON ..."));
            }
        }
    }
}

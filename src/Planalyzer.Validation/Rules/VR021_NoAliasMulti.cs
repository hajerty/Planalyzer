using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.021 — Two or more tables in a query without aliases (PD).
/// Hurts refactor and makes columns ambiguous on schema evolution.
/// </summary>
public sealed class VR021_NoAliasMulti : RuleBase
{
    public override string RuleId => "VR.021";
    public override string Title => "2+ tabelle senza alias";
    public override Severity DefaultSeverity => Severity.Low;
    public override string Source => "PD";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this, ctx.Options.MaxTablesWithoutAlias);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR021_NoAliasMulti _rule;
        private readonly int _max;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR021_NoAliasMulti r, int max) { _rule = r; _max = max; }

        public override void Visit(QuerySpecification node)
        {
            if (node.FromClause is null) return;
            int tables = 0;
            int noAlias = 0;
            foreach (var tr in node.FromClause.TableReferences) Walk(tr, ref tables, ref noAlias);
            if (tables >= 2 && noAlias > _max)
            {
                Findings.Add(_rule.Finding(
                    $"{noAlias} tabelle senza alias su {tables} totali (soglia {_max}).",
                    node.FromClause,
                    "Aggiungi alias brevi e parlanti."));
            }
        }

        private static void Walk(TableReference tr, ref int total, ref int noAlias)
        {
            switch (tr)
            {
                case QualifiedJoin qj:
                    Walk(qj.FirstTableReference, ref total, ref noAlias);
                    Walk(qj.SecondTableReference, ref total, ref noAlias);
                    break;
                case UnqualifiedJoin uj:
                    Walk(uj.FirstTableReference, ref total, ref noAlias);
                    Walk(uj.SecondTableReference, ref total, ref noAlias);
                    break;
                case JoinParenthesisTableReference jp:
                    Walk(jp.Join, ref total, ref noAlias);
                    break;
                case NamedTableReference ntr:
                    total++;
                    if (ntr.Alias is null) noAlias++;
                    break;
                case QueryDerivedTable qdt:
                    total++;
                    if (qdt.Alias is null) noAlias++;
                    break;
                default:
                    total++; break;
            }
        }
    }
}

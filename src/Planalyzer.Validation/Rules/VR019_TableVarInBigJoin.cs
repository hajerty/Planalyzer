using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.019 — Table variable used inside a query that joins many tables (HK).
/// Table variables have a fixed cardinality estimate (1 row pre-2019) which
/// poisons plan choice in joins. We flag when @TableVar appears in a
/// QuerySpecification with 3+ table references.
/// </summary>
public sealed class VR019_TableVarInBigJoin : RuleBase
{
    public override string RuleId => "VR.019";
    public override string Title => "Table variable in join multi-tabella";
    public override Severity DefaultSeverity => Severity.Medium;
    public override string Source => "HK";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR019_TableVarInBigJoin _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR019_TableVarInBigJoin r) => _rule = r;

        public override void Visit(QuerySpecification node)
        {
            if (node.FromClause is null) return;
            int total = 0;
            bool hasVar = false;
            VariableTableReference? offender = null;
            foreach (var tr in node.FromClause.TableReferences) Walk(tr, ref total, ref hasVar, ref offender);
            if (hasVar && total >= 3)
            {
                Findings.Add(_rule.Finding(
                    "Table variable in join con 3+ tabelle: stima 1 riga -> piani sbagliati.",
                    (TSqlFragment?)offender ?? node,
                    "Usa una temp table (#t) con statistiche reali."));
            }
        }

        private static void Walk(TableReference tr, ref int total, ref bool hasVar, ref VariableTableReference? offender)
        {
            switch (tr)
            {
                case QualifiedJoin qj:
                    Walk(qj.FirstTableReference, ref total, ref hasVar, ref offender);
                    Walk(qj.SecondTableReference, ref total, ref hasVar, ref offender);
                    break;
                case UnqualifiedJoin uj:
                    Walk(uj.FirstTableReference, ref total, ref hasVar, ref offender);
                    Walk(uj.SecondTableReference, ref total, ref hasVar, ref offender);
                    break;
                case JoinParenthesisTableReference jp:
                    Walk(jp.Join, ref total, ref hasVar, ref offender);
                    break;
                case VariableTableReference vtr:
                    total++; hasVar = true; offender ??= vtr; break;
                default:
                    total++; break;
            }
        }
    }
}

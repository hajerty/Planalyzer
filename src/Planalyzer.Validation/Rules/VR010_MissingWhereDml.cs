using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.010 — UPDATE/DELETE senza WHERE. Critical, non giustificabile inline.
/// </summary>
public sealed class VR010_MissingWhereDml : RuleBase
{
    public override string RuleId => "VR.010";
    public override string Title => "UPDATE/DELETE senza WHERE";
    public override Severity DefaultSeverity => Severity.Critical;
    public override string Source => "BP";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR010_MissingWhereDml _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR010_MissingWhereDml r) => _rule = r;

        public override void Visit(UpdateStatement node)
        {
            if (node.UpdateSpecification?.WhereClause is null)
                Findings.Add(_rule.Finding("UPDATE senza WHERE — toccherà l'intera tabella.", node,
                    "Aggiungi una WHERE clause oppure usa TRUNCATE/DELETE consapevolmente."));
        }

        public override void Visit(DeleteStatement node)
        {
            if (node.DeleteSpecification?.WhereClause is null)
                Findings.Add(_rule.Finding("DELETE senza WHERE — svuoterà l'intera tabella.", node,
                    "Aggiungi WHERE o usa TRUNCATE TABLE (più veloce, locking diverso)."));
        }
    }
}

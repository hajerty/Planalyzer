using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.017 — Stored procedure body that does not begin with SET NOCOUNT ON (PD).
/// Avoids extra "n rows affected" round-trips for every DML inside the proc.
/// </summary>
public sealed class VR017_NoSetNocount : RuleBase
{
    public override string RuleId => "VR.017";
    public override string Title => "SP senza SET NOCOUNT ON";
    public override Severity DefaultSeverity => Severity.Medium;
    public override string Source => "PD";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR017_NoSetNocount _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR017_NoSetNocount r) => _rule = r;

        public override void Visit(CreateProcedureStatement node) => CheckProc(node, node.StatementList);
        public override void Visit(AlterProcedureStatement node) => CheckProc(node, node.StatementList);

        private void CheckProc(TSqlStatement at, StatementList? body)
        {
            if (body is null || body.Statements.Count == 0) return;
            var first = body.Statements[0];
            // Walk the first few statements; allow leading USE/SET DATEFORMAT etc.
            int limit = Math.Min(3, body.Statements.Count);
            for (int i = 0; i < limit; i++)
            {
                if (IsSetNocountOn(body.Statements[i])) return;
            }
            Findings.Add(_rule.Finding(
                "La procedura non inizia con SET NOCOUNT ON.",
                at,
                "Aggiungi `SET NOCOUNT ON;` come prima istruzione."));
        }

        private static bool IsSetNocountOn(TSqlStatement s)
        {
            if (s is PredicateSetStatement ps && ps.Options.HasFlag(SetOptions.NoCount))
                return ps.IsOn;
            return false;
        }
    }
}

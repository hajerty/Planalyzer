using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.027 — sp_executesql invoked without parameters (PD).
/// If no parameters are passed, the prepared-statement benefit disappears:
/// every distinct literal text gets its own plan in cache.
/// </summary>
public sealed class VR027_SpExecuteSqlNoParam : RuleBase
{
    public override string RuleId => "VR.027";
    public override string Title => "sp_executesql senza parametri";
    public override Severity DefaultSeverity => Severity.High;
    public override string Source => "PD";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR027_SpExecuteSqlNoParam _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR027_SpExecuteSqlNoParam r) => _rule = r;

        public override void Visit(ExecuteStatement node)
        {
            var spec = node.ExecuteSpecification;
            if (spec?.ExecutableEntity is not ExecutableProcedureReference epr) return;
            var name = epr.ProcedureReference?.ProcedureReference?.Name?.BaseIdentifier?.Value;
            if (!string.Equals(name, "sp_executesql", StringComparison.OrdinalIgnoreCase)) return;

            // sp_executesql with just one argument (the SQL text) and no @params decl.
            int paramCount = epr.Parameters?.Count ?? 0;
            if (paramCount <= 1)
            {
                Findings.Add(_rule.Finding(
                    "sp_executesql chiamato senza parametri tipizzati: niente plan reuse.",
                    node,
                    "Passa @params N'@x int' e i valori come parametri."));
            }
        }
    }
}

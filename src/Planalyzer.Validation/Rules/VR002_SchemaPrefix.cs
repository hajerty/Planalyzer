using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.002 — Reference to a table without schema prefix.
/// PD: omitting "dbo." costs name resolution + extra cache plan lookups
/// (each user-default-schema variant compiles a fresh plan).
/// </summary>
public sealed class VR002_SchemaPrefix : RuleBase
{
    public override string RuleId => "VR.002";
    public override string Title => "Schema prefix mancante";
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
        private readonly VR002_SchemaPrefix _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR002_SchemaPrefix r) => _rule = r;

        public override void Visit(NamedTableReference node)
        {
            var name = node.SchemaObject;
            if (name is null) return;
            // SchemaIdentifier null => no schema prefix. Skip temp tables (#...) and table-vars (@...).
            var baseId = name.BaseIdentifier?.Value;
            if (string.IsNullOrEmpty(baseId)) return;
            if (baseId.StartsWith("#") || baseId.StartsWith("@")) return;
            if (name.SchemaIdentifier is null)
            {
                Findings.Add(_rule.Finding(
                    $"Tabella '{baseId}' senza prefisso schema (es. dbo.{baseId}).",
                    node,
                    "Anteponi sempre lo schema: 'dbo.' o lo schema applicativo."));
            }
        }
    }
}

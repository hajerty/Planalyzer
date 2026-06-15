using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.020 — Use of legacy DATETIME / SMALLDATETIME types (BP).
/// DATETIME2 has better precision, smaller storage on common precisions, and
/// no leap-second misalignment with .NET DateTime. Heuristic: scan DataTypeReference.
/// </summary>
public sealed class VR020_DeprecatedDateTime : RuleBase
{
    public override string RuleId => "VR.020";
    public override string Title => "Tipo DATETIME legacy";
    public override Severity DefaultSeverity => Severity.Low;
    public override string Source => "BP";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR020_DeprecatedDateTime _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR020_DeprecatedDateTime r) => _rule = r;

        public override void Visit(SqlDataTypeReference node)
        {
            var name = node.Name?.BaseIdentifier?.Value;
            if (name is null) return;
            if (string.Equals(name, "datetime", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "smalldatetime", StringComparison.OrdinalIgnoreCase))
            {
                Findings.Add(_rule.Finding(
                    $"Tipo '{name}' legacy: preferisci DATETIME2.",
                    node,
                    "Usa DATETIME2 (precisione configurabile, range esteso)."));
            }
        }
    }
}

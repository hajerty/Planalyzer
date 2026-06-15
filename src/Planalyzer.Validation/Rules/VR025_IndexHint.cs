using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.025 — Hard-coded INDEX hint without justify (HK).
/// `WITH(INDEX(...))` freezes an access path and breaks when the index is
/// renamed or dropped. Require an inline justify.
/// </summary>
public sealed class VR025_IndexHint : RuleBase
{
    public override string RuleId => "VR.025";
    public override string Title => "Index hint senza justify";
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
        private readonly VR025_IndexHint _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR025_IndexHint r) => _rule = r;

        public override void Visit(NamedTableReference node)
        {
            if (node.TableHints is null) return;
            foreach (var h in node.TableHints)
            {
                if (h is TableHint th && th.HintKind == TableHintKind.Index)
                {
                    Findings.Add(_rule.Finding(
                        "Index hint hard-coded: fragile al refactor degli indici.",
                        node,
                        "Rimuovi o documenta con `-- justify: VR.025 motivo`."));
                    return;
                }
            }
        }
    }
}

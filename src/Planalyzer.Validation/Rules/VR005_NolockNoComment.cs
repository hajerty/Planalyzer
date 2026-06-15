using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.005 — WITH(NOLOCK) used without an inline justify comment (PD).
/// NOLOCK means dirty / phantom / missing-row reads; if intentional, must be
/// documented above the statement via `-- justify: VR.005 ...`.
/// The engine attaches Justification automatically when present.
/// </summary>
public sealed class VR005_NolockNoComment : RuleBase
{
    public override string RuleId => "VR.005";
    public override string Title => "WITH(NOLOCK) usato (richiede justify)";
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
        private readonly VR005_NolockNoComment _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR005_NolockNoComment r) => _rule = r;

        public override void Visit(NamedTableReference node)
        {
            if (node.TableHints is null) return;
            foreach (var h in node.TableHints)
            {
                if (h is TableHint th && (th.HintKind == TableHintKind.NoLock
                                          || th.HintKind == TableHintKind.ReadUncommitted))
                {
                    Findings.Add(_rule.Finding(
                        "NOLOCK / READUNCOMMITTED: dirty read. Documenta con `-- justify: VR.005 motivo`.",
                        node,
                        "Preferisci READ COMMITTED SNAPSHOT a livello DB, o motiva esplicitamente."));
                    return;
                }
            }
        }
    }
}

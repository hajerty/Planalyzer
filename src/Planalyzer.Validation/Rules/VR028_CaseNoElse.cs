using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.028 — CASE expression without an ELSE branch (BP).
/// Implicit NULL on unmatched branches — silent bug source.
/// </summary>
public sealed class VR028_CaseNoElse : RuleBase
{
    public override string RuleId => "VR.028";
    public override string Title => "CASE senza ELSE";
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
        private readonly VR028_CaseNoElse _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR028_CaseNoElse r) => _rule = r;

        public override void Visit(SimpleCaseExpression node)
        {
            if (node.ElseExpression is null) Emit(node);
        }
        public override void Visit(SearchedCaseExpression node)
        {
            if (node.ElseExpression is null) Emit(node);
        }

        private void Emit(TSqlFragment at) =>
            Findings.Add(_rule.Finding(
                "CASE senza ELSE: i rami non coperti producono NULL silente.",
                at,
                "Aggiungi un ramo ELSE esplicito (anche ELSE NULL come scelta documentata)."));
    }
}

using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.011 — Likely implicit conversion (HK).
/// Heuristic: without catalog info we can't know the column type, so we flag:
///   - column compared to N-prefixed literal vs no N (varchar vs nvarchar mismatch)
///   - column compared to numeric literal where the literal is a string
///     (e.g. col = '123') or vice-versa (col = 123 when name suggests text)
///   - column with name suggesting one family vs literal of the other
/// All findings are approximate; the real check is in the actual plan.
/// </summary>
public sealed class VR011_ImplicitConvert : RuleBase
{
    public override string RuleId => "VR.011";
    public override string Title => "Probabile implicit conversion";
    public override Severity DefaultSeverity => Severity.High;
    public override string Source => "HK";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR011_ImplicitConvert _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR011_ImplicitConvert r) => _rule = r;

        public override void Visit(BooleanComparisonExpression node)
        {
            Check(node, node.FirstExpression, node.SecondExpression);
            Check(node, node.SecondExpression, node.FirstExpression);
        }

        private void Check(TSqlFragment at, ScalarExpression colSide, ScalarExpression valSide)
        {
            if (colSide is not ColumnReferenceExpression col) return;
            var colName = col.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value ?? "";

            // Heuristic flags ------------------------------------------------
            if (valSide is StringLiteral sl)
            {
                // nvarchar vs varchar: N'...' vs '...'.
                // We flag '...' compared to a column whose name hints unicode.
                if (!sl.IsNational && LooksUnicode(colName))
                {
                    Findings.Add(_rule.Finding(
                        $"Letterale non-Unicode confrontato con colonna '{colName}' che sembra nvarchar.",
                        at,
                        "Usa il prefisso N'...' oppure normalizza i tipi."));
                    return;
                }
                // Column name hints numeric/date but value is string.
                if (LooksNumeric(colName) || LooksDate(colName))
                {
                    Findings.Add(_rule.Finding(
                        $"Colonna '{colName}' sembra numerica/data ma confrontata con stringa letterale.",
                        at,
                        "Verifica i tipi: implicit convert possibile."));
                }
            }
            else if (valSide is IntegerLiteral or NumericLiteral)
            {
                if (LooksTextual(colName))
                {
                    Findings.Add(_rule.Finding(
                        $"Colonna '{colName}' sembra testuale ma confrontata con letterale numerico.",
                        at,
                        "Quota il valore o casta esplicitamente."));
                }
            }
        }

        private static bool LooksUnicode(string n) =>
            n.StartsWith("n", StringComparison.OrdinalIgnoreCase)
            || n.Contains("name", StringComparison.OrdinalIgnoreCase)
            || n.Contains("desc", StringComparison.OrdinalIgnoreCase)
            || n.Contains("text", StringComparison.OrdinalIgnoreCase);

        private static bool LooksNumeric(string n) =>
            n.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
            || n.Contains("amount", StringComparison.OrdinalIgnoreCase)
            || n.Contains("qty", StringComparison.OrdinalIgnoreCase)
            || n.Contains("count", StringComparison.OrdinalIgnoreCase)
            || n.Contains("number", StringComparison.OrdinalIgnoreCase);

        private static bool LooksDate(string n) =>
            n.Contains("date", StringComparison.OrdinalIgnoreCase)
            || n.Contains("time", StringComparison.OrdinalIgnoreCase);

        private static bool LooksTextual(string n) =>
            n.Contains("name", StringComparison.OrdinalIgnoreCase)
            || n.Contains("code", StringComparison.OrdinalIgnoreCase)
            || n.Contains("desc", StringComparison.OrdinalIgnoreCase)
            || n.Contains("text", StringComparison.OrdinalIgnoreCase)
            || n.Contains("email", StringComparison.OrdinalIgnoreCase);
    }
}

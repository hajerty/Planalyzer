using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.009 — Likely scalar UDF call in WHERE or SELECT (HK).
/// Heuristic: we flag user-style function calls (two-part name or unknown
/// single-part name) inside WHERE/SELECT. Pre-2019 these were not inlinable and
/// turned the query into row-by-row.
/// Heuristic note: we don't have a catalog, so we exclude a curated set of
/// built-in T-SQL functions; everything else is treated as suspicious.
/// </summary>
public sealed class VR009_ScalarUdfInWhere : RuleBase
{
    public override string RuleId => "VR.009";
    public override string Title => "Probabile scalar UDF in WHERE/SELECT";
    public override Severity DefaultSeverity => Severity.High;
    public override string Source => "HK";

    private static readonly HashSet<string> BuiltIns = new(StringComparer.OrdinalIgnoreCase)
    {
        // string
        "LEN","LOWER","UPPER","LTRIM","RTRIM","TRIM","SUBSTRING","REPLACE","CHARINDEX","PATINDEX",
        "LEFT","RIGHT","CONCAT","CONCAT_WS","STRING_AGG","STRING_SPLIT","FORMAT","REVERSE","STUFF",
        "REPLICATE","SPACE","QUOTENAME","STR","ASCII","UNICODE","CHAR","NCHAR","SOUNDEX","DIFFERENCE",
        // date
        "GETDATE","SYSDATETIME","SYSUTCDATETIME","GETUTCDATE","CURRENT_TIMESTAMP","DATEADD","DATEDIFF",
        "DATEDIFF_BIG","DATEPART","DATENAME","DAY","MONTH","YEAR","EOMONTH","DATEFROMPARTS",
        "DATETIMEFROMPARTS","DATETIME2FROMPARTS","TIMEFROMPARTS","SWITCHOFFSET","TODATETIMEOFFSET",
        // math
        "ABS","CEILING","FLOOR","ROUND","POWER","SQRT","EXP","LOG","LOG10","SIGN","RAND","PI",
        "SIN","COS","TAN","ASIN","ACOS","ATAN","ATN2","DEGREES","RADIANS",
        // conv / system / null / aggregate / window
        "CAST","CONVERT","TRY_CAST","TRY_CONVERT","PARSE","TRY_PARSE",
        "ISNULL","COALESCE","NULLIF","IIF","CHOOSE",
        "COUNT","COUNT_BIG","SUM","AVG","MIN","MAX","STDEV","STDEVP","VAR","VARP","CHECKSUM_AGG",
        "ROW_NUMBER","RANK","DENSE_RANK","NTILE","LEAD","LAG","FIRST_VALUE","LAST_VALUE",
        "PERCENT_RANK","CUME_DIST","PERCENTILE_CONT","PERCENTILE_DISC",
        "NEWID","NEWSEQUENTIALID","HASHBYTES","CHECKSUM","BINARY_CHECKSUM",
        "OBJECT_ID","OBJECT_NAME","DB_ID","DB_NAME","USER_NAME","SUSER_SNAME","SUSER_NAME",
        "ERROR_NUMBER","ERROR_MESSAGE","ERROR_LINE","ERROR_PROCEDURE","ERROR_SEVERITY","ERROR_STATE",
        "OPENJSON","JSON_VALUE","JSON_QUERY","JSON_MODIFY","ISJSON",
        "XQUERY","XML",
        "@@ROWCOUNT","@@IDENTITY","@@ERROR","@@TRANCOUNT","@@VERSION","@@SPID","SCOPE_IDENTITY",
        "IDENT_CURRENT","IDENTITY","NEXT","NEXT VALUE FOR",
        // misc
        "GROUPING","GROUPING_ID","TYPE_NAME","SQL_VARIANT_PROPERTY","XACT_STATE","SESSION_CONTEXT",
        "OPENROWSET","OPENQUERY","OPENXML","CURSOR_STATUS"
    };

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR009_ScalarUdfInWhere _rule;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR009_ScalarUdfInWhere r) => _rule = r;

        public override void Visit(FunctionCall node)
        {
            var name = node.FunctionName?.Value;
            if (string.IsNullOrEmpty(name)) return;

            // Two-part name like dbo.MyFunc -> very likely a UDF.
            bool twoPart = node.CallTarget is MultiPartIdentifierCallTarget;

            if (!twoPart && BuiltIns.Contains(name)) return;

            // Skip if the only thing this looks like is a known aggregate/window function w/ OVER.
            if (!twoPart && node.OverClause is not null) return;

            Findings.Add(_rule.Finding(
                $"Funzione '{name}' assomiglia a una scalar UDF: pre-2019 non inlinabile.",
                node,
                "Inline la logica nel predicato o usa una inline TVF (RETURNS TABLE)."));
        }
    }
}

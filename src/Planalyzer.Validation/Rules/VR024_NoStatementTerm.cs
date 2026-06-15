using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.024 — Statement missing trailing ';' (BP, Info).
/// THROW, MERGE, ;WITH require terminators; future T-SQL deprecates the
/// optional semicolon. Heuristic: inspect the last non-comment token of each
/// top-level statement; if it isn't ';' we emit Info.
/// </summary>
public sealed class VR024_NoStatementTerm : RuleBase
{
    public override string RuleId => "VR.024";
    public override string Title => "Terminatore ';' mancante";
    public override Severity DefaultSeverity => Severity.Info;
    public override string Source => "BP";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var findings = new List<ValidationFinding>();
        foreach (var s in ctx.Statements)
        {
            if (s.ScriptTokenStream is null) continue;
            // Walk backwards from LastTokenIndex skipping whitespace/comments.
            int i = s.LastTokenIndex;
            while (i >= 0 && i < s.ScriptTokenStream.Count)
            {
                var t = s.ScriptTokenStream[i];
                if (t.TokenType == TSqlTokenType.WhiteSpace
                    || t.TokenType == TSqlTokenType.MultilineComment
                    || t.TokenType == TSqlTokenType.SingleLineComment)
                {
                    i--; continue;
                }
                break;
            }
            if (i < 0 || i >= s.ScriptTokenStream.Count) continue;
            var last = s.ScriptTokenStream[i];
            if (last.TokenType != TSqlTokenType.Semicolon)
            {
                findings.Add(Finding(
                    "Statement non terminato da ';'.",
                    s,
                    "Aggiungi ';' a fine statement (richiesto per CTE, THROW, MERGE)."));
            }
        }
        return findings;
    }
}

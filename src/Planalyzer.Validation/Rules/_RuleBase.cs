using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// Convenience base: implements <see cref="IValidationRule"/> and exposes a
/// helper for emitting findings with the correct source / severity.
/// </summary>
public abstract class RuleBase : IValidationRule
{
    public abstract string RuleId { get; }
    public abstract string Title { get; }
    public abstract Severity DefaultSeverity { get; }
    public abstract string Source { get; }

    public abstract IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx);

    protected ValidationFinding Finding(
        string message,
        TSqlFragment? at,
        string? fixHint = null,
        Severity? sevOverride = null) =>
        new(
            RuleId,
            Title,
            sevOverride ?? DefaultSeverity,
            Source,
            message,
            at?.StartLine,
            at?.StartColumn,
            at is null ? null : SnippetOf(at),
            null,
            fixHint);

    private static string SnippetOf(TSqlFragment f)
    {
        if (f.ScriptTokenStream is null) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = f.FirstTokenIndex; i <= f.LastTokenIndex && i < f.ScriptTokenStream.Count; i++)
            sb.Append(f.ScriptTokenStream[i].Text);
        var s = sb.ToString().Trim();
        return s.Length > 240 ? s.Substring(0, 240) + "…" : s;
    }
}

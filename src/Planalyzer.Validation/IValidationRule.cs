using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation;

/// <summary>
/// Per-statement justify map: line number -> set of rule ids that have been
/// suppressed by an inline `-- justify: VR.NNN reason` comment.
/// </summary>
public sealed class JustifyMap
{
    private readonly Dictionary<int, Dictionary<string, string>> _byLine = new();

    public void Add(int line, string ruleId, string reason)
    {
        if (!_byLine.TryGetValue(line, out var inner))
        {
            inner = new(StringComparer.OrdinalIgnoreCase);
            _byLine[line] = inner;
        }
        inner[ruleId] = reason;
    }

    /// <summary>Look up a justify reason for a rule. Inspects the given line
    /// and a small window above (the comment is typically just before the
    /// offending statement).</summary>
    public string? FindReason(string ruleId, int line, int lookbackLines = 4)
    {
        for (int l = line; l >= Math.Max(1, line - lookbackLines); l--)
        {
            if (_byLine.TryGetValue(l, out var inner) && inner.TryGetValue(ruleId, out var why))
                return why;
        }
        return null;
    }
}

public sealed class ValidationContext
{
    public required ValidationOptions Options { get; init; }
    public required JustifyMap Justify { get; init; }
    public required string OriginalSql { get; init; }
    /// <summary>Set by the engine before running rules: every parse-detected
    /// statement boundary, used by some rules.</summary>
    public IReadOnlyList<TSqlStatement> Statements { get; init; } = Array.Empty<TSqlStatement>();
}

public interface IValidationRule
{
    /// <summary>Stable code like "VR.001".</summary>
    string RuleId { get; }
    string Title { get; }
    Severity DefaultSeverity { get; }
    /// <summary>"HK" | "PD" | "BP".</summary>
    string Source { get; }

    IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx);
}

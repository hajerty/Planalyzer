namespace Planalyzer.Validation;

public enum Severity { Info, Low, Medium, High, Critical }

public sealed record ValidationFinding(
    string RuleId,
    string Title,
    Severity Severity,
    string Source,            // "HK" | "PD" | "BP"
    string Message,
    int? Line,
    int? Column,
    string? Snippet,
    string? Justification,    // -- justify: ... above the statement
    string? FixHint);

public sealed record ValidationReport(
    IReadOnlyList<ValidationFinding> Findings,
    int Score,
    bool Certifiable,
    IReadOnlyDictionary<string, int> CountsBySeverity,
    string SqlNormalized,
    string? ParseError = null);

/// <summary>
/// Tunable thresholds. Any rule may consult these via ValidationContext.
/// </summary>
public sealed class ValidationOptions
{
    public int MaxOutputColumns { get; init; } = 30;
    public int MaxJoinedTables { get; init; } = 7;
    public int MaxDerivedColumns { get; init; } = 8;
    public long MaxEstimatedRows { get; init; } = 100_000;
    public int MaxTablesWithoutAlias { get; init; } = 1;
    public int MaxStatementLines { get; init; } = 200;
    /// <summary>Rules to skip (e.g. "VR.020").</summary>
    public HashSet<string> DisabledRules { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public static ValidationOptions Default => new();
}

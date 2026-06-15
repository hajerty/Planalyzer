using System;
using System.Linq;
using Planalyzer.Validation;

namespace Planalyzer.Tests.Validation;

/// <summary>
/// Shared scaffolding for VR.NNN rule tests. Each per-rule file stays small:
/// build SQL, call <see cref="Validate"/>, assert via <see cref="HasFinding"/>
/// or <see cref="FindingOf"/>.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Runs the validator. The <paramref name="configure"/> callback receives
    /// the same <see cref="ValidationOptions"/> instance used by the engine.
    /// Init-only scalar properties can't be reassigned at that point, but
    /// mutable members such as <see cref="ValidationOptions.DisabledRules"/>
    /// can be tweaked. For tests that need different thresholds, prefer
    /// <see cref="ValidateWith"/>.
    /// </summary>
    public static ValidationReport Validate(string sql, Action<ValidationOptions>? configure = null)
    {
        var opts = new ValidationOptions();
        configure?.Invoke(opts);
        return new QueryValidator().Validate(sql, opts);
    }

    /// <summary>
    /// Overload that accepts a fully-built <see cref="ValidationOptions"/>
    /// (useful when init-only thresholds must change).
    /// </summary>
    public static ValidationReport ValidateWith(string sql, ValidationOptions options) =>
        new QueryValidator().Validate(sql, options);

    public static bool HasFinding(ValidationReport r, string ruleId) =>
        r.Findings.Any(f => string.Equals(f.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));

    public static ValidationFinding? FindingOf(ValidationReport r, string ruleId) =>
        r.Findings.FirstOrDefault(f => string.Equals(f.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
}

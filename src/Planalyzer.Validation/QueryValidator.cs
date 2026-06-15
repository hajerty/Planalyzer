using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation;

/// <summary>
/// Engine that parses T-SQL via ScriptDom and applies every registered rule.
/// Rules are discovered via the static <see cref="Registry"/>; backend agents
/// add new rules by appending to <c>Registry.All</c>.
/// </summary>
public sealed class QueryValidator
{
    private readonly IReadOnlyList<IValidationRule> _rules;

    public QueryValidator(IEnumerable<IValidationRule>? rules = null)
    {
        _rules = (rules ?? Registry.All).ToList();
    }

    public ValidationReport Validate(string sql, ValidationOptions? options = null)
    {
        options ??= ValidationOptions.Default;
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        IList<ParseError> errors;
        TSqlFragment tree;
        using (var rdr = new StringReader(sql))
            tree = parser.Parse(rdr, out errors);

        var findings = new List<ValidationFinding>();
        if (errors is { Count: > 0 })
        {
            // We still attempt to walk what was parsed, but flag the parse error.
            return new ValidationReport(
                findings,
                Score: 0,
                Certifiable: false,
                CountsBySeverity: new Dictionary<string, int>(),
                SqlNormalized: sql,
                ParseError: string.Join("; ", errors.Select(e => $"L{e.Line}:{e.Column} {e.Message}")));
        }

        var justify = JustifyExtractor.Build(sql);
        var stmts = new List<TSqlStatement>();
        var stmtCollector = new StatementCollector(stmts);
        tree.Accept(stmtCollector);

        var ctx = new ValidationContext
        {
            Options = options,
            Justify = justify,
            OriginalSql = sql,
            Statements = stmts,
        };

        foreach (var rule in _rules)
        {
            if (options.DisabledRules.Contains(rule.RuleId)) continue;
            try
            {
                foreach (var f in rule.Apply(tree, ctx))
                    findings.Add(MaybeJustify(f, justify));
            }
            catch (Exception ex)
            {
                findings.Add(new ValidationFinding(
                    rule.RuleId, rule.Title, Severity.Info, rule.Source,
                    $"Rule errored: {ex.Message}", null, null, null, null, null));
            }
        }

        var counts = findings
            .GroupBy(f => f.Severity.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var unjustifiedHigh = findings.Count(f => f.Severity == Severity.High && f.Justification is null);
        var critical = findings.Count(f => f.Severity == Severity.Critical);
        var medium = findings.Count(f => f.Severity == Severity.Medium);
        var low = findings.Count(f => f.Severity == Severity.Low);
        var score = Math.Max(0, 100 - (critical * 40 + unjustifiedHigh * 15 + medium * 5 + low * 1));
        var certifiable = critical == 0 && unjustifiedHigh == 0;

        return new ValidationReport(findings, score, certifiable, counts, sql);
    }

    private static ValidationFinding MaybeJustify(ValidationFinding f, JustifyMap justify)
    {
        if (f.Line is null || f.Severity == Severity.Critical) return f;
        var reason = justify.FindReason(f.RuleId, f.Line.Value);
        return reason is null ? f : f with { Justification = reason };
    }

    private sealed class StatementCollector : TSqlFragmentVisitor
    {
        private readonly List<TSqlStatement> _stmts;
        public StatementCollector(List<TSqlStatement> stmts) => _stmts = stmts;
        public override void Visit(TSqlStatement node) => _stmts.Add(node);
    }
}

internal static class JustifyExtractor
{
    // Looks for: -- justify: VR.001 free reason text
    // (case-insensitive). Block comments /* ... */ also supported.
    private static readonly Regex JustifyRegex = new(
        @"(?:--|/\*)\s*justify\s*:\s*(?<id>VR\.\d{3})\s+(?<reason>[^\r\n*]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static JustifyMap Build(string sql)
    {
        var map = new JustifyMap();
        int line = 1;
        int idx = 0;
        foreach (Match m in JustifyRegex.Matches(sql))
        {
            while (idx < m.Index)
            {
                if (sql[idx] == '\n') line++;
                idx++;
            }
            var ruleId = m.Groups["id"].Value;
            var reason = m.Groups["reason"].Value.Trim();
            map.Add(line, ruleId, reason);
        }
        return map;
    }
}

/// <summary>
/// Central registry. Rules are appended here by the backend agent.
/// The order does not matter for output (engine orders by severity).
/// </summary>
public static class Registry
{
    public static IReadOnlyList<IValidationRule> All { get; } = Discover();

    private static IReadOnlyList<IValidationRule> Discover()
    {
        // Reflection-based discovery: any non-abstract class implementing
        // IValidationRule with a parameterless ctor is included.
        return typeof(Registry).Assembly.GetTypes()
            .Where(t => typeof(IValidationRule).IsAssignableFrom(t)
                        && !t.IsAbstract
                        && t.GetConstructor(Type.EmptyTypes) != null)
            .Select(t => (IValidationRule)Activator.CreateInstance(t)!)
            .OrderBy(r => r.RuleId, StringComparer.Ordinal)
            .ToList();
    }
}

using System.CommandLine;
using System.Text;
using System.Text.Json;
using Planalyzer.Validation;

namespace Planalyzer.Cli.Commands;

public static class ValidateCommand
{
    public static Command Build()
    {
        var sqlOpt = new Option<string?>("--sql", "Testo SQL inline.");
        var sqlFileOpt = new Option<FileInfo?>("--sql-file", "File con il testo SQL (.sql).");
        var jsonOpt = new Option<bool>("--json", "Output in JSON anziché testo.");
        var disableOpt = new Option<string[]>("--disable",
            description: "Codici regola da disabilitare (es. --disable VR.020 VR.024).")
        { AllowMultipleArgumentsPerToken = true };
        var failOnHighOpt = new Option<bool>("--fail-on-high",
            "Exit code != 0 se ci sono High non giustificate (in aggiunta alle Critical che sono sempre bloccanti).");
        var maxColsOpt = new Option<int?>("--max-output-columns");
        var maxJoinsOpt = new Option<int?>("--max-joined-tables");
        var maxDerivedOpt = new Option<int?>("--max-derived-columns");

        var cmd = new Command("validate", "Valida un SQL contro le convenzioni interne (VR.001..VR.034).")
        {
            sqlOpt, sqlFileOpt, jsonOpt, disableOpt, failOnHighOpt,
            maxColsOpt, maxJoinsOpt, maxDerivedOpt,
        };
        cmd.SetHandler(ctx =>
        {
            var sqlInline = ctx.ParseResult.GetValueForOption(sqlOpt);
            var sqlFile = ctx.ParseResult.GetValueForOption(sqlFileOpt);
            var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
            var disabled = ctx.ParseResult.GetValueForOption(disableOpt) ?? Array.Empty<string>();
            var failOnHigh = ctx.ParseResult.GetValueForOption(failOnHighOpt);
            var maxCols = ctx.ParseResult.GetValueForOption(maxColsOpt);
            var maxJoins = ctx.ParseResult.GetValueForOption(maxJoinsOpt);
            var maxDerived = ctx.ParseResult.GetValueForOption(maxDerivedOpt);

            string sql = sqlInline
                ?? (sqlFile is { Exists: true } ? File.ReadAllText(sqlFile.FullName)
                : throw new InvalidOperationException("Specificare --sql oppure --sql-file."));

            var opts = new ValidationOptions
            {
                MaxOutputColumns = maxCols ?? 30,
                MaxJoinedTables = maxJoins ?? 7,
                MaxDerivedColumns = maxDerived ?? 8,
                DisabledRules = new HashSet<string>(disabled, StringComparer.OrdinalIgnoreCase),
            };

            var report = new QueryValidator().Validate(sql, opts);

            if (asJson)
            {
                Console.Write(JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }));
            }
            else
            {
                Console.Write(RenderText(report));
            }

            var critical = report.CountsBySeverity.GetValueOrDefault(nameof(Severity.Critical), 0);
            var unjustHigh = report.Findings.Count(f =>
                f.Severity == Severity.High && f.Justification is null);
            if (critical > 0) ctx.ExitCode = 2;
            else if (failOnHigh && unjustHigh > 0) ctx.ExitCode = 1;
            else ctx.ExitCode = 0;
        });
        return cmd;
    }

    private static string RenderText(ValidationReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Planalyzer — Query Certification");
        if (!string.IsNullOrEmpty(r.ParseError))
            sb.AppendLine($"PARSE ERROR: {r.ParseError}");
        sb.AppendLine($"Score: {r.Score}/100   {(r.Certifiable ? "[CERTIFICATA]" : "[NON CERTIFICATA]")}");
        sb.Append("Findings per severity: ");
        sb.AppendLine(string.Join(", ", r.CountsBySeverity.Select(kv => $"{kv.Key}={kv.Value}")));
        sb.AppendLine();
        if (r.Findings.Count == 0)
        {
            sb.AppendLine("Nessun finding.");
            return sb.ToString();
        }
        foreach (var f in r.Findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Line ?? int.MaxValue))
        {
            sb.AppendLine($"[{f.Severity}] {f.RuleId} {f.Title}  ({f.Source})");
            if (f.Line is not null) sb.AppendLine($"   L{f.Line}:C{f.Column}");
            sb.AppendLine($"   {f.Message}");
            if (!string.IsNullOrEmpty(f.Snippet)) sb.AppendLine($"   > {f.Snippet}");
            if (!string.IsNullOrEmpty(f.FixHint)) sb.AppendLine($"   -> {f.FixHint}");
            if (!string.IsNullOrEmpty(f.Justification)) sb.AppendLine($"   justify: {f.Justification}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

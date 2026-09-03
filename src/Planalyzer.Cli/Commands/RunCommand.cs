using System.CommandLine;
using Planalyzer.Core.Analysis;
using Planalyzer.Core.Parsing;
using Planalyzer.Execution;

namespace Planalyzer.Cli.Commands;

public static class RunCommand
{
    public static Command Build()
    {
        var connOpt = new Option<string>("--conn", "Connection string SQL Server.") { IsRequired = true };
        var historyOpt = new Option<string>("--history",
            () => Environment.GetEnvironmentVariable("PLANALYZER_HISTORY_CONN")
                  ?? "Host=localhost;Port=5432;Database=planalyzer;Username=planalyzer;Password=planalyzer_dev_2026",
            "Connection string Postgres per l'history (default: env PLANALYZER_HISTORY_CONN o localhost).");
        var slugOpt = new Option<string>("--slug", "Identificativo logico della query (la stessa per tutte le sue revisioni).") { IsRequired = true };
        var sqlOpt = new Option<string?>("--sql", "Testo SQL inline.");
        var sqlFileOpt = new Option<FileInfo?>("--sql-file", "File con il testo SQL.");
        var noteOpt = new Option<string?>("--note", "Annotazione libera per la revisione.");
        var modeOpt = new Option<string>("--mode", () => "actual", "Modalità: 'actual' (esegue + plan reale) o 'estimated'.");
        var levelOpt = new Option<string>("--level", () => "beginner", "beginner | expert.");
        var timeoutOpt = new Option<int>("--timeout", () => 120, "Command timeout (secondi).");

        var cmd = new Command("run", "Esegue la query, salva la revisione e analizza il piano.")
        {
            connOpt, historyOpt, slugOpt, sqlOpt, sqlFileOpt, noteOpt, modeOpt, levelOpt, timeoutOpt
        };
        cmd.SetHandler(async ctx =>
        {
            var conn = ctx.ParseResult.GetValueForOption(connOpt)!;
            var history = ctx.ParseResult.GetValueForOption(historyOpt)!;
            var slug = ctx.ParseResult.GetValueForOption(slugOpt)!;
            var sqlInline = ctx.ParseResult.GetValueForOption(sqlOpt);
            var sqlFile = ctx.ParseResult.GetValueForOption(sqlFileOpt);
            var note = ctx.ParseResult.GetValueForOption(noteOpt);
            var modeStr = ctx.ParseResult.GetValueForOption(modeOpt)!;
            var levelStr = ctx.ParseResult.GetValueForOption(levelOpt)!;
            var timeout = ctx.ParseResult.GetValueForOption(timeoutOpt);

            string sql = sqlInline
                ?? (sqlFile != null && sqlFile.Exists ? File.ReadAllText(sqlFile.FullName)
                : throw new InvalidOperationException("Specificare --sql oppure --sql-file."));

            var mode = modeStr.Equals("estimated", StringComparison.OrdinalIgnoreCase)
                ? CaptureMode.EstimatedPlanOnly : CaptureMode.ActualPlan;
            var level = levelStr.Equals("expert", StringComparison.OrdinalIgnoreCase) ? AudienceLevel.Expert : AudienceLevel.Beginner;

            // Pre-flight: il validator blocca le query con violazioni Critical
            // (es. UPDATE/DELETE senza WHERE, EXEC con concatenazione).
            var preflight = new Planalyzer.Validation.QueryValidator().Validate(sql);
            var criticals = preflight.CountsBySeverity.GetValueOrDefault(
                nameof(Planalyzer.Validation.Severity.Critical), 0);
            if (criticals > 0)
            {
                Console.Error.WriteLine("Esecuzione abortita: la query viola regole Critical.");
                foreach (var f in preflight.Findings.Where(f => f.Severity == Planalyzer.Validation.Severity.Critical))
                    Console.Error.WriteLine($"  [{f.RuleId}] {f.Title} (L{f.Line}): {f.Message}");
                Console.Error.WriteLine("Disinnesco non possibile (Critical non si giustificano).");
                ctx.ExitCode = 2;
                return;
            }

            var wb = new QueryWorkbench(conn, history);
            var rev = await wb.TryRunAsync(slug, sql, note, mode, timeout, ctx.GetCancellationToken());

            Console.WriteLine($"Revision #{rev.RevisionNo} salvata in {wb.HistoryConnectionString}");
            Console.WriteLine($"  duration={rev.DurationMs}ms cpu={rev.CpuMs}ms logical={rev.LogicalReads} physical={rev.PhysicalReads} rows={rev.RowCount}");
            if (!string.IsNullOrEmpty(rev.Error)) Console.Error.WriteLine($"  ERROR: {rev.Error}");

            if (!string.IsNullOrEmpty(rev.PlanXml))
            {
                var plan = ShowplanParser.ParseString(rev.PlanXml);
                var report = PlanAnalyzer.Analyze(plan);
                Console.WriteLine();
                Console.WriteLine(TextRenderer.Render(report, level));
            }
            wb.Dispose();
        });
        return cmd;
    }
}

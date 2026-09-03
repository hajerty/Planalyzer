using Planalyzer.Core.Analysis;
using Planalyzer.Core.Parsing;
using Planalyzer.Execution;
using Planalyzer.Validation;

namespace Planalyzer.Web.Api;

public sealed record RunRequest(
    string ConnectionString,
    string Slug,
    string Sql,
    string? Note,
    string? Mode,                        // "actual" | "estimated"
    string? HistoryConnectionString);    // Postgres conn string; fallback env PLANALYZER_HISTORY_CONN

public static class RunEndpoints
{
    private const string DefaultLocalHistoryConn =
        "Host=localhost;Port=5432;Database=planalyzer;Username=planalyzer;Password=planalyzer_dev_2026";

    private static string ResolveHistoryConn(string? fromRequest) =>
        !string.IsNullOrWhiteSpace(fromRequest)
            ? fromRequest!
            : Environment.GetEnvironmentVariable("PLANALYZER_HISTORY_CONN") ?? DefaultLocalHistoryConn;

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/run", async (RunRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.ConnectionString)
                || string.IsNullOrWhiteSpace(req.Slug)
                || string.IsNullOrWhiteSpace(req.Sql))
                return Results.BadRequest(new { error = "connectionString, slug, sql required" });

            var mode = string.Equals(req.Mode, "estimated", StringComparison.OrdinalIgnoreCase)
                ? CaptureMode.EstimatedPlanOnly : CaptureMode.ActualPlan;
            var historyConn = ResolveHistoryConn(req.HistoryConnectionString);

            // Always run the validator before sending the SQL: if it's
            // Critical-broken we refuse to execute it.
            var report = new QueryValidator().Validate(req.Sql);
            if (report.CountsBySeverity.TryGetValue("Critical", out var crit) && crit > 0)
                return Results.BadRequest(new
                {
                    error = "Query non eseguibile: violazioni Critical della certificazione interna.",
                    validation = report,
                });

            using var wb = new QueryWorkbench(req.ConnectionString, historyConn);
            var rev = await wb.TryRunAsync(req.Slug, req.Sql, req.Note, mode, 120);

            object? analysis = null;
            if (!string.IsNullOrEmpty(rev.PlanXml))
            {
                var plan = ShowplanParser.ParseString(rev.PlanXml);
                var an = PlanAnalyzer.Analyze(plan);
                analysis = new { text = TextRenderer.Render(an, AudienceLevel.Beginner) };
            }
            return Results.Ok(new { revision = rev, analysis, validation = report });
        })
        .WithName("PostRun")
        .WithOpenApi(o =>
        {
            o.Summary = "Esegue una query e salva la revisione";
            o.Description = "Certifica il SQL (blocca se Critical), lo esegue su SQL Server con il piano richiesto, salva la revisione nell'history Postgres e ritorna analisi + telemetria.";
            return o;
        });

        app.MapPost("/api/rollback", (RollbackRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Slug))
                return Results.BadRequest(new { error = "slug required" });
            var historyConn = ResolveHistoryConn(req.HistoryConnectionString);
            using var wb = new QueryWorkbench("Server=.", historyConn);
            var rec = wb.Rollback(req.Slug, req.To, "rollback via API");
            return rec is null
                ? Results.NotFound(new { error = $"revision {req.To} not found" })
                : Results.Ok(rec);
        })
        .WithName("PostRollback")
        .WithOpenApi(o =>
        {
            o.Summary = "Ripristina una revisione precedente";
            o.Description = "Crea una nuova revisione con lo stesso SQL della revisione target; le statistiche vanno ricalcolate rieseguendo la query.";
            return o;
        });
    }
}

public sealed record RollbackRequest(string Slug, int To, string? HistoryConnectionString);

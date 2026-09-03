using Planalyzer.Execution;

namespace Planalyzer.Web.Api;

public static class HistoryEndpoints
{
    private const string DefaultLocalHistoryConn =
        "Host=localhost;Port=5432;Database=planalyzer;Username=planalyzer;Password=planalyzer_dev_2026";

    private static string ResolveHistoryConn(string? fromQuery) =>
        !string.IsNullOrWhiteSpace(fromQuery)
            ? fromQuery!
            : Environment.GetEnvironmentVariable("PLANALYZER_HISTORY_CONN") ?? DefaultLocalHistoryConn;

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/history/{slug}", (string slug, string? historyConnectionString) =>
        {
            var conn = ResolveHistoryConn(historyConnectionString);
            try
            {
                using var store = new QueryHistoryStore(conn);
                return Results.Ok(store.List(slug));
            }
            catch (Exception ex)
            {
                return Results.Problem(title: "history unavailable", detail: ex.Message, statusCode: 503);
            }
        })
        .WithName("GetHistoryBySlug")
        .WithOpenApi(o =>
        {
            o.Summary = "Lista revisioni di uno slug";
            o.Description = "Ritorna tutte le revisioni salvate per lo slug indicato, ordinate per numero di revisione.";
            return o;
        });

        app.MapGet("/api/history", (string? historyConnectionString) =>
        {
            var conn = ResolveHistoryConn(historyConnectionString);
            try
            {
                using var store = new QueryHistoryStore(conn);
                return Results.Ok(store.ListSlugs());
            }
            catch (Exception ex)
            {
                return Results.Problem(title: "history unavailable", detail: ex.Message, statusCode: 503);
            }
        })
        .WithName("GetHistorySlugs")
        .WithOpenApi(o =>
        {
            o.Summary = "Elenca tutti gli slug tracciati";
            o.Description = "Ritorna la lista distinta di tutti gli slug presenti nell'history.";
            return o;
        });
    }
}

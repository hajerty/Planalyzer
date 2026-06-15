using Planalyzer.Execution;

namespace Planalyzer.Web.Api;

public static class HistoryEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/history/{slug}", (string slug, string? historyDbPath) =>
        {
            var dbPath = string.IsNullOrWhiteSpace(historyDbPath) ? "planalyzer.db" : historyDbPath;
            if (!File.Exists(dbPath)) return Results.Ok(Array.Empty<QueryRevision>());
            using var store = new QueryHistoryStore(dbPath);
            return Results.Ok(store.List(slug));
        });

        app.MapGet("/api/history", (string? historyDbPath) =>
        {
            var dbPath = string.IsNullOrWhiteSpace(historyDbPath) ? "planalyzer.db" : historyDbPath;
            if (!File.Exists(dbPath)) return Results.Ok(Array.Empty<string>());
            using var store = new QueryHistoryStore(dbPath);
            return Results.Ok(store.ListSlugs());
        });
    }
}

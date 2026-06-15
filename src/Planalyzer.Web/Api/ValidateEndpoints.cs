using Planalyzer.Validation;

namespace Planalyzer.Web.Api;

public sealed record ValidateRequest(string Sql, ValidationOptionsDto? Options);
public sealed record ValidationOptionsDto(
    int? MaxOutputColumns,
    int? MaxJoinedTables,
    int? MaxDerivedColumns,
    long? MaxEstimatedRows,
    int? MaxTablesWithoutAlias,
    int? MaxStatementLines,
    string[]? DisabledRules);

public static class ValidateEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/validate", (ValidateRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Sql))
                return Results.BadRequest(new { error = "sql is required" });
            var opts = req.Options is null ? ValidationOptions.Default : new ValidationOptions
            {
                MaxOutputColumns = req.Options.MaxOutputColumns ?? 30,
                MaxJoinedTables = req.Options.MaxJoinedTables ?? 7,
                MaxDerivedColumns = req.Options.MaxDerivedColumns ?? 8,
                MaxEstimatedRows = req.Options.MaxEstimatedRows ?? 100_000,
                MaxTablesWithoutAlias = req.Options.MaxTablesWithoutAlias ?? 1,
                MaxStatementLines = req.Options.MaxStatementLines ?? 200,
                DisabledRules = new HashSet<string>(req.Options.DisabledRules ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
            };
            var v = new QueryValidator();
            return Results.Ok(v.Validate(req.Sql, opts));
        });

        app.MapGet("/api/rules", () =>
        {
            return Results.Ok(Registry.All.Select(r => new
            {
                ruleId = r.RuleId,
                title = r.Title,
                severity = r.DefaultSeverity.ToString(),
                source = r.Source,
            }));
        });
    }
}

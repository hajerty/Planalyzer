using Planalyzer.Comparison;
using Planalyzer.Core.Parsing;

namespace Planalyzer.Web.Api;

public sealed record MultiDbVariantDto(string Label, string PlanXml);
public sealed record MultiDbRequest(List<MultiDbVariantDto> Variants);

public static class MultiDbEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/multidb", (MultiDbRequest req) =>
        {
            if (req?.Variants is null || req.Variants.Count < 2)
                return Results.BadRequest(new { error = "at least 2 variants required" });
            try
            {
                var variants = req.Variants
                    .Select(v => new DbVariant(v.Label, ShowplanParser.ParseString(v.PlanXml)))
                    .ToList();
                var rep = MultiDbComparer.Compare(variants);
                return Results.Ok(new
                {
                    queryHashStatus = rep.QueryHashStatus,
                    variants = rep.Variants,
                    common = rep.CommonRecommendations,
                    divergent = rep.DivergentRecommendations,
                    sqlSuggestion = rep.SqlSuggestion,
                    summary = rep.Summary,
                    text = MultiDbComparer.Render(rep),
                });
            }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
    }
}

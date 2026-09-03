using Planalyzer.Comparison;
using Planalyzer.Core.Parsing;

namespace Planalyzer.Web.Api;

public sealed record CompareRequest(string EstimatedXml, string ActualXml);

public static class CompareEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/compare", (CompareRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.EstimatedXml) || string.IsNullOrWhiteSpace(req.ActualXml))
                return Results.BadRequest(new { error = "estimatedXml and actualXml are required" });
            try
            {
                var pe = ShowplanParser.ParseString(req.EstimatedXml);
                var pa = ShowplanParser.ParseString(req.ActualXml);
                var rep = EstimatedVsActual.Compare(pe, pa);
                return Results.Ok(new
                {
                    shapesMatch = rep.ShapesMatch,
                    shapeDifference = rep.ShapeDifference,
                    operators = rep.Operators,
                    extras = rep.ExtraFindings,
                    text = EstimatedVsActual.Render(rep),
                });
            }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        })
        .WithName("PostCompare")
        .WithOpenApi(o =>
        {
            o.Summary = "Confronta piano stimato vs reale";
            o.Description = "Confronta shape, operatori e cardinalità tra un piano estimated e un actual per evidenziare regressioni e mismatch.";
            return o;
        });
    }
}

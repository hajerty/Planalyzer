using Planalyzer.Core.Analysis;
using Planalyzer.Core.Parsing;

namespace Planalyzer.Web.Api;

public sealed record AnalyzeRequest(string PlanXml, string? Level);

public static class AnalyzeEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/analyze", (AnalyzeRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.PlanXml))
                return Results.BadRequest(new { error = "planXml is required" });
            try
            {
                var plan = ShowplanParser.ParseString(req.PlanXml);
                var rep = PlanAnalyzer.Analyze(plan);
                var lvl = string.Equals(req.Level, "expert", StringComparison.OrdinalIgnoreCase)
                    ? AudienceLevel.Expert : AudienceLevel.Beginner;
                return Results.Ok(new
                {
                    text = TextRenderer.Render(rep, lvl),
                    statements = rep.Statements.Select(s => new
                    {
                        queryHash = s.Statement.QueryHash,
                        planHash = s.Statement.QueryPlanHash,
                        cost = s.Statement.StatementSubTreeCost,
                        findings = s.Findings.Select(f => new
                        {
                            severity = f.Severity.ToString(),
                            code = f.Code,
                            title = f.Title,
                            explanation = f.Explanation,
                            recommendation = f.Recommendation,
                            operatorName = f.Operator,
                            nodeId = f.NodeId,
                            reference = f.Reference,
                        }),
                        hot = s.HotOperators,
                    }),
                });
            }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
    }
}

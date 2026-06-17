using System.Text.Json.Serialization;
using Planalyzer.Web.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddDefaultPolicy(b => b
    .SetIsOriginAllowed(origin =>
    {
        if (string.IsNullOrEmpty(origin)) return false;
        var u = new Uri(origin);
        return u.Host == "localhost"
            || u.Host == "127.0.0.1"
            || u.Host.StartsWith("192.168.")
            || u.Host.StartsWith("10.")
            || u.Host.EndsWith(".local");
    })
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

ValidateEndpoints.Map(app);
AnalyzeEndpoints.Map(app);
CompareEndpoints.Map(app);
MultiDbEndpoints.Map(app);
RunEndpoints.Map(app);
HistoryEndpoints.Map(app);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// In Codespaces / LAN il bind DEVE essere su 0.0.0.0 (loopback non è
// raggiungibile dal port-forwarder). In sviluppo locale puoi forzare il
// loopback con PLANALYZER_URL=http://127.0.0.1:5057.
var bindUrl = Environment.GetEnvironmentVariable("PLANALYZER_URL") ?? "http://0.0.0.0:5057";
app.Urls.Add(bindUrl);
app.Logger.LogInformation("Planalyzer Web in ascolto su {Url}", bindUrl);
app.Run();

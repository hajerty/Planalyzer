using System.Text;

namespace Planalyzer.Execution;

/// <summary>
/// High-level façade that combines the runner and the history store: each
/// invocation of TryRun creates a new revision row tied to a logical "slug",
/// and the user can list, diff and rollback.
/// </summary>
public sealed class QueryWorkbench : IDisposable
{
    private readonly SqlPlanRunner _runner;
    private readonly QueryHistoryStore _store;

    public QueryWorkbench(string connectionString, string historyDbPath)
    {
        _runner = new SqlPlanRunner(connectionString);
        _store = new QueryHistoryStore(historyDbPath);
    }

    public string HistoryPath => _store.Path;

    public async Task<QueryRevision> TryRunAsync(string slug, string sql, string? note,
                                                 CaptureMode mode = CaptureMode.ActualPlan,
                                                 int timeoutSeconds = 120,
                                                 CancellationToken ct = default)
    {
        var rev = _store.NextRevisionNo(slug);
        var result = await _runner.RunAsync(sql, mode, timeoutSeconds, ct);
        var record = new QueryRevision(
            Id: 0,
            Slug: slug,
            RevisionNo: rev,
            CreatedAtUtc: DateTime.UtcNow,
            Sql: sql,
            Note: note,
            PlanXml: result.PlanXml,
            DurationMs: result.DurationMs,
            CpuMs: result.CpuMs,
            LogicalReads: result.LogicalReads,
            PhysicalReads: result.PhysicalReads,
            RowCount: result.RowCount,
            StatsIoText: result.StatsIo,
            StatsTimeText: result.StatsTime,
            Error: result.Error);
        var id = _store.Save(record);
        return record with { Id = id };
    }

    public List<QueryRevision> History(string slug) => _store.List(slug);

    public List<string> Slugs() => _store.ListSlugs();

    public QueryRevision? Rollback(string slug, int targetRevision, string? note = null)
    {
        var src = _store.Get(slug, targetRevision);
        if (src is null) return null;
        // Rollback = create a new revision whose SQL is identical to the targeted one.
        var nextNo = _store.NextRevisionNo(slug);
        var rec = src with
        {
            Id = 0,
            RevisionNo = nextNo,
            CreatedAtUtc = DateTime.UtcNow,
            Note = $"Rollback to rev #{targetRevision}" + (note is null ? "" : $" — {note}"),
            // Don't carry the previous timings; the user must re-run to re-measure.
            PlanXml = null,
            DurationMs = null, CpuMs = null,
            LogicalReads = null, PhysicalReads = null,
            RowCount = null, StatsIoText = null, StatsTimeText = null,
            Error = null
        };
        var id = _store.Save(rec);
        return rec with { Id = id };
    }

    public string RenderHistory(string slug)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# History per '{slug}'");
        sb.AppendLine($"  {"Rev",-5}{"WhenUTC",-22}{"Dur(ms)",10}{"CPU(ms)",10}{"LRead",10}{"PRead",10}{"Rows",10}  Note");
        foreach (var r in _store.List(slug))
        {
            sb.AppendLine(
                $"  {r.RevisionNo,-5}{r.CreatedAtUtc:yyyy-MM-dd HH:mm:ss}  {r.DurationMs,10}{r.CpuMs,10}{r.LogicalReads,10}{r.PhysicalReads,10}{r.RowCount,10}  {r.Note}");
            if (!string.IsNullOrEmpty(r.Error))
                sb.AppendLine($"      ERROR: {r.Error}");
        }
        return sb.ToString();
    }

    public void Dispose() => _store.Dispose();
}

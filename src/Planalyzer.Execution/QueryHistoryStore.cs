using Microsoft.Data.Sqlite;

namespace Planalyzer.Execution;

public sealed record QueryRevision(
    long Id,
    string Slug,
    int RevisionNo,
    DateTime CreatedAtUtc,
    string Sql,
    string? Note,
    string? PlanXml,
    long? DurationMs,
    long? CpuMs,
    long? LogicalReads,
    long? PhysicalReads,
    long? RowCount,
    string? StatsIoText,
    string? StatsTimeText,
    string? Error);

/// <summary>
/// Stores the history of edits to a query plus the execution telemetry collected
/// each time it was run, allowing the user to rollback to any previous revision.
/// SQLite-backed so the tool has zero external dependencies for the history feature.
/// </summary>
public sealed class QueryHistoryStore : IDisposable
{
    private readonly SqliteConnection _conn;
    public string Path { get; }

    public QueryHistoryStore(string path)
    {
        Path = path;
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();
        Init();
    }

    private void Init()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS revisions(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                slug TEXT NOT NULL,
                revision_no INTEGER NOT NULL,
                created_at_utc TEXT NOT NULL,
                sql TEXT NOT NULL,
                note TEXT,
                plan_xml TEXT,
                duration_ms INTEGER,
                cpu_ms INTEGER,
                logical_reads INTEGER,
                physical_reads INTEGER,
                row_count INTEGER,
                stats_io_text TEXT,
                stats_time_text TEXT,
                error TEXT,
                UNIQUE(slug, revision_no)
            );
            CREATE INDEX IF NOT EXISTS ix_rev_slug ON revisions(slug, revision_no);
        """;
        cmd.ExecuteNonQuery();
    }

    public int NextRevisionNo(string slug)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(revision_no), 0) + 1 FROM revisions WHERE slug = $slug";
        cmd.Parameters.AddWithValue("$slug", slug);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public long Save(QueryRevision rev)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO revisions(slug, revision_no, created_at_utc, sql, note, plan_xml,
                                  duration_ms, cpu_ms, logical_reads, physical_reads, row_count,
                                  stats_io_text, stats_time_text, error)
            VALUES($slug,$rev,$ts,$sql,$note,$plan,$dur,$cpu,$lr,$pr,$rc,$io,$time,$err);
            SELECT last_insert_rowid();
        """;
        cmd.Parameters.AddWithValue("$slug", rev.Slug);
        cmd.Parameters.AddWithValue("$rev", rev.RevisionNo);
        cmd.Parameters.AddWithValue("$ts", rev.CreatedAtUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$sql", rev.Sql);
        cmd.Parameters.AddWithValue("$note", (object?)rev.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$plan", (object?)rev.PlanXml ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dur", (object?)rev.DurationMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cpu", (object?)rev.CpuMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lr", (object?)rev.LogicalReads ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pr", (object?)rev.PhysicalReads ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$rc", (object?)rev.RowCount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$io", (object?)rev.StatsIoText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$time", (object?)rev.StatsTimeText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$err", (object?)rev.Error ?? DBNull.Value);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return id;
    }

    public List<QueryRevision> List(string slug)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id,slug,revision_no,created_at_utc,sql,note,plan_xml,duration_ms,cpu_ms,logical_reads,physical_reads,row_count,stats_io_text,stats_time_text,error FROM revisions WHERE slug=$slug ORDER BY revision_no";
        cmd.Parameters.AddWithValue("$slug", slug);
        using var r = cmd.ExecuteReader();
        var list = new List<QueryRevision>();
        while (r.Read())
            list.Add(Read(r));
        return list;
    }

    public QueryRevision? Get(string slug, int revisionNo)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id,slug,revision_no,created_at_utc,sql,note,plan_xml,duration_ms,cpu_ms,logical_reads,physical_reads,row_count,stats_io_text,stats_time_text,error FROM revisions WHERE slug=$slug AND revision_no=$rev";
        cmd.Parameters.AddWithValue("$slug", slug);
        cmd.Parameters.AddWithValue("$rev", revisionNo);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    public List<string> ListSlugs()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT slug FROM revisions ORDER BY slug";
        using var r = cmd.ExecuteReader();
        var list = new List<string>();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    private static QueryRevision Read(SqliteDataReader r) => new(
        r.GetInt64(0),
        r.GetString(1),
        r.GetInt32(2),
        DateTime.Parse(r.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
        r.GetString(4),
        r.IsDBNull(5) ? null : r.GetString(5),
        r.IsDBNull(6) ? null : r.GetString(6),
        r.IsDBNull(7) ? null : r.GetInt64(7),
        r.IsDBNull(8) ? null : r.GetInt64(8),
        r.IsDBNull(9) ? null : r.GetInt64(9),
        r.IsDBNull(10) ? null : r.GetInt64(10),
        r.IsDBNull(11) ? null : r.GetInt64(11),
        r.IsDBNull(12) ? null : r.GetString(12),
        r.IsDBNull(13) ? null : r.GetString(13),
        r.IsDBNull(14) ? null : r.GetString(14));

    public void Dispose() => _conn.Dispose();
}

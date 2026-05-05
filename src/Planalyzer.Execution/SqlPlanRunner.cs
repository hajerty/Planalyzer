using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Planalyzer.Execution;

public sealed record RunResult(
    bool Success,
    long DurationMs,
    long? CpuMs,
    long? LogicalReads,
    long? PhysicalReads,
    long? RowCount,
    string? PlanXml,
    string StatsIo,
    string StatsTime,
    string? Error);

public enum CaptureMode
{
    EstimatedPlanOnly,   // SET SHOWPLAN_XML ON  (does not execute)
    ActualPlan,          // SET STATISTICS XML ON (executes and returns plan)
}

/// <summary>
/// Executes a query against SQL Server and captures the plan XML, the IO/TIME
/// statistics text and the row counters. Used by the live mode for tracking
/// the effect of each query revision over time.
/// </summary>
public sealed class SqlPlanRunner
{
    private readonly string _connectionString;

    public SqlPlanRunner(string connectionString) => _connectionString = connectionString;

    public async Task<RunResult> RunAsync(string sql, CaptureMode mode, int commandTimeoutSeconds = 60, CancellationToken ct = default)
    {
        var statsIo = new StringBuilder();
        var statsTime = new StringBuilder();

        using var conn = new SqlConnection(_connectionString);
        conn.InfoMessage += (_, e) =>
        {
            foreach (SqlError err in e.Errors)
            {
                // 3604/3605 = TextData / DBCC; 0 = informational (incl. STATISTICS IO).
                var msg = err.Message ?? "";
                if (msg.StartsWith("Table '", StringComparison.Ordinal)) statsIo.AppendLine(msg);
                else if (msg.Contains("CPU time", StringComparison.Ordinal) || msg.Contains("execution time", StringComparison.Ordinal)) statsTime.AppendLine(msg);
                else statsIo.AppendLine(msg);
            }
        };
        await conn.OpenAsync(ct).ConfigureAwait(false);

        // Pragmas
        await Exec(conn, "SET STATISTICS IO ON", ct);
        await Exec(conn, "SET STATISTICS TIME ON", ct);

        if (mode == CaptureMode.EstimatedPlanOnly)
            await Exec(conn, "SET SHOWPLAN_XML ON", ct);
        else
            await Exec(conn, "SET STATISTICS XML ON", ct);

        var sw = Stopwatch.StartNew();
        string? planXml = null;
        long rowCount = 0;
        try
        {
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = commandTimeoutSeconds };
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            do
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    // STATISTICS XML / SHOWPLAN_XML returns a one-column "Microsoft SQL Server 20XX XML Showplan"
                    // result set. Detect by single XML column.
                    if (reader.FieldCount == 1 && reader.GetName(0).Contains("Showplan", StringComparison.OrdinalIgnoreCase))
                    {
                        var v = reader.GetValue(0)?.ToString();
                        if (!string.IsNullOrWhiteSpace(v) && v.Contains("ShowPlanXML", StringComparison.OrdinalIgnoreCase))
                            planXml ??= v;
                    }
                    else
                    {
                        rowCount++;
                    }
                }
            } while (await reader.NextResultAsync(ct).ConfigureAwait(false));
            sw.Stop();
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new RunResult(false, sw.ElapsedMilliseconds, null, null, null, null,
                planXml, statsIo.ToString(), statsTime.ToString(), ex.Message);
        }

        var (cpu, lr, pr) = ParseStats(statsIo.ToString(), statsTime.ToString());
        return new RunResult(true, sw.ElapsedMilliseconds, cpu, lr, pr, rowCount,
            planXml, statsIo.ToString(), statsTime.ToString(), null);
    }

    private static async Task Exec(SqlConnection conn, string sql, CancellationToken ct)
    {
        using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static (long? cpu, long? lr, long? pr) ParseStats(string statsIo, string statsTime)
    {
        long? lr = null, pr = null, cpu = null;
        // Aggregate logical/physical reads from "logical reads N, physical reads M"
        foreach (Match m in Regex.Matches(statsIo, @"logical reads (\d+).*?physical reads (\d+)"))
        {
            if (long.TryParse(m.Groups[1].Value, out var l)) lr = (lr ?? 0) + l;
            if (long.TryParse(m.Groups[2].Value, out var p)) pr = (pr ?? 0) + p;
        }
        // CPU time = N ms  elapsed time = M ms
        var cpuMatch = Regex.Match(statsTime, @"CPU time = (\d+) ms");
        if (cpuMatch.Success && long.TryParse(cpuMatch.Groups[1].Value, out var c)) cpu = c;
        return (cpu, lr, pr);
    }
}

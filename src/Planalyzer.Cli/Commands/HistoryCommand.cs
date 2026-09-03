using System.CommandLine;
using Planalyzer.Execution;

namespace Planalyzer.Cli.Commands;

public static class HistoryCommand
{
    public static Command Build()
    {
        var historyOpt = new Option<string>("--history",
            () => Environment.GetEnvironmentVariable("PLANALYZER_HISTORY_CONN")
                  ?? "Host=localhost;Port=5432;Database=planalyzer;Username=planalyzer;Password=planalyzer_dev_2026",
            "Connection string Postgres per l'history.");
        var slugOpt = new Option<string?>("--slug", "Slug della query (se omesso, lista tutti gli slug).");

        var cmd = new Command("history", "Mostra la cronologia delle revisioni di una query.")
        { historyOpt, slugOpt };
        cmd.SetHandler((history, slug) =>
        {
            var wb = new QueryWorkbench(connectionString: "Server=.", historyConnectionString: history);
            try
            {
                if (string.IsNullOrEmpty(slug))
                {
                    foreach (var s in wb.Slugs()) Console.WriteLine(s);
                }
                else
                {
                    Console.Write(wb.RenderHistory(slug));
                }
            }
            finally { wb.Dispose(); }
        }, historyOpt, slugOpt);
        return cmd;
    }
}

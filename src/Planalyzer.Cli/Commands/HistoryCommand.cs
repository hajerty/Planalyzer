using System.CommandLine;
using Planalyzer.Execution;

namespace Planalyzer.Cli.Commands;

public static class HistoryCommand
{
    public static Command Build()
    {
        var historyOpt = new Option<string>("--history", () => "planalyzer.db", "Path del DB di history.");
        var slugOpt = new Option<string?>("--slug", "Slug della query (se omesso, lista tutti gli slug).");

        var cmd = new Command("history", "Mostra la cronologia delle revisioni di una query.")
        { historyOpt, slugOpt };
        cmd.SetHandler((history, slug) =>
        {
            var wb = new QueryWorkbench(connectionString: "Server=.", historyDbPath: history);
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

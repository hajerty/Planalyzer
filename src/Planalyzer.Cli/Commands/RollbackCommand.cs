using System.CommandLine;
using Planalyzer.Execution;

namespace Planalyzer.Cli.Commands;

public static class RollbackCommand
{
    public static Command Build()
    {
        var historyOpt = new Option<string>("--history",
            () => Environment.GetEnvironmentVariable("PLANALYZER_HISTORY_CONN")
                  ?? "Host=localhost;Port=5432;Database=planalyzer;Username=planalyzer;Password=planalyzer_dev_2026",
            "Connection string Postgres per l'history.");
        var slugOpt = new Option<string>("--slug", "Slug della query.") { IsRequired = true };
        var revOpt = new Option<int>("--to", "Numero della revisione a cui tornare.") { IsRequired = true };
        var noteOpt = new Option<string?>("--note", "Note del rollback.");

        var cmd = new Command("rollback", "Crea una nuova revisione che ripristina il SQL di una revisione precedente.")
        { historyOpt, slugOpt, revOpt, noteOpt };
        cmd.SetHandler((history, slug, target, note) =>
        {
            var wb = new QueryWorkbench("Server=.", history);
            try
            {
                var rec = wb.Rollback(slug, target, note);
                if (rec is null)
                {
                    Console.Error.WriteLine($"Revisione {target} non trovata per slug '{slug}'.");
                    Environment.ExitCode = 1;
                    return;
                }
                Console.WriteLine($"Creata revisione #{rec.RevisionNo} (rollback a #{target}).");
                Console.WriteLine("Esegui 'planalyzer run --slug ... --sql-file ...' per rimisurare le statistiche.");
            }
            finally { wb.Dispose(); }
        }, historyOpt, slugOpt, revOpt, noteOpt);
        return cmd;
    }
}

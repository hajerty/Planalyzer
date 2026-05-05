using System.CommandLine;
using Planalyzer.Cli.Commands;

var root = new RootCommand("Planalyzer — analizzatore piani esecuzione SQL Server (sintesi + dettaglio non troncato).");
root.AddCommand(AnalyzeCommand.Build());
root.AddCommand(CompareCommand.Build());
root.AddCommand(MultiDbCommand.Build());
root.AddCommand(RunCommand.Build());
root.AddCommand(HistoryCommand.Build());
root.AddCommand(RollbackCommand.Build());
return await root.InvokeAsync(args);

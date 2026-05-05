using System.CommandLine;
using Planalyzer.Comparison;
using Planalyzer.Core.Parsing;

namespace Planalyzer.Cli.Commands;

public static class MultiDbCommand
{
    public static Command Build()
    {
        var planOpt = new Option<string[]>("--plan",
            "Coppia 'label=path' ripetibile. Es. --plan prod=prod.sqlplan --plan staging=stg.sqlplan")
        { IsRequired = true, AllowMultipleArgumentsPerToken = true };
        var outOpt = new Option<FileInfo?>("--out", "Salva il report su file.");

        var cmd = new Command("multidb", "Confronta più piani per la stessa query (DB diversi) e sintetizza una soluzione comune.")
        {
            planOpt, outOpt,
        };
        cmd.SetHandler((plans, output) =>
        {
            var variants = new List<DbVariant>();
            foreach (var p in plans)
            {
                var idx = p.IndexOf('=');
                if (idx <= 0) { Console.Error.WriteLine($"Formato atteso label=path: {p}"); continue; }
                var label = p[..idx];
                var path = p[(idx + 1)..];
                if (!File.Exists(path)) { Console.Error.WriteLine($"File non trovato: {path}"); continue; }
                variants.Add(new DbVariant(label, ShowplanParser.ParseFile(path)));
            }
            if (variants.Count < 2) { Console.Error.WriteLine("Servono almeno 2 piani."); Environment.ExitCode = 2; return; }
            var rep = MultiDbComparer.Compare(variants);
            var text = MultiDbComparer.Render(rep);
            if (output is null) Console.Write(text);
            else File.WriteAllText(output.FullName, text);
        }, planOpt, outOpt);
        return cmd;
    }
}

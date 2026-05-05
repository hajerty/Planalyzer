using System.CommandLine;
using Planalyzer.Core.Analysis;
using Planalyzer.Core.Parsing;

namespace Planalyzer.Cli.Commands;

public static class AnalyzeCommand
{
    public static Command Build()
    {
        var pathArg = new Argument<FileInfo[]>("plan-files", "Uno o più file .sqlplan / .xml.") { Arity = ArgumentArity.OneOrMore };
        var levelOpt = new Option<string>("--level", () => "beginner",
            "Livello di dettaglio: 'beginner' (sintesi) o 'expert' (dump completo proprietà).");
        levelOpt.AddAlias("-l");
        var outOpt = new Option<FileInfo?>("--out", "Salva il report su file invece di stdout.");

        var cmd = new Command("analyze", "Analizza uno o più piani XML e produce sintesi + dettaglio.")
        {
            pathArg, levelOpt, outOpt,
        };
        cmd.SetHandler((files, level, output) =>
        {
            var lvl = level.Equals("expert", StringComparison.OrdinalIgnoreCase) ? AudienceLevel.Expert : AudienceLevel.Beginner;
            var sb = new System.Text.StringBuilder();
            foreach (var f in files)
            {
                if (!f.Exists) { Console.Error.WriteLine($"File non trovato: {f.FullName}"); continue; }
                var plan = ShowplanParser.ParseFile(f.FullName);
                var report = PlanAnalyzer.Analyze(plan);
                sb.AppendLine(TextRenderer.Render(report, lvl));
                sb.AppendLine(new string('-', 80));
            }
            var text = sb.ToString();
            if (output is null) Console.Write(text);
            else File.WriteAllText(output.FullName, text);
        }, pathArg, levelOpt, outOpt);
        return cmd;
    }
}

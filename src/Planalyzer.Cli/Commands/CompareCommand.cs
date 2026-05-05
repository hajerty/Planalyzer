using System.CommandLine;
using Planalyzer.Comparison;
using Planalyzer.Core.Parsing;

namespace Planalyzer.Cli.Commands;

public static class CompareCommand
{
    public static Command Build()
    {
        var estOpt = new Option<FileInfo>("--estimated", "File del piano stimato.") { IsRequired = true };
        var actOpt = new Option<FileInfo>("--actual", "File del piano effettivo.") { IsRequired = true };
        var outOpt = new Option<FileInfo?>("--out", "Salva il report su file.");

        var cmd = new Command("compare", "Confronta piano stimato vs effettivo.")
        {
            estOpt, actOpt, outOpt,
        };
        cmd.SetHandler((est, act, output) =>
        {
            var pe = ShowplanParser.ParseFile(est.FullName);
            var pa = ShowplanParser.ParseFile(act.FullName);
            var report = EstimatedVsActual.Compare(pe, pa);
            var text = EstimatedVsActual.Render(report);
            if (output is null) Console.Write(text);
            else File.WriteAllText(output.FullName, text);
        }, estOpt, actOpt, outOpt);
        return cmd;
    }
}

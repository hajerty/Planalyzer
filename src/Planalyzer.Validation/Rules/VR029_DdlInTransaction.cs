using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.029 — DDL inside an explicit BEGIN TRAN ... COMMIT block (BP).
/// Holds schema (Sch-M) locks for the whole transaction; tends to block
/// every other reader. Heuristic walks top-level statement order.
/// </summary>
public sealed class VR029_DdlInTransaction : RuleBase
{
    public override string RuleId => "VR.029";
    public override string Title => "DDL dentro transazione esplicita";
    public override Severity DefaultSeverity => Severity.Medium;
    public override string Source => "BP";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var findings = new List<ValidationFinding>();
        int depth = 0;
        foreach (var s in ctx.Statements)
        {
            switch (s)
            {
                case BeginTransactionStatement: depth++; break;
                case CommitTransactionStatement:
                case RollbackTransactionStatement:
                    depth = Math.Max(0, depth - 1); break;
                default:
                    if (depth > 0 && IsDdl(s))
                    {
                        findings.Add(Finding(
                            $"DDL ({s.GetType().Name}) dentro transazione esplicita: lock metadata prolungati.",
                            s,
                            "Sposta il DDL fuori dalla transazione o spezza il deploy."));
                    }
                    break;
            }
        }
        return findings;
    }

    private static bool IsDdl(TSqlStatement s) =>
        s is CreateTableStatement or AlterTableStatement or DropTableStatement
          or CreateIndexStatement or AlterIndexStatement or DropIndexStatement
          or CreateViewStatement or AlterViewStatement or DropViewStatement
          or CreateProcedureStatement or AlterProcedureStatement or DropProcedureStatement
          or CreateFunctionStatement or AlterFunctionStatement or DropFunctionStatement
          or CreateSchemaStatement
          or CreateTriggerStatement or AlterTriggerStatement or DropTriggerStatement;
}

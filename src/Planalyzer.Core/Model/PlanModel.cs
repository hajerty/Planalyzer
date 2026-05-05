namespace Planalyzer.Core.Model;

/// <summary>
/// Root container of a parsed Showplan XML. One file may carry multiple
/// statements (BatchSequence -> Batch -> Statements -> StmtSimple).
/// </summary>
public sealed class ParsedPlan
{
    public string? SourcePath { get; init; }
    public string? Version { get; init; }
    public string? Build { get; init; }
    public bool IsActual { get; init; }
    public List<PlanStatement> Statements { get; } = new();
    public List<string> ParseWarnings { get; } = new();
}

public sealed class PlanStatement
{
    public string? StatementText { get; init; }
    public string? StatementType { get; init; }
    public string? QueryHash { get; init; }
    public string? QueryPlanHash { get; init; }
    public double? StatementSubTreeCost { get; init; }
    public double? StatementEstRows { get; init; }
    public string? StatementOptmLevel { get; init; }
    public string? StatementOptmEarlyAbortReason { get; init; }
    public int? CardinalityEstimationModelVersion { get; init; }
    public bool? RetrievedFromCache { get; init; }
    public PlanOperator? Root { get; init; }
    public List<MissingIndex> MissingIndexes { get; } = new();
    public List<UsedPlanForcing> PlanForcing { get; } = new();
    public Dictionary<string, string> RawAttributes { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PlanOperator
{
    public int NodeId { get; init; }
    public string PhysicalOp { get; init; } = "";
    public string LogicalOp { get; init; } = "";
    public double EstimateRows { get; init; }
    public double? ActualRows { get; set; }
    public double? ActualRowsRead { get; set; }
    public double? ActualExecutions { get; set; }
    public double EstimatedTotalSubtreeCost { get; init; }
    public double EstimateCPU { get; init; }
    public double EstimateIO { get; init; }
    public int EstimatedRowsRead { get; init; }
    public bool Parallel { get; init; }
    public int? EstimatedRowSize { get; init; }
    public string? Statistics { get; init; }
    public string? StatisticsModificationCounter { get; init; }
    public List<PlanOperator> Children { get; } = new();
    /// <summary> Operator-specific fields, fully captured (no truncation). </summary>
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary> Warnings reported in the plan itself (sort spill, hash spill, ...). </summary>
    public List<PlanWarning> Warnings { get; } = new();
    /// <summary> Output columns from OutputList. </summary>
    public List<string> OutputColumns { get; } = new();
    /// <summary> Tables/indexes touched (Object[]/IndexedColumn). </summary>
    public List<ObjectRef> Objects { get; } = new();
    /// <summary> RunTimeInformation entries (one per thread when parallel). </summary>
    public List<RuntimeThreadInfo> RuntimeInfo { get; } = new();
    public PlanOperator? Parent { get; set; }

    public IEnumerable<PlanOperator> Walk()
    {
        yield return this;
        foreach (var c in Children)
            foreach (var d in c.Walk())
                yield return d;
    }
}

public sealed class PlanWarning
{
    public string Kind { get; init; } = "";        // SpillToTempDb, ColumnsWithNoStatistics, ...
    public string Detail { get; init; } = "";
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ObjectRef
{
    public string? Database { get; init; }
    public string? Schema { get; init; }
    public string? Table { get; init; }
    public string? Index { get; init; }
    public string? IndexKind { get; init; }
    public string? Storage { get; init; }
    public string? Alias { get; init; }
    public override string ToString() =>
        $"{Database}.{Schema}.{Table}" + (Index is null ? "" : $" [{IndexKind}:{Index}]");
}

public sealed class RuntimeThreadInfo
{
    public int Thread { get; init; }
    public double ActualRows { get; init; }
    public double ActualRowsRead { get; init; }
    public double ActualExecutions { get; init; }
    public double? ActualElapsedms { get; init; }
    public double? ActualCPUms { get; init; }
    public long? ActualLogicalReads { get; init; }
    public long? ActualPhysicalReads { get; init; }
    public long? ActualReadAheads { get; init; }
}

public sealed class MissingIndex
{
    public double Impact { get; init; }
    public string? Database { get; init; }
    public string? Schema { get; init; }
    public string? Table { get; init; }
    public List<string> EqualityColumns { get; } = new();
    public List<string> InequalityColumns { get; } = new();
    public List<string> IncludeColumns { get; } = new();

    public string CreateIndexStatement(string? name = null)
    {
        var idxName = name ?? $"IX_{Table}_{string.Join("_", EqualityColumns.Concat(InequalityColumns).Select(Strip))}";
        var key = string.Join(", ", EqualityColumns.Concat(InequalityColumns));
        var include = IncludeColumns.Count == 0 ? "" : $" INCLUDE ({string.Join(", ", IncludeColumns)})";
        return $"CREATE NONCLUSTERED INDEX [{idxName}] ON {Schema}.{Table} ({key}){include};";
        static string Strip(string s) => new(s.Where(char.IsLetterOrDigit).ToArray());
    }
}

public sealed class UsedPlanForcing
{
    public string? Type { get; init; }
    public string? Hint { get; init; }
}

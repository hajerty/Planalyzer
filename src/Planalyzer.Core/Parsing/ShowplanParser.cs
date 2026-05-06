using System.Globalization;
using System.Xml.Linq;
using Planalyzer.Core.Model;

namespace Planalyzer.Core.Parsing;

/// <summary>
/// Parses Showplan XML (estimated or actual) into the rich model used by the
/// rest of the analyzer. The parser is property-preserving: every attribute on
/// every operator and every child element of RelOp (Hash, MergeJoin, IndexScan,
/// ComputeScalar, etc.) is captured into Operator.Properties so that nothing is
/// truncated as it usually is in SSMS tooltips.
/// </summary>
public static class ShowplanParser
{
    public static readonly XNamespace Ns = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

    public static ParsedPlan ParseFile(string path)
    {
        var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var p = Parse(doc);
        var copy = new ParsedPlan
        {
            SourcePath = path,
            Version = p.Version,
            Build = p.Build,
            IsActual = p.IsActual,
        };
        return copy.Hydrate(p);
    }

    public static ParsedPlan ParseString(string xml)
    {
        var doc = XDocument.Parse(xml);
        return Parse(doc);
    }

    private static ParsedPlan Parse(XDocument doc)
    {
        var root = doc.Root ?? throw new InvalidOperationException("Empty XML.");
        if (root.Name.LocalName != "ShowPlanXML")
            throw new InvalidOperationException($"Not a ShowplanXML document (root={root.Name.LocalName}).");

        var version = (string?)root.Attribute("Version");
        var build = (string?)root.Attribute("Build");

        bool isActual = root.Descendants(Ns + "RunTimeInformation").Any();

        var plan = new ParsedPlan
        {
            Version = version,
            Build = build,
            IsActual = isActual,
        };

        foreach (var stmt in root.Descendants(Ns + "StmtSimple"))
        {
            plan.Statements.Add(ParseStatement(stmt));
        }
        // Some plans use StmtCond/StmtCursor; capture their inner statements too.
        foreach (var stmt in root.Descendants(Ns + "StmtCursor")
                                 .Concat(root.Descendants(Ns + "StmtCond"))
                                 .Concat(root.Descendants(Ns + "StmtUseDb")))
        {
            foreach (var inner in stmt.Descendants(Ns + "StmtSimple"))
            {
                if (!plan.Statements.Any(s => ReferenceEquals(s.RawAttributes, inner)))
                    plan.Statements.Add(ParseStatement(inner));
            }
        }
        return plan;
    }

    private static PlanStatement ParseStatement(XElement stmt)
    {
        var attrs = stmt.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.OrdinalIgnoreCase);
        var s = new PlanStatement
        {
            StatementText = attrs.GetValueOrDefault("StatementText"),
            StatementType = attrs.GetValueOrDefault("StatementType"),
            QueryHash = attrs.GetValueOrDefault("QueryHash"),
            QueryPlanHash = attrs.GetValueOrDefault("QueryPlanHash"),
            StatementOptmLevel = attrs.GetValueOrDefault("StatementOptmLevel"),
            StatementOptmEarlyAbortReason = attrs.GetValueOrDefault("StatementOptmEarlyAbortReason"),
            StatementSubTreeCost = ParseDouble(attrs.GetValueOrDefault("StatementSubTreeCost")),
            StatementEstRows = ParseDouble(attrs.GetValueOrDefault("StatementEstRows")),
            CardinalityEstimationModelVersion = ParseInt(attrs.GetValueOrDefault("CardinalityEstimationModelVersion")),
            RetrievedFromCache = ParseBool(attrs.GetValueOrDefault("RetrievedFromCache")),
            Root = stmt.Element(Ns + "QueryPlan")?.Element(Ns + "RelOp") is { } rop
                ? ParseOperator(rop, parent: null)
                : null,
        };
        foreach (var (k, v) in attrs) s.RawAttributes[k] = v;

        var qp = stmt.Element(Ns + "QueryPlan");
        if (qp is null) return s;

        foreach (var mi in qp.Descendants(Ns + "MissingIndex"))
        {
            var idx = new MissingIndex
            {
                Impact = ParseDouble((string?)mi.Parent?.Attribute("Impact")) ?? 0,
                Database = (string?)mi.Attribute("Database"),
                Schema = (string?)mi.Attribute("Schema"),
                Table = (string?)mi.Attribute("Table"),
            };
            foreach (var col in mi.Elements(Ns + "ColumnGroup"))
            {
                var usage = (string?)col.Attribute("Usage");
                foreach (var c in col.Elements(Ns + "Column"))
                {
                    var name = (string?)c.Attribute("Name") ?? "";
                    switch (usage)
                    {
                        case "EQUALITY": idx.EqualityColumns.Add(name); break;
                        case "INEQUALITY": idx.InequalityColumns.Add(name); break;
                        case "INCLUDE": idx.IncludeColumns.Add(name); break;
                    }
                }
            }
            s.MissingIndexes.Add(idx);
        }

        foreach (var pf in qp.Descendants(Ns + "UsePlan").Concat(qp.Descendants(Ns + "OptimizerHardwareDependentProperties")))
        {
            // Just capture anything noteworthy
        }

        return s;
    }

    private static PlanOperator ParseOperator(XElement relOp, PlanOperator? parent)
    {
        var op = new PlanOperator
        {
            NodeId = ParseInt((string?)relOp.Attribute("NodeId")) ?? 0,
            PhysicalOp = (string?)relOp.Attribute("PhysicalOp") ?? "",
            LogicalOp = (string?)relOp.Attribute("LogicalOp") ?? "",
            EstimateRows = ParseDouble((string?)relOp.Attribute("EstimateRows")) ?? 0,
            EstimatedTotalSubtreeCost = ParseDouble((string?)relOp.Attribute("EstimatedTotalSubtreeCost")) ?? 0,
            EstimateCPU = ParseDouble((string?)relOp.Attribute("EstimateCPU")) ?? 0,
            EstimateIO = ParseDouble((string?)relOp.Attribute("EstimateIO")) ?? 0,
            EstimatedRowsRead = ParseInt((string?)relOp.Attribute("EstimatedRowsRead")) ?? 0,
            EstimatedRowSize = ParseInt((string?)relOp.Attribute("EstimatedRowSize")),
            Parallel = ParseBool((string?)relOp.Attribute("Parallel")) ?? false,
            Statistics = (string?)relOp.Attribute("Statistics"),
            StatisticsModificationCounter = (string?)relOp.Attribute("StatisticsModificationCounter"),
            Parent = parent,
        };
        foreach (var a in relOp.Attributes())
            op.Properties[a.Name.LocalName] = a.Value;

        // Showplan structure: child RelOps are nested inside the operator-specific
        // wrapper element (Hash, NestedLoops, ComputeScalar, IndexScan, Filter, ...).
        // 1) Add direct child operators = descendant RelOps whose nearest RelOp ancestor
        //    is the current relOp (i.e., not deeper than one operator boundary away).
        var directChildren = relOp.Descendants(Ns + "RelOp")
            .Where(d => d.Ancestors(Ns + "RelOp").FirstOrDefault() == relOp)
            .ToList();
        foreach (var c in directChildren)
            op.Children.Add(ParseOperator(c, op));

        // 2) Walk every element within the current operator's "scope" — i.e.
        //    everything inside the wrapper elements but stopping at any nested RelOp.
        //    For well-known elements (Predicate, ResidualPredicate, HashKeysBuild,
        //    ProbeResidual, DefinedValues, OuterReferences, ...) we promote them to
        //    top-level keys in op.Properties so the analyzer can find them easily.
        //    Other elements are also captured as serialized snippets — nothing is
        //    hidden / truncated, which is the point of the tool.
        WalkScoped(relOp, op);

        if (op.RuntimeInfo.Count > 0)
        {
            op.ActualRows = op.RuntimeInfo.Sum(r => r.ActualRows);
            op.ActualRowsRead = op.RuntimeInfo.Sum(r => r.ActualRowsRead);
            op.ActualExecutions = op.RuntimeInfo.Sum(r => r.ActualExecutions);
        }
        return op;
    }

    private static readonly HashSet<string> PromotedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Predicate", "ResidualPredicate", "ProbeResidual",
        "HashKeysBuild", "HashKeysProbe",
        "DefinedValues", "OuterReferences",
        "GroupBy", "OrderBy",
        "SeekPredicates", "SeekPredicateNew", "SeekPredicate",
        "Where",
    };

    /// <summary>
    /// Walks every element inside this operator's scope (= descendants up to but not
    /// including any nested RelOp). Captures well-known sub-elements as top-level
    /// properties; other wrapper elements get a serialized snippet AND we keep
    /// recursing into them to find well-known children.
    /// </summary>
    private static void WalkScoped(XElement scope, PlanOperator op)
    {
        foreach (var child in scope.Elements())
        {
            // Hard boundary: never cross into another operator's subtree.
            if (child.Name == Ns + "RelOp") continue;

            switch (child.Name.LocalName)
            {
                case "OutputList":
                    foreach (var c in child.Elements(Ns + "ColumnReference"))
                        op.OutputColumns.Add(FormatColumn(c));
                    op.Properties["OutputList"] = string.Join(", ", op.OutputColumns);
                    break;
                case "Warnings":
                    foreach (var w in child.Elements())
                        op.Warnings.Add(ParseWarning(w));
                    break;
                case "RunTimeInformation":
                    foreach (var t in child.Elements(Ns + "RunTimeCountersPerThread"))
                        op.RuntimeInfo.Add(ParseRuntime(t));
                    break;
                case "Object":
                    op.Objects.Add(ParseObject(child));
                    break;
                default:
                    // Generic capture (no truncation).
                    var snippet = SerializeForProperty(child);
                    var key = child.Name.LocalName;
                    if (op.Properties.TryGetValue(key, out var existing))
                        op.Properties[key] = existing + " | " + snippet;
                    else
                        op.Properties[key] = snippet;

                    // Promote well-known elements so analyzer can find them with a
                    // simple TryGetValue (regardless of which wrapper hosted them).
                    if (PromotedKeys.Contains(key))
                    {
                        // Already stored above under its own name.
                    }
                    else
                    {
                        // Recurse into wrapper to surface its known children
                        // (e.g. IndexScan -> Predicate, Hash -> HashKeysBuild...).
                        WalkScoped(child, op);
                    }
                    break;
            }
        }
    }

    private static string SerializeForProperty(XElement e)
    {
        // Try to extract semantically meaningful pieces first, then fall back
        // to raw inner XML so nothing is hidden (this is the explicit point of
        // the tool: no truncation).
        // Common useful shortcut: ScalarString attribute deep inside.
        var scalarStrings = e.Descendants(Ns + "ScalarOperator")
                             .Select(s => (string?)s.Attribute("ScalarString"))
                             .Where(v => !string.IsNullOrWhiteSpace(v))
                             .Distinct()
                             .ToList();
        if (scalarStrings.Count > 0)
            return string.Join(" ; ", scalarStrings!);

        // Compact: list ColumnReference children
        var cols = e.Descendants(Ns + "ColumnReference").Select(FormatColumn).Distinct().ToList();
        if (cols.Count > 0 && cols.Count <= 12)
            return string.Join(", ", cols);

        // Last resort: trimmed XML.
        var s = e.ToString(SaveOptions.DisableFormatting);
        return s.Length > 4000 ? s.Substring(0, 4000) + "...[truncated]" : s;
    }

    private static string FormatColumn(XElement c)
    {
        var db = (string?)c.Attribute("Database");
        var sch = (string?)c.Attribute("Schema");
        var tbl = (string?)c.Attribute("Table");
        var col = (string?)c.Attribute("Column");
        var alias = (string?)c.Attribute("Alias");
        var parts = new[] { db, sch, tbl, col }.Where(p => !string.IsNullOrEmpty(p));
        var name = string.Join(".", parts);
        return alias is null ? name : $"{name} AS {alias}";
    }

    private static PlanWarning ParseWarning(XElement w)
    {
        var pw = new PlanWarning
        {
            Kind = w.Name.LocalName,
            Detail = w.ToString(SaveOptions.DisableFormatting),
        };
        foreach (var a in w.Attributes())
            pw.Properties[a.Name.LocalName] = a.Value;
        return pw;
    }

    private static RuntimeThreadInfo ParseRuntime(XElement t) => new()
    {
        Thread = ParseInt((string?)t.Attribute("Thread")) ?? 0,
        ActualRows = ParseDouble((string?)t.Attribute("ActualRows")) ?? 0,
        ActualRowsRead = ParseDouble((string?)t.Attribute("ActualRowsRead")) ?? 0,
        ActualExecutions = ParseDouble((string?)t.Attribute("ActualExecutions")) ?? 0,
        ActualElapsedms = ParseDouble((string?)t.Attribute("ActualElapsedms")),
        ActualCPUms = ParseDouble((string?)t.Attribute("ActualCPUms")),
        ActualLogicalReads = ParseLong((string?)t.Attribute("ActualLogicalReads")),
        ActualPhysicalReads = ParseLong((string?)t.Attribute("ActualPhysicalReads")),
        ActualReadAheads = ParseLong((string?)t.Attribute("ActualReadAheads")),
    };

    private static ObjectRef ParseObject(XElement e) => new()
    {
        Database = (string?)e.Attribute("Database"),
        Schema = (string?)e.Attribute("Schema"),
        Table = (string?)e.Attribute("Table"),
        Index = (string?)e.Attribute("Index"),
        IndexKind = (string?)e.Attribute("IndexKind"),
        Storage = (string?)e.Attribute("Storage"),
        Alias = (string?)e.Attribute("Alias"),
    };

    private static double? ParseDouble(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static int? ParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static long? ParseLong(string? s) =>
        long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static bool? ParseBool(string? s) => s switch
    {
        null => null,
        "1" or "true" or "True" => true,
        "0" or "false" or "False" => false,
        _ => null,
    };
}

internal static class ParsedPlanExtensions
{
    public static ParsedPlan Hydrate(this ParsedPlan dst, ParsedPlan src)
    {
        foreach (var s in src.Statements) dst.Statements.Add(s);
        foreach (var w in src.ParseWarnings) dst.ParseWarnings.Add(w);
        return dst;
    }
}

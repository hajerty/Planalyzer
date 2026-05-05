namespace Planalyzer.Core.Knowledge;

/// <summary>
/// Beginner-friendly reference for the most common physical operators, paraphrased
/// from the public Hugo Kornelis "Plan Operators" series on sqlserverfast.com.
/// Each entry includes a short explanation, what to look for, and frequent pitfalls.
/// References:
///   https://sqlserverfast.com/epr/   (Execution Plan Reference)
/// </summary>
public static class OperatorReference
{
    public sealed record Entry(
        string Operator,
        string Summary,
        string[] LookFor,
        string[] Pitfalls,
        string Reference);

    public static readonly Dictionary<string, Entry> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Clustered Index Scan"] = new(
            "Clustered Index Scan",
            "Legge tutte le righe della tabella seguendo l'indice cluster (= scan dell'intera tabella).",
            new[] { "EstimatedRowsRead vs ActualRows: se RowsRead >> Rows, c'è un residual predicate non sargable." },
            new[] { "Spesso indica WHERE non sargable (funzioni sulle colonne, ISNULL/COALESCE, implicit conversion).",
                    "Non sempre è male: per scansioni di poche righe o per le query che leggono > ~20% della tabella può essere ottimale." },
            "https://sqlserverfast.com/epr/clustered-index-scan/"),

        ["Index Scan"] = new(
            "(Nonclustered) Index Scan",
            "Scansione completa di un indice non-cluster.",
            new[] { "Predicate elemento (residual) presente -> filtri non sargable." },
            new[] { "Indice sbagliato per la query, oppure WHERE non sargable.",
                    "Verifica se serve una colonna ulteriore (key/include) per evitare lookup successivi." },
            "https://sqlserverfast.com/epr/index-scan/"),

        ["Clustered Index Seek"] = new(
            "Clustered Index Seek",
            "Cerca direttamente nel cluster usando le colonne chiave: ottima quando seleziona poche righe.",
            new[] { "Seek Predicate vs Predicate (residual): il primo è sargable, il secondo no." },
            new[] { "Un Seek che restituisce milioni di righe non è automaticamente buono: guarda EstimatedRowsRead." },
            "https://sqlserverfast.com/epr/clustered-index-seek/"),

        ["Index Seek"] = new(
            "(Nonclustered) Index Seek",
            "Cerca su indice non cluster; se non è covering segue Key/RID Lookup.",
            new[] { "Numero di esecuzioni del Lookup correlato = numero di righe del Seek." },
            new[] { "Costo nascosto: tipping point fra seek+lookup e scan.",
                    "Frequenti Key Lookup -> valuta INCLUDE delle colonne mancanti." },
            "https://sqlserverfast.com/epr/index-seek/"),

        ["Key Lookup"] = new(
            "Key Lookup (Clustered)",
            "Per ogni riga restituita da un nonclustered index recupera le colonne mancanti dal cluster.",
            new[] { "ActualExecutions: ogni esecuzione = un I/O random." },
            new[] { "Sopra un certo numero di righe è più costoso di una scan completa (tipping point ~0.2% delle pagine).",
                    "Risolto da un covering index (INCLUDE)." },
            "https://sqlserverfast.com/epr/key-lookup/"),

        ["RID Lookup"] = new(
            "RID Lookup (Heap)",
            "Lookup su una tabella heap a partire dal Row ID.",
            new[] { "Heap senza indice cluster + nonclustered = RID lookup." },
            new[] { "Heap raramente è la scelta giusta in OLTP: valuta clustered index." },
            "https://sqlserverfast.com/epr/rid-lookup/"),

        ["Nested Loops"] = new(
            "Nested Loops Join",
            "Per ogni riga del lato esterno cerca le corrispondenti del lato interno. Ottimo con poche righe esterne e seek interno indicizzato.",
            new[] { "Outer rows * cost-per-iteration: se outer è grande il costo esplode.",
                    "Verifica WithUnorderedPrefetch / WithOrderedPrefetch per I/O paralleli." },
            new[] { "Cardinality estimation errata sul lato esterno -> NL su molte righe = disastro.",
                    "OPTIMIZE FOR / parameter sniffing classico problema." },
            "https://sqlserverfast.com/epr/nested-loops/"),

        ["Hash Match"] = new(
            "Hash Match (Inner/Outer/Aggregate/Flow Distinct)",
            "Costruisce hash table sul lato build, poi sonda con il lato probe. Ottimo con grandi volumi non ordinati.",
            new[] { "Memory grant: spill to tempdb se sotto-stimato.",
                    "Bitmap filter sul probe (Bitmap operator) = optimized bitmap, ottimo segno." },
            new[] { "Spill = stima cardinalità rotta o statistiche obsolete.",
                    "Hash su tipi diversi = implicit conversion + hash diversa = risultati funny." },
            "https://sqlserverfast.com/epr/hash-match/"),

        ["Merge Join"] = new(
            "Merge Join",
            "Richiede entrambi i lati ordinati sulle colonne di join. Costo O(n+m).",
            new[] { "Many-To-Many=true => SQL aggiunge una worktable in tempdb." },
            new[] { "Serve ordinamento: se forzato da Sort, spesso peggio di Hash.",
                    "Many-To-Many con grandi volumi è insidioso." },
            "https://sqlserverfast.com/epr/merge-join/"),

        ["Sort"] = new(
            "Sort",
            "Ordina le righe in memoria; se non basta, spilla in tempdb.",
            new[] { "SortWarnings -> spill di livello N (multi-pass = molto male).",
                    "Distinct Sort è un Sort che fa anche dedup." },
            new[] { "Sort grande indica spesso ORDER BY non supportato da indice o ridistribuzione parallela.",
                    "MemoryFractions errato -> spill cronico." },
            "https://sqlserverfast.com/epr/sort/"),

        ["Stream Aggregate"] = new(
            "Stream Aggregate",
            "Aggrega su input già ordinato sui group-by; minima memoria.",
            Array.Empty<string>(),
            new[] { "Se appare un Sort prima dell'aggregate, spesso un indice giusto lo elimina." },
            "https://sqlserverfast.com/epr/stream-aggregate/"),

        ["Hash Match (Aggregate)"] = new(
            "Hash Aggregate",
            "Aggrega senza richiedere input ordinato; usa memoria + eventuale tempdb.",
            new[] { "Memory grant e spill come Hash Match join." },
            new[] { "Group by su tipo non sargable o expression -> hash aggregate forzato." },
            "https://sqlserverfast.com/epr/hash-match/"),

        ["Compute Scalar"] = new(
            "Compute Scalar",
            "Calcola un'espressione. Spesso 'lazy': l'espressione vera è valutata dall'operatore consumer.",
            Array.Empty<string>(),
            new[] { "Compute Scalar di tipo conversione = implicit conversion mascherata." },
            "https://sqlserverfast.com/epr/compute-scalar/"),

        ["Filter"] = new(
            "Filter",
            "Applica un predicato che il motore non ha potuto pushare nello Scan/Seek.",
            new[] { "Predicate property: leggilo per intero (qui sta la non-sargabilità)." },
            new[] { "Filter dopo uno Scan grande = non-sargable WHERE.",
                    "Filter su Startup Expression = subquery scalare ottimizzata." },
            "https://sqlserverfast.com/epr/filter/"),

        ["Eager Spool"] = new(
            "Eager Spool",
            "Materializza in tempdb tutto l'input prima di rilasciarlo a valle. Halloween protection o riuso.",
            Array.Empty<string>(),
            new[] { "Su DML (UPDATE/INSERT/DELETE) può essere Halloween protection, ma anche sintomo di scelte di piano subottimali." },
            "https://sqlserverfast.com/epr/eager-spool/"),

        ["Lazy Spool"] = new(
            "Lazy Spool",
            "Materializza in tempdb on-demand, una riga alla volta; spesso usato per riusare risultati.",
            Array.Empty<string>(),
            new[] { "Spool ricorrenti dentro nested loops = il pattern 'index spool' può segnalare assenza di un indice." },
            "https://sqlserverfast.com/epr/lazy-spool/"),

        ["Table Spool"] = new(
            "Index/Table Spool",
            "Spool che ricostruisce un indice on-the-fly in tempdb. Spesso surrogato di un indice mancante.",
            new[] { "Table Spool dentro un loop = indice mancante quasi sempre." },
            new[] { "EstimateRebinds vs EstimateRewinds: rewinds = riusa, rebinds = rimaterializza." },
            "https://sqlserverfast.com/epr/table-spool/"),

        ["Parallelism"] = new(
            "Parallelism (Exchange)",
            "Distribuisce/raccoglie righe fra thread. Tipi: Distribute Streams, Repartition Streams, Gather Streams.",
            new[] { "Skew: thread con ActualRows molto diversi = distribuzione non bilanciata." },
            new[] { "Gather Streams sopra a Sort di solito ok; Repartition prima di un join hash ok; ma Sort *sopra* Gather di solito reinforced sort.",
                    "Order Preserving Gather con TOP può serializzare l'intero piano." },
            "https://sqlserverfast.com/epr/parallelism/"),

        ["Constant Scan"] = new(
            "Constant Scan",
            "Genera 0 o N righe costanti; spesso usato come 'pin' per costruire il piano (es. Anti Semi Join NOT IN).",
            Array.Empty<string>(),
            Array.Empty<string>(),
            "https://sqlserverfast.com/epr/constant-scan/"),

        ["Top"] = new(
            "Top",
            "Restituisce solo le prime N righe; può essere blocking se preceduto da Sort.",
            new[] { "Sort + Top = Top N Sort ottimizzato (TopRowCount property)." },
            new[] { "TOP con ORDER BY su colonna non indicizzata = Sort completo del set, non un mini-sort." },
            "https://sqlserverfast.com/epr/top/"),

        ["Sequence Project"] = new(
            "Sequence Project",
            "Calcola funzioni di window (ROW_NUMBER, RANK, ...) basate sulla Segment property.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            "https://sqlserverfast.com/epr/sequence-project/"),

        ["Window Spool"] = new(
            "Window Spool",
            "Materializza una window per OVER(). On-disk se la finestra supera 10000 righe.",
            new[] { "Verifica se è on-disk vs in-memory (RowsetType nelle proprietà)." },
            new[] { "Window function su grandi partizioni può forzare spool on-disk pesanti." },
            "https://sqlserverfast.com/epr/window-spool/"),
    };

    public static Entry? Lookup(string physicalOp)
    {
        if (Map.TryGetValue(physicalOp, out var e)) return e;
        // Try case-insensitive partial
        foreach (var kv in Map)
            if (physicalOp.Equals(kv.Key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        return null;
    }
}

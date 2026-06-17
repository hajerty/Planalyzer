/* ============================================================
 * Esempi pronti da incollare nel tab "Certifica query" o
 * "Esegui & history" della Web UI per vedere il validator/analyzer.
 *
 * Tutti girano contro PlanalyzerDemo (creato dal seed.sql).
 * ============================================================ */

------------------------------------------------------------
-- 1) CERTIFICATA — Score atteso: 100/100
------------------------------------------------------------
SELECT TOP (100)
       o.OrderId, o.OrderDate, o.Total
FROM dbo.Orders AS o
WHERE o.OrderDate >= '20250101'
  AND o.OrderDate <  '20260101'
ORDER BY o.OrderDate DESC;

------------------------------------------------------------
-- 2) NON CERTIFICATA — viola VR.001, VR.002, VR.006, VR.007, VR.013
------------------------------------------------------------
SELECT *
FROM Orders o
JOIN Customers c ON YEAR(o.OrderDate) = 2025
                AND c.CustomerId = o.CustomerId
WHERE c.Email LIKE '%example.com'
  AND c.Name = NULL;

------------------------------------------------------------
-- 3) CRITICAL — viola VR.010 (UPDATE senza WHERE).
--    Il comando `run` la rifiuta SEMPRE (pre-flight).
------------------------------------------------------------
UPDATE dbo.Orders SET Status = 'New';

------------------------------------------------------------
-- 4) CRITICAL — viola VR.018 (EXEC con concatenazione).
------------------------------------------------------------
DECLARE @t SYSNAME = 'Orders';
EXEC ('SELECT TOP 1 * FROM dbo.' + @t);

------------------------------------------------------------
-- 5) HIGH ma giustificata — la justify la trasforma in non bloccante
------------------------------------------------------------
-- justify: VR.005 lettura dashboard, dirty read accettata
SELECT COUNT(*) FROM dbo.Orders WITH (NOLOCK);

------------------------------------------------------------
-- 6) Query "grande utente" — utile per il tab "Esegui & history":
--    fa Key Lookup pesante; il piano effettivo lo mostra.
------------------------------------------------------------
SELECT o.OrderId, o.Total, o.Status
FROM dbo.Orders AS o
WHERE o.OrderDate >= DATEADD(DAY, -30, SYSUTCDATETIME())
ORDER BY o.Total DESC;

------------------------------------------------------------
-- 7) Stessa query in versione "buona" — covering index suggerito
--    nei findings dell'analisi del piano.
------------------------------------------------------------
SELECT TOP (1000) o.OrderId, o.Total, o.Status
FROM dbo.Orders AS o
WHERE o.OrderDate >= DATEADD(DAY, -30, SYSUTCDATETIME())
ORDER BY o.OrderDate DESC;

/* ============================================================
 * Planalyzer demo seed
 * Crea il database PlanalyzerDemo con tabelle Customers / Orders /
 * OrderLines + una scalar UDF (usata per dimostrare VR.009) +
 * popolamento sufficiente a generare piani interessanti
 * (~1.000 customers, ~50.000 orders, ~150.000 order_lines).
 * ============================================================ */
SET NOCOUNT ON;

IF DB_ID('PlanalyzerDemo') IS NULL
BEGIN
    CREATE DATABASE PlanalyzerDemo;
END
GO

USE PlanalyzerDemo;
GO

/* Tabelle */
IF OBJECT_ID('dbo.OrderLines') IS NOT NULL DROP TABLE dbo.OrderLines;
IF OBJECT_ID('dbo.Orders')     IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID('dbo.Customers')  IS NOT NULL DROP TABLE dbo.Customers;
IF OBJECT_ID('dbo.fn_IsBigSpender') IS NOT NULL DROP FUNCTION dbo.fn_IsBigSpender;
GO

CREATE TABLE dbo.Customers (
    CustomerId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name       NVARCHAR(100) NOT NULL,
    Email      VARCHAR(120)  NOT NULL,
    Country    NVARCHAR(50)  NOT NULL,
    CreatedAt  DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Customers_Country ON dbo.Customers(Country);
GO

CREATE TABLE dbo.Orders (
    OrderId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CustomerId INT NOT NULL,
    OrderDate  DATETIME2(0) NOT NULL,
    Total      DECIMAL(12,2) NOT NULL,
    Status     VARCHAR(20) NOT NULL,
    CONSTRAINT FK_Orders_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId)
);
/* Indice volutamente NON covering: il piano dovra' fare key lookup quando
   si seleziona Total -- ottimo per dimostrare LOOKUP.HEAVY. */
CREATE INDEX IX_Orders_OrderDate ON dbo.Orders(OrderDate);
CREATE INDEX IX_Orders_CustomerId ON dbo.Orders(CustomerId);
GO

CREATE TABLE dbo.OrderLines (
    OrderLineId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    OrderId     INT NOT NULL,
    Sku         VARCHAR(40) NOT NULL,
    Quantity    INT NOT NULL,
    UnitPrice   DECIMAL(12,2) NOT NULL,
    CONSTRAINT FK_OrderLines_Order FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId)
);
CREATE INDEX IX_OrderLines_OrderId ON dbo.OrderLines(OrderId);
GO

/* Scalar UDF intenzionalmente non-inlinable (apre la finestra a VR.009 +
   non-sargability quando usata in WHERE). */
CREATE FUNCTION dbo.fn_IsBigSpender(@cid INT) RETURNS BIT
AS
BEGIN
    DECLARE @r BIT = 0;
    DECLARE @total DECIMAL(12,2);
    SELECT @total = SUM(Total) FROM dbo.Orders WHERE CustomerId = @cid;
    IF @total > 5000 SET @r = 1;
    RETURN @r;
END;
GO

/* Popolamento Customers (1.000 righe) */
;WITH N AS (
    SELECT TOP (1000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT dbo.Customers(Name, Email, Country)
SELECT
    CONCAT('Customer ', n),
    CONCAT('user', n, '@example.com'),
    CHOOSE(1 + (n % 5), 'IT', 'US', 'FR', 'DE', 'UK')
FROM N;
GO

/* Popolamento Orders (50.000 righe, 2 anni di storico) */
;WITH N AS (
    SELECT TOP (50000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT dbo.Orders(CustomerId, OrderDate, Total, Status)
SELECT
    1 + ABS(CHECKSUM(NEWID())) % 1000,
    DATEADD(MINUTE, -ABS(CHECKSUM(NEWID())) % (60 * 24 * 365 * 2), SYSUTCDATETIME()),
    CAST(RAND(CHECKSUM(NEWID())) * 5000 AS DECIMAL(12,2)),
    CHOOSE(1 + (n % 4), 'New', 'Paid', 'Shipped', 'Closed')
FROM N;
GO

/* Popolamento OrderLines (~3 per ordine, ~150.000 righe) */
;WITH N AS (
    SELECT TOP (150000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT dbo.OrderLines(OrderId, Sku, Quantity, UnitPrice)
SELECT
    1 + ABS(CHECKSUM(NEWID())) % 50000,
    CONCAT('SKU-', RIGHT('00000' + CAST(ABS(CHECKSUM(NEWID())) % 99999 AS VARCHAR(5)), 5)),
    1 + ABS(CHECKSUM(NEWID())) % 5,
    CAST(RAND(CHECKSUM(NEWID())) * 200 AS DECIMAL(12,2))
FROM N;
GO

UPDATE STATISTICS dbo.Customers WITH FULLSCAN;
UPDATE STATISTICS dbo.Orders WITH FULLSCAN;
UPDATE STATISTICS dbo.OrderLines WITH FULLSCAN;
GO

PRINT 'PlanalyzerDemo seed OK.';
PRINT '  Customers : 1.000';
PRINT '  Orders    : 50.000';
PRINT '  OrderLines: 150.000';
GO

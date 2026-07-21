USE [CespedVentas];
GO

IF COL_LENGTH(N'dbo.Ventas', N'MontoEntrega') IS NULL
BEGIN
    ALTER TABLE dbo.Ventas
        ADD MontoEntrega DECIMAL(18,2) NOT NULL
            CONSTRAINT DF_Ventas_MontoEntrega DEFAULT 0 WITH VALUES;
END;
GO

-- Las ventas históricas conservan cero porque no se conoce cuánto se recibió como entrega.
-- Las ventas nuevas exigen un importe mayor a cero desde la API.

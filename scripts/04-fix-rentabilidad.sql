USE [CespedVentas];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

ALTER TABLE dbo.Ventas ALTER COLUMN Margen DECIMAL(18,6) NOT NULL;

UPDATE v
SET
    CostoCompraTotal = calc.CostoCompraTotal,
    PrecioTotal = calc.PrecioTotal,
    Iva = calc.Iva,
    GananciaBruta = calc.GananciaBruta,
    GananciaNeta = calc.GananciaNeta,
    Margen = CASE WHEN calc.PrecioTotal = 0 THEN 0 ELSE calc.GananciaNeta / calc.PrecioTotal END
FROM dbo.Ventas v
INNER JOIN dbo.AlicuotasIva a ON a.Id = v.AlicuotaIvaId
CROSS APPLY
(
    SELECT
        v.CostoCompraUnitario * v.CantidadM2 AS CostoCompraTotal,
        v.PrecioUnitario * v.CantidadM2 AS PrecioTotal
) totals
CROSS APPLY
(
    SELECT
        totals.PrecioTotal * a.Porcentaje / 100 AS Iva,
        totals.PrecioTotal - totals.CostoCompraTotal - v.CostoEnvio - v.OtrosCostos AS GananciaBruta
) gross
CROSS APPLY
(
    SELECT totals.CostoCompraTotal, totals.PrecioTotal, gross.Iva, gross.GananciaBruta,
           gross.GananciaBruta - gross.Iva AS GananciaNeta
) calc;

COMMIT TRANSACTION;
GO

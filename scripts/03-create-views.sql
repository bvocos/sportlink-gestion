USE [CespedVentas];
GO

CREATE OR ALTER VIEW dbo.vw_SaldoCaja
AS
    SELECT CAST(COALESCE(SUM(CASE WHEN Tipo = N'Ingreso' THEN Monto ELSE -Monto END), 0) AS DECIMAL(18,2)) AS Saldo
    FROM dbo.MovimientosCaja;
GO

CREATE OR ALTER VIEW dbo.vw_RentabilidadVentas
AS
    SELECT
        v.Id AS VentaId,
        CONCAT(c.Nombre, N' ', c.Apellido) AS Cliente,
        v.PrecioTotal,
        v.CostoCompraTotal + v.CostoEnvio + v.OtrosCostos AS CostoTotal,
        v.GananciaBruta,
        v.GananciaNeta,
        v.Margen,
        CASE WHEN v.FormaPago = N'Cuotas' THEN COALESCE(p.TotalCobrado, 0) ELSE v.PrecioTotal END AS TotalCobrado,
        v.PrecioTotal - CASE WHEN v.FormaPago = N'Cuotas' THEN COALESCE(p.TotalCobrado, 0) ELSE v.PrecioTotal END AS TotalPendiente
    FROM dbo.Ventas v
    INNER JOIN dbo.Clientes c ON c.Id = v.ClienteId
    OUTER APPLY (SELECT SUM(cu.ImportePagado) AS TotalCobrado FROM dbo.Cuotas cu WHERE cu.VentaId = v.Id) p
    WHERE v.Estado <> N'Cancelada';
GO

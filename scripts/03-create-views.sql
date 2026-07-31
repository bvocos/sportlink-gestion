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
        v.MontoEntrega + COALESCE(p.TotalCobradoCuotas, 0) AS TotalCobrado,
        CASE WHEN v.PrecioTotal > v.MontoEntrega + COALESCE(p.TotalCobradoCuotas, 0)
            THEN v.PrecioTotal - v.MontoEntrega - COALESCE(p.TotalCobradoCuotas, 0) ELSE 0 END AS TotalPendiente,
        COALESCE(p.SaldoPendienteCuotas, 0) AS SaldoPendienteCuotas
    FROM dbo.Ventas v
    INNER JOIN dbo.Clientes c ON c.Id = v.ClienteId
    OUTER APPLY (SELECT SUM(cu.ImportePagado) AS TotalCobradoCuotas,
        SUM(cu.ImportePactado - cu.ImportePagado) AS SaldoPendienteCuotas
        FROM dbo.Cuotas cu WHERE cu.VentaId = v.Id) p
    WHERE v.Estado <> N'Cancelada';
GO

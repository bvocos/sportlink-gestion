-- Solo diagnóstico. No modifica datos.
SELECT
    s."Nombre" AS "Sucursal",
    v."Id",
    v."FechaVenta",
    COALESCE(NULLIF(c."RazonSocial", ''), CONCAT(c."Nombre", ' ', c."Apellido")) AS "Cliente",
    v."PrecioTotal" AS "TotalUsd",
    v."MontoEntrega" AS "EntregaUsd",
    v."PrecioTotal" - v."MontoEntrega" AS "DiferenciaUsd",
    v."FormaPago",
    v."Estado"
FROM "Ventas" v
JOIN "Sucursales" s ON s."Id" = v."SucursalId"
JOIN "Clientes" c ON c."Id" = v."ClienteId"
WHERE v."Estado" <> 'Cancelada'
  AND v."FormaPago" <> 'Cuotas'
  AND v."SeguimientoSaldo" = FALSE
  AND v."MontoEntrega" < v."PrecioTotal"
ORDER BY s."Nombre", v."FechaVenta", v."Id";

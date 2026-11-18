-- Diagnóstico de ventas canceladas que todavía conservan salidas de stock.
-- SOLO LECTURA: este archivo no modifica ni corrige datos.

-- Detalle de movimientos afectados.
SELECT
    v."Id" AS "VentaId",
    s."Nombre" AS "Sucursal",
    c."Nombre" || ' ' || c."Apellido" AS "Cliente",
    v."FechaVenta",
    d."Nombre" AS "Deposito",
    p."Nombre" AS "Producto",
    m."CantidadM2",
    i."Codigo" AS "CodigoImportacion",
    m."Color"
FROM "MovimientosStock" m
JOIN "Ventas" v ON v."Id" = m."VentaId"
JOIN "Sucursales" s ON s."Id" = v."SucursalId"
JOIN "Clientes" c ON c."Id" = v."ClienteId"
JOIN "Depositos" d ON d."Id" = m."DepositoId"
JOIN "TiposCesped" p ON p."Id" = m."TipoCespedId"
LEFT JOIN "Importaciones" i ON i."Id" = m."ImportacionId"
WHERE v."Estado" = 'Cancelada'
  AND m."Tipo" = 'SalidaPorVenta'
ORDER BY s."Nombre", v."FechaVenta", v."Id", p."Nombre";

-- Totales para dimensionar la corrección manual, agrupados por sucursal, depósito y producto.
SELECT
    s."Nombre" AS "Sucursal",
    d."Nombre" AS "Deposito",
    p."Nombre" AS "Producto",
    SUM(m."CantidadM2") AS "TotalM2"
FROM "MovimientosStock" m
JOIN "Ventas" v ON v."Id" = m."VentaId"
JOIN "Sucursales" s ON s."Id" = v."SucursalId"
JOIN "Depositos" d ON d."Id" = m."DepositoId"
JOIN "TiposCesped" p ON p."Id" = m."TipoCespedId"
WHERE v."Estado" = 'Cancelada'
  AND m."Tipo" = 'SalidaPorVenta'
GROUP BY s."Nombre", d."Nombre", p."Nombre"
ORDER BY s."Nombre", d."Nombre", p."Nombre";

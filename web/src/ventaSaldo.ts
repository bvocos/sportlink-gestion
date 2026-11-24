export function saldoVenta(total: number, entrega: number, cobros = 0) {
  return Math.max(0, Math.round((Number(total || 0) - Number(entrega || 0) - Number(cobros || 0)) * 100) / 100)
}

export function entregaAutomatica(total: number, formaPago: string, modificada: boolean, actual: number) {
  return formaPago !== 'Cuotas' && !modificada ? Math.max(0, Number(total || 0)) : Number(actual || 0)
}

export function equivalenteArs(importeUsd: number, cotizacion: number | null | undefined) {
  return cotizacion && cotizacion > 0 ? Math.round(importeUsd * cotizacion * 100) / 100 : null
}

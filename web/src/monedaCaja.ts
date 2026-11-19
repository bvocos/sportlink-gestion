export type MonedaCaja = 'USD' | 'ARS'

export function importeEnPesos(montoUsd: number, cotizacion: number | null | undefined) {
  if (!(cotizacion && cotizacion > 0)) return null
  return Math.round((Number(montoUsd) || 0) * cotizacion * 100) / 100
}

export function montoEnUsd(importe: number, moneda: MonedaCaja, cotizacion?: number | null) {
  if (moneda === 'USD') return Math.round((Number(importe) || 0) * 100) / 100
  if (!(cotizacion && cotizacion > 0)) return null
  return Math.round(((Number(importe) || 0) / cotizacion) * 100) / 100
}

export function validarCotizacion(moneda: MonedaCaja, cotizacion?: number | null) {
  return moneda === 'ARS' && !(cotizacion && cotizacion > 0)
    ? 'Ingresá la cotización del dólar con la que se cobró en pesos.'
    : null
}

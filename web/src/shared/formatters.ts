const arsFormatter = new Intl.NumberFormat('es-AR', {
  style: 'currency',
  currency: 'ARS',
  minimumFractionDigits: 0,
  maximumFractionDigits: 2
})

export function formatCurrency(value: number | null | undefined) {
  return arsFormatter.format(Number(value ?? 0))
}

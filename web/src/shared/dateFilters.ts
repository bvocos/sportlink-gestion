export function isoDate(date: Date = new Date()) {
  return date.toISOString().slice(0, 10)
}

export function parseIsoDate(value?: string | null) {
  if (!value) return null
  const parsed = new Date(`${value}T00:00:00`)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

export function monthRange() {
  const today = new Date()
  const start = new Date(today.getFullYear(), today.getMonth(), 1)
  return { desde: isoDate(start), hasta: isoDate(today) }
}

export function weekRange() {
  const end = new Date()
  const start = new Date(end)
  start.setDate(start.getDate() - 6)
  return { desde: isoDate(start), hasta: isoDate(end) }
}

export function sixMonthsRange() {
  const end = new Date()
  const start = new Date(end)
  start.setMonth(start.getMonth() - 6)
  return { desde: isoDate(start), hasta: isoDate(end) }
}

export function auditDefaultRange() {
  const end = new Date()
  const start = new Date(end)
  start.setDate(start.getDate() - 6)
  return { desde: isoDate(start), hasta: isoDate(end) }
}

export function formatDateRangeLabel(desde?: string, hasta?: string) {
  const fmt = (value: string) => new Date(`${value}T00:00:00`).toLocaleDateString('es-AR')
  if (desde && hasta) return `${fmt(desde)} al ${fmt(hasta)}`
  if (desde) return `Desde ${fmt(desde)}`
  if (hasta) return `Hasta ${fmt(hasta)}`
  return 'Sin filtro de fechas'
}

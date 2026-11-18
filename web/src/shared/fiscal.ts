export type TipoDocumentoCliente = 'Dni' | 'Cuit' | 'Cuil'
export type CondicionIvaCliente =
  | 'ConsumidorFinal' | 'ResponsableInscripto' | 'Monotributista' | 'Exento'
  | 'SujetoNoCategorizado' | 'MonotributistaSocial' | 'IvaNoAlcanzado'

export const tipoDocumentoOptions = [
  { label: 'DNI', value: 'Dni' },
  { label: 'CUIT', value: 'Cuit' },
  { label: 'CUIL', value: 'Cuil' },
]

export const condicionIvaOptions = [
  { label: 'Consumidor final', value: 'ConsumidorFinal' },
  { label: 'Responsable inscripto', value: 'ResponsableInscripto' },
  { label: 'Monotributista', value: 'Monotributista' },
  { label: 'Exento', value: 'Exento' },
  { label: 'Sujeto no categorizado', value: 'SujetoNoCategorizado' },
  { label: 'Monotributista social', value: 'MonotributistaSocial' },
  { label: 'IVA no alcanzado', value: 'IvaNoAlcanzado' },
]

export const condicionIvaLabels = condicionIvaOptions.map(x => x.label)

export function normalizarDocumento(value?: string | null): string | null {
  const digits = String(value ?? '').replace(/\D/g, '')
  return digits || null
}

export function esCuitValido(value: string): boolean {
  const digits = normalizarDocumento(value) ?? ''
  if (digits.length !== 11) return false
  const weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2]
  const sum = weights.reduce((total, weight, index) => total + Number(digits[index]) * weight, 0)
  let verifier = 11 - (sum % 11)
  if (verifier === 11) verifier = 0
  if (verifier === 10) return false
  return Number(digits[10]) === verifier
}

export function esDniValido(value: string): boolean {
  const digits = normalizarDocumento(value) ?? ''
  return digits.length === 7 || digits.length === 8
}

export function formatearDocumento(tipo: TipoDocumentoCliente, value: string): string {
  const digits = normalizarDocumento(value) ?? ''
  if ((tipo === 'Cuit' || tipo === 'Cuil') && digits.length === 11)
    return `${digits.slice(0, 2)}-${digits.slice(2, 10)}-${digits.slice(10)}`
  if (tipo === 'Dni' && (digits.length === 7 || digits.length === 8))
    return Number(digits).toLocaleString('es-AR')
  return digits
}

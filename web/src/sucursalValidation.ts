export function normalizeCuit(value: string) {
  return value.trim().replace(/[ -]/g, '')
}

export function validCuit(value: string) {
  const cuit = normalizeCuit(value)
  if (!/^\d{11}$/.test(cuit)) return false
  const weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2]
  const sum = weights.reduce((total, weight, index) => total + Number(cuit[index]) * weight, 0)
  let verifier = 11 - (sum % 11)
  if (verifier === 11) verifier = 0
  if (verifier === 10) verifier = 9
  return verifier === Number(cuit[10])
}

export function validateSucursalForm(form: { nombre: string; cuit?: string; puntoVentaAfip?: number | null }) {
  const errors: Record<string, string> = {}
  if (!form.nombre.trim()) errors.nombre = 'El nombre es obligatorio.'
  if (form.cuit?.trim() && !validCuit(form.cuit)) errors.cuit = 'Ingresá un CUIT válido de 11 dígitos.'
  if (form.puntoVentaAfip != null && (form.puntoVentaAfip < 1 || form.puntoVentaAfip > 99999))
    errors.puntoVentaAfip = 'El punto de venta debe estar entre 1 y 99999.'
  return errors
}

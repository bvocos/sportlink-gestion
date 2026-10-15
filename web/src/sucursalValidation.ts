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

export function validateSucursalLogo(file: { type: string; size: number }) {
  if (!['image/png', 'image/jpeg'].includes(file.type)) return 'El logo debe ser PNG o JPG.'
  if (file.size > 1024 * 1024) return 'El logo no puede superar 1 MB.'
  return ''
}

export function validateSucursalLogoDimensions(width: number, height: number) {
  return width > 4000 || height > 4000
    ? 'El logo no puede superar 4000 píxeles de ancho ni de alto.'
    : ''
}

export async function readAndValidateSucursalLogoDimensions(file: File) {
  if (typeof createImageBitmap === 'function') {
    const bitmap = await createImageBitmap(file)
    try { return validateSucursalLogoDimensions(bitmap.width, bitmap.height) }
    finally { bitmap.close() }
  }
  return await new Promise<string>((resolve, reject) => {
    const url = URL.createObjectURL(file)
    const image = new Image()
    image.onload = () => {
      URL.revokeObjectURL(url)
      resolve(validateSucursalLogoDimensions(image.naturalWidth, image.naturalHeight))
    }
    image.onerror = () => {
      URL.revokeObjectURL(url)
      reject(new Error('No se pudo leer la imagen.'))
    }
    image.src = url
  })
}

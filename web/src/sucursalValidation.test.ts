import { describe, expect, it } from 'vitest'
import { normalizeCuit, validCuit, validateSucursalForm, validateSucursalLogo } from './sucursalValidation'

describe('validación fiscal de sucursales', () => {
  it('normaliza y acepta un CUIT válido con guiones', () => {
    expect(normalizeCuit('20-12345678-6')).toBe('20123456786')
    expect(validCuit('20-12345678-6')).toBe(true)
  })
  it('rechaza CUIT y punto de venta inválidos', () => {
    const errors = validateSucursalForm({ nombre: 'Centro', cuit: '20-12345678-9', puntoVentaAfip: 100000 })
    expect(errors.cuit).toBeTruthy()
    expect(errors.puntoVentaAfip).toBeTruthy()
  })
  it('rechaza logos con tipo o tamaño inválidos', () => {
    expect(validateSucursalLogo({ type: 'image/svg+xml', size: 100 })).toContain('PNG')
    expect(validateSucursalLogo({ type: 'image/png', size: 1024 * 1024 + 1 })).toContain('1 MB')
    expect(validateSucursalLogo({ type: 'image/jpeg', size: 100 })).toBe('')
  })
})

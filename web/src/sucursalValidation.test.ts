import { describe, expect, it } from 'vitest'
import { normalizeCuit, validCuit, validateSucursalForm } from './sucursalValidation'

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
})

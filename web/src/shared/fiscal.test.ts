import { describe, expect, it } from 'vitest'
import { esCuitValido, esDniValido, formatearDocumento, normalizarDocumento } from './fiscal'

describe('datos fiscales', () => {
  it('normaliza documentos', () => {
    expect(normalizarDocumento('20-12345678-6')).toBe('20123456786')
    expect(normalizarDocumento(' 20.123.456 ')).toBe('20123456')
    expect(normalizarDocumento('')).toBeNull()
  })

  it('valida CUIT y DNI', () => {
    expect(esCuitValido('20-12345678-6')).toBe(true)
    expect(esCuitValido('20-12345678-5')).toBe(false)
    expect(esDniValido('1234567')).toBe(true)
    expect(esDniValido('12345678')).toBe(true)
    expect(esDniValido('123456')).toBe(false)
    expect(esDniValido('123456789')).toBe(false)
  })

  it('formatea documentos', () => {
    expect(formatearDocumento('Cuit', '20123456786')).toBe('20-12345678-6')
    expect(formatearDocumento('Dni', '12345678')).toBe('12.345.678')
  })
})

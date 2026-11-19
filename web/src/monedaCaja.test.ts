import { describe, expect, it } from 'vitest'
import { importeEnPesos, montoEnUsd, validarCotizacion } from './monedaCaja'

describe('moneda de caja', () => {
  it('convierte USD a ARS con redondeo', () => expect(importeEnPesos(10.125, 1250)).toBe(12656.25))
  it('convierte ARS a USD con redondeo', () => expect(montoEnUsd(1500, 'ARS', 1250)).toBe(1.2))
  it('requiere cotización en ARS', () => expect(validarCotizacion('ARS', 0)).toContain('cotización'))
  it('no requiere cotización en USD', () => expect(validarCotizacion('USD', null)).toBeNull())
})

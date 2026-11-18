import { describe, expect, it } from 'vitest'
import { receptionLineErrors, receptionTotal } from './recepcionImportacion'

describe('recepción de importación', () => {
  it('suma cantidades normales y rollos', () => {
    expect(receptionTotal([
      { controlPorLotes: false, cantidadM2: 25, rollos: [] },
      { controlPorLotes: true, cantidadM2: 999, rollos: [
        { posicion: 'A', codigoBarra: 'A', cantidadM2: 10 },
        { posicion: 'B', codigoBarra: 'B', cantidadM2: 11 },
        { posicion: 'C', codigoBarra: 'C', cantidadM2: 12 },
      ] },
    ])).toBe(58)
  })

  it('detecta cantidades inválidas y códigos repetidos', () => {
    expect(receptionLineErrors({ controlPorLotes: false, cantidadM2: 0, rollos: [] })).not.toHaveLength(0)
    expect(receptionLineErrors({ controlPorLotes: true, cantidadM2: 0, rollos: [
      { posicion: 'A', codigoBarra: 'X', cantidadM2: 1 },
      { posicion: 'B', codigoBarra: 'x', cantidadM2: 1 },
      { posicion: 'C', codigoBarra: 'C', cantidadM2: 1 },
    ] })).toContain('Los códigos de barra no pueden repetirse.')
  })
})

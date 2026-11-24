import { describe, expect, it } from 'vitest'
import { entregaAutomatica, equivalenteArs, saldoVenta } from './ventaSaldo'

describe('ventaSaldo', () => {
  it('calcula el saldo sin permitir negativos', () => {
    expect(saldoVenta(1000, 200, 300)).toBe(500)
    expect(saldoVenta(100, 80, 50)).toBe(0)
  })

  it('precarga el total salvo cuotas o edición manual', () => {
    expect(entregaAutomatica(250, 'Contado', false, 0)).toBe(250)
    expect(entregaAutomatica(250, 'Contado', true, 90)).toBe(90)
    expect(entregaAutomatica(250, 'Cuotas', false, 40)).toBe(40)
  })

  it('calcula el equivalente en pesos sin inventar cotización', () => {
    expect(equivalenteArs(10, 1200)).toBe(12000)
    expect(equivalenteArs(10, null)).toBeNull()
  })
})

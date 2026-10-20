import { describe, expect, it } from 'vitest'
import { isDownPaymentSuggestionEnabled, resolveSuggestedDownPayment } from './presupuestoEntrega'

describe('entrega inicial sugerida de presupuestos', () => {
  it('habilita la sugerencia automática únicamente para San Francisco', () => {
    expect(isDownPaymentSuggestionEnabled('7b100000-0000-0000-0000-000000000001')).toBe(true)
    expect(isDownPaymentSuggestionEnabled('7B100000-0000-0000-0000-000000000001')).toBe(true)
    expect(isDownPaymentSuggestionEnabled('7b100000-0000-0000-0000-000000000002')).toBe(false)
    expect(isDownPaymentSuggestionEnabled(null)).toBe(false)
  })

  it('aplica la sugerencia mientras el campo permanece automático', () => {
    expect(resolveSuggestedDownPayment(0, 255, 500, false)).toEqual({
      suggested: 255,
      value: 255,
      apply: true,
    })
  })

  it('conserva el valor cuando fue modificado manualmente', () => {
    expect(resolveSuggestedDownPayment(180, 255, 500, true)).toEqual({
      suggested: 255,
      value: 180,
      apply: false,
    })
  })

  it('limita la sugerencia al total financiado', () => {
    expect(resolveSuggestedDownPayment(0, 600, 450, false)).toEqual({
      suggested: 450,
      value: 450,
      apply: true,
    })
  })
})

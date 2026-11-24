import { describe, expect, it } from 'vitest'
import { apiErrorMessage } from './httpClient'

describe('apiErrorMessage', () => {
  it('prioriza los mensajes de ValidationProblem', () => {
    expect(apiErrorMessage({ response: { status: 400, data: { title: 'One or more validation errors occurred.', errors: { fecha: ['Fecha inválida'], importe: ['Importe inválido'] } } } })).toBe('Fecha inválida · Importe inválido')
  })
  it('lee message y conserva el mensaje seguro de errores 500', () => {
    expect(apiErrorMessage({ response: { status: 409, data: { message: 'Conflicto claro' } } })).toBe('Conflicto claro')
    expect(apiErrorMessage({ response: { status: 500, data: {} } })).toContain('servidor encontró un error')
  })
})

import { afterEach, describe, expect, it, vi } from 'vitest'
import { isoDate } from './dateFilters'

describe('isoDate Argentina', () => {
  afterEach(() => vi.useRealTimers())
  it('mantiene la fecha de negocio argentina a las 22:30', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-25T01:30:00.000Z'))
    expect(isoDate()).toBe('2026-11-24')
  })
})

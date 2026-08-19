import { describe, expect, it } from 'vitest'
import { duration, statusLabel } from './system'
describe('system observability presentation', () => {
  it('covers every operational state', () => expect(Object.values(statusLabel)).toEqual(['Saudável','Degradado','Indisponível','Desconhecido','Desabilitado']))
  it('formats queue age', () => { expect(duration(37)).toBe('37s'); expect(duration(125)).toBe('2min'); expect(duration(undefined)).toBe('—') })
})

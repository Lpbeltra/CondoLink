import { describe, expect, it, vi } from 'vitest'
const get = vi.hoisted(() => vi.fn())
vi.mock('../services/api', () => ({ api: { get } }))
import { downloadSystemDiagnostic, duration, statusLabel } from './system'
describe('system observability presentation', () => {
  it('covers every operational state', () => expect(Object.values(statusLabel)).toEqual(['Saudável','Degradado','Crítico','Desconhecido','Desabilitado']))
  it('formats queue age', () => { expect(duration(37)).toBe('37s'); expect(duration(125)).toBe('2min'); expect(duration(undefined)).toBe('—') })
  it('downloads through authenticated HTTP and revokes the object URL', async () => {
    const blob = new Blob(['diagnostic'])
    get.mockResolvedValue({ data: blob, headers: { 'content-disposition': 'attachment; filename="comvy-diagnostico-2026-08-18-120000.txt"' } })
    const create = vi.fn(() => 'blob:diagnostic'); const revoke = vi.fn()
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: create })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revoke })
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    await downloadSystemDiagnostic()
    expect(get).toHaveBeenCalledWith('/overwatch/system/diagnostic', { responseType: 'blob' })
    expect(create).toHaveBeenCalledWith(blob); expect(click).toHaveBeenCalled(); expect(revoke).toHaveBeenCalledWith('blob:diagnostic')
  })
})

import { beforeEach, describe, expect, it, vi } from 'vitest'

const http = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() }))
vi.mock('../services/api', () => ({ api: http }))
import { completeAgendaReminder, deleteAgendaReminder, getAgendaOptions, listAgenda, reactivateAgendaReminder, saveAgendaReminder } from './api'

describe('agenda API', () => {
  beforeEach(() => { vi.clearAllMocks(); http.get.mockResolvedValue({ data: [] }); http.post.mockResolvedValue({ data: { id: 'a1' } }); http.put.mockResolvedValue({ data: { id: 'a1' } }); http.delete.mockResolvedValue({}) })
  const input = { title: 'Elevadores', description: null, unitId: null, relatedThirdParty: null, startsAtUtc: '2026-08-25T12:00:00.000Z', recurrenceType: 'Weekly' as const, notifyByWhatsApp: true, notifyByEmail: false, requestIds: ['r1'] }
  it('scopes list and options by condominium', async () => { await listAgenda('c1', 'upcoming', 'elevador'); await getAgendaOptions('c1', 'a1'); expect(http.get).toHaveBeenNthCalledWith(1, '/management/condominiums/c1/agenda', { params: { view: 'upcoming', search: 'elevador' } }); expect(http.get).toHaveBeenNthCalledWith(2, '/management/condominiums/c1/agenda/options', { params: { reminderId: 'a1' } }) })
  it('creates, edits and deletes through the same resource', async () => { await saveAgendaReminder('c1', input); await saveAgendaReminder('c1', input, 'a1'); await deleteAgendaReminder('c1', 'a1'); expect(http.post).toHaveBeenCalledWith('/management/condominiums/c1/agenda', input); expect(http.put).toHaveBeenCalledWith('/management/condominiums/c1/agenda/a1', input); expect(http.delete).toHaveBeenCalledWith('/management/condominiums/c1/agenda/a1') })
  it('completes and reactivates explicitly', async () => { await completeAgendaReminder('c1', 'a1'); await reactivateAgendaReminder('c1', 'a1'); expect(http.post).toHaveBeenNthCalledWith(1, '/management/condominiums/c1/agenda/a1/complete'); expect(http.post).toHaveBeenNthCalledWith(2, '/management/condominiums/c1/agenda/a1/reactivate') })
})

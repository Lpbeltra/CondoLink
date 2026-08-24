import { describe, expect, it } from 'vitest'
import { filterAgendaRequests, toggleAgendaRequest } from './AgendaReminderDialog'
import type { AgendaRequestOption } from './types'

const requests: AgendaRequestOption[] = [
  { id: 'r1', protocol: '3250FA3F', title: 'Solicitação de TAG', residentName: 'Creuza', block: '1', unitIdentifier: '1201', status: 'InProgress', linkedReminderId: null },
  { id: 'r2', protocol: '8F21C2A0', title: 'Portão', residentName: 'João', block: null, unitIdentifier: '22', status: 'Open', linkedReminderId: null },
]

describe('agenda request checklist', () => {
  it('searches protocol, resident and unit with accents ignored', () => {
    expect(filterAgendaRequests(requests, '3250').map(x => x.id)).toEqual(['r1'])
    expect(filterAgendaRequests(requests, 'Joao').map(x => x.id)).toEqual(['r2'])
    expect(filterAgendaRequests(requests, '1201').map(x => x.id)).toEqual(['r1'])
  })
  it('keeps the request of origin fixed while allowing multiple links', () => {
    expect(toggleAgendaRequest(['r1'], 'r1', 'r1')).toEqual(['r1'])
    expect(toggleAgendaRequest(['r1'], 'r2', 'r1')).toEqual(['r1', 'r2'])
  })
})

import { api } from '../services/api'
import type { AgendaInput, AgendaOptions, AgendaReminder } from './types'

const root = (condominiumId: string) => `/management/condominiums/${condominiumId}/agenda`
export async function listAgenda(condominiumId: string, view: string, search: string) {
  return (await api.get<AgendaReminder[]>(root(condominiumId), { params: { view, search } })).data
}
export async function getAgendaOptions(condominiumId: string, reminderId?: string) {
  return (await api.get<AgendaOptions>(`${root(condominiumId)}/options`, { params: { reminderId } })).data
}
export async function saveAgendaReminder(condominiumId: string, input: AgendaInput, reminderId?: string) {
  return (await (reminderId ? api.put<{ id: string }>(`${root(condominiumId)}/${reminderId}`, input) : api.post<{ id: string }>(root(condominiumId), input))).data
}
export async function deleteAgendaReminder(condominiumId: string, reminderId: string) {
  await api.delete(`${root(condominiumId)}/${reminderId}`)
}

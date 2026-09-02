import { api } from '../../services/api'
import type { PixKeyType } from '../components/PixFields'
export type { PixKeyType } from '../components/PixFields'
export interface SubManager { id: string; fullName: string; email: string; phoneNumber: string | null; condominiumId: string; condominiumName: string; isActive: boolean; hasActiveLink: boolean; pixKeyType: PixKeyType | null; pixKey: string | null }
export interface SubManagerInput { fullName: string; email: string; phoneNumber: string | null; condominiumId: string; pixKeyType: PixKeyType | null; pixKey: string | null }
export async function listSubManagers() { return (await api.get<SubManager[]>('/overwatch/submanagers')).data }
export async function createSubManager(input: SubManagerInput) { return (await api.post<SubManager & { temporaryPassword: string }>('/overwatch/submanagers', input)).data }
export async function setSubManagerStatus(id: string, isActive: boolean) { await api.patch(`/overwatch/submanagers/${id}/status`, { isActive }) }
export const subManagerModules = ['Requests', 'Attendance', 'ManagementCompany', 'Agenda', 'Assistant', 'Documents', 'Management'] as const
export type SubManagerModule = typeof subManagerModules[number]
export async function listSubManagerPermissions(id: string) { return (await api.get<{ module: SubManagerModule; allowed: boolean }[]>(`/overwatch/submanagers/${id}/permissions`)).data }
export async function updateSubManagerPermissions(id: string, permissions: { module: SubManagerModule; allowed: boolean }[]) { await api.put(`/overwatch/submanagers/${id}/permissions`, { permissions }) }
export async function resendSubManagerFirstAccess(item: SubManager, channel = 'WhatsAppAndEmail') { return (await api.post(`/condominiums/${item.condominiumId}/members/${item.id}/first-access/resend`, { channel })).data as { status: string; emailSent?: boolean; whatsappQueued?: boolean } }
export async function resetSubManagerPassword(item: SubManager) { return (await api.post<{ temporaryPassword: string }>(`/condominiums/${item.condominiumId}/members/${item.id}/reset-temporary-password`)).data }

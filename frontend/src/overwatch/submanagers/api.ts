import { api } from '../../services/api'
import type { PixKeyType } from '../components/PixFields'
export type { PixKeyType } from '../components/PixFields'
export interface SubManager { id: string; fullName: string; email: string; phoneNumber: string | null; condominiumId: string; condominiumName: string; isActive: boolean; hasActiveLink: boolean; pixKeyType: PixKeyType | null; pixKey: string | null }
export interface SubManagerInput { fullName: string; email: string; phoneNumber: string | null; condominiumId: string; pixKeyType: PixKeyType | null; pixKey: string | null }
export async function listSubManagers() { return (await api.get<SubManager[]>('/overwatch/submanagers')).data }
export async function createSubManager(input: SubManagerInput) { return (await api.post<SubManager & { temporaryPassword: string }>('/overwatch/submanagers', input)).data }
export async function setSubManagerStatus(id: string, isActive: boolean) { await api.patch(`/overwatch/submanagers/${id}/status`, { isActive }) }

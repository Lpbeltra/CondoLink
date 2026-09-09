import { api } from '../../services/api'

export interface OverwatchServiceProvider { id: string; name: string; phone: string; specialties: string[]; isActive: boolean; personalOwners: string[]; condominiums: string[]; createdAt: string }
export interface ServiceProviderDeletionImpact { personalLinks: number; condominiumLinks: number; currentRequests: number; historicalRecords: number; paymentRequests: number }
export async function listOverwatchServiceProviders(search = '') { return (await api.get<OverwatchServiceProvider[]>('/overwatch/service-providers', { params: search ? { search } : undefined })).data }
export async function getServiceProviderDeletionImpact(id: string) { return (await api.get<ServiceProviderDeletionImpact>(`/overwatch/service-providers/${id}/deletion-impact`)).data }
export async function deleteOverwatchServiceProvider(id: string) { await api.delete(`/overwatch/service-providers/${id}`, { data: { confirmation: 'EXCLUIR PERMANENTEMENTE' } }) }

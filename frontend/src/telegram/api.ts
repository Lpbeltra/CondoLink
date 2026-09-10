import { api } from '../services/api'

export interface TelegramStatus {
  enabled: boolean
  botUsername: string | null
  linked: boolean
  linkedAt: string | null
  activeCondominiumName: string | null
}
export interface TelegramLinkCode { code: string; deepLink: string | null; expiresAt: string }

export async function getTelegramStatus() {
  return (await api.get<TelegramStatus>('/users/me/telegram')).data
}
export async function createTelegramLinkCode() {
  return (await api.post<TelegramLinkCode>('/users/me/telegram/link-code')).data
}
export async function unlinkTelegram() { await api.delete('/users/me/telegram') }

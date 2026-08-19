import { api } from '../services/api'
export type HealthState = 'Healthy' | 'Degraded' | 'Unhealthy' | 'Unknown' | 'Disabled'
export interface AiMetrics { calls: number; failures: number; successRate?: number; averageLatencyMs?: number; p95LatencyMs?: number; inputTokens: number; outputTokens: number; totalTokens: number }
export interface SystemStatus {
  generatedAt: string; globalStatus: HealthState
  components: { name: string; status: HealthState; detail: string }[]
  activity24h: { requestsCreated: number; whatsappReceived: number; whatsappSent: number; aiCalls: number; operationalErrors: number }
  workers: { workerName: string; instanceId: string; status: HealthState; enabled: boolean; lastHeartbeatAt?: string; lastCompletedAt?: string; lastSucceeded?: boolean; lastProcessedItems?: number; lastResultCode?: string }[]
  whatsapp: { status: HealthState; queued: number; sending: number; failed: number; delivered: number; read: number; failed24h: number; sent24h: number; oldestQueuedAgeSeconds?: number; lastWebhookReceived?: string }
  ai: { status: HealthState; configured: boolean; periods: { period: string; metrics: AiMetrics }[]; breakdown: { operation: string; model?: string; metrics: AiMetrics }[] }
  email: { status: HealthState; enabled: boolean; configured: boolean; lastSend?: string; failures24h: number; successes24h: number }
  recentEvents: { timestamp: string; component: string; category: string; severity: string; reasonCode: string; correlationId?: string }[]
}
export async function getSystemStatus() { return (await api.get<SystemStatus>('/overwatch/system')).data }
export const statusLabel: Record<HealthState, string> = { Healthy: 'Saudável', Degraded: 'Degradado', Unhealthy: 'Indisponível', Unknown: 'Desconhecido', Disabled: 'Desabilitado' }
export function duration(seconds?: number) { if (seconds == null) return '—'; if (seconds < 60) return `${seconds}s`; if (seconds < 3600) return `${Math.floor(seconds / 60)}min`; return `${Math.floor(seconds / 3600)}h` }

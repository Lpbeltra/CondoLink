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
  performance: { periods: PerformancePeriod[]; topSlowest: EndpointPerformance[] }
}
export interface PerformancePeriod { period:string; requests:number; averageMs:number; p95Ms:number; errors5xx:number; errorRate5xx:number; sampleSmall:boolean; averageResponseBytes:number; averageQueries:number; slowQueries:number }
export interface EndpointPerformance { method:string; route:string; calls:number; averageMs:number; p95Ms:number; errors5xx:number; averageQueries:number; maximumQueries:number; slowQueries:number; averageResponseBytes:number; isHeavyOperation:boolean; sampleSmall:boolean }
export async function getSystemStatus() { return (await api.get<SystemStatus>('/overwatch/system')).data }
export async function downloadSystemDiagnostic() {
  const response = await api.get<Blob>('/overwatch/system/diagnostic', { responseType: 'blob' })
  const disposition = String(response.headers?.['content-disposition'] ?? '')
  const filename = /filename\*?=(?:UTF-8''|"?)([^";]+)/i.exec(disposition)?.[1]
  const url = URL.createObjectURL(response.data)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename ? decodeURIComponent(filename.replace(/"/g, '')) : 'comvy-diagnostico.txt'
  document.body.appendChild(anchor); anchor.click(); anchor.remove(); URL.revokeObjectURL(url)
}
export const statusLabel: Record<HealthState, string> = { Healthy: 'Saudável', Degraded: 'Degradado', Unhealthy: 'Crítico', Unknown: 'Desconhecido', Disabled: 'Desabilitado' }
export function duration(seconds?: number) { if (seconds == null) return '—'; if (seconds < 60) return `${seconds}s`; if (seconds < 3600) return `${Math.floor(seconds / 60)}min`; return `${Math.floor(seconds / 3600)}h` }

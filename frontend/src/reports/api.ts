import { api } from '../services/api'
import type { RequestReport } from './types'

export async function getRequestReport(days: number, condominiumId?: string) {
  const params = condominiumId ? { days, condominiumId } : { days }
  return (
    await api.get<RequestReport>('/management/reports/requests', { params })
  ).data
}

import { api } from '../services/api'
import type { RequestReport } from './types'

export async function getRequestReport(days: number) {
  return (
    await api.get<RequestReport>('/management/reports/requests', { params: { days } })
  ).data
}

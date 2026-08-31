import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { clearStoredToken, getStoredToken, storeToken } from '../auth/authStorage'

export const api = axios.create({ baseURL: import.meta.env.VITE_API_URL || '/api', timeout: 15_000, withCredentials: true })
type RetryConfig = InternalAxiosRequestConfig & { _refreshRetried?: boolean }
let refreshFlight: Promise<string> | null = null

export function setAccessToken(token: string | null) {
  if (token) { storeToken(token); api.defaults.headers.common.Authorization = `Bearer ${token}` }
  else { clearStoredToken(); delete api.defaults.headers.common.Authorization }
}

export async function refreshAccessToken() {
  if (!refreshFlight) refreshFlight = api.post<{ accessToken: string }>('/auth/refresh', undefined, { _refreshRetried: true } as RetryConfig)
    .then(({ data }) => { setAccessToken(data.accessToken); return data.accessToken })
    .finally(() => { refreshFlight = null })
  return refreshFlight
}

api.interceptors.request.use(config => {
  const token = getStoredToken()
  if (token && !config.headers.Authorization) config.headers.Authorization = `Bearer ${token}`
  return config
})
api.interceptors.response.use(response => response, async (error: AxiosError) => {
  const config = error.config as RetryConfig | undefined
  const isRefresh = config?.url?.includes('/auth/refresh')
  if (error.response?.status === 401 && config && !config._refreshRetried && !isRefresh) {
    config._refreshRetried = true
    try { const token = await refreshAccessToken(); config.headers.Authorization = `Bearer ${token}`; return api.request(config) }
    catch { setAccessToken(null); window.dispatchEvent(new Event('condolink:unauthorized')) }
  }
  return Promise.reject(error)
})

export function getErrorMessageForStatus(status:number|undefined){if(status===undefined)return'Não foi possível conectar ao servidor. Tente novamente.';if(status===400)return'Os dados informados são inválidos. Revise e tente novamente.';if(status===401)return'Sua sessão expirou. Entre novamente.';if(status===403)return'Você não possui permissão para realizar esta ação.';if(status===404)return'O conteúdo solicitado não foi encontrado.';if(status===409)return'A operação não pôde ser concluída devido ao estado atual dos dados.';if(status>=500)return'A Comvy está temporariamente indisponível.';return'Não foi possível concluir esta ação. Verifique os dados e tente novamente.'}
export function getErrorMessage(error:unknown){return axios.isAxiosError(error)?getErrorMessageForStatus(error.response?.status):'Não foi possível concluir esta ação. Verifique os dados e tente novamente.'}

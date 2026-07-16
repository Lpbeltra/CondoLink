import axios from 'axios'

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      window.dispatchEvent(new Event('condolink:unauthorized'))
    }
    return Promise.reject(error)
  },
)

export function getErrorMessage(error: unknown) {
  if (axios.isAxiosError<{ error?: string }>(error)) {
    if (!error.response) return 'Não foi possível conectar ao CondoLink. Tente novamente.'
    if (error.response.status >= 500) return 'O CondoLink está temporariamente indisponível.'
  }
  return 'Não foi possível concluir esta ação. Verifique os dados e tente novamente.'
}

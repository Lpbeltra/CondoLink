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
    if (error.response.status === 400) return 'Os dados informados são inválidos. Revise e tente novamente.'
    if (error.response.status === 401) return 'Sua sessão expirou. Entre novamente.'
    if (error.response.status === 403) return 'Você não possui permissão para realizar esta ação.'
    if (error.response.status === 404) return 'O conteúdo solicitado não foi encontrado.'
    if (error.response.status === 409) return 'A operação não pôde ser concluída devido ao estado atual dos dados.'
    if (error.response.status >= 500) return 'O CondoLink está temporariamente indisponível.'
  }
  return 'Não foi possível concluir esta ação. Verifique os dados e tente novamente.'
}

import axios from 'axios'

export function authError(error: unknown) {
  if (axios.isAxiosError<{ error?: string }>(error)) {
    const message = error.response?.data?.error
    if (typeof message === 'string' && message.trim()) return message.trim()
    if (!error.response) {
      return 'Não foi possível conectar ao CondoLink. Tente novamente.'
    }
    if (error.response.status === 401) return 'E-mail ou senha inválidos.'
    if (error.response.status === 403) return 'Esta conta não pode acessar o CondoLink.'
  }
  return 'Não foi possível concluir esta ação. Tente novamente.'
}

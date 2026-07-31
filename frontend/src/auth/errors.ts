import axios from 'axios'

export function authError(error: unknown) {
  if (axios.isAxiosError<{ error?: string; code?: string }>(error)) {
    const message = error.response?.data?.error?.toLowerCase() ?? ''
    const code = error.response?.data?.code?.toLowerCase() ?? ''
    if (message.includes('inativ') || code.includes('inactive')) {
      return 'Seu acesso está inativo. Entre em contato com o responsável pelo condomínio.'
    }
    if (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT') {
      return 'O servidor demorou para responder. Tente novamente.'
    }
    if (!error.response) {
      return 'Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.'
    }
    if (error.response.status === 400 || error.response.status === 401) {
      return 'E-mail ou senha incorretos. Confira os dados e tente novamente.'
    }
    if (error.response.status >= 500) {
      return 'Não foi possível concluir o acesso agora. Tente novamente em alguns instantes.'
    }
  }
  return 'Não foi possível concluir o acesso agora. Tente novamente em alguns instantes.'
}

export function whatsAppAuthError(error: unknown) {
  if (axios.isAxiosError<{ error?: string; code?: string }>(error)) {
    const code = error.response?.data?.code
    if (code === 'code_expired')
      return 'O código expirou. Solicite um novo código.'
    if (code === 'attempts_exhausted')
      return 'O limite de tentativas foi atingido. Solicite um novo código.'
    if (code === 'code_consumed')
      return 'Este código não está mais disponível. Solicite outro.'
    if (code === 'invalid_code')
      return 'Telefone ou código inválido.'
    if (code === 'whatsapp_unavailable')
      return 'O login pelo WhatsApp está temporariamente indisponível.'
    if (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT')
      return 'O servidor demorou para responder. Tente novamente.'
    if (!error.response)
      return 'Não foi possível conectar ao servidor. Verifique sua conexão.'
  }
  return 'Não foi possível concluir o acesso agora. Tente novamente.'
}

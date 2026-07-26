import axios from 'axios'

interface ErrorBody {
  message?: string
  error?: string
  detail?: string
  title?: string
  errors?: Record<string, string[]>
}

function responseMessage(error: unknown) {
  if (!axios.isAxiosError<ErrorBody>(error)) return null
  const body = error.response?.data
  if (!body) return null
  if (body.message?.trim()) return body.message.trim()
  if (body.error?.trim()) return body.error.trim()
  if (body.detail?.trim()) return body.detail.trim()
  const messages = Object.values(body.errors ?? {}).flat()
  return messages.length ? messages.join(' ') : body.title?.trim() || null
}

export function condominiumError(error: unknown) {
  if (!axios.isAxiosError(error)) return 'Não foi possível concluir a operação. Tente novamente.'

  switch (error.response?.status) {
    case 400: {
      const message = responseMessage(error)
      if (message?.includes('Name')) return 'Informe um nome válido para o condomínio.'
      return message ?? 'Revise os dados do condomínio e tente novamente.'
    }
    case 401:
      return 'Sua sessão expirou. Entre novamente.'
    case 403:
      return 'Você não possui permissão para gerenciar condomínios.'
    case 404:
      return responseMessage(error)?.includes('Management company')
        ? 'A administradora selecionada não foi encontrada.'
        : 'O condomínio solicitado não foi encontrado.'
    case 409:
      return 'Já existe um condomínio com este nome.'
    default:
      return 'Não foi possível concluir a operação. Tente novamente.'
  }
}

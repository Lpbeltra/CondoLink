import axios from 'axios'

interface ErrorBody {
  message?: string
  detail?: string
  error?: string
  title?: string
  errors?: Record<string, string[]>
}

export function managerError(error: unknown) {
  if (!axios.isAxiosError<ErrorBody>(error)) {
    return 'Não foi possível concluir a operação. Tente novamente.'
  }
  const body = error.response?.data
  const message = body?.message || body?.detail || body?.error ||
    Object.values(body?.errors ?? {}).flat().join(' ') || body?.title
  switch (error.response?.status) {
    case 400:
      return message || 'Os dados informados são inválidos.'
    case 401:
      return 'Sua sessão expirou. Entre novamente.'
    case 403:
      return 'Você não possui permissão para gerenciar síndicos.'
    case 404:
      return message || 'O síndico ou condomínio não foi encontrado.'
    case 409:
      return message?.includes('already associated')
        ? 'Este síndico já está vinculado ao condomínio.'
        : message || 'Já existe um usuário cadastrado com este e-mail.'
    default:
      return 'Não foi possível concluir a operação. Tente novamente.'
  }
}

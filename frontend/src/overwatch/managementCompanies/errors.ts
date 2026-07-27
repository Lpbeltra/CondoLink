import axios from 'axios'

interface ErrorBody {
  message?: string
  detail?: string
  title?: string
  errors?: Record<string, string[]>
}

function getResponseMessage(error: unknown) {
  if (!axios.isAxiosError<ErrorBody>(error)) return null
  const body = error.response?.data
  if (!body) return null
  if (body.message?.trim()) return body.message.trim()
  if (body.detail?.trim()) return body.detail.trim()
  const validationMessages = Object.values(body.errors ?? {}).flat()
  if (validationMessages.length) return validationMessages.join(' ')
  return body.title?.trim() || null
}

function managementCompanyValidationMessage(message: string | null) {
  if (!message) return null
  if (message.includes('Name is required')) return 'Informe o nome da administradora.'
  if (message.includes('Name must not exceed')) return 'O nome deve possuir no máximo 150 caracteres.'
  if (message.includes('Legal name')) return 'A razão social deve possuir no máximo 200 caracteres.'
  if (message.includes('CNPJ')) return 'Informe um CNPJ válido.'
  if (message.includes('Email')) return 'O e-mail deve possuir no máximo 254 caracteres.'
  if (message.includes('Phone number')) return 'O telefone deve possuir no máximo 30 caracteres.'
  return message
}

export function managementCompanyError(error: unknown) {
  if (!axios.isAxiosError(error)) return 'Não foi possível concluir a operação. Tente novamente.'

  switch (error.response?.status) {
    case 400:
      return managementCompanyValidationMessage(getResponseMessage(error))
        ?? 'Os dados informados são inválidos. Revise os campos e tente novamente.'
    case 401:
      return 'Sua sessão expirou. Entre novamente.'
    case 403:
      return 'Você não possui permissão para realizar esta operação.'
    case 404:
      return 'A administradora solicitada não foi encontrada.'
    case 409: {
      const message = getResponseMessage(error)
      if (message?.toLowerCase().includes('cnpj')) return 'Já existe uma administradora com este CNPJ.'
      if (message?.includes('email')) return 'Já existe uma administradora com este e-mail.'
      return 'Já existe um cadastro utilizando este e-mail ou CNPJ.'
    }
    default:
      return 'Não foi possível concluir a operação. Tente novamente.'
  }
}

export function employeeError(error: unknown) {
  if (!axios.isAxiosError(error)) return 'Não foi possível concluir a operação. Tente novamente.'

  switch (error.response?.status) {
    case 400:
      return getResponseMessage(error)
        ?? 'Os dados do funcionário são inválidos. Revise os campos e tente novamente.'
    case 401:
      return 'Sua sessão expirou. Entre novamente.'
    case 403:
      return 'Você não possui permissão para gerenciar funcionários.'
    case 404:
      return 'A administradora ou o funcionário não foi encontrado.'
    case 409: {
      const message = getResponseMessage(error)
      return message?.includes('already belongs')
        ? 'Este usuário já pertence a uma administradora.'
        : 'Já existe um usuário cadastrado com este e-mail.'
    }
    default:
      return 'Não foi possível concluir a operação. Tente novamente.'
  }
}

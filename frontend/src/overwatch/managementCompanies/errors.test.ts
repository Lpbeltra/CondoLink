import { describe, expect, it } from 'vitest'
import { managementCompanyError } from './errors'

function apiError(status: number, data: unknown) {
  return { isAxiosError: true, response: { status, data } }
}

describe('management company errors', () => {
  it('uses a friendly backend validation message', () => {
    expect(managementCompanyError(apiError(400, {
      message: 'Name is required.',
    }))).toBe('Informe o nome da administradora.')
  })

  it('uses ProblemDetails detail when available', () => {
    expect(managementCompanyError(apiError(400, {
      title: 'Validation failed',
      detail: 'O CNPJ informado é inválido.',
    }))).toBe('Informe um CNPJ válido.')
  })
})

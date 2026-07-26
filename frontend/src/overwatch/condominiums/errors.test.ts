import { describe, expect, it } from 'vitest'
import { condominiumError } from './errors'

function apiError(status: number, data: unknown) {
  return { isAxiosError: true, response: { status, data } }
}

describe('Overwatch condominium errors', () => {
  it('uses ProblemDetails detail when available', () => {
    expect(condominiumError(apiError(400, {
      title: 'Validation failed',
      detail: 'O telefone informado é inválido.',
    }))).toBe('O telefone informado é inválido.')
  })

  it('identifies an invalid management company', () => {
    expect(condominiumError(apiError(404, {
      message: 'Management company not found.',
    }))).toBe('A administradora selecionada não foi encontrada.')
  })
})

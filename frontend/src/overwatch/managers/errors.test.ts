import { describe, expect, it } from 'vitest'
import { managerError } from './errors'

describe('manager errors', () => {
  it('presents ProblemDetails and duplicate relationships', () => {
    const problem = {
      isAxiosError: true,
      response: {
        status: 409,
        data: { error: 'Manager is already associated with this condominium.' },
      },
    }
    expect(managerError(problem))
      .toBe('Este síndico já está vinculado ao condomínio.')
  })

  it('maps unique-manager and inactive-manager conflicts', () => {
    expect(managerError({
      isAxiosError: true,
      response: {
        status: 409,
        data: { error: 'Este condomínio já possui um síndico vinculado.' },
      },
    })).toBe('Este condomínio já possui um síndico vinculado.')

    expect(managerError({
      isAxiosError: true,
      response: {
        status: 409,
        data: { error: 'O síndico selecionado está inativo.' },
      },
    })).toBe('O síndico selecionado está inativo.')
  })
})

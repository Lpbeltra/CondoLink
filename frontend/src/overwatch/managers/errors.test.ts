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
})

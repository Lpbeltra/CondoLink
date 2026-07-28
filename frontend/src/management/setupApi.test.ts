import { beforeEach, describe, expect, it, vi } from 'vitest'

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}))

vi.mock('../services/api', () => ({ api: http }))

import { previewSetupImport } from './setupApi'

describe('setup API', () => {
  beforeEach(() => http.post.mockReset())

  it('uploads spreadsheets as FormData without converting them to JSON', async () => {
    http.post.mockResolvedValue({ data: { errors: [] } })
    const structure = new File(['Block,Unit\n,01'], 'structure.csv')

    await previewSetupImport('condo-1', structure, null, false)

    const [path, body] = http.post.mock.calls[0]
    expect(path).toBe('/condominiums/condo-1/setup/import/preview')
    expect(body).toBeInstanceOf(FormData)
    expect(body.get('structureFile')).toBe(structure)
    expect(body.get('noRegistrableUnits')).toBe('false')
    expect(http.post.mock.calls[0]).toHaveLength(2)
  })
})

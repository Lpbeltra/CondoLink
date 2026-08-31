import { render, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdministratorProvider } from './AdministratorProvider'
import { getAdministratorContext } from '../managementCompanyRequests/api'
import { useAuth } from '../auth/AuthContext'

vi.mock('../managementCompanyRequests/api', () => ({ getAdministratorContext: vi.fn() }))
vi.mock('../auth/AuthContext', () => ({ useAuth: vi.fn() }))

describe('AdministratorProvider eligibility', () => {
  beforeEach(() => vi.clearAllMocks())
  it('does not request administrator context for a pure management user', async () => {
    vi.mocked(useAuth).mockReturnValue({ user: { id:'1',fullName:'Gestor',email:'g@test',isActive:true,roles:['Manager'],hasAdministratorAccess:false } } as never)
    render(<AdministratorProvider><span>conteúdo</span></AdministratorProvider>)
    await waitFor(() => expect(getAdministratorContext).not.toHaveBeenCalled())
  })
  it('loads context for an eligible administrator user', async () => {
    vi.mocked(useAuth).mockReturnValue({ user: { id:'2',fullName:'Pessoa',email:'p@test',isActive:true,hasAdministratorAccess:true } } as never)
    vi.mocked(getAdministratorContext).mockResolvedValue({ managementCompanyId:'c',managementCompanyName:'Empresa',jobTitle:'Atendimento',accessType:'Person',categories:[] })
    render(<AdministratorProvider><span>conteúdo</span></AdministratorProvider>)
    await waitFor(() => expect(getAdministratorContext).toHaveBeenCalledTimes(1))
  })
})

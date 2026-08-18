import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type {
  CondominiumMember,
  Unit,
  UnitMembership,
} from '../management/types'

const managementApi = vi.hoisted(() => ({
  createUnitMembership: vi.fn(),
  deleteUnitMembership: vi.fn(),
  getUnit: vi.fn(),
  listBlocks: vi.fn(),
  listCondominiumMembers: vi.fn(),
  listUnitMemberships: vi.fn(),
  updateUnit: vi.fn(),
  updateUnitMembership: vi.fn(),
}))

vi.mock('../management/api', () => managementApi)
vi.mock('../management/ManagementContext', () => ({
  useManagementContext: () => ({
    activeCondominiumId: 'condominium-id',
    isLoading: false,
  }),
}))

import { UnitDetailsPage } from './UnitDetailsPage'

const unit: Unit = {
  id: 'unit-id',
  condominiumId: 'condominium-id',
  identifier: '101',
  blockId: null,
  block: null,
  floor: null,
  description: null,
  isActive: true,
  createdAt: '2026-07-28T10:00:00Z',
  updatedAt: '2026-07-28T10:00:00Z',
}

const member: CondominiumMember = {
  membershipId: 'condominium-membership-id',
  userId: 'user-id',
  fullName: 'Maria Silva',
  email: 'maria@example.com',
  phoneNumber: null,
  userActive: true,
  mustChangePassword: false,
  emailDeliveryEnabled: true,
  firstAccessStatus: 'Completed',
  lastLoginAt: null,
  membershipActive: true,
  joinedAt: '2026-07-28T10:00:00Z',
  endedAt: null,
  roles: ['Resident'],
  unitLinks: [],
}

const link: UnitMembership = {
  unitMembershipId: 'unit-membership-id',
  userId: member.userId,
  fullName: member.fullName,
  email: member.email,
  phoneNumber: null,
  relationshipType: 'Owner',
  isResident: true,
  isPrimaryResidence: false,
  membershipActive: true,
  startedAt: '2026-07-28T10:00:00Z',
  endedAt: null,
  createdAt: '2026-07-28T10:00:00Z',
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/management/units/unit-id']}>
      <Routes>
        <Route
          path="/management/units/:unitId"
          element={<UnitDetailsPage />}
        />
      </Routes>
    </MemoryRouter>,
  )
}

describe('UnitDetailsPage memberships', () => {
  beforeEach(() => {
    managementApi.getUnit.mockResolvedValue(unit)
    managementApi.listBlocks.mockResolvedValue([])
    managementApi.listCondominiumMembers.mockResolvedValue([member])
    managementApi.listUnitMemberships.mockResolvedValue([])
    managementApi.createUnitMembership.mockResolvedValue({})
  })

  it('renders a compact responsive action and a separate empty-state block', async () => {
    renderPage()

    const button = await screen.findByRole('button', {
      name: /Adicionar v.nculo/,
    })
    expect(button).toHaveClass('MuiButton-sizeSmall')

    const heading = screen.getByRole('heading', {
      name: /Pessoas vinculadas/,
    })
    const emptyTitle = screen.getByText(
      /Nenhuma pessoa vinculada a esta unidade/,
    )
    expect(heading.parentElement).not.toContainElement(emptyTitle)
    expect(emptyTitle.closest('.MuiBox-root')).not.toBe(
      heading.parentElement,
    )
  })

  it('creates a link, refreshes the list immediately, and closes after success', async () => {
    const user = userEvent.setup()
    managementApi.listUnitMemberships
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([link])
    renderPage()

    await user.click(await screen.findByRole('button', {
      name: /Adicionar v.nculo/,
    }))
    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('combobox', {
      name: /Pessoa/,
    }))
    await user.click(await screen.findByText('Maria Silva'))
    await user.click(within(dialog).getByRole('button', {
      name: /Salvar v.nculo/,
    }))

    await waitFor(() => {
      expect(managementApi.createUnitMembership).toHaveBeenCalledWith(
        'unit-id',
        expect.objectContaining({ userId: 'user-id' }),
      )
    })
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
    expect(screen.getByText('Maria Silva')).toBeInTheDocument()
  })

  it('keeps the populated dialog open when creation fails', async () => {
    const user = userEvent.setup()
    managementApi.createUnitMembership.mockRejectedValue(
      new Error('request failed'),
    )
    renderPage()

    await user.click(await screen.findByRole('button', {
      name: /Adicionar v.nculo/,
    }))
    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('combobox', {
      name: /Pessoa/,
    }))
    await user.click(await screen.findByText('Maria Silva'))
    await user.click(within(dialog).getByRole('button', {
      name: /Salvar v.nculo/,
    }))

    await waitFor(() => {
      expect(managementApi.createUnitMembership).toHaveBeenCalled()
    })
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(within(screen.getByRole('dialog')).getByRole('combobox', {
      name: /Pessoa/,
    })).toHaveValue('Maria Silva')
  })
})

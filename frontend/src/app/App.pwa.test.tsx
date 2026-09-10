import type { PropsWithChildren } from 'react'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { notifyPwaUpdateAvailable, resetPwaUpdateForTests } from '../pwa/pwaUpdate'
import { App } from './App'

vi.mock('../theme/AppThemeProvider', () => ({
  AppThemeProvider: ({ children }: PropsWithChildren) => <>{children}</>,
}))
vi.mock('../auth/AuthProvider', () => ({
  AuthProvider: ({ children }: PropsWithChildren) => <>{children}</>,
}))
vi.mock('../condominiums/CondominiumProvider', () => ({
  CondominiumProvider: ({ children }: PropsWithChildren) => <>{children}</>,
}))
vi.mock('../pages/LoginPage', () => ({ LoginPage: () => <div>Login route</div> }))

describe('global PWA update prompt', () => {
  beforeEach(() => {
    resetPwaUpdateForTests()
    window.history.pushState({}, '', '/login')
  })

  it('shows a pending update on a route outside the authenticated AppShell', async () => {
    notifyPwaUpdateAvailable()
    render(<App />)

    expect(screen.getByText('Login route')).toBeVisible()
    expect(await screen.findByText('Nova versão do Comvy disponível.')).toBeVisible()
  })
})

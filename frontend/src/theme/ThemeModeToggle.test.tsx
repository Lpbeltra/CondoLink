import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AppThemeProvider } from './AppThemeProvider'
import { ThemeModeToggle } from './ThemeModeToggle'
import { useThemeMode } from './ThemeModeContext'

/** Installs a matchMedia whose dark-scheme answer we control. */
function mockColorScheme(prefersDark: boolean) {
  const listeners = new Set<(event: MediaQueryListEvent) => void>()
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: query.includes('prefers-color-scheme: dark') ? prefersDark : false,
      media: query,
      onchange: null,
      addEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) =>
        listeners.add(listener),
      removeEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) =>
        listeners.delete(listener),
      dispatchEvent: () => false,
    })),
  })
  return listeners
}

function ModeProbe() {
  const { mode, preference } = useThemeMode()
  return <output data-testid="probe">{`${preference}:${mode}`}</output>
}

function renderToggle() {
  return render(
    <AppThemeProvider>
      <ThemeModeToggle />
      <ModeProbe />
    </AppThemeProvider>,
  )
}

describe('ThemeModeToggle', () => {
  beforeEach(() => {
    localStorage.clear()
    mockColorScheme(false)
  })

  it('starts by following the system preference', () => {
    renderToggle()
    expect(screen.getByTestId('probe')).toHaveTextContent('system:light')
  })

  it('resolves to dark when the OS prefers dark', () => {
    mockColorScheme(true)
    renderToggle()
    expect(screen.getByTestId('probe')).toHaveTextContent('system:dark')
  })

  it('exposes an accessible name describing the next action', () => {
    renderToggle()
    expect(
      screen.getByRole('button', { name: 'Alternar para o tema claro' }),
    ).toBeInTheDocument()
  })

  it('cycles system -> light -> dark -> system on click', async () => {
    const user = userEvent.setup()
    renderToggle()
    const probe = screen.getByTestId('probe')

    await user.click(screen.getByRole('button'))
    expect(probe).toHaveTextContent('light:light')

    await user.click(screen.getByRole('button'))
    expect(probe).toHaveTextContent('dark:dark')

    await user.click(screen.getByRole('button'))
    expect(probe).toHaveTextContent('system:light')
  })

  it('persists the chosen preference across remounts', async () => {
    const user = userEvent.setup()
    const first = renderToggle()
    await user.click(screen.getByRole('button'))
    await user.click(screen.getByRole('button'))
    expect(screen.getByTestId('probe')).toHaveTextContent('dark:dark')
    first.unmount()

    renderToggle()
    expect(screen.getByTestId('probe')).toHaveTextContent('dark:dark')
  })

  it('keeps an explicit choice even when the OS disagrees', async () => {
    const user = userEvent.setup()
    mockColorScheme(true)
    renderToggle()

    await user.click(screen.getByRole('button'))
    expect(screen.getByTestId('probe')).toHaveTextContent('light:light')
  })

  it('marks the document with the active mode for non-MUI styling', async () => {
    const user = userEvent.setup()
    renderToggle()
    expect(document.documentElement.dataset.theme).toBe('light')

    await user.click(screen.getByRole('button'))
    await user.click(screen.getByRole('button'))
    expect(document.documentElement.dataset.theme).toBe('dark')
  })

  it('syncs the PWA theme-color meta tag to the active mode', async () => {
    const user = userEvent.setup()
    renderToggle()
    const meta = () =>
      document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')?.content

    const lightColor = meta()
    await user.click(screen.getByRole('button'))
    await user.click(screen.getByRole('button'))
    expect(meta()).not.toBe(lightColor)
  })
})

describe('useThemeMode outside the provider', () => {
  it('fails loudly instead of silently rendering unthemed', () => {
    // Suppress React's expected error log for this intentional failure.
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => render(<ModeProbe />)).toThrow(/must be used within AppThemeProvider/)
    spy.mockRestore()
  })
})

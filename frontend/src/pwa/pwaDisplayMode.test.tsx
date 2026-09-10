import { act, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { isIosDevice, isIosInstallableBrowser, readPwaDisplayMode, usePwaDisplayMode } from './pwaDisplayMode'

function navigatorLike(values: Partial<Navigator> & { standalone?: boolean } = {}) {
  return {
    userAgent: '',
    platform: '',
    maxTouchPoints: 0,
    ...values,
  } as Navigator
}

describe('PWA display mode', () => {
  it('detects Chromium standalone and a normal browser', () => {
    const standaloneWindow = { matchMedia: () => ({ matches: true }) } as unknown as Window
    const browserWindow = { matchMedia: () => ({ matches: false }) } as unknown as Window
    expect(readPwaDisplayMode(standaloneWindow, navigatorLike()).isStandalone).toBe(true)
    expect(readPwaDisplayMode(browserWindow, navigatorLike()).isStandalone).toBe(false)
  })

  it('detects iOS standalone and iPadOS desktop user agent', () => {
    const browserWindow = { matchMedia: () => ({ matches: false }) } as unknown as Window
    const iphone = navigatorLike({ userAgent: 'Mozilla/5.0 (iPhone) AppleWebKit/605.1.15 Mobile/15E148 Safari/604.1', standalone: true })
    const ipad = navigatorLike({ userAgent: 'Mozilla/5.0 (Macintosh) AppleWebKit/605.1.15 Safari/605.1.15', platform: 'MacIntel', maxTouchPoints: 5 })
    expect(readPwaDisplayMode(browserWindow, iphone)).toMatchObject({ isStandalone: true, isIosStandalone: true, isIos: true })
    expect(isIosDevice(ipad)).toBe(true)
    expect(isIosInstallableBrowser(ipad)).toBe(true)
    expect(isIosInstallableBrowser(navigatorLike({ userAgent: 'Mozilla/5.0 (iPhone) Instagram' }))).toBe(false)
  })

  it('reacts when display-mode changes', () => {
    let standalone = false
    let listener: (() => void) | undefined
    window.matchMedia = vi.fn().mockImplementation(() => ({
      get matches() { return standalone },
      media: '(display-mode: standalone)',
      onchange: null,
      addEventListener: (_event: string, callback: () => void) => { listener = callback },
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }))
    function Probe() {
      return <output>{usePwaDisplayMode().isStandalone ? 'standalone' : 'browser'}</output>
    }
    render(<Probe />)
    expect(screen.getByText('browser')).toBeInTheDocument()
    standalone = true
    act(() => listener?.())
    expect(screen.getByText('standalone')).toBeInTheDocument()
  })
})

import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

// Node 26 exposes an incomplete global localStorage unless a backing file is
// configured. Keep component tests on the browser-compatible Storage contract.
if (!globalThis.localStorage?.clear) {
  const values = new Map<string, string>()
  Object.defineProperty(globalThis, 'localStorage', {
    configurable: true,
    value: {
      get length() { return values.size },
      clear: () => values.clear(),
      getItem: (key: string) => values.get(key) ?? null,
      key: (index: number) => [...values.keys()][index] ?? null,
      removeItem: (key: string) => values.delete(key),
      setItem: (key: string, value: string) => values.set(key, String(value)),
    } satisfies Storage,
  })
}

afterEach(() => {
  cleanup()
  // Some suites swap in their own minimal localStorage stub, so `clear` is not
  // guaranteed to exist here.
  globalThis.localStorage?.clear?.()
  vi.clearAllMocks()
})

// jsdom implements neither matchMedia nor the ResizeObserver MUI's transitions
// rely on. Default to "light" so themed component tests are deterministic.
if (!window.matchMedia) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      dispatchEvent: () => false,
    }),
  })
}

if (!globalThis.ResizeObserver) {
  globalThis.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver
}

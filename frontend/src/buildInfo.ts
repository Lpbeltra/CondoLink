export const frontendBuildId = import.meta.env.VITE_BUILD_ID?.trim() || null

declare global {
  interface Window {
    __COMVY_BUILD_ID__: string | null
  }
}

export function exposeFrontendBuildId() {
  window.__COMVY_BUILD_ID__ = frontendBuildId
  console.info(`[Comvy] Frontend build: ${frontendBuildId ?? 'unavailable'}`)
}

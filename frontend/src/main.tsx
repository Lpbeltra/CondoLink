import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { registerSW } from 'virtual:pwa-register'
import { App } from './app/App'
import './index.css'
import { notifyPwaUpdateAvailable, setPwaUpdateHandler } from './pwa/pwaUpdate'

setPwaUpdateHandler(registerSW({
  immediate: true,
  onNeedRefresh: notifyPwaUpdateAvailable,
  onRegisteredSW: (_swUrl, registration) => {
    if (registration && navigator.onLine) void registration.update()
  },
}))

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

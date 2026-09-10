(function () {
  const fallbackBody = 'Há uma nova atualização no Comvy.'

  function internalUrl(value) {
    if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//')) return '/'
    try {
      const parsed = new URL(value, self.location.origin)
      return parsed.origin === self.location.origin
        ? `${parsed.pathname}${parsed.search}${parsed.hash}`
        : '/'
    } catch {
      return '/'
    }
  }

  function parsePayload(data) {
    try {
      const payload = data && typeof data.json === 'function' ? data.json() : null
      return {
        title: typeof payload?.title === 'string' && payload.title.trim()
          ? payload.title.trim().slice(0, 80) : 'Comvy',
        body: typeof payload?.body === 'string' && payload.body.trim()
          ? payload.body.trim().slice(0, 180) : fallbackBody,
        url: internalUrl(payload?.url),
      }
    } catch {
      return { title: 'Comvy', body: fallbackBody, url: '/' }
    }
  }

  self.addEventListener('push', function (event) {
    const payload = parsePayload(event.data)
    event.waitUntil(self.registration.showNotification(payload.title, {
      body: payload.body,
      icon: '/comvy-icon-192-v1.png',
      data: { url: payload.url },
    }))
  })

  self.addEventListener('notificationclick', function (event) {
    event.notification.close()
    const path = internalUrl(event.notification?.data?.url)
    const target = new URL(path, self.location.origin).href
    event.waitUntil((async function () {
      const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
      const existing = windows.find(client => {
        try { return new URL(client.url).origin === self.location.origin } catch { return false }
      })
      if (existing) {
        if (typeof existing.navigate === 'function') await existing.navigate(target)
        return existing.focus()
      }
      return self.clients.openWindow(target)
    })())
  })

  // Pure hooks used by the focused service-worker tests.
  self.__comvyPush = { internalUrl, parsePayload }
})()

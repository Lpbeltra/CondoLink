const tokenKey = 'condolink.accessToken'

export function getStoredToken() { return localStorage.getItem(tokenKey) }
export function storeToken(token: string) { localStorage.setItem(tokenKey, token) }
export function clearStoredToken() { localStorage.removeItem(tokenKey) }

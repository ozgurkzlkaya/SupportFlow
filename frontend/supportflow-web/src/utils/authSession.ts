import { removeAccessToken, getAccessToken } from './authStorage'

const sessionExpiredReasonKey = 'supportflow.session-expired'

type SessionInvalidatedHandler = () => void

const handlers = new Set<SessionInvalidatedHandler>()

function isAuthRoute(url: string | undefined): boolean {
  return Boolean(
    url?.includes('/api/auth/login') ||
      url?.includes('/api/auth/register') ||
      url?.includes('/api/auth/me'),
  )
}

function redirectToLogin(): void {
  const currentPath = `${window.location.pathname}${window.location.search}`

  if (currentPath.startsWith('/login')) {
    return
  }

  window.location.replace('/login?reason=session-expired')
}

export function subscribeToSessionInvalidation(
  handler: SessionInvalidatedHandler,
): () => void {
  handlers.add(handler)

  return () => {
    handlers.delete(handler)
  }
}

export function clearSessionStorage(): void {
  removeAccessToken()
}

export function invalidateSession(): void {
  removeAccessToken()
  sessionStorage.setItem(sessionExpiredReasonKey, '1')
  handlers.forEach((handler) => handler())
}

export function handleUnauthorizedApiResponse(url: string | undefined): void {
  if (!getAccessToken() || isAuthRoute(url)) {
    return
  }

  invalidateSession()
  redirectToLogin()
}

export function consumeSessionExpiredFlag(): boolean {
  const hasFlag = sessionStorage.getItem(sessionExpiredReasonKey) === '1'

  if (hasFlag) {
    sessionStorage.removeItem(sessionExpiredReasonKey)
  }

  return hasFlag
}

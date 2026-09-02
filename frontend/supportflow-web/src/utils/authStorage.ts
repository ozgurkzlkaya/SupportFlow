const accessTokenKey = 'supportflow.accessToken'

// localStorage is acceptable for this internship phase. Production systems
// should evaluate secure HTTP-only cookies or another hardened token strategy.
export function getAccessToken(): string | null {
  return localStorage.getItem(accessTokenKey)
}

export function setAccessToken(accessToken: string): void {
  localStorage.setItem(accessTokenKey, accessToken)
}

export function removeAccessToken(): void {
  localStorage.removeItem(accessTokenKey)
}

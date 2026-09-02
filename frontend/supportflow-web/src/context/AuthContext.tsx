import axios from 'axios'
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react'
import { AuthContext } from './authContextValue'
import {
  getMe,
  login as loginRequest,
  logout as clearStoredSession,
} from '../services/authService'
import type { AuthUser, LoginRequest } from '../types/auth'
import { getAccessToken } from '../utils/authStorage'
import {
  clearSessionStorage,
  subscribeToSessionInvalidation,
} from '../utils/authSession'

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let active = true

    async function restoreSession() {
      if (!getAccessToken()) {
        if (active) {
          setLoading(false)
        }
        return
      }

      try {
        const currentUser = await getMe()
        if (active) {
          setUser(currentUser)
        }
      } catch (error: unknown) {
        if (axios.isAxiosError(error) && error.response?.status === 401) {
          clearStoredSession()
        }

        if (active) {
          setUser(null)
        }
      } finally {
        if (active) {
          setLoading(false)
        }
      }
    }

    void restoreSession()

    const unsubscribe = subscribeToSessionInvalidation(() => {
      if (active) {
        setUser(null)
        setLoading(false)
      }
    })

    return () => {
      active = false
      unsubscribe()
    }
  }, [])

  const login = useCallback(async (request: LoginRequest) => {
    const response = await loginRequest(request)
    setUser(response.user)
    return response
  }, [])

  const refreshUser = useCallback(async () => {
    const currentUser = await getMe()
    setUser(currentUser)
  }, [])

  const logout = useCallback(() => {
    clearStoredSession()
    clearSessionStorage()
    setUser(null)
  }, [])

  const value = useMemo(
    () => ({ user, loading, login, logout, refreshUser }),
    [user, loading, login, logout, refreshUser],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

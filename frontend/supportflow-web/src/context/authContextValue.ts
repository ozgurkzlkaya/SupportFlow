import { createContext } from 'react'
import type { AuthUser, LoginRequest, LoginResponse } from '../types/auth'

export interface AuthContextValue {
  user: AuthUser | null
  loading: boolean
  login: (request: LoginRequest) => Promise<LoginResponse>
  logout: () => void
  refreshUser: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)

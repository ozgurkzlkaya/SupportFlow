import { apiClient } from '../api/apiClient'
import type {
  AuthUser,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
} from '../types/auth'
import { removeAccessToken, setAccessToken } from '../utils/authStorage'

export async function login(request: LoginRequest): Promise<LoginResponse> {
  const { data } = await apiClient.post<LoginResponse>('/api/auth/login', request)
  setAccessToken(data.accessToken)
  return data
}

export async function register(request: RegisterRequest): Promise<AuthUser> {
  const { data } = await apiClient.post<AuthUser>('/api/auth/register', request)
  return data
}

export async function getMe(): Promise<AuthUser> {
  const { data } = await apiClient.get<AuthUser>('/api/auth/me')
  return data
}

export function logout(): void {
  removeAccessToken()
}

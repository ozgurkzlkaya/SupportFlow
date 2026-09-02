export const UserRole = {
  Customer: 'Customer',
  TechnicalStaff: 'TechnicalStaff',
  Admin: 'Admin',
} as const

export type UserRole = (typeof UserRole)[keyof typeof UserRole]

export interface AuthUser {
  id: number
  firstName: string
  lastName: string
  email: string
  role: UserRole
  companyId: number | null
  createdAt: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  expiresAt: string
  user: AuthUser
}

export interface RegisterRequest {
  firstName: string
  lastName: string
  email: string
  password: string
  companyId: number | null
}

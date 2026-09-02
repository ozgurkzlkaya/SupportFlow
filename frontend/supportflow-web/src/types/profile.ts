import type { UserRole } from './auth'

export interface UserProfile {
  id: number
  firstName: string
  lastName: string
  email: string
  role: UserRole
  companyId: number | null
  isActive: boolean
  createdAt: string
}

export interface UpdateUserProfileRequest {
  firstName: string
  lastName: string
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
  confirmNewPassword: string
}

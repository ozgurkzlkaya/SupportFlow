import { UserRole, type UserRole as UserRoleType } from '../types/auth'

export function getDefaultRouteForRole(role: UserRoleType): string {
  switch (role) {
    case UserRole.Customer:
      return '/customer'
    case UserRole.TechnicalStaff:
      return '/technical'
    case UserRole.Admin:
      return '/admin'
    default:
      return '/login'
  }
}

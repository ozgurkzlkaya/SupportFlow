import { UserRole, type UserRole as UserRoleType } from '../types/auth'

export function getUserRoleLabel(role: UserRoleType | string | null | undefined): string {
  switch (role) {
    case UserRole.Admin:
      return 'Yönetici'
    case UserRole.TechnicalStaff:
      return 'Teknik Personel'
    case UserRole.Customer:
      return 'Müşteri'
    default:
      return 'Bilinmeyen rol'
  }
}

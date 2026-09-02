export type UserRole = 'Customer' | 'TechnicalStaff' | 'Admin';

export interface AuthUser {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  role: UserRole;
  companyId: number | null;
  createdAt: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  companyId?: number | null;
}

export const roleLabels: Record<UserRole, string> = {
  Customer: 'Müşteri',
  TechnicalStaff: 'Teknik Personel',
  Admin: 'Yönetici',
};

export function isStaffRole(role: UserRole): boolean {
  return role === 'TechnicalStaff' || role === 'Admin';
}

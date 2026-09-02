import { UserRole, type UserRole as UserRoleType } from './auth'

export const UserAccountStatusFilter = {
  Active: 'Active',
  Inactive: 'Inactive',
} as const

export type UserAccountStatusFilter =
  (typeof UserAccountStatusFilter)[keyof typeof UserAccountStatusFilter]

export interface AdminUserListItem {
  id: number
  firstName: string
  lastName: string
  fullName: string
  email: string
  role: UserRoleType
  isActive: boolean
  createdAt: string
  activeTicketCount: number
}

export interface TechnicalStaffUserContext {
  activeTicketCount: number
  criticalTicketCount: number
  awaitingReplyCount: number
  slaBreachedCount: number
}

export interface CustomerUserContext {
  totalTicketCount: number
  activeTicketCount: number
  resolvedTicketCount: number
  closedTicketCount: number
}

export interface AdminUserDetail extends AdminUserListItem {
  updatedAt: string | null
  technicalStaffContext: TechnicalStaffUserContext | null
  customerContext: CustomerUserContext | null
}

export interface AdminUserStatusResponse {
  id: number
  isActive: boolean
  updatedAt: string | null
}

export const UserAdminAuditAction = {
  Activated: 'Activated',
  Deactivated: 'Deactivated',
} as const

export type UserAdminAuditAction =
  (typeof UserAdminAuditAction)[keyof typeof UserAdminAuditAction]

export interface AdminUserAudit {
  id: number
  actorUserId: number
  actorDisplayName: string
  targetUserId: number
  targetDisplayName: string
  targetEmail: string
  targetRole: UserRoleType
  action: UserAdminAuditAction
  oldIsActive: boolean
  newIsActive: boolean
  occurredAt: string
}

export interface AdminUsersQuery {
  search?: string
  role?: UserRoleType
  status?: UserAccountStatusFilter
  page?: number
  pageSize?: number
}

export function isAdminUserRole(value: string | null): value is UserRoleType {
  return value !== null && Object.values(UserRole).includes(value as UserRoleType)
}

export function isUserAccountStatusFilter(value: string | null): value is UserAccountStatusFilter {
  return value !== null && Object.values(UserAccountStatusFilter).includes(value as UserAccountStatusFilter)
}

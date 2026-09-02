import { apiClient } from '../api/apiClient'
import type {
  AdminUserDetail,
  AdminUserAudit,
  AdminUserListItem,
  AdminUserStatusResponse,
  AdminUsersQuery,
} from '../types/adminUser'
import type { PagedResponse } from '../types/pagination'

export async function getAdminUsers(
  query: AdminUsersQuery,
): Promise<PagedResponse<AdminUserListItem>> {
  const { data } = await apiClient.get<PagedResponse<AdminUserListItem>>('/api/admin/users', {
    params: query,
  })
  return data
}

export async function getAdminUserDetail(id: number): Promise<AdminUserDetail> {
  const { data } = await apiClient.get<AdminUserDetail>(`/api/admin/users/${id}`)
  return data
}

export async function getAdminUserAudits(
  id: number,
  page: number,
  pageSize: number,
): Promise<PagedResponse<AdminUserAudit>> {
  const { data } = await apiClient.get<PagedResponse<AdminUserAudit>>(
    `/api/admin/users/${id}/audit`,
    { params: { page, pageSize } },
  )
  return data
}

export async function updateAdminUserStatus(
  id: number,
  isActive: boolean,
): Promise<AdminUserStatusResponse> {
  const { data } = await apiClient.patch<AdminUserStatusResponse>(
    `/api/admin/users/${id}/status`,
    { isActive },
  )
  return data
}

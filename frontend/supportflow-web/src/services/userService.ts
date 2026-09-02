import { apiClient } from '../api/apiClient'
import type { TechnicalStaffUser } from '../types/user'

export async function getTechnicalStaff(): Promise<TechnicalStaffUser[]> {
  const { data } = await apiClient.get<TechnicalStaffUser[]>(
    '/api/users/technical-staff',
  )
  return data
}

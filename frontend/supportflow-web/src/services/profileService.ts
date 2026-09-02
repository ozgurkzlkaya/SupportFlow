import { apiClient } from '../api/apiClient'
import type {
  ChangePasswordRequest,
  UpdateUserProfileRequest,
  UserProfile,
} from '../types/profile'

export async function getMyProfile(): Promise<UserProfile> {
  const { data } = await apiClient.get<UserProfile>('/api/users/me')
  return data
}

export async function updateMyProfile(
  request: UpdateUserProfileRequest,
): Promise<UserProfile> {
  const { data } = await apiClient.patch<UserProfile>('/api/users/me', request)
  return data
}

export async function changeMyPassword(request: ChangePasswordRequest): Promise<void> {
  await apiClient.post('/api/users/me/change-password', request)
}

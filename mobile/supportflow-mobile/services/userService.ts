import { apiRequest } from '@/services/apiClient';
import type {
  ChangePasswordRequest,
  UpdateUserProfileRequest,
  UserProfile,
} from '@/types/profile';
import type { TechnicalStaffUser } from '@/types/users';

export const userService = {
  getProfile(token: string) {
    return apiRequest<UserProfile>('/api/users/me', { token });
  },
  updateProfile(token: string, request: UpdateUserProfileRequest) {
    return apiRequest<UserProfile>('/api/users/me', { method: 'PATCH', token, body: request });
  },
  changePassword(token: string, request: ChangePasswordRequest) {
    return apiRequest<void>('/api/users/me/change-password', { method: 'POST', token, body: request });
  },
  getTechnicalStaff(token: string) {
    return apiRequest<TechnicalStaffUser[]>('/api/users/technical-staff', { token });
  },
};

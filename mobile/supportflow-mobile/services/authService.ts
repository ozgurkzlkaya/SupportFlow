import { apiRequest } from '@/services/apiClient';
import type { AuthUser, LoginRequest, LoginResponse, RegisterRequest } from '@/types/auth';

export const authService = {
  login: (request: LoginRequest) =>
    apiRequest<LoginResponse>('/api/auth/login', { method: 'POST', body: request }),
  register: (request: RegisterRequest) =>
    apiRequest<AuthUser>('/api/auth/register', { method: 'POST', body: request }),
  me: (token: string) => apiRequest<AuthUser>('/api/auth/me', { token }),
};

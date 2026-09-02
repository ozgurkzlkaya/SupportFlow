import { setUnauthorizedHandler } from '@/services/apiClient';
import { authService } from '@/services/authService';
import { authStorage } from '@/services/authStorage';
import type { AuthUser, LoginRequest, RegisterRequest } from '@/types/auth';
import { useEffect, useState } from 'react';
import { createContext, useContext } from 'react';

interface AuthContextValue {
  user: AuthUser | null;
  token: string | null;
  isRestoring: boolean;
  isSubmitting: boolean;
  isAuthenticated: boolean;
  sessionExpired: boolean;
  login: (request: LoginRequest) => Promise<void>;
  register: (request: RegisterRequest) => Promise<void>;
  setCurrentUser: (user: AuthUser) => void;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isRestoring, setIsRestoring] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [sessionExpired, setSessionExpired] = useState(false);

  async function clearSession(expired: boolean) {
    await authStorage.clearAccessToken();
    setToken(null);
    setUser(null);
    setSessionExpired(expired);
  }

  useEffect(() => {
    async function restoreSession() {
      try {
        const storedToken = await authStorage.getAccessToken();
        if (!storedToken) {
          return;
        }

        const restoredUser = await authService.me(storedToken);
        setToken(storedToken);
        setUser(restoredUser);
      } catch {
        await authStorage.clearAccessToken();
        setToken(null);
        setUser(null);
      } finally {
        setIsRestoring(false);
      }
    }

    void restoreSession();
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(() => {
      void authStorage.clearAccessToken();
      setToken(null);
      setUser(null);
      setSessionExpired(true);
    });

    return () => setUnauthorizedHandler(null);
  }, []);

  async function login(request: LoginRequest) {
    setIsSubmitting(true);
    try {
      const response = await authService.login(request);
      await authStorage.saveAccessToken(response.accessToken);
      setToken(response.accessToken);
      setUser(response.user);
      setSessionExpired(false);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function register(request: RegisterRequest) {
    setIsSubmitting(true);
    try {
      await authService.register(request);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function logout() {
    await clearSession(false);
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isRestoring,
        isSubmitting,
        isAuthenticated: user !== null && token !== null,
        sessionExpired,
        login,
        register,
        setCurrentUser: setUser,
        logout,
      }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider.');
  }

  return context;
}

import { useAuth } from '@/context/AuthContext';
import { notificationService } from '@/services/notificationService';
import { createContext, useCallback, useContext, useEffect, useState } from 'react';

interface NotificationsContextValue {
  unreadCount: number;
  refreshUnreadCount: () => Promise<void>;
  setUnreadCount: (count: number) => void;
}

const NotificationsContext = createContext<NotificationsContextValue | null>(null);

export function NotificationsProvider({ children }: { children: React.ReactNode }) {
  const { token } = useAuth();
  const [unreadCount, setUnreadCount] = useState(0);
  const refreshUnreadCount = useCallback(async () => {
    if (!token) {
      setUnreadCount(0);
      return;
    }
    const response = await notificationService.getUnreadCount(token);
    setUnreadCount(response.count);
  }, [token]);

  useEffect(() => {
    void refreshUnreadCount().catch(() => setUnreadCount(0));
  }, [refreshUnreadCount]);

  return <NotificationsContext.Provider value={{ unreadCount, refreshUnreadCount, setUnreadCount }}>{children}</NotificationsContext.Provider>;
}

export function useNotifications() {
  const context = useContext(NotificationsContext);
  if (!context) throw new Error('useNotifications must be used within NotificationsProvider.');
  return context;
}

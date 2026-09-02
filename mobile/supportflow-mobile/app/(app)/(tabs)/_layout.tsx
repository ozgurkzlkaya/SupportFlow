import { useAuth } from '@/context/AuthContext';
import { useNotifications } from '@/context/NotificationsContext';
import { isStaffRole } from '@/types/auth';
import { Ionicons } from '@expo/vector-icons';
import { Tabs } from 'expo-router';
import { theme } from '@/theme/theme';

export default function AppTabLayout() {
  const { user } = useAuth();
  const { unreadCount } = useNotifications();
  const canAccessConversations = user ? isStaffRole(user.role) : false;

  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: theme.colors.primary,
        tabBarInactiveTintColor: theme.colors.textMuted,
        tabBarActiveBackgroundColor: 'transparent',
        tabBarLabelStyle: { fontSize: 10, fontWeight: '700', marginBottom: 1 },
        tabBarStyle: { backgroundColor: theme.colors.surface, borderTopColor: theme.colors.border, elevation: 8, height: 62, paddingBottom: 6, paddingTop: 5, shadowColor: theme.colors.text, shadowOffset: { width: 0, height: -2 }, shadowOpacity: 0.05, shadowRadius: 8 },
      }}>
      <Tabs.Screen name="index" options={{ title: 'Ana Sayfa', tabBarIcon: ({ color, size }) => <Ionicons color={color} name="home-outline" size={size} /> }} />
      <Tabs.Screen name="tickets" options={{ title: 'Talepler', tabBarIcon: ({ color, size }) => <Ionicons color={color} name="ticket-outline" size={size} /> }} />
      <Tabs.Screen name="conversations" options={{ href: canAccessConversations ? undefined : null, title: 'Sohbetler', tabBarIcon: ({ color, size }) => <Ionicons color={color} name="chatbubbles-outline" size={size} /> }} />
      <Tabs.Screen name="notifications" options={{ title: 'Bildirimler', tabBarBadge: unreadCount || undefined, tabBarBadgeStyle: { backgroundColor: theme.colors.error, color: theme.colors.onPrimary }, tabBarIcon: ({ color, size }) => <Ionicons color={color} name="notifications-outline" size={size} /> }} />
      <Tabs.Screen name="profile" options={{ title: 'Profil', tabBarIcon: ({ color, size }) => <Ionicons color={color} name="person-outline" size={size} /> }} />
    </Tabs>
  );
}

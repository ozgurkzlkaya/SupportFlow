import { LoadingView } from '@/components/LoadingView';
import { AuthProvider, useAuth } from '@/context/AuthContext';
import { NotificationsProvider } from '@/context/NotificationsContext';
import { isStaffRole } from '@/types/auth';
import { DarkTheme, DefaultTheme, ThemeProvider } from '@react-navigation/native';
import { type Href, Redirect, Stack, useSegments } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useColorScheme } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import 'react-native-reanimated';

export default function RootLayout() {
  const colorScheme = useColorScheme();

  return (
    <SafeAreaProvider>
      <ThemeProvider value={colorScheme === 'dark' ? DarkTheme : DefaultTheme}>
        <AuthProvider>
          <NotificationsProvider>
            <AppNavigator />
          </NotificationsProvider>
        </AuthProvider>
        <StatusBar style="light" />
      </ThemeProvider>
    </SafeAreaProvider>
  );
}

function AppNavigator() {
  const { isAuthenticated, isRestoring, user } = useAuth();
  const segments = Array.from(useSegments()) as string[];
  const inAuthGroup = segments[0] === '(auth)';

  if (isRestoring) {
    return <LoadingView />;
  }

  if (!isAuthenticated && !inAuthGroup) {
    return <Redirect href={'/(auth)/login' as Href} />;
  }

  if (isAuthenticated && inAuthGroup) {
    return <Redirect href={'/(app)/(tabs)' as Href} />;
  }

  if (isAuthenticated && user && !isStaffRole(user.role) && segments.includes('conversations')) {
    return <Redirect href={'/(app)/(tabs)' as Href} />;
  }

  if (isAuthenticated && user?.role !== 'Admin'
    && (segments.includes('gmail') || segments.includes('gmail-auth'))) {
    return <Redirect href={'/(app)/(tabs)' as Href} />;
  }

  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="(auth)" />
      <Stack.Screen name="(app)" />
    </Stack>
  );
}

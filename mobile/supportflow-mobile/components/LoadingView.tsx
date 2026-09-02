import { theme } from '@/theme/theme';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';

export function LoadingView({ label = 'Oturum yükleniyor...' }: { label?: string }) {
  return (
    <View style={styles.container}>
      <View style={styles.indicator}><ActivityIndicator color={theme.colors.primary} size="large" /></View>
      <Text style={styles.label}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { alignItems: 'center', backgroundColor: theme.colors.background, flex: 1, gap: theme.spacing.sm, justifyContent: 'center', padding: theme.spacing.xl },
  indicator: { alignItems: 'center', backgroundColor: theme.colors.primarySoft, borderRadius: theme.radius.pill, height: 56, justifyContent: 'center', width: 56 },
  label: { color: theme.colors.textMuted, fontSize: 15 },
});

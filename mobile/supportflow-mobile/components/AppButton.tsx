import { theme } from '@/theme/theme';
import type { ReactNode } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text } from 'react-native';

interface AppButtonProps {
  label: string;
  onPress: () => void;
  loading?: boolean;
  disabled?: boolean;
  variant?: 'primary' | 'secondary' | 'text';
  icon?: ReactNode;
}

export function AppButton({
  label,
  onPress,
  loading = false,
  disabled = false,
  variant = 'primary',
  icon,
}: AppButtonProps) {
  const inactive = disabled || loading;
  return (
    <Pressable
      accessibilityRole="button"
      disabled={inactive}
      onPress={onPress}
      style={({ pressed }) => [
        styles.base,
        styles[variant],
        inactive && styles.disabled,
        pressed && !inactive && styles.pressed,
      ]}>
      {loading ? <ActivityIndicator color={variant === 'primary' ? theme.colors.onPrimary : theme.colors.primary} /> : icon}
      <Text style={[styles.label, variant !== 'primary' && styles.secondaryLabel]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: { alignItems: 'center', borderRadius: theme.radius.md, flexDirection: 'row', gap: theme.spacing.xs, justifyContent: 'center', minHeight: 46, paddingHorizontal: theme.spacing.md },
  primary: { backgroundColor: theme.colors.primary, ...theme.shadow.card },
  secondary: { backgroundColor: theme.colors.primarySoft, borderColor: '#DCD2FC', borderWidth: 1 },
  text: { backgroundColor: 'transparent', minHeight: 36, paddingHorizontal: theme.spacing.xs },
  label: { color: theme.colors.onPrimary, fontSize: 14, fontWeight: '800' },
  secondaryLabel: { color: theme.colors.primary },
  disabled: { opacity: 0.55 },
  pressed: { opacity: 0.82 },
});

import { theme } from '@/theme/theme';
import type { TextInputProps } from 'react-native';
import { StyleSheet, Text, TextInput, View } from 'react-native';

interface AppTextInputProps extends TextInputProps {
  label: string;
  error?: string;
}

export function AppTextInput({ label, error, style, ...inputProps }: AppTextInputProps) {
  return (
    <View style={styles.group}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        {...inputProps}
        accessibilityLabel={label}
        placeholderTextColor={theme.colors.textMuted}
        style={[styles.input, style, Boolean(error) && styles.inputError]}
      />
      {error ? <Text style={styles.error}>{error}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  group: { gap: theme.spacing.xs },
  label: { color: theme.colors.text, fontSize: 13, fontWeight: '800' },
  input: { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, color: theme.colors.text, fontSize: 15, minHeight: 46, paddingHorizontal: theme.spacing.sm },
  inputError: { borderColor: theme.colors.error },
  error: { color: theme.colors.error, fontSize: 13 },
});

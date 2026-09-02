import { theme } from '@/theme/theme';
import { Ionicons } from '@expo/vector-icons';
import { StyleSheet, Text, View } from 'react-native';

export function ErrorMessage({ message }: { message: string }) {
  return (
    <View accessibilityRole="alert" style={styles.container}>
      <Ionicons color={theme.colors.error} name="alert-circle" size={18} />
      <Text style={styles.text}>{message}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { alignItems: 'flex-start', backgroundColor: '#FEF2F2', borderColor: '#FECACA', borderRadius: theme.radius.sm, borderWidth: 1, flexDirection: 'row', gap: theme.spacing.xs, padding: theme.spacing.sm },
  text: { color: theme.colors.error, flex: 1, fontSize: 14, lineHeight: 20 },
});

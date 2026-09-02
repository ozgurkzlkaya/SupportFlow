import { ScreenContainer } from '@/components/ScreenContainer';
import { theme } from '@/theme/theme';
import { Ionicons } from '@expo/vector-icons';
import type { ComponentProps } from 'react';
import { StyleSheet, Text, View } from 'react-native';

interface PlaceholderScreenProps {
  title: string;
  description: string;
  icon: ComponentProps<typeof Ionicons>['name'];
}

export function PlaceholderScreen({ title, description, icon }: PlaceholderScreenProps) {
  return (
    <ScreenContainer>
      <View style={styles.container}>
        <View style={styles.iconWrap}>
          <Ionicons color={theme.colors.primary} name={icon} size={32} />
        </View>
        <Text style={styles.title}>{title}</Text>
        <Text style={styles.description}>{description}</Text>
        <Text style={styles.note}>Bu alan bir sonraki mobil aşamada kullanıma açılacaktır.</Text>
      </View>
    </ScreenContainer>
  );
}

const styles = StyleSheet.create({
  container: { alignItems: 'center', flex: 1, justifyContent: 'center', padding: theme.spacing.xl },
  iconWrap: { alignItems: 'center', backgroundColor: theme.colors.primarySoft, borderRadius: 28, height: 72, justifyContent: 'center', marginBottom: theme.spacing.lg, width: 72 },
  title: { color: theme.colors.text, fontSize: 24, fontWeight: '800', textAlign: 'center' },
  description: { color: theme.colors.textMuted, fontSize: 16, lineHeight: 24, marginTop: theme.spacing.sm, textAlign: 'center' },
  note: { color: theme.colors.warning, fontSize: 13, lineHeight: 20, marginTop: theme.spacing.lg, textAlign: 'center' },
});

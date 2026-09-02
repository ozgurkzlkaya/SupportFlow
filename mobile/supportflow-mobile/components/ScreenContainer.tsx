import { theme } from '@/theme/theme';
import type { PropsWithChildren } from 'react';
import type { ViewStyle } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

export function ScreenContainer({
  children,
  style,
}: PropsWithChildren<{ style?: ViewStyle }>) {
  return (
    <SafeAreaView edges={['top', 'left', 'right']} style={[{ flex: 1, backgroundColor: theme.colors.background }, style]}>
      {children}
    </SafeAreaView>
  );
}

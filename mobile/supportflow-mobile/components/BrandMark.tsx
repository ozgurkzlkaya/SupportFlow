import { theme } from '@/theme/theme';
import { StyleSheet, View } from 'react-native';

export function BrandMark({ size = 44 }: { size?: number }) {
  const nodeSize = Math.max(6, Math.round(size * 0.19));
  const lineWidth = Math.round(size * 0.36);

  return (
    <View accessibilityLabel="SupportFlow" style={[styles.mark, { borderRadius: Math.round(size * 0.3), height: size, width: size }]}>
      <View style={styles.flow}>
        <View style={[styles.node, styles.nodeStart, { height: nodeSize, width: nodeSize }]} />
        <View style={[styles.line, { width: lineWidth }]} />
        <View style={[styles.node, styles.nodeEnd, { height: nodeSize, width: nodeSize }]} />
      </View>
      <View style={[styles.replyNode, { height: nodeSize, width: nodeSize }]} />
    </View>
  );
}

const styles = StyleSheet.create({
  mark: { alignItems: 'center', backgroundColor: theme.colors.primary, justifyContent: 'center', ...theme.shadow.card },
  flow: { alignItems: 'center', flexDirection: 'row', marginTop: 4 },
  node: { borderRadius: theme.radius.pill },
  nodeStart: { backgroundColor: '#EDE9FE' },
  nodeEnd: { backgroundColor: '#FFFFFF' },
  line: { backgroundColor: '#C9BCF5', height: 2 },
  replyNode: { backgroundColor: '#FFFFFF', borderColor: '#C9BCF5', borderRadius: theme.radius.pill, borderWidth: 2, marginLeft: 4, marginTop: 3 },
});

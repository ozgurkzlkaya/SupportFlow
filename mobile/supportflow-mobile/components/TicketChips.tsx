import { theme } from '@/theme/theme';
import type { TicketPriority, TicketStatus } from '@/types/tickets';
import { StyleSheet, Text, View } from 'react-native';

const statusLabels: Record<TicketStatus, string> = {
  New: 'Yeni', Assigned: 'Atandı', InProgress: 'İşlemde', WaitingForCustomer: 'Müşteri Bekleniyor',
  Resolved: 'Çözüldü', Closed: 'Kapalı', Reopened: 'Yeniden Açıldı',
};

const priorityLabels: Record<TicketPriority, string> = {
  Low: 'Düşük', Normal: 'Normal', High: 'Yüksek', Critical: 'Kritik',
};

export function TicketStatusChip({ status }: { status: TicketStatus }) {
  const tone = status === 'Resolved' || status === 'Closed' ? styles.success : status === 'New' || status === 'Reopened' ? styles.primary : styles.warning;
  return <View style={[styles.chip, tone]}><Text style={[styles.text, tone === styles.success ? styles.successText : tone === styles.warning ? styles.warningText : styles.primaryText]}>{statusLabels[status]}</Text></View>;
}

export function TicketPriorityChip({ priority }: { priority: TicketPriority }) {
  const tone = priority === 'Critical' ? styles.danger : priority === 'High' ? styles.warning : styles.muted;
  return <View style={[styles.chip, tone]}><Text style={[styles.text, tone === styles.danger ? styles.dangerText : tone === styles.warning ? styles.warningText : styles.mutedText]}>{priorityLabels[priority]}</Text></View>;
}

const styles = StyleSheet.create({
  chip: { borderRadius: 999, paddingHorizontal: 9, paddingVertical: 5 },
  text: { fontSize: 12, fontWeight: '700' },
  primary: { backgroundColor: theme.colors.primarySoft }, primaryText: { color: theme.colors.primary },
  success: { backgroundColor: '#DCFCE7' }, successText: { color: theme.colors.success },
  warning: { backgroundColor: '#FEF3C7' }, warningText: { color: theme.colors.warning },
  danger: { backgroundColor: '#FEE2E2' }, dangerText: { color: theme.colors.error },
  muted: { backgroundColor: theme.colors.surfaceMuted }, mutedText: { color: theme.colors.textMuted },
});

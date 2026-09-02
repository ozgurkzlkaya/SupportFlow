import { AppButton } from '@/components/AppButton';
import { ErrorMessage } from '@/components/ErrorMessage';
import { LoadingView } from '@/components/LoadingView';
import { ScreenContainer } from '@/components/ScreenContainer';
import { useAuth } from '@/context/AuthContext';
import { useNotifications } from '@/context/NotificationsContext';
import { notificationService } from '@/services/notificationService';
import { theme } from '@/theme/theme';
import type { NotificationType, SupportNotification } from '@/types/notifications';
import { formatDateTime } from '@/utils/formatDate';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';

const typeLabels: Record<NotificationType, string> = {
  TicketCreated: 'Talep', CustomerReply: 'Müşteri yanıtı', TechnicalReply: 'Destek yanıtı', Assignment: 'Atama',
  StatusChange: 'Durum güncellemesi', GmailInbound: 'E-posta', GmailOutboundSuccess: 'E-posta', GmailOutboundFailure: 'E-posta',
};

export default function NotificationsScreen() {
  const { token } = useAuth();
  const { refreshUnreadCount, setUnreadCount, unreadCount } = useNotifications();
  const [notifications, setNotifications] = useState<SupportNotification[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [markingAll, setMarkingAll] = useState(false);
  const [openingNotificationId, setOpeningNotificationId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token) return;
    setError(null);
    try {
      const response = await notificationService.getNotifications(token);
      setNotifications(response);
      await refreshUnreadCount();
    } catch {
      setError('Bildirimler yüklenemedi. Lütfen tekrar deneyin.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [refreshUnreadCount, token]);

  useEffect(() => { void load(); }, [load]);

  async function openNotification(notification: SupportNotification) {
    if (!token || openingNotificationId !== null) return;
    setOpeningNotificationId(notification.id);
    try {
      if (!notification.isRead) {
        await notificationService.markAsRead(token, notification.id);
        setNotifications(current => current.map(item => item.id === notification.id ? { ...item, isRead: true } : item));
        setUnreadCount(Math.max(0, unreadCount - 1));
      }
      if (notification.ticketId) {
        router.push({ pathname: '/(app)/tickets/[id]', params: { id: String(notification.ticketId) } });
      }
    } catch {
      setError('Bildirim güncellenemedi. Lütfen tekrar deneyin.');
    } finally {
      setOpeningNotificationId(null);
    }
  }

  async function markAllAsRead() {
    if (!token || !unreadCount) return;
    setMarkingAll(true);
    setError(null);
    try {
      await notificationService.markAllAsRead(token);
      setNotifications(current => current.map(item => ({ ...item, isRead: true })));
      setUnreadCount(0);
    } catch {
      setError('Bildirimler güncellenemedi. Lütfen tekrar deneyin.');
    } finally {
      setMarkingAll(false);
    }
  }

  if (loading) return <LoadingView label="Bildirimler yükleniyor..." />;

  return <ScreenContainer>
    <FlatList
      style={styles.list}
      contentContainerStyle={styles.content}
      data={notifications}
      keyExtractor={item => String(item.id)}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => { setRefreshing(true); void load(); }} tintColor={theme.colors.primary} />}
      ListHeaderComponent={<View style={styles.header}>
        <View><Text style={styles.title}>Bildirimler</Text><Text style={styles.subtitle}>{unreadCount ? `${unreadCount} okunmamış bildiriminiz var.` : 'Tüm bildirimleriniz güncel.'}</Text></View>
        {unreadCount ? <AppButton disabled={markingAll} label="Tümünü Okundu Yap" loading={markingAll} onPress={() => void markAllAsRead()} variant="text" /> : null}
        {error ? <><ErrorMessage message={error} /><AppButton label="Tekrar Dene" onPress={() => void load()} variant="secondary" /></> : null}
      </View>}
      renderItem={({ item }) => <NotificationCard disabled={openingNotificationId !== null} item={item} onPress={() => void openNotification(item)} />}
      ItemSeparatorComponent={() => <View style={styles.separator} />}
      ListEmptyComponent={<View style={styles.empty}><Ionicons color={theme.colors.textMuted} name="notifications-off-outline" size={38} /><Text style={styles.emptyTitle}>Bildirim yok</Text><Text style={styles.emptyText}>Yeni gelişmeler burada görünecek.</Text></View>}
    />
  </ScreenContainer>;
}

function NotificationCard({ item, onPress, disabled }: { item: SupportNotification; onPress: () => void; disabled: boolean }) {
  return <Pressable accessibilityRole="button" disabled={disabled} onPress={onPress} style={({ pressed }) => [styles.card, !item.isRead && styles.unreadCard, pressed && styles.pressed]}>
    <View style={styles.cardTop}><View style={styles.typeRow}><View style={[styles.dot, !item.isRead && styles.unreadDot]} /><Text style={styles.type}>{typeLabels[item.type]}</Text></View><Text style={styles.date}>{formatDateTime(item.createdAt)}</Text></View>
    <View style={styles.titleRow}><Text numberOfLines={1} style={styles.cardTitle}>{item.title}</Text>{item.ticketId ? <Ionicons color={theme.colors.textMuted} name="chevron-forward" size={17} /> : null}</View><Text numberOfLines={2} style={styles.message}>{item.message}</Text>
  </Pressable>;
}

const styles = StyleSheet.create({
  list: { flex: 1 }, content: { padding: theme.spacing.md, paddingBottom: theme.spacing.xl }, header: { gap: theme.spacing.xs, marginBottom: theme.spacing.sm }, title: { color: theme.colors.text, fontSize: 24, fontWeight: '800', letterSpacing: -0.5 }, subtitle: { color: theme.colors.textMuted, fontSize: 13, marginTop: 2 }, card: { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, padding: theme.spacing.sm, ...theme.shadow.card }, unreadCard: { backgroundColor: '#FBFAFF', borderColor: '#D9CCFF' }, pressed: { opacity: 0.8 }, cardTop: { alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' }, typeRow: { alignItems: 'center', flexDirection: 'row', gap: 6 }, dot: { backgroundColor: theme.colors.border, borderRadius: 4, height: 8, width: 8 }, unreadDot: { backgroundColor: theme.colors.primary }, type: { color: theme.colors.primary, fontSize: 11, fontWeight: '800' }, date: { color: theme.colors.textMuted, fontSize: 10 }, titleRow: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.xs, marginTop: 5 }, cardTitle: { color: theme.colors.text, flex: 1, fontSize: 15, fontWeight: '800' }, message: { color: theme.colors.textMuted, fontSize: 13, lineHeight: 18, marginTop: 2 }, separator: { height: theme.spacing.xs }, empty: { alignItems: 'center', paddingHorizontal: theme.spacing.xl, paddingVertical: 64 }, emptyTitle: { color: theme.colors.text, fontSize: 18, fontWeight: '800', marginTop: theme.spacing.md }, emptyText: { color: theme.colors.textMuted, marginTop: 6 },
});

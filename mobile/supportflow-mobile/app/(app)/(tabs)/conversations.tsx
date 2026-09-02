import { AppButton } from '@/components/AppButton';
import { AppTextInput } from '@/components/AppTextInput';
import { ErrorMessage } from '@/components/ErrorMessage';
import { LoadingView } from '@/components/LoadingView';
import { ScreenContainer } from '@/components/ScreenContainer';
import { TicketPriorityChip, TicketStatusChip } from '@/components/TicketChips';
import { useAuth } from '@/context/AuthContext';
import { conversationService } from '@/services/conversationService';
import { ticketService } from '@/services/ticketService';
import { theme } from '@/theme/theme';
import { isStaffRole } from '@/types/auth';
import type { ConversationDetail, ConversationFilter, ConversationSummary, ConversationTicket } from '@/types/conversations';
import { formatDateTime, formatFileSize } from '@/utils/formatDate';
import { Ionicons } from '@expo/vector-icons';
import { Redirect, router, type Href } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { FlatList, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

const filters: { label: string; value: ConversationFilter }[] = [
  { label: 'Tümü', value: 'All' }, { label: 'Yanıt Bekleyenler', value: 'AwaitingReply' }, { label: 'Okunmamışlar', value: 'Unread' }, { label: 'Sabitlenenler', value: 'Pinned' },
];

export default function ConversationsScreen() {
  const { token, user } = useAuth();
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<ConversationFilter>('All');
  const [summaries, setSummaries] = useState<ConversationSummary[]>([]);
  const [counts, setCounts] = useState({ all: 0, awaitingReply: 0, unread: 0, pinned: 0 });
  const [selected, setSelected] = useState<ConversationSummary | null>(null);
  const [detail, setDetail] = useState<ConversationDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pinning, setPinning] = useState(false);
  const [replyTicketId, setReplyTicketId] = useState<number | null>(null);
  const [replyDraft, setReplyDraft] = useState('');
  const [sendingReply, setSendingReply] = useState(false);

  const loadSummaries = useCallback(async () => {
    if (!token) return;
    setError(null);
    try {
      const response = await conversationService.getConversations(token, search, filter);
      setSummaries(response.items);
      setCounts(response.counts);
      setSelected(current => current ? response.items.find(item => isSameConversation(item, current)) ?? current : null);
    } catch {
      setError('Sohbetler yüklenemedi. Lütfen tekrar deneyin.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [filter, search, token]);

  useEffect(() => { void loadSummaries(); }, [loadSummaries]);

  if (!user || !isStaffRole(user.role)) return <Redirect href={'/(app)/(tabs)' as Href} />;
  if (loading) return <LoadingView label="Sohbetler yükleniyor..." />;

  async function selectConversation(summary: ConversationSummary) {
    if (!token) return;
    setSelected(summary); setDetail(null); setLoadingDetail(true); setError(null); setReplyTicketId(null); setReplyDraft('');
    try {
      const response = await conversationService.getConversation(token, summary.requesterType, summary.requesterId);
      await conversationService.markRead(token, summary.requesterType, summary.requesterId);
      setDetail({ ...response, isUnread: false });
      setSelected({ ...summary, isUnread: false });
      await loadSummaries();
    } catch {
      setError('Sohbet ayrıntısı yüklenemedi. Lütfen tekrar deneyin.');
    } finally { setLoadingDetail(false); }
  }

  async function refreshDetail() {
    if (!token || !selected) return;
    const response = await conversationService.getConversation(token, selected.requesterType, selected.requesterId);
    setDetail(response);
    await loadSummaries();
  }

  async function togglePin() {
    if (!token || !selected || pinning) return;
    const nextPinned = !selected.isPinned;
    setPinning(true); setError(null);
    try {
      await conversationService.setPinned(token, selected.requesterType, selected.requesterId, nextPinned);
      setSelected(current => current ? { ...current, isPinned: nextPinned } : current);
      setDetail(current => current ? { ...current, isPinned: nextPinned } : current);
      await loadSummaries();
    } catch {
      setError('Sohbet sabitlemesi güncellenemedi.');
    } finally { setPinning(false); }
  }

  async function sendReply() {
    if (!token || !replyTicketId || !replyDraft.trim()) return;
    setSendingReply(true); setError(null);
    try {
      await ticketService.addMessage(token, String(replyTicketId), replyDraft.trim(), false);
      setReplyDraft(''); setReplyTicketId(null);
      await refreshDetail();
    } catch (cause) {
      const status = cause instanceof Error && 'status' in cause ? Number(cause.status) : null;
      if (status === 502) {
        await refreshDetail();
        setReplyDraft(''); setReplyTicketId(null);
        setError('Yanıt kaydedildi ancak e-posta gönderilemedi.');
      } else {
        setError('Yanıt gönderilemedi.');
      }
    } finally { setSendingReply(false); }
  }

  if (selected) return <ConversationDetailScreen detail={detail} error={error} loading={loadingDetail} onBack={() => { setSelected(null); setDetail(null); setReplyTicketId(null); }} onOpenTicket={ticketId => router.push({ pathname: '/(app)/tickets/[id]', params: { id: String(ticketId) } })} onPin={togglePin} pinning={pinning} replyDraft={replyDraft} replyTicketId={replyTicketId} sendingReply={sendingReply} setReplyDraft={setReplyDraft} setReplyTicketId={setReplyTicketId} onSendReply={() => void sendReply()} />;

  return <ScreenContainer><FlatList
    data={summaries}
    keyExtractor={item => conversationKey(item)}
    contentContainerStyle={styles.content}
    refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => { setRefreshing(true); void loadSummaries(); }} tintColor={theme.colors.primary} />}
    ListHeaderComponent={<View style={styles.header}><Text style={styles.title}>Sohbetler</Text><Text style={styles.subtitle}>Müşteri ve harici müşteri iletişimlerini yönetin.</Text><AppTextInput label="" onChangeText={setSearch} placeholder="Müşteri, e-posta veya talep ara" value={search} /><View style={styles.filterRow}>{filters.map(item => <Pressable key={item.value} onPress={() => setFilter(item.value)} style={[styles.filterChip, filter === item.value && styles.filterChipActive]}><Text style={[styles.filterText, filter === item.value && styles.filterTextActive]}>{item.label} ({countForFilter(counts, item.value)})</Text></Pressable>)}</View>{error ? <ErrorMessage message={error} /> : null}</View>}
    renderItem={({ item }) => <ConversationCard item={item} onPress={() => void selectConversation(item)} />}
    ItemSeparatorComponent={() => <View style={styles.separator} />}
    ListEmptyComponent={<View style={styles.empty}><Ionicons color={theme.colors.textMuted} name="chatbubbles-outline" size={36} /><Text style={styles.emptyTitle}>Sohbet bulunamadı</Text><Text style={styles.emptyText}>Arama veya filtre ölçütlerini değiştirmeyi deneyin.</Text></View>}
  /></ScreenContainer>;
}

function ConversationDetailScreen({ detail, error, loading, onBack, onOpenTicket, onPin, pinning, replyDraft, replyTicketId, sendingReply, setReplyDraft, setReplyTicketId, onSendReply }: { detail: ConversationDetail | null; error: string | null; loading: boolean; onBack: () => void; onOpenTicket: (id: number) => void; onPin: () => void; pinning: boolean; replyDraft: string; replyTicketId: number | null; sendingReply: boolean; setReplyDraft: (value: string) => void; setReplyTicketId: (value: number | null) => void; onSendReply: () => void }) {
  if (loading) return <LoadingView label="Sohbet yükleniyor..." />;
  if (!detail) return <ScreenContainer><View style={styles.content}><AppButton label="Listeye Dön" onPress={onBack} variant="secondary" />{error ? <ErrorMessage message={error} /> : null}</View></ScreenContainer>;
  return <ScreenContainer><ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
    <AppButton label="Sohbet Listesine Dön" onPress={onBack} variant="secondary" />
    {error ? <ErrorMessage message={error} /> : null}
    <View style={styles.requesterCard}><View style={styles.requesterHeading}><View style={styles.requesterText}><Text style={styles.title}>{detail.displayName}</Text><Text style={styles.subtitle}>{detail.email}</Text></View><Pressable accessibilityLabel="Sohbeti sabitle" disabled={pinning} onPress={onPin}><Ionicons color={detail.isPinned ? theme.colors.warning : theme.colors.textMuted} name={detail.isPinned ? 'pin' : 'pin-outline'} size={24} /></Pressable></View><Text style={styles.metadata}>{detail.ticketCount} talep · {detail.openTicketCount} açık talep</Text></View>
    {detail.tickets.map(ticket => <ConversationTicketCard key={ticket.id} onOpenTicket={onOpenTicket} replyDraft={replyDraft} replyTicketId={replyTicketId} sendingReply={sendingReply} setReplyDraft={setReplyDraft} setReplyTicketId={setReplyTicketId} ticket={ticket} onSendReply={onSendReply} />)}
  </ScrollView></ScreenContainer>;
}

function ConversationCard({ item, onPress }: { item: ConversationSummary; onPress: () => void }) {
  const initial = item.displayName.trim().charAt(0).toLocaleUpperCase('tr-TR') || '?';
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.card, item.isUnread && styles.unreadCard, pressed && styles.pressed]}><View style={styles.cardBody}><View style={styles.avatar}><Text style={styles.avatarText}>{initial}</Text></View><View style={styles.cardText}><View style={styles.cardTop}><Text numberOfLines={1} style={styles.cardTitle}>{item.displayName}</Text><Text style={styles.date}>{formatDateTime(item.lastActivityAt)}</Text>{item.isPinned ? <Ionicons color={theme.colors.warning} name="pin" size={15} /> : null}</View><Text numberOfLines={1} style={styles.muted}>{item.email}</Text><Text numberOfLines={2} style={styles.preview}>{item.lastMessagePreview || item.latestTicketTitle}</Text><View style={styles.conversationMeta}>{item.isUnread ? <Text style={styles.unreadLabel}>Okunmamış</Text> : null}{item.isAwaitingStaffReply ? <Text style={styles.awaitingLabel}>Yanıt bekliyor ({item.awaitingReplyCount})</Text> : null}</View></View></View></Pressable>;
}

function ConversationTicketCard({ ticket, onOpenTicket, replyDraft, replyTicketId, sendingReply, setReplyDraft, setReplyTicketId, onSendReply }: { ticket: ConversationTicket; onOpenTicket: (id: number) => void; replyDraft: string; replyTicketId: number | null; sendingReply: boolean; setReplyDraft: (value: string) => void; setReplyTicketId: (value: number | null) => void; onSendReply: () => void }) {
  const replying = replyTicketId === ticket.id;
  return <View style={styles.ticketCard}><Pressable onPress={() => onOpenTicket(ticket.id)}><Text style={styles.number}>{ticket.ticketNumber}</Text><Text style={styles.ticketTitle}>{ticket.title}</Text><View style={styles.chips}><TicketStatusChip status={ticket.status} /><TicketPriorityChip priority={ticket.priority} /></View></Pressable>
    {ticket.initialMessage ? <MessageCard message={ticket.initialMessage} name="İlk Talep" supportReply={false} timestamp={ticket.createdAt} /> : null}
    {ticket.messages.map(message => <MessageCard key={message.id} message={message.message} name={message.senderName || message.senderLabel} supportReply={message.isSupportReply} timestamp={message.createdAt} />)}
    {ticket.attachments.length ? <Text style={styles.attachmentHint}>Ek dosyalar: {ticket.attachments.map(item => `${item.fileName} (${formatFileSize(item.fileSize)})`).join(', ')}</Text> : null}
    <AppButton label={replying ? 'Yanıtlanıyor' : 'Müşteriye Yanıtla'} onPress={() => { setReplyTicketId(replying ? null : ticket.id); setReplyDraft(''); }} variant="secondary" />
    {replying ? <View style={styles.replyComposer}><AppTextInput label="Müşteriye Yanıt" multiline numberOfLines={4} onChangeText={setReplyDraft} placeholder="Yanıtınızı yazın" style={styles.replyInput} textAlignVertical="top" value={replyDraft} /><AppButton disabled={!replyDraft.trim() || sendingReply} label="Yanıtı Gönder" loading={sendingReply} onPress={onSendReply} /></View> : null}
  </View>;
}

function MessageCard({ message, name, supportReply, timestamp }: { message: string; name: string; supportReply: boolean; timestamp: string }) { return <View style={[styles.message, supportReply ? styles.supportMessage : styles.requesterMessage]}><Text style={styles.messageName}>{name}</Text><Text style={styles.messageBody}>{message}</Text><Text style={styles.messageDate}>{formatDateTime(timestamp)}</Text></View>; }

function conversationKey(item: Pick<ConversationSummary, 'requesterType' | 'requesterId'>) { return `${item.requesterType}-${item.requesterId}`; }
function isSameConversation(left: ConversationSummary, right: ConversationSummary) { return conversationKey(left) === conversationKey(right); }
function countForFilter(counts: { all: number; awaitingReply: number; unread: number; pinned: number }, filter: ConversationFilter) { return filter === 'All' ? counts.all : filter === 'AwaitingReply' ? counts.awaitingReply : filter === 'Unread' ? counts.unread : counts.pinned; }

const styles = StyleSheet.create({
  screen: { backgroundColor: theme.colors.background, flex: 1 }, content: { gap: theme.spacing.sm, padding: theme.spacing.md, paddingBottom: theme.spacing.xl }, header: { gap: theme.spacing.xs, marginBottom: theme.spacing.xs }, title: { color: theme.colors.text, fontSize: 24, fontWeight: '800', letterSpacing: -0.5 }, subtitle: { color: theme.colors.textMuted, fontSize: 13, lineHeight: 19 }, filterRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 6 }, filterChip: { borderColor: theme.colors.border, borderRadius: theme.radius.pill, borderWidth: 1, paddingHorizontal: 9, paddingVertical: 6 }, filterChipActive: { backgroundColor: theme.colors.primarySoft, borderColor: theme.colors.primary }, filterText: { color: theme.colors.textMuted, fontSize: 11, fontWeight: '700' }, filterTextActive: { color: theme.colors.primary },
  card: { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, padding: theme.spacing.sm, ...theme.shadow.card }, unreadCard: { backgroundColor: '#FBFAFF', borderColor: '#D9CCFF' }, pressed: { opacity: 0.8 }, cardBody: { flexDirection: 'row', gap: theme.spacing.sm }, avatar: { alignItems: 'center', backgroundColor: theme.colors.primarySoft, borderRadius: theme.radius.sm, height: 34, justifyContent: 'center', width: 34 }, avatarText: { color: theme.colors.primary, fontSize: 14, fontWeight: '800' }, cardText: { flex: 1 }, cardTop: { alignItems: 'center', flexDirection: 'row', gap: 6 }, cardTitle: { color: theme.colors.text, flex: 1, fontSize: 15, fontWeight: '800' }, preview: { color: theme.colors.text, fontSize: 13, lineHeight: 18, marginTop: 3 }, conversationMeta: { alignItems: 'center', flexDirection: 'row', flexWrap: 'wrap', gap: 7, marginTop: 5 }, unreadLabel: { color: theme.colors.primary, fontSize: 11, fontWeight: '800' }, awaitingLabel: { color: theme.colors.warning, fontSize: 11, fontWeight: '800' }, date: { color: theme.colors.textMuted, fontSize: 10 }, separator: { height: theme.spacing.xs }, empty: { alignItems: 'center', paddingHorizontal: theme.spacing.xl, paddingVertical: 64 }, emptyTitle: { color: theme.colors.text, fontSize: 18, fontWeight: '800', marginTop: theme.spacing.md }, emptyText: { color: theme.colors.textMuted, lineHeight: 21, marginTop: 6, textAlign: 'center' },
  requesterCard: { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, padding: theme.spacing.md, ...theme.shadow.card }, requesterHeading: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.sm, justifyContent: 'space-between' }, requesterText: { flex: 1 }, metadata: { color: theme.colors.textMuted, fontSize: 13, marginTop: theme.spacing.sm }, ticketCard: { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, gap: theme.spacing.sm, padding: theme.spacing.md, ...theme.shadow.card }, number: { color: theme.colors.primary, fontSize: 12, fontWeight: '800', letterSpacing: 0.4 }, ticketTitle: { color: theme.colors.text, fontSize: 18, fontWeight: '800', marginTop: 4 }, chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: theme.spacing.sm }, message: { borderRadius: theme.radius.md, padding: theme.spacing.sm }, requesterMessage: { backgroundColor: theme.colors.surfaceMuted }, supportMessage: { backgroundColor: theme.colors.primarySoft, marginLeft: theme.spacing.lg }, messageName: { color: theme.colors.primary, fontSize: 12, fontWeight: '800' }, messageBody: { color: theme.colors.text, fontSize: 14, lineHeight: 21, marginTop: 3 }, messageDate: { color: theme.colors.textMuted, fontSize: 11, marginTop: 6 }, attachmentHint: { color: theme.colors.textMuted, fontSize: 12, lineHeight: 18 }, replyComposer: { borderTopColor: theme.colors.border, borderTopWidth: 1, gap: theme.spacing.sm, paddingTop: theme.spacing.md }, replyInput: { minHeight: 96, paddingTop: theme.spacing.md }, muted: { color: theme.colors.textMuted, fontSize: 13 },
});

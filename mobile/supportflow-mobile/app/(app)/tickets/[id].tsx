import { AppButton } from '@/components/AppButton';
import { AppTextInput } from '@/components/AppTextInput';
import { ErrorMessage } from '@/components/ErrorMessage';
import { LoadingView } from '@/components/LoadingView';
import { ScreenContainer } from '@/components/ScreenContainer';
import { TicketPriorityChip, TicketStatusChip } from '@/components/TicketChips';
import { useAuth } from '@/context/AuthContext';
import { ticketService } from '@/services/ticketService';
import { userService } from '@/services/userService';
import { theme } from '@/theme/theme';
import { isStaffRole } from '@/types/auth';
import type { TicketAttachment, TicketDetail, TicketMessage, TicketPriority, TicketStatus, TimelineItem } from '@/types/tickets';
import type { TechnicalStaffUser } from '@/types/users';
import { formatDateTime, formatFileSize } from '@/utils/formatDate';
import * as DocumentPicker from 'expo-document-picker';
import * as Sharing from 'expo-sharing';
import { Ionicons } from '@expo/vector-icons';
import { router, useLocalSearchParams } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

const priorityOptions: { value: TicketPriority; label: string }[] = [
  { value: 'Low', label: 'Düşük' }, { value: 'Normal', label: 'Normal' }, { value: 'High', label: 'Yüksek' }, { value: 'Critical', label: 'Kritik' },
];

const allowedNextStatuses: Record<TicketStatus, TicketStatus[]> = {
  New: ['Assigned', 'InProgress', 'WaitingForCustomer', 'Resolved', 'Closed'],
  Assigned: ['InProgress', 'WaitingForCustomer', 'Resolved', 'Closed'],
  InProgress: ['WaitingForCustomer', 'Resolved', 'Closed'],
  WaitingForCustomer: ['InProgress', 'Resolved', 'Closed'],
  Resolved: ['Closed', 'InProgress', 'Reopened'],
  Closed: ['Reopened'],
  Reopened: ['Assigned', 'InProgress', 'WaitingForCustomer', 'Resolved', 'Closed'],
};

const statusLabels: Record<TicketStatus, string> = {
  New: 'Yeni', Assigned: 'Atandı', InProgress: 'İşlemde', WaitingForCustomer: 'Müşteri Bekleniyor', Resolved: 'Çözüldü', Closed: 'Kapalı', Reopened: 'Yeniden Açıldı',
};

export default function TicketDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { token, user } = useAuth();
  const [ticket, setTicket] = useState<TicketDetail | null>(null);
  const [messages, setMessages] = useState<TicketMessage[]>([]);
  const [attachments, setAttachments] = useState<TicketAttachment[]>([]);
  const [timeline, setTimeline] = useState<TimelineItem[]>([]);
  const [timelinePage, setTimelinePage] = useState(1);
  const [hasMoreTimeline, setHasMoreTimeline] = useState(false);
  const [loadingMoreTimeline, setLoadingMoreTimeline] = useState(false);
  const [technicalStaff, setTechnicalStaff] = useState<TechnicalStaffUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [notFound, setNotFound] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [draft, setDraft] = useState('');
  const [messageMode, setMessageMode] = useState<'public' | 'internal'>('public');
  const [sending, setSending] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [operationLoading, setOperationLoading] = useState<'assign' | 'status' | 'priority' | null>(null);
  const [selectedAssigneeId, setSelectedAssigneeId] = useState<number | null>(null);
  const [selectedStatus, setSelectedStatus] = useState<TicketStatus | null>(null);
  const [selectedPriority, setSelectedPriority] = useState<TicketPriority | null>(null);
  const [statusNote, setStatusNote] = useState('');
  const openingAttachmentIdRef = useRef<number | null>(null);
  const isStaff = user ? isStaffRole(user.role) : false;

  const load = useCallback(async () => {
    if (!token || !id) return;
    setError(null);
    try {
      const [nextTicket, nextMessages, nextAttachments, nextTimeline, nextStaff] = await Promise.all([
        ticketService.getTicket(token, id), ticketService.getMessages(token, id), ticketService.getAttachments(token, id), ticketService.getTimeline(token, id),
        isStaff ? userService.getTechnicalStaff(token).catch(() => []) : Promise.resolve([]),
      ]);
      setTicket(nextTicket);
      setMessages(nextMessages);
      setAttachments(nextAttachments);
      setTimeline(nextTimeline.items);
      setTimelinePage(nextTimeline.pagination.page);
      setHasMoreTimeline(nextTimeline.pagination.hasNextPage);
      setTechnicalStaff(nextStaff);
      setSelectedAssigneeId(nextTicket.assignedUserId);
      setSelectedPriority(nextTicket.priority);
      setSelectedStatus(null);
      setNotFound(false);
    } catch (cause) {
      const status = cause instanceof Error && 'status' in cause ? Number(cause.status) : null;
      setNotFound(status === 404);
      setError(status === 404 ? null : 'Bir hata oluştu. Lütfen tekrar deneyin.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [id, isStaff, token]);

  useEffect(() => { void load(); }, [load]);

  async function sendMessage() {
    const message = draft.trim();
    if (!message || !token || !id) return;
    const isInternal = isStaff && messageMode === 'internal';
    setSending(true);
    setError(null);
    setSuccess(null);
    try {
      await ticketService.addMessage(token, id, message, isInternal);
      setDraft('');
      await load();
      setSuccess(isInternal ? 'İç not eklendi.' : 'Yanıt gönderildi.');
    } catch (cause) {
      const status = cause instanceof Error && 'status' in cause ? Number(cause.status) : null;
      if (!isInternal && status === 502) {
        await load();
        setDraft('');
        setError('Yanıt kaydedildi ancak e-posta gönderilemedi. Daha sonra yeniden deneyebilirsiniz.');
      } else {
        setError(isInternal ? 'İç not eklenemedi.' : 'Mesaj gönderilemedi.');
      }
    } finally {
      setSending(false);
    }
  }

  async function pickAndUpload() {
    if (!token || !id) return;
    const result = await DocumentPicker.getDocumentAsync({ copyToCacheDirectory: true, multiple: false });
    if (result.canceled || !result.assets[0]) return;
    const asset = result.assets[0];
    setUploading(true);
    setError(null);
    try {
      await ticketService.uploadAttachment(token, id, { uri: asset.uri, name: asset.name, mimeType: asset.mimeType ?? undefined });
      await load();
      setSuccess('Dosya eklendi.');
    } catch {
      setError('Dosya yüklenemedi.');
    } finally {
      setUploading(false);
    }
  }

  async function openAttachment(attachment: TicketAttachment) {
    if (!token || !id || openingAttachmentIdRef.current !== null) return;
    openingAttachmentIdRef.current = attachment.id;
    setError(null);
    try {
      const file = await ticketService.downloadAttachment(token, id, attachment);
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(file.uri, { mimeType: attachment.contentType, dialogTitle: attachment.fileName });
      } else {
        setError('Bu cihazda dosya paylaşımı kullanılamıyor.');
      }
    } catch {
      setError('Dosya açılamadı. Lütfen tekrar deneyin.');
    } finally {
      openingAttachmentIdRef.current = null;
    }
  }

  async function updateAssignment() {
    if (!token || !id || !selectedAssigneeId) return;
    setOperationLoading('assign'); setError(null); setSuccess(null);
    try {
      await ticketService.assignTicket(token, id, selectedAssigneeId);
      await load();
      setSuccess('Atama güncellendi.');
    } catch {
      setError('Talep atanamadı.');
    } finally { setOperationLoading(null); }
  }

  async function updateStatus() {
    if (!token || !id || !selectedStatus) return;
    setOperationLoading('status'); setError(null); setSuccess(null);
    try {
      await ticketService.updateStatus(token, id, selectedStatus, statusNote);
      setStatusNote('');
      await load();
      setSuccess('Talep durumu güncellendi.');
    } catch {
      setError('Talep durumu güncellenemedi.');
    } finally { setOperationLoading(null); }
  }

  async function updatePriority() {
    if (!token || !id || !selectedPriority) return;
    setOperationLoading('priority'); setError(null); setSuccess(null);
    try {
      await ticketService.updatePriority(token, id, selectedPriority);
      await load();
      setSuccess('Talep önceliği güncellendi.');
    } catch {
      setError('Talep önceliği güncellenemedi.');
    } finally { setOperationLoading(null); }
  }

  async function loadMoreTimeline() {
    if (!token || !id || !hasMoreTimeline || loadingMoreTimeline) return;
    setLoadingMoreTimeline(true);
    try {
      const response = await ticketService.getTimeline(token, id, timelinePage + 1);
      setTimeline(current => [...current, ...response.items.filter(item => !current.some(existing => existing.id === item.id))]);
      setTimelinePage(response.pagination.page);
      setHasMoreTimeline(response.pagination.hasNextPage);
    } catch {
      setError('Zaman çizelgesi yüklenemedi. Lütfen tekrar deneyin.');
    } finally { setLoadingMoreTimeline(false); }
  }

  if (loading) return <LoadingView label="Talep yükleniyor..." />;
  if (notFound) return <SafeMessage icon="alert-circle-outline" message="Talep bulunamadı." />;
  if (!ticket) return <SafeMessage icon="cloud-offline-outline" message="Bir hata oluştu. Lütfen tekrar deneyin." />;

  return <ScreenContainer>
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.screen}>
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled" refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => { setRefreshing(true); void load(); }} tintColor={theme.colors.primary} />}>
      <View style={styles.header}><Pressable accessibilityLabel="Taleplere dön" onPress={() => router.back()} style={styles.backButton}><Ionicons color={theme.colors.primary} name="arrow-back" size={21} /></Pressable><View style={styles.headerText}><Text style={styles.headerEyebrow}>TALEP DETAYI</Text><Text numberOfLines={1} style={styles.headerTitle}>{ticket.ticketNumber}</Text></View></View>
      {error ? <ErrorMessage message={error} /> : null}{success ? <Text style={styles.success}>{success}</Text> : null}
      <View style={styles.card}><Text style={styles.number}>{ticket.ticketNumber}</Text><Text style={styles.title}>{ticket.title}</Text><View style={styles.chips}><TicketStatusChip status={ticket.status} /><TicketPriorityChip priority={ticket.priority} />{ticket.category ? <Text style={styles.category}>{ticket.category.name}</Text> : null}</View><Text style={styles.date}>Oluşturuldu: {formatDateTime(ticket.createdAt)}</Text><Text style={styles.date}>Güncellendi: {formatDateTime(ticket.updatedAt)}</Text></View>
      {isStaff ? <Section title="Talep Bilgileri"><Text style={styles.body}>Talep sahibi: {ticket.customerName}</Text><Text style={styles.muted}>{ticket.customerEmail}</Text><Text style={styles.body}>Atanan: {ticket.assignedUserName || 'Atanmadı'}</Text>{ticket.requesterType === 'ExternalRequester' ? <Text style={styles.muted}>Harici e-posta talebi</Text> : null}</Section> : null}
      <Section title="Açıklama"><Text style={styles.body}>{ticket.description}</Text></Section>
      {isStaff ? <StaffOperations assigneeId={selectedAssigneeId} loading={operationLoading} onAssign={updateAssignment} onPriority={updatePriority} onSelectAssignee={setSelectedAssigneeId} onSelectPriority={setSelectedPriority} onSelectStatus={setSelectedStatus} onStatus={updateStatus} priority={selectedPriority} status={ticket.status} statusNote={statusNote} statusSelection={selectedStatus} staff={technicalStaff} onStatusNote={setStatusNote} /> : null}
      <Section title="Mesajlar">
        {messages.length ? messages.map(message => <MessageBubble currentUserId={user?.id} key={message.id} message={message} />) : <Text style={styles.muted}>Henüz mesaj yok.</Text>}
        {isStaff ? <View style={styles.modeRow}><ModeButton active={messageMode === 'public'} label="Müşteriye Yanıt" onPress={() => setMessageMode('public')} /><ModeButton active={messageMode === 'internal'} label="İç Not" onPress={() => setMessageMode('internal')} /></View> : null}
        {isStaff && messageMode === 'internal' ? <Text style={styles.internalHint}>İç not müşteriye gösterilmez.</Text> : null}
        <View style={styles.composer}><AppTextInput label={isStaff && messageMode === 'internal' ? 'İç Not' : 'Yanıtınız'} multiline maxLength={5000} numberOfLines={4} onChangeText={setDraft} placeholder={isStaff && messageMode === 'internal' ? 'Ekibiniz için not yazın' : 'Mesajınızı yazın'} style={styles.messageInput} textAlignVertical="top" value={draft} /><AppButton disabled={!draft.trim() || sending} label={isStaff && messageMode === 'internal' ? 'İç Not Ekle' : 'Gönder'} loading={sending} onPress={() => void sendMessage()} /></View>
      </Section>
      <Section title="Ek Dosyalar" action={<Pressable accessibilityLabel="Dosya ekle" disabled={uploading} onPress={() => void pickAndUpload()} style={styles.iconButton}>{uploading ? <Text style={styles.uploading}>Yükleniyor</Text> : <Ionicons color={theme.colors.primary} name="attach-outline" size={22} />}</Pressable>}>
        {attachments.length ? attachments.map(attachment => <Pressable key={attachment.id} onPress={() => void openAttachment(attachment)} style={styles.attachment}><Ionicons color={theme.colors.primary} name={attachment.isImage ? 'image-outline' : 'document-outline'} size={22} /><View style={styles.attachmentText}><Text numberOfLines={1} style={styles.attachmentName}>{attachment.fileName}</Text><Text style={styles.muted}>{formatFileSize(attachment.fileSize)} · {formatDateTime(attachment.createdAt)}</Text></View><Ionicons color={theme.colors.textMuted} name="open-outline" size={18} /></Pressable>) : <Text style={styles.muted}>Ek dosya yok.</Text>}
      </Section>
      <Section title="Zaman Çizelgesi">{timeline.length ? timeline.map(item => <TimelineRow item={item} key={item.id} />) : <Text style={styles.muted}>Gösterilecek etkinlik yok.</Text>}{hasMoreTimeline ? <AppButton disabled={loadingMoreTimeline} label="Daha Fazla Yükle" loading={loadingMoreTimeline} onPress={() => void loadMoreTimeline()} variant="secondary" /> : null}</Section>
    </ScrollView>
    </KeyboardAvoidingView>
  </ScreenContainer>;
}

function StaffOperations({ assigneeId, loading, onAssign, onPriority, onSelectAssignee, onSelectPriority, onSelectStatus, onStatus, priority, status, statusNote, statusSelection, staff, onStatusNote }: { assigneeId: number | null; loading: string | null; onAssign: () => void; onPriority: () => void; onSelectAssignee: (value: number) => void; onSelectPriority: (value: TicketPriority) => void; onSelectStatus: (value: TicketStatus) => void; onStatus: () => void; priority: TicketPriority | null; status: TicketStatus; statusNote: string; statusSelection: TicketStatus | null; staff: TechnicalStaffUser[]; onStatusNote: (value: string) => void }) {
  return <Section title="Talep İşlemleri">
    <Text style={styles.operationLabel}>Atama</Text><OptionRow options={staff.map(item => ({ label: `${item.firstName} ${item.lastName}`, value: item.id }))} selected={assigneeId} onSelect={onSelectAssignee} />
    <AppButton disabled={!assigneeId || loading !== null} label="Atamayı Güncelle" loading={loading === 'assign'} onPress={onAssign} variant="secondary" />
    <Text style={styles.operationLabel}>Durum</Text><OptionRow options={allowedNextStatuses[status].map(value => ({ label: statusLabels[value], value }))} selected={statusSelection} onSelect={onSelectStatus} />
    <AppTextInput label="Durum notu (opsiyonel)" maxLength={1000} onChangeText={onStatusNote} placeholder="Değişiklik notu" value={statusNote} />
    <AppButton disabled={!statusSelection || loading !== null} label="Durumu Güncelle" loading={loading === 'status'} onPress={onStatus} variant="secondary" />
    <Text style={styles.operationLabel}>Öncelik</Text><OptionRow options={priorityOptions} selected={priority} onSelect={onSelectPriority} />
    <AppButton disabled={!priority || loading !== null} label="Önceliği Güncelle" loading={loading === 'priority'} onPress={onPriority} variant="secondary" />
  </Section>;
}

function OptionRow<T extends string | number>({ options, selected, onSelect }: { options: { label: string; value: T }[]; selected: T | null; onSelect: (value: T) => void }) {
  return <View style={styles.options}>{options.map(option => <Pressable key={String(option.value)} onPress={() => onSelect(option.value)} style={[styles.option, selected === option.value && styles.optionSelected]}><Text style={[styles.optionText, selected === option.value && styles.optionTextSelected]}>{option.label}</Text></Pressable>)}</View>;
}

function ModeButton({ active, label, onPress }: { active: boolean; label: string; onPress: () => void }) {
  return <Pressable onPress={onPress} style={[styles.modeButton, active && styles.modeButtonActive]}><Text style={[styles.modeText, active && styles.modeTextActive]}>{label}</Text></Pressable>;
}

function Section({ title, children, action }: { title: string; children: React.ReactNode; action?: React.ReactNode }) { return <View style={styles.section}><View style={styles.sectionHeader}><Text style={styles.sectionTitle}>{title}</Text>{action}</View>{children}</View>; }

function MessageBubble({ currentUserId, message }: { currentUserId?: number; message: TicketMessage }) {
  const own = message.senderId === currentUserId;
  return <View style={[styles.message, message.isInternal ? styles.internalMessage : own ? styles.ownMessage : styles.staffMessage]}><Text style={styles.messageName}>{message.isInternal ? 'İç Not' : own ? 'Siz' : message.senderName || 'Destek Ekibi'}</Text><Text style={styles.messageBody}>{message.message}</Text><Text style={styles.messageDate}>{formatDateTime(message.createdAt)}</Text></View>;
}

function TimelineRow({ item }: { item: TimelineItem }) { return <View style={styles.timelineRow}><View style={styles.timelineDot} /><View style={styles.timelineText}><Text style={styles.timelineTitle}>{item.title}</Text>{item.summary ? <Text style={styles.muted}>{item.summary}</Text> : null}<Text style={styles.timelineDate}>{item.actor.displayName} · {formatDateTime(item.occurredAt)}</Text></View></View>; }
function SafeMessage({ icon, message }: { icon: keyof typeof Ionicons.glyphMap; message: string }) { return <ScreenContainer><View style={styles.safeMessage}><Ionicons color={theme.colors.textMuted} name={icon} size={38} /><Text style={styles.safeMessageText}>{message}</Text><AppButton label="Geri dön" onPress={() => router.back()} variant="secondary" /></View></ScreenContainer>; }

const styles = StyleSheet.create({
  screen: { backgroundColor: theme.colors.background, flex: 1 }, content: { gap: theme.spacing.sm, padding: theme.spacing.md, paddingBottom: 40 }, header: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.sm }, backButton: { alignItems: 'center', backgroundColor: theme.colors.primarySoft, borderRadius: theme.radius.pill, height: 36, justifyContent: 'center', width: 36 }, headerText: { flex: 1 }, headerEyebrow: { color: theme.colors.textMuted, fontSize: 10, fontWeight: '800', letterSpacing: 0.9 }, headerTitle: { color: theme.colors.text, fontSize: 17, fontWeight: '800', marginTop: 1 }, success: { backgroundColor: '#DCFCE7', borderRadius: theme.radius.sm, color: theme.colors.success, fontSize: 13, fontWeight: '700', padding: theme.spacing.xs }, card: { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, padding: theme.spacing.sm, ...theme.shadow.card }, number: { color: theme.colors.primary, fontSize: 11, fontWeight: '800', letterSpacing: 0.4 }, title: { color: theme.colors.text, fontSize: 20, fontWeight: '800', letterSpacing: -0.4, marginTop: 4 }, chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: theme.spacing.sm }, category: { backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.pill, color: theme.colors.textMuted, fontSize: 11, fontWeight: '700', overflow: 'hidden', paddingHorizontal: 8, paddingVertical: 4 }, date: { color: theme.colors.textMuted, fontSize: 11, marginTop: 5 }, section: { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, gap: theme.spacing.xs, padding: theme.spacing.sm, ...theme.shadow.card }, sectionHeader: { alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' }, sectionTitle: { color: theme.colors.text, fontSize: 15, fontWeight: '800' }, body: { color: theme.colors.text, fontSize: 14, lineHeight: 21 }, muted: { color: theme.colors.textMuted, fontSize: 12, lineHeight: 18 }, operationLabel: { color: theme.colors.text, fontSize: 12, fontWeight: '800', marginTop: theme.spacing.xs, textTransform: 'uppercase' }, options: { flexDirection: 'row', flexWrap: 'wrap', gap: 6 }, option: { borderColor: theme.colors.border, borderRadius: theme.radius.pill, borderWidth: 1, paddingHorizontal: 9, paddingVertical: 6 }, optionSelected: { backgroundColor: theme.colors.primarySoft, borderColor: theme.colors.primary }, optionText: { color: theme.colors.textMuted, fontSize: 12, fontWeight: '700' }, optionTextSelected: { color: theme.colors.primary }, composer: { borderTopColor: theme.colors.border, borderTopWidth: 1, gap: theme.spacing.xs, marginTop: theme.spacing.xs, paddingTop: theme.spacing.sm }, messageInput: { minHeight: 88, paddingTop: theme.spacing.sm }, modeRow: { flexDirection: 'row', gap: 6 }, modeButton: { borderColor: theme.colors.border, borderRadius: theme.radius.pill, borderWidth: 1, paddingHorizontal: 10, paddingVertical: 7 }, modeButtonActive: { backgroundColor: theme.colors.primarySoft, borderColor: theme.colors.primary }, modeText: { color: theme.colors.textMuted, fontSize: 12, fontWeight: '700' }, modeTextActive: { color: theme.colors.primary }, internalHint: { backgroundColor: '#FFF7E6', borderRadius: theme.radius.sm, color: theme.colors.warning, fontSize: 12, fontWeight: '700', padding: theme.spacing.xs }, message: { borderRadius: theme.radius.md, marginTop: 3, padding: theme.spacing.xs }, ownMessage: { alignSelf: 'flex-end', backgroundColor: theme.colors.primarySoft, maxWidth: '92%' }, staffMessage: { alignSelf: 'flex-start', backgroundColor: theme.colors.surfaceMuted, maxWidth: '92%' }, internalMessage: { alignSelf: 'stretch', backgroundColor: '#FFF7E6', borderColor: '#F6D99F', borderWidth: 1 }, messageName: { color: theme.colors.primary, fontSize: 11, fontWeight: '800' }, messageBody: { color: theme.colors.text, fontSize: 14, lineHeight: 20, marginTop: 2 }, messageDate: { color: theme.colors.textMuted, fontSize: 10, marginTop: 5 }, iconButton: { alignItems: 'center', backgroundColor: theme.colors.primarySoft, borderRadius: theme.radius.pill, height: 32, justifyContent: 'center', width: 32 }, uploading: { color: theme.colors.primary, fontSize: 11, fontWeight: '700' }, attachment: { alignItems: 'center', borderTopColor: theme.colors.border, borderTopWidth: 1, flexDirection: 'row', gap: theme.spacing.sm, paddingTop: theme.spacing.sm }, attachmentText: { flex: 1 }, attachmentName: { color: theme.colors.text, fontSize: 13, fontWeight: '700' }, timelineRow: { flexDirection: 'row', gap: theme.spacing.sm }, timelineDot: { backgroundColor: theme.colors.primary, borderRadius: 4, height: 8, marginTop: 5, width: 8 }, timelineText: { flex: 1 }, timelineTitle: { color: theme.colors.text, fontSize: 13, fontWeight: '700' }, timelineDate: { color: theme.colors.textMuted, fontSize: 11, marginTop: 2 }, safeMessage: { alignItems: 'center', backgroundColor: theme.colors.background, flex: 1, gap: theme.spacing.md, justifyContent: 'center', padding: theme.spacing.xl }, safeMessageText: { color: theme.colors.text, fontSize: 17, fontWeight: '700' },
});

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
import type { TicketFilters, TicketListItem, TicketPriority, TicketStatus } from '@/types/tickets';
import type { TechnicalStaffUser } from '@/types/users';
import { formatDateTime } from '@/utils/formatDate';
import { Ionicons } from '@expo/vector-icons';
import { useFocusEffect } from '@react-navigation/native';
import { router } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { FlatList, Modal, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';

const initialFilters: TicketFilters = { search: '', sort: 'UpdatedNewest' };
const customerStatusOptions: { label: string; value?: TicketStatus }[] = [
  { label: 'Tüm durumlar' }, { label: 'Yeni', value: 'New' }, { label: 'İşlemde', value: 'InProgress' },
  { label: 'Müşteri bekleniyor', value: 'WaitingForCustomer' }, { label: 'Çözüldü', value: 'Resolved' }, { label: 'Kapalı', value: 'Closed' },
];
const staffStatusOptions: { label: string; value?: TicketStatus }[] = [
  ...customerStatusOptions, { label: 'Atandı', value: 'Assigned' }, { label: 'Yeniden açıldı', value: 'Reopened' },
];
const priorityOptions: { label: string; value?: TicketPriority }[] = [
  { label: 'Tüm öncelikler' }, { label: 'Düşük', value: 'Low' }, { label: 'Normal', value: 'Normal' },
  { label: 'Yüksek', value: 'High' }, { label: 'Kritik', value: 'Critical' },
];

export default function TicketsScreen() {
  const { token, user } = useAuth();
  const [filters, setFilters] = useState<TicketFilters>(initialFilters);
  const [draftSearch, setDraftSearch] = useState('');
  const [tickets, setTickets] = useState<TicketListItem[]>([]);
  const [technicalStaff, setTechnicalStaff] = useState<TechnicalStaffUser[]>([]);
  const [hasNextPage, setHasNextPage] = useState(false);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filterOpen, setFilterOpen] = useState(false);
  const pageRef = useRef(1);
  const requestRef = useRef(0);
  const hasLoadedRef = useRef(false);
  const isCustomer = user?.role === 'Customer';
  const isAdmin = user?.role === 'Admin';

  const load = useCallback(async (page: number, mode: 'replace' | 'append' = 'replace') => {
    if (!token) return;
    const requestId = ++requestRef.current;
    if (mode === 'append') setLoadingMore(true);
    else if (page === 1 && !hasLoadedRef.current) setLoading(true);
    setError(null);
    try {
      const response = await ticketService.getTickets(token, filters, page);
      if (requestId !== requestRef.current) return;
      pageRef.current = page;
      hasLoadedRef.current = true;
      setHasNextPage(response.pagination.hasNextPage);
      setTickets(current => mode === 'append'
        ? [...current, ...response.items.filter(item => !current.some(ticket => ticket.id === item.id))]
        : response.items);
    } catch {
      if (requestId === requestRef.current) setError('Talepler yüklenemedi. Lütfen tekrar deneyin.');
    } finally {
      if (requestId === requestRef.current) {
        setLoading(false);
        setRefreshing(false);
        setLoadingMore(false);
      }
    }
  }, [filters, token]);

  useEffect(() => {
    const timeout = setTimeout(() => setFilters(current => ({ ...current, search: draftSearch })), 375);
    return () => clearTimeout(timeout);
  }, [draftSearch]);

  useEffect(() => {
    if (!hasLoadedRef.current) void load(1);
  }, [load]);

  useEffect(() => {
    if (!token || isCustomer) return;
    void userService.getTechnicalStaff(token).then(setTechnicalStaff).catch(() => setTechnicalStaff([]));
  }, [isCustomer, token]);

  useFocusEffect(useCallback(() => {
    if (hasLoadedRef.current) void load(1);
  }, [load]));

  function updateFilters(next: Partial<TicketFilters>) {
    setFilters(current => ({ ...current, ...next }));
  }

  const clearFilters = () => updateFilters({ status: undefined, priority: undefined, assignedUserId: undefined, assignmentState: undefined, activeOnly: undefined });
  const hasExtraFilters = Boolean(filters.status || filters.priority || filters.assignedUserId || filters.assignmentState || filters.activeOnly);

  if (loading) return <LoadingView label="Talepler yükleniyor..." />;

  return <ScreenContainer>
    <FlatList
      data={tickets}
      keyExtractor={item => String(item.id)}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => { setRefreshing(true); void load(1); }} tintColor={theme.colors.primary} />}
      ListHeaderComponent={<View style={styles.header}>
        <View style={styles.titleRow}>
          <View><Text style={styles.title}>{isCustomer ? 'Taleplerim' : 'Talepler'}</Text><Text style={styles.subtitle}>{isCustomer ? 'Açık ve geçmiş taleplerinizi takip edin.' : 'Atamaları ve destek taleplerini yönetin.'}</Text></View>
          {isCustomer ? <Pressable accessibilityLabel="Yeni talep oluştur" onPress={() => router.push('/(app)/tickets/new')} style={styles.newButton}><Ionicons color={theme.colors.onPrimary} name="add" size={24} /></Pressable> : null}
        </View>
        <AppTextInput label="" value={draftSearch} onChangeText={setDraftSearch} placeholder={isCustomer ? 'Talep ara' : 'Talep, müşteri veya e-posta ara'} returnKeyType="search" />
        <View style={styles.toolbar}><Pressable onPress={() => setFilterOpen(true)} style={styles.filterButton}><Ionicons color={theme.colors.primary} name="options-outline" size={19} /><Text style={styles.filterText}>Filtrele</Text></Pressable>{hasExtraFilters ? <Pressable onPress={clearFilters}><Text style={styles.clearText}>Temizle</Text></Pressable> : null}</View>
        {error ? <><ErrorMessage message={error} /><AppButton label="Tekrar Dene" onPress={() => void load(1)} variant="secondary" /></> : null}
      </View>}
      renderItem={({ item }) => <TicketCard staffView={!isCustomer} ticket={item} />}
      ItemSeparatorComponent={() => <View style={styles.separator} />}
      ListEmptyComponent={<View style={styles.empty}><Ionicons color={theme.colors.textMuted} name="ticket-outline" size={34} /><Text style={styles.emptyTitle}>Henüz talep yok</Text><Text style={styles.emptyText}>{isCustomer ? 'Yeni bir destek talebi oluşturarak başlayabilirsiniz.' : 'Bu ölçütlere uygun talep bulunmuyor.'}</Text></View>}
      ListFooterComponent={hasNextPage ? <View style={styles.footer}><AppButton disabled={loadingMore} label="Daha Fazla Yükle" loading={loadingMore} onPress={() => void load(pageRef.current + 1, 'append')} variant="secondary" /></View> : null}
    />
    <FilterModal filters={filters} isAdmin={isAdmin} isCustomer={isCustomer} onClose={() => setFilterOpen(false)} onChange={updateFilters} staff={technicalStaff} visible={filterOpen} />
  </ScreenContainer>;
}

function TicketCard({ ticket, staffView }: { ticket: TicketListItem; staffView: boolean }) {
  return <Pressable onPress={() => router.push({ pathname: '/(app)/tickets/[id]', params: { id: String(ticket.id) } })} style={({ pressed }) => [styles.card, pressed && styles.pressed]}>
    <View style={styles.cardTop}><Text style={styles.number}>{ticket.ticketNumber}</Text><TicketStatusChip status={ticket.status} /></View>
    <Text numberOfLines={2} style={styles.cardTitle}>{ticket.title}</Text>
    <View style={styles.cardBottom}><TicketPriorityChip priority={ticket.priority} />{ticket.category ? <View style={styles.categoryChip}><Text style={styles.categoryText}>{ticket.category.name}</Text></View> : null}</View>
    <View style={styles.metadataRow}><Text numberOfLines={1} style={styles.metadata}>{staffView ? `${ticket.customerName} · ${ticket.assignedUserName || 'Atanmadı'}` : 'Talep güncellendi'}</Text><Text style={styles.date}>{formatDateTime(ticket.updatedAt)}</Text></View>
  </Pressable>;
}

function FilterModal({ filters, isAdmin, isCustomer, staff, visible, onClose, onChange }: { filters: TicketFilters; isAdmin: boolean; isCustomer: boolean; staff: TechnicalStaffUser[]; visible: boolean; onClose: () => void; onChange: (next: Partial<TicketFilters>) => void }) {
  const staffOptions = [{ label: 'Tümü' }, { label: 'Atanmamış', value: 'unassigned' }, ...staff.map(item => ({ label: `${item.firstName} ${item.lastName}`, value: String(item.id) }))];
  return <Modal animationType="slide" onRequestClose={onClose} transparent visible={visible}><Pressable onPress={onClose} style={styles.modalBackdrop}><Pressable onPress={event => event.stopPropagation()} style={styles.modal}>
    <View style={styles.modalTitleRow}><Text style={styles.modalTitle}>Filtreler</Text><Pressable accessibilityLabel="Filtreleri kapat" onPress={onClose}><Ionicons color={theme.colors.textMuted} name="close" size={24} /></Pressable></View>
    <Text style={styles.filterLabel}>Durum</Text><FilterOptions options={isCustomer ? customerStatusOptions : staffStatusOptions} selected={filters.status} onSelect={status => onChange({ status })} />
    <Text style={styles.filterLabel}>Öncelik</Text><FilterOptions options={priorityOptions} selected={filters.priority} onSelect={priority => onChange({ priority })} />
    {!isCustomer ? <><Text style={styles.filterLabel}>Atanan personel</Text><FilterOptions options={staffOptions} selected={filters.assignmentState === 'Unassigned' ? 'unassigned' : filters.assignedUserId ? String(filters.assignedUserId) : undefined} onSelect={value => onChange(value === 'unassigned' ? { assignmentState: 'Unassigned', assignedUserId: undefined } : { assignmentState: undefined, assignedUserId: value ? Number(value) : undefined })} />
      {isAdmin ? <Pressable onPress={() => onChange({ assignmentState: filters.assignmentState === 'InactiveAssignee' ? undefined : 'InactiveAssignee', assignedUserId: undefined })} style={[styles.option, filters.assignmentState === 'InactiveAssignee' && styles.optionSelected]}><Text style={[styles.optionText, filters.assignmentState === 'InactiveAssignee' && styles.optionTextSelected]}>Pasif personele atanmış</Text></Pressable> : null}
      <Pressable onPress={() => onChange({ activeOnly: !filters.activeOnly })} style={[styles.option, filters.activeOnly && styles.optionSelected]}><Text style={[styles.optionText, filters.activeOnly && styles.optionTextSelected]}>Yalnızca aktif talepler</Text></Pressable></> : null}
    <Text style={styles.filterLabel}>Sıralama</Text><FilterOptions options={[{ label: 'Son güncellenen', value: 'UpdatedNewest' }, { label: 'En yeni talep', value: 'NewestTicket' }, { label: 'En eski talep', value: 'OldestTicket' }, ...(!isCustomer ? [{ label: 'En uzun bekleyen', value: 'LongestWaiting' }] : [])]} selected={filters.sort} onSelect={sort => onChange({ sort: sort as TicketFilters['sort'] })} />
    <AppButton label="Uygula" onPress={onClose} />
  </Pressable></Pressable></Modal>;
}

function FilterOptions<T extends string>({ options, selected, onSelect }: { options: { label: string; value?: T }[]; selected?: T; onSelect: (value?: T) => void }) {
  return <View style={styles.options}>{options.map(option => <Pressable key={option.label} onPress={() => onSelect(option.value)} style={[styles.option, selected === option.value && styles.optionSelected]}><Text style={[styles.optionText, selected === option.value && styles.optionTextSelected]}>{option.label}</Text></Pressable>)}</View>;
}

const styles = StyleSheet.create({
  screen: { backgroundColor: theme.colors.background, flex: 1 }, content: { padding: theme.spacing.md, paddingBottom: theme.spacing.xl }, header: { gap: theme.spacing.sm, marginBottom: theme.spacing.sm }, titleRow: { alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' }, title: { color: theme.colors.text, fontSize: 24, fontWeight: '800', letterSpacing: -0.5 }, subtitle: { color: theme.colors.textMuted, fontSize: 13, lineHeight: 19, marginTop: 3 }, newButton: { alignItems: 'center', backgroundColor: theme.colors.primary, borderRadius: theme.radius.pill, height: 42, justifyContent: 'center', width: 42, ...theme.shadow.card }, toolbar: { alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' }, filterButton: { alignItems: 'center', backgroundColor: theme.colors.primarySoft, borderRadius: theme.radius.sm, flexDirection: 'row', gap: 6, minHeight: 36, paddingHorizontal: 10 }, filterText: { color: theme.colors.primary, fontSize: 13, fontWeight: '800' }, clearText: { color: theme.colors.textMuted, fontSize: 13, fontWeight: '700' },
  card: { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, padding: theme.spacing.sm, ...theme.shadow.card }, pressed: { opacity: 0.8 }, cardTop: { alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' }, number: { color: theme.colors.primary, fontSize: 11, fontWeight: '800', letterSpacing: 0.4 }, cardTitle: { color: theme.colors.text, fontSize: 16, fontWeight: '800', lineHeight: 21, marginTop: 4 }, cardBottom: { alignItems: 'center', flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: theme.spacing.xs }, categoryChip: { backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.pill, paddingHorizontal: 8, paddingVertical: 4 }, categoryText: { color: theme.colors.textMuted, fontSize: 11, fontWeight: '700' }, metadataRow: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.xs, marginTop: theme.spacing.sm }, metadata: { color: theme.colors.textMuted, flex: 1, fontSize: 11 }, date: { color: theme.colors.textMuted, fontSize: 10 }, separator: { height: theme.spacing.xs },
  empty: { alignItems: 'center', paddingHorizontal: theme.spacing.xl, paddingVertical: 64 }, emptyTitle: { color: theme.colors.text, fontSize: 18, fontWeight: '800', marginTop: theme.spacing.md }, emptyText: { color: theme.colors.textMuted, lineHeight: 21, marginTop: 6, textAlign: 'center' }, footer: { marginTop: theme.spacing.md },
  modalBackdrop: { backgroundColor: 'rgba(31, 23, 52, 0.35)', flex: 1, justifyContent: 'flex-end' }, modal: { backgroundColor: theme.colors.surface, borderTopLeftRadius: theme.radius.lg, borderTopRightRadius: theme.radius.lg, gap: theme.spacing.sm, padding: theme.spacing.lg }, modalTitleRow: { alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between' }, modalTitle: { color: theme.colors.text, fontSize: 20, fontWeight: '800' }, filterLabel: { color: theme.colors.text, fontSize: 13, fontWeight: '800', marginTop: theme.spacing.sm, textTransform: 'uppercase' }, options: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 }, option: { borderColor: theme.colors.border, borderRadius: theme.radius.pill, borderWidth: 1, paddingHorizontal: 10, paddingVertical: 8 }, optionSelected: { backgroundColor: theme.colors.primarySoft, borderColor: theme.colors.primary }, optionText: { color: theme.colors.textMuted, fontSize: 13, fontWeight: '700' }, optionTextSelected: { color: theme.colors.primary },
});

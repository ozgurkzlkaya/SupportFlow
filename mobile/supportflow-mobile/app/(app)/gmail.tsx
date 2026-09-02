import { AppButton } from '@/components/AppButton';
import { ErrorMessage } from '@/components/ErrorMessage';
import { LoadingView } from '@/components/LoadingView';
import { ScreenContainer } from '@/components/ScreenContainer';
import { useAuth } from '@/context/AuthContext';
import { ApiError } from '@/services/apiClient';
import { gmailService } from '@/services/gmailService';
import { theme } from '@/theme/theme';
import type { GmailIntegrationStatus, GmailMailboxProcessingSummary } from '@/types/gmail';
import { formatDateTime } from '@/utils/formatDate';
import { Ionicons } from '@expo/vector-icons';
import * as Linking from 'expo-linking';
import { type Href, Redirect, router, useLocalSearchParams } from 'expo-router';
import * as WebBrowser from 'expo-web-browser';
import { useCallback, useEffect, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

const summaryItems: { key: keyof GmailMailboxProcessingSummary; label: string }[] = [
  { key: 'processed', label: 'İncelenen' },
  { key: 'newTickets', label: 'Yeni Talep' },
  { key: 'newExternalTickets', label: 'Yeni Harici Talep' },
  { key: 'externalRequestersCreated', label: 'Harici Müşteri Oluşturuldu' },
  { key: 'externalRequestersMatched', label: 'Harici Müşteri Eşleşti' },
  { key: 'messagesAdded', label: 'Eklenen Mesaj' },
  { key: 'alreadyProcessed', label: 'Zaten İşlenmiş' },
  { key: 'unknownCustomers', label: 'Bilinmeyen Müşteri' },
  { key: 'senderMismatches', label: 'Gönderen Uyuşmazlığı' },
  { key: 'selfMessagesIgnored', label: 'Kendi Mesajı' },
  { key: 'systemMessagesSkipped', label: 'Sistem Maili Atlandı' },
  { key: 'failed', label: 'Başarısız' },
  { key: 'listedCandidates', label: 'Listelenen Aday' },
  { key: 'alreadyProcessedSkipped', label: 'Önceden İşlenmiş Atlandı' },
  { key: 'unseenFetched', label: 'Yeni İleti Ayrıntısı' },
  { key: 'attachmentsFetched', label: 'Ek Dosya Getirildi' },
];

export default function GmailIntegrationScreen() {
  const { token, user } = useAuth();
  const { oauth } = useLocalSearchParams<{ oauth?: string }>();
  const isAdmin = user?.role === 'Admin';
  const [status, setStatus] = useState<GmailIntegrationStatus | null>(null);
  const [summary, setSummary] = useState<GmailMailboxProcessingSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [authorizing, setAuthorizing] = useState(false);
  const [processing, setProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const loadStatus = useCallback(async (showRefresh = false) => {
    if (!token || !isAdmin) return;
    if (showRefresh) setRefreshing(true);
    else setLoading(true);
    setError(null);
    try {
      setStatus(await gmailService.getStatus(token));
    } catch (cause) {
      setError(getGmailError(cause, 'Gmail entegrasyon durumu alınamadı.'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [isAdmin, token]);

  useEffect(() => { void loadStatus(); }, [loadStatus]);

  useEffect(() => {
    if (oauth === 'success') {
      setNotice('Gmail bağlantısı hazır. Yetkilendirme başarıyla tamamlandı.');
      void loadStatus(true);
    } else if (oauth === 'error') {
      setError('Google yetkilendirmesi tamamlanamadı. Lütfen tekrar deneyin.');
    }
  }, [loadStatus, oauth]);

  if (!isAdmin) return <Redirect href={'/(app)/(tabs)' as Href} />;
  if (loading) return <LoadingView label="Gmail durumu yükleniyor..." />;

  async function startAuthorization() {
    if (!token || authorizing) return;
    setAuthorizing(true);
    setError(null);
    setNotice(null);
    try {
      const response = await gmailService.startAuthorization(token);
      const returnUrl = Linking.createURL('gmail-auth');
      if (!response.completionReturnUri || response.completionReturnUri !== returnUrl) {
        setError('Gmail mobil dönüş ayarı bu uygulama oturumuyla eşleşmiyor. Backend MobileReturnUri değerini uygulamanın dönüş URI’si ile eşleştirin.');
        return;
      }

      const result = await WebBrowser.openAuthSessionAsync(
        response.authorizationUrl,
        response.completionReturnUri,
      );

      if (result.type !== 'success') {
        setNotice('Google yetkilendirmesi tamamlanmadı. Dilerseniz tekrar deneyin.');
        return;
      }

      const callbackStatus = new URL(result.url).searchParams.get('status');
      if (callbackStatus !== 'success') {
        setError('Google yetkilendirmesi tamamlanamadı. Lütfen tekrar deneyin.');
        return;
      }

      setNotice('Gmail bağlantısı hazır. Yetkilendirme başarıyla tamamlandı.');
      await loadStatus(true);
    } catch (cause) {
      setError(getGmailError(cause, 'Google yetkilendirme bağlantısı oluşturulamadı.'));
    } finally {
      setAuthorizing(false);
    }
  }

  async function processMailbox() {
    if (!token || processing) return;
    setProcessing(true);
    setError(null);
    setNotice(null);
    try {
      const response = await gmailService.processMailbox(token);
      setSummary(response);
      setNotice(response.failed > 0 ? 'İşlem tamamlandı, bazı iletiler işlenemedi.' : 'Posta kutusu başarıyla işlendi.');
      await loadStatus(true);
    } catch (cause) {
      setError(getGmailError(cause, 'Gelen mailler işlenemedi.'));
      await loadStatus(true);
    } finally {
      setProcessing(false);
    }
  }

  const isHealthy = Boolean(status?.configured && status.authorized && status.canRead && status.canSend);
  const isRateLimited = Boolean(status?.backgroundSync?.isRateLimited);

  return <ScreenContainer>
    <ScrollView contentContainerStyle={styles.content} refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void loadStatus(true)} tintColor={theme.colors.primary} />}>
      <View style={styles.header}><Pressable accessibilityLabel="Profile dön" onPress={() => router.back()}><Ionicons color={theme.colors.primary} name="arrow-back" size={24} /></Pressable><View><Text style={styles.title}>Gmail Entegrasyonu</Text><Text style={styles.subtitle}>Yalnızca yönetici erişimi</Text></View></View>
      {error ? <ErrorMessage message={error} /> : null}
      {notice ? <View style={styles.notice}><Ionicons color={theme.colors.primary} name="information-circle" size={19} /><Text style={styles.noticeText}>{notice}</Text></View> : null}

      <Section title="Bağlantı Durumu">
        <View style={styles.healthRow}><Ionicons color={isHealthy ? theme.colors.success : theme.colors.warning} name={isHealthy ? 'checkmark-circle' : 'alert-circle'} size={22} /><Text style={[styles.healthText, { color: isHealthy ? theme.colors.success : theme.colors.warning }]}>{isHealthy ? 'Gmail bağlantısı hazır' : 'Gmail yetkilendirmesi gerekiyor'}</Text></View>
        <InfoRow label="Bağlı Posta Kutusu" value={status?.mailbox || 'Belirtilmemiş'} />
        <Capability label="Yapılandırma" value={status?.configured ?? false} />
        <Capability label="Yetkilendirme" value={status?.authorized ?? false} />
        <Capability label="Gelen Mail Okuma" value={status?.canRead ?? false} />
        <Capability label="Mail Gönderme" value={status?.canSend ?? false} />
        <AppButton label="Durumu Yenile" onPress={() => void loadStatus(true)} variant="secondary" icon={<Ionicons color={theme.colors.primary} name="refresh" size={19} />} />
      </Section>

      <Section title="Google Yetkilendirmesi">
        <Text style={styles.bodyText}>Google erişimi yalnızca backend tarafından saklanır. Mobil uygulama Gmail erişim veya yenileme anahtarı tutmaz.</Text>
        <AppButton label="Google ile Yetkilendir" loading={authorizing} onPress={() => void startAuthorization()} icon={<Ionicons color={theme.colors.onPrimary} name="logo-google" size={19} />} />
      </Section>

      <Section title="Arka Plan Eşitlemesi">
        <InfoRow label="Durum" value={status?.backgroundSync?.enabled ? 'Etkin' : 'Devre dışı'} />
        <InfoRow label="Aralık" value={status?.backgroundSync ? `${status.backgroundSync.intervalSeconds} saniye` : 'Bilgi yok'} />
        <InfoRow label="Çalışıyor" value={status?.backgroundSync?.isRunning ? 'Evet' : 'Hayır'} />
        <InfoRow label="Son başarılı çalışma" value={formatOptionalDate(status?.backgroundSync?.lastSuccessfulAt)} />
        {isRateLimited ? <View style={styles.warning}><Text style={styles.warningText}>Gmail API geçici olarak hız sınırında. Yeniden deneme: {formatOptionalDate(status?.backgroundSync?.rateLimitedUntil)}</Text></View> : null}
        {status?.backgroundSync?.lastError ? <Text style={styles.bodyText}>Son hata: {status.backgroundSync.lastError}</Text> : null}
      </Section>

      <Section title="Gelen Mailleri İşle">
        <Text style={styles.bodyText}>Bu işlem yalnızca siz düğmeye bastığınızda mevcut Admin Gmail işleme akışını çalıştırır. Arka plan eşitlemesini etkinleştirmez.</Text>
        <AppButton disabled={isRateLimited} label="Gelen Mailleri İşle" loading={processing} onPress={() => void processMailbox()} icon={<Ionicons color={theme.colors.onPrimary} name="mail-unread-outline" size={19} />} />
        {isRateLimited ? <Text style={styles.warningText}>Hız sınırı kalkana kadar manuel işleme kullanılamaz.</Text> : null}
      </Section>

      {summary ? <Section title="İşleme Sonucu"><View style={styles.summaryGrid}>{summaryItems.map(item => <View key={item.key} style={styles.metric}><Text style={styles.metricValue}>{summary[item.key]}</Text><Text style={styles.metricLabel}>{item.label}</Text></View>)}</View></Section> : null}
    </ScrollView>
  </ScreenContainer>;
}

function Capability({ label, value }: { label: string; value: boolean }) {
  return <View style={styles.capability}><Ionicons color={value ? theme.colors.success : theme.colors.textMuted} name={value ? 'checkmark-circle' : 'close-circle'} size={20} /><Text style={styles.infoLabel}>{label}</Text><Text style={[styles.capabilityValue, { color: value ? theme.colors.success : theme.colors.error }]}>{value ? 'Hazır' : 'Eksik'}</Text></View>;
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return <View style={styles.infoRow}><Text style={styles.infoLabel}>{label}</Text><Text style={styles.infoValue}>{value}</Text></View>;
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return <View style={styles.section}><Text style={styles.sectionTitle}>{title}</Text>{children}</View>;
}

function formatOptionalDate(value: string | null | undefined) {
  return value ? formatDateTime(value) : 'Henüz yok';
}

function getGmailError(cause: unknown, fallback: string) {
  if (!(cause instanceof ApiError)) return fallback;
  if (cause.status === 401) return 'Oturumunuz sona erdi. Lütfen yeniden giriş yapın.';
  if (cause.status === 403) return 'Bu işlem için yönetici yetkisi gerekiyor.';
  if (cause.status === 409) return 'Gmail yetkilendirmesi gerekiyor.';
  if (cause.status === 429) return 'Gmail API geçici olarak hız sınırında. Daha sonra tekrar deneyin.';
  if (cause.status === 503) return 'Gmail entegrasyonu şu anda kullanılamıyor.';
  return fallback;
}

const styles = StyleSheet.create({
  content: { gap: theme.spacing.md, padding: theme.spacing.lg, paddingBottom: theme.spacing.xxl },
  header: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.md, marginBottom: theme.spacing.sm },
  title: { color: theme.colors.text, fontSize: 26, fontWeight: '800', letterSpacing: -0.5 },
  subtitle: { color: theme.colors.textMuted, fontSize: 14, marginTop: 2 },
  section: { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, gap: theme.spacing.md, padding: theme.spacing.md, ...theme.shadow.card },
  sectionTitle: { color: theme.colors.text, fontSize: 17, fontWeight: '800' },
  healthRow: { alignItems: 'center', backgroundColor: '#F5FBF7', borderRadius: theme.radius.sm, flexDirection: 'row', gap: 8, padding: theme.spacing.sm },
  healthText: { fontSize: 15, fontWeight: '800' },
  infoRow: { gap: 3 },
  infoLabel: { color: theme.colors.textMuted, fontSize: 12, fontWeight: '700' },
  infoValue: { color: theme.colors.text, fontSize: 15, fontWeight: '700' },
  capability: { alignItems: 'center', flexDirection: 'row', gap: 8 },
  capabilityValue: { fontSize: 13, fontWeight: '800', marginLeft: 'auto' },
  bodyText: { color: theme.colors.textMuted, fontSize: 14, lineHeight: 21 },
  notice: { alignItems: 'flex-start', backgroundColor: theme.colors.infoSoft, borderRadius: theme.radius.sm, flexDirection: 'row', gap: 7, padding: theme.spacing.sm },
  noticeText: { color: theme.colors.primary, flex: 1, fontSize: 14, lineHeight: 20 },
  warning: { backgroundColor: '#FFF7ED', borderRadius: theme.radius.sm, padding: theme.spacing.sm },
  warningText: { color: theme.colors.warning, fontSize: 13, fontWeight: '700', lineHeight: 19 },
  summaryGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  metric: { backgroundColor: theme.colors.surfaceMuted, borderRadius: theme.radius.sm, minWidth: '47%', padding: theme.spacing.sm },
  metricValue: { color: theme.colors.primary, fontSize: 20, fontWeight: '800' },
  metricLabel: { color: theme.colors.textMuted, fontSize: 12, marginTop: 3 },
});

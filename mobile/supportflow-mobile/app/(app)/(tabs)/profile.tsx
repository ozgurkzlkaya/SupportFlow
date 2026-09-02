import { AppButton } from '@/components/AppButton';
import { AppCard } from '@/components/AppCard';
import { AppTextInput } from '@/components/AppTextInput';
import { ErrorMessage } from '@/components/ErrorMessage';
import { LoadingView } from '@/components/LoadingView';
import { ScreenContainer } from '@/components/ScreenContainer';
import { useAuth } from '@/context/AuthContext';
import { ApiError } from '@/services/apiClient';
import { gmailService } from '@/services/gmailService';
import { userService } from '@/services/userService';
import { theme } from '@/theme/theme';
import { roleLabels } from '@/types/auth';
import type { UserProfile } from '@/types/profile';
import type { GmailIntegrationStatus } from '@/types/gmail';
import { formatDateTime } from '@/utils/formatDate';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { KeyboardAvoidingView, Platform, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

export default function ProfileScreen() {
  const { logout, setCurrentUser, token } = useAuth();
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [changingPassword, setChangingPassword] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmNewPassword, setConfirmNewPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [gmailStatus, setGmailStatus] = useState<GmailIntegrationStatus | null>(null);
  const [gmailStatusLoading, setGmailStatusLoading] = useState(false);

  const load = useCallback(async () => {
    if (!token) return;
    setError(null);
    try {
      const response = await userService.getProfile(token);
      setProfile(response);
      setFirstName(response.firstName);
      setLastName(response.lastName);
      setCurrentUser(response);
    } catch {
      setError('Profil bilgileri yüklenemedi. Lütfen tekrar deneyin.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [setCurrentUser, token]);

  useEffect(() => { void load(); }, [load]);

  useEffect(() => {
    if (!token || profile?.role !== 'Admin') return;
    let active = true;
    setGmailStatusLoading(true);
    void gmailService.getStatus(token)
      .then(status => { if (active) setGmailStatus(status); })
      .catch(() => { if (active) setGmailStatus(null); })
      .finally(() => { if (active) setGmailStatusLoading(false); });
    return () => { active = false; };
  }, [profile?.role, token]);

  async function saveProfile() {
    if (!token) return;
    const nextFirstName = firstName.trim();
    const nextLastName = lastName.trim();
    if (!nextFirstName || !nextLastName) {
      setError('Ad ve soyad zorunludur.');
      return;
    }
    if (nextFirstName.length > 100 || nextLastName.length > 100) {
      setError('Ad ve soyad en fazla 100 karakter olabilir.');
      return;
    }
    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      const updated = await userService.updateProfile(token, { firstName: nextFirstName, lastName: nextLastName });
      setProfile(updated);
      setFirstName(updated.firstName);
      setLastName(updated.lastName);
      setCurrentUser(updated);
      setSuccess('Profiliniz güncellendi.');
    } catch {
      setError('Profil güncellenemedi. Lütfen tekrar deneyin.');
    } finally {
      setSaving(false);
    }
  }

  async function changePassword() {
    if (!token || changingPassword) return;
    if (!currentPassword || !newPassword || !confirmNewPassword) {
      setError('Şifre alanlarının tümü zorunludur.');
      return;
    }
    if (newPassword.length < 8) {
      setError('Yeni şifre en az 8 karakter olmalıdır.');
      return;
    }
    if (newPassword !== confirmNewPassword) {
      setError('Yeni şifre ve tekrarı eşleşmiyor.');
      return;
    }

    setChangingPassword(true);
    setError(null);
    setSuccess(null);
    try {
      await userService.changePassword(token, {
        currentPassword,
        newPassword,
        confirmNewPassword,
      });
      setCurrentPassword('');
      setNewPassword('');
      setConfirmNewPassword('');
      setSuccess('Şifreniz güncellendi.');
    } catch (changePasswordError) {
      setError(
        changePasswordError instanceof ApiError
          ? changePasswordError.message
          : 'Şifre güncellenemedi. Lütfen tekrar deneyin.',
      );
    } finally {
      setChangingPassword(false);
    }
  }

  if (loading) return <LoadingView label="Profil yükleniyor..." />;
  if (!profile) return <ScreenContainer><View style={styles.errorScreen}>{error ? <ErrorMessage message={error} /> : null}<AppButton label="Tekrar Dene" onPress={() => void load()} variant="secondary" /></View></ScreenContainer>;

  return <ScreenContainer>
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.flex}>
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled" refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => { setRefreshing(true); void load(); }} tintColor={theme.colors.primary} />}>
      <View style={styles.hero}><View style={styles.avatar}><Text style={styles.avatarText}>{profile.firstName.charAt(0).toUpperCase()}</Text></View><View style={styles.heroText}><Text style={styles.title}>{profile.firstName} {profile.lastName}</Text><View style={styles.roleBadge}><Text style={styles.roleBadgeText}>{roleLabels[profile.role]}</Text></View></View></View>
      {error ? <ErrorMessage message={error} /> : null}
      {success ? <View style={styles.success}><Ionicons color={theme.colors.success} name="checkmark-circle" size={18} /><Text style={styles.successText}>{success}</Text></View> : null}
      <Section title="Kişisel Bilgiler"><AppTextInput label="Ad" maxLength={100} onChangeText={setFirstName} value={firstName} /><AppTextInput label="Soyad" maxLength={100} onChangeText={setLastName} value={lastName} /><AppButton disabled={saving} label="Değişiklikleri Kaydet" loading={saving} onPress={() => void saveProfile()} /></Section>
      <Section title="Hesap Bilgileri"><InfoRow icon="mail-outline" label="E-posta" value={profile.email} /><InfoRow icon="person-outline" label="Rol" value={roleLabels[profile.role]} /><InfoRow icon="business-outline" label="Şirket" value={profile.companyId ? `Şirket #${profile.companyId}` : 'Bağlı şirket yok'} /><InfoRow icon="calendar-outline" label="Hesap oluşturma" value={formatDateTime(profile.createdAt)} /></Section>
      <Section title="Şifre Değiştir"><Text style={styles.sessionText}>Güvenliğiniz için mevcut şifrenizi doğrulayın.</Text><AppTextInput autoComplete="current-password" label="Mevcut Şifre" onChangeText={setCurrentPassword} secureTextEntry value={currentPassword} /><AppTextInput autoComplete="new-password" label="Yeni Şifre" onChangeText={setNewPassword} secureTextEntry value={newPassword} /><AppTextInput autoComplete="new-password" label="Yeni Şifre Tekrarı" onChangeText={setConfirmNewPassword} secureTextEntry value={confirmNewPassword} /><AppButton disabled={changingPassword} label="Şifreyi Güncelle" loading={changingPassword} onPress={() => void changePassword()} /></Section>
      {profile.role === 'Admin' ? <Section title="Entegrasyonlar"><GmailSummary loading={gmailStatusLoading} status={gmailStatus} /><AppButton label="Gmail Entegrasyonu" onPress={() => router.push('/(app)/gmail')} variant="secondary" icon={<Ionicons color={theme.colors.primary} name="mail-outline" size={19} />} /></Section> : null}
      <Section title="Oturum"><Text style={styles.sessionText}>Oturumunuz güvenli olarak bu cihazda saklanır. Erişim anahtarınız gösterilmez.</Text><AppButton label="Çıkış Yap" onPress={() => void logout()} variant="secondary" /></Section>
    </ScrollView>
    </KeyboardAvoidingView>
  </ScreenContainer>;
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return <View style={styles.section}><Text style={styles.sectionTitle}>{title}</Text>{children}</View>;
}

function InfoRow({ icon, label, value }: { icon: keyof typeof Ionicons.glyphMap; label: string; value: string }) {
  return <View style={styles.infoRow}><Ionicons color={theme.colors.primary} name={icon} size={20} /><View style={styles.infoText}><Text style={styles.infoLabel}>{label}</Text><Text style={styles.infoValue}>{value}</Text></View></View>;
}

function GmailSummary({ loading, status }: { loading: boolean; status: GmailIntegrationStatus | null }) {
  const ready = Boolean(status?.configured && status.authorized && status.canRead && status.canSend);
  const label = loading ? 'Durum kontrol ediliyor' : ready ? 'Bağlı' : 'Bağlı Değil';
  const detail = loading ? 'Gmail bağlantı durumu alınıyor.' : ready ? status?.mailbox || 'Posta kutusu hazır' : 'Bağlantı için Gmail Entegrasyonu ekranını açın.';

  return <AppCard style={[styles.gmailCard, ready ? styles.gmailReady : styles.gmailIdle]}>
    <View style={styles.gmailIcon}><Ionicons color={ready ? theme.colors.success : theme.colors.warning} name={ready ? 'checkmark-circle' : 'mail-outline'} size={23} /></View>
    <View style={styles.gmailText}><Text style={styles.gmailTitle}>Gmail Entegrasyonu</Text><Text style={[styles.gmailStatus, { color: ready ? theme.colors.success : theme.colors.warning }]}>{label}</Text><Text numberOfLines={1} style={styles.gmailDetail}>{detail}</Text></View>
  </AppCard>;
}

const styles = StyleSheet.create({
  flex: { flex: 1 }, content: { gap: theme.spacing.sm, padding: theme.spacing.md, paddingBottom: theme.spacing.xl }, hero: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.sm, marginBottom: theme.spacing.xs }, heroText: { flex: 1 }, avatar: { alignItems: 'center', backgroundColor: theme.colors.primary, borderRadius: 24, height: 48, justifyContent: 'center', width: 48, ...theme.shadow.card }, avatarText: { color: theme.colors.onPrimary, fontSize: 20, fontWeight: '800' }, title: { color: theme.colors.text, fontSize: 21, fontWeight: '800', letterSpacing: -0.4 }, roleBadge: { alignSelf: 'flex-start', backgroundColor: theme.colors.primarySoft, borderRadius: theme.radius.pill, marginTop: 3, paddingHorizontal: 8, paddingVertical: 3 }, roleBadgeText: { color: theme.colors.primary, fontSize: 11, fontWeight: '800' }, section: { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.md, borderWidth: 1, gap: theme.spacing.sm, padding: theme.spacing.sm, ...theme.shadow.card }, sectionTitle: { color: theme.colors.text, fontSize: 15, fontWeight: '800' }, infoRow: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.xs }, infoText: { flex: 1 }, infoLabel: { color: theme.colors.textMuted, fontSize: 11, fontWeight: '700' }, infoValue: { color: theme.colors.text, fontSize: 14, fontWeight: '700', marginTop: 1 }, sessionText: { color: theme.colors.textMuted, fontSize: 13, lineHeight: 19 }, success: { alignItems: 'center', backgroundColor: '#DCFCE7', borderRadius: theme.radius.sm, flexDirection: 'row', gap: 7, padding: theme.spacing.xs }, successText: { color: theme.colors.success, fontSize: 13, fontWeight: '700' }, gmailCard: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.sm, padding: theme.spacing.sm }, gmailReady: { backgroundColor: '#F2FBF6', borderColor: '#CDECD9' }, gmailIdle: { backgroundColor: '#FFF9ED', borderColor: '#F9E4B7' }, gmailIcon: { alignItems: 'center', backgroundColor: theme.colors.surface, borderRadius: theme.radius.pill, height: 36, justifyContent: 'center', width: 36 }, gmailText: { flex: 1 }, gmailTitle: { color: theme.colors.text, fontSize: 13, fontWeight: '800' }, gmailStatus: { fontSize: 12, fontWeight: '800', marginTop: 1 }, gmailDetail: { color: theme.colors.textMuted, fontSize: 11, marginTop: 2 }, errorScreen: { flex: 1, gap: theme.spacing.md, justifyContent: 'center', padding: theme.spacing.lg },
});

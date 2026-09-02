import { AppCard } from '@/components/AppCard';
import { BrandMark } from '@/components/BrandMark';
import { ScreenContainer } from '@/components/ScreenContainer';
import { useAuth } from '@/context/AuthContext';
import { gmailService } from '@/services/gmailService';
import { theme } from '@/theme/theme';
import { roleLabels } from '@/types/auth';
import type { GmailIntegrationStatus } from '@/types/gmail';
import { Ionicons } from '@expo/vector-icons';
import { router, type Href } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

type QuickAction = {
  icon: keyof typeof Ionicons.glyphMap;
  title: string;
  description: string;
  route: Href;
};

export default function HomeScreen() {
  const { token, user } = useAuth();
  const [gmailStatus, setGmailStatus] = useState<GmailIntegrationStatus | null>(null);
  const name = `${user?.firstName ?? ''} ${user?.lastName ?? ''}`.trim();
  const isAdmin = user?.role === 'Admin';

  useEffect(() => {
    if (!token || !isAdmin) return;
    let active = true;
    void gmailService.getStatus(token)
      .then(status => { if (active) setGmailStatus(status); })
      .catch(() => { if (active) setGmailStatus(null); });
    return () => { active = false; };
  }, [isAdmin, token]);

  const actions = useMemo<QuickAction[]>(() => {
    if (user?.role === 'Customer') {
      return [
        { icon: 'add-circle-outline', title: 'Yeni Talep', description: 'Yeni destek talebi oluşturun.', route: '/(app)/tickets/new' },
        { icon: 'ticket-outline', title: 'Taleplerim', description: 'Açık ve geçmiş talepleri inceleyin.', route: '/(app)/(tabs)/tickets' },
        { icon: 'notifications-outline', title: 'Bildirimler', description: 'Güncel gelişmeleri takip edin.', route: '/(app)/(tabs)/notifications' },
      ];
    }

    return [
      { icon: 'ticket-outline', title: 'Talepler', description: 'Destek kuyruğunu yönetin.', route: '/(app)/(tabs)/tickets' },
      { icon: 'chatbubbles-outline', title: 'Sohbetler', description: 'Müşteri iletişimlerine gidin.', route: '/(app)/(tabs)/conversations' },
      { icon: 'notifications-outline', title: 'Bildirimler', description: 'Yeni gelişmeleri görüntüleyin.', route: '/(app)/(tabs)/notifications' },
    ];
  }, [user?.role]);

  const gmailReady = Boolean(gmailStatus?.configured && gmailStatus.authorized && gmailStatus.canRead && gmailStatus.canSend);

  return <ScreenContainer>
    <ScrollView contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
      <View style={styles.identity}>
        <BrandMark size={38} />
        <View style={styles.identityText}>
          <Text style={styles.brand}>SupportFlow</Text>
          <Text style={styles.role}>{user ? roleLabels[user.role] : ''}</Text>
        </View>
      </View>

      <View style={styles.hero}>
        <Text style={styles.eyebrow}>GÜNLÜK ÇALIŞMA ALANI</Text>
        <Text style={styles.welcome}>Merhaba, {name || 'Kullanıcı'}.</Text>
        <Text style={styles.description}>Destek akışınızdaki önemli alanlara hızlıca ulaşın.</Text>
      </View>

      <View style={styles.sectionHeading}>
        <Text style={styles.sectionTitle}>Hızlı erişim</Text>
        <Text style={styles.sectionMeta}>{actions.length} alan</Text>
      </View>
      <View style={styles.actions}>
        {actions.map(action => <QuickActionCard action={action} key={action.title} />)}
      </View>

      {isAdmin ? <Pressable accessibilityLabel="Gmail entegrasyonuna git" onPress={() => router.push('/(app)/gmail')} style={({ pressed }) => [styles.gmailPressable, pressed && styles.pressed]}>
        <AppCard style={[styles.gmailCard, gmailReady ? styles.gmailReady : styles.gmailIdle]}>
          <View style={[styles.gmailIcon, gmailReady ? styles.gmailIconReady : styles.gmailIconIdle]}><Ionicons color={gmailReady ? theme.colors.success : theme.colors.warning} name={gmailReady ? 'checkmark' : 'mail-outline'} size={18} /></View>
          <View style={styles.gmailText}><Text style={styles.gmailTitle}>Gmail Entegrasyonu</Text><Text numberOfLines={1} style={styles.gmailDetail}>{gmailReady ? gmailStatus?.mailbox || 'Bağlı' : 'Durumu görüntülemek için açın.'}</Text></View>
          <Text style={[styles.gmailStatus, { color: gmailReady ? theme.colors.success : theme.colors.warning }]}>{gmailReady ? 'Bağlı' : 'Kontrol et'}</Text>
          <Ionicons color={theme.colors.textMuted} name="chevron-forward" size={18} />
        </AppCard>
      </Pressable> : null}
    </ScrollView>
  </ScreenContainer>;
}

function QuickActionCard({ action }: { action: QuickAction }) {
  return <Pressable accessibilityLabel={action.title} onPress={() => router.push(action.route)} style={({ pressed }) => [styles.actionPressable, pressed && styles.pressed]}>
    <AppCard style={styles.actionCard}>
      <View style={styles.actionIcon}><Ionicons color={theme.colors.primary} name={action.icon} size={19} /></View>
      <View style={styles.actionText}><Text style={styles.actionTitle}>{action.title}</Text><Text numberOfLines={2} style={styles.actionDescription}>{action.description}</Text></View>
      <Ionicons color={theme.colors.textMuted} name="arrow-forward" size={18} />
    </AppCard>
  </Pressable>;
}

const styles = StyleSheet.create({
  content: { gap: theme.spacing.md, padding: theme.spacing.md, paddingBottom: theme.spacing.xl },
  identity: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.sm, marginTop: theme.spacing.xs },
  identityText: { flex: 1 },
  brand: { color: theme.colors.text, fontSize: 18, fontWeight: '800', letterSpacing: -0.3 },
  role: { color: theme.colors.textMuted, fontSize: 12, marginTop: 1 },
  hero: { marginTop: theme.spacing.xs },
  eyebrow: { color: theme.colors.primary, fontSize: 10, fontWeight: '800', letterSpacing: 1 },
  welcome: { color: theme.colors.text, fontSize: 25, fontWeight: '800', letterSpacing: -0.55, marginTop: 5 },
  description: { color: theme.colors.textMuted, fontSize: 14, lineHeight: 20, marginTop: 5 },
  sectionHeading: { alignItems: 'center', flexDirection: 'row', justifyContent: 'space-between', marginTop: theme.spacing.xs },
  sectionTitle: { color: theme.colors.text, fontSize: 15, fontWeight: '800' },
  sectionMeta: { color: theme.colors.textMuted, fontSize: 12 },
  actions: { gap: theme.spacing.xs },
  actionPressable: { borderRadius: theme.radius.md },
  actionCard: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.sm },
  actionIcon: { alignItems: 'center', backgroundColor: theme.colors.primarySoft, borderRadius: theme.radius.sm, height: 36, justifyContent: 'center', width: 36 },
  actionText: { flex: 1 },
  actionTitle: { color: theme.colors.text, fontSize: 14, fontWeight: '800' },
  actionDescription: { color: theme.colors.textMuted, fontSize: 12, lineHeight: 17, marginTop: 1 },
  gmailPressable: { borderRadius: theme.radius.md, marginTop: theme.spacing.xs },
  gmailCard: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.xs },
  gmailReady: { backgroundColor: '#F2FBF6', borderColor: '#CDECD9' },
  gmailIdle: { backgroundColor: '#FFF9ED', borderColor: '#F9E4B7' },
  gmailIcon: { alignItems: 'center', borderRadius: theme.radius.pill, height: 32, justifyContent: 'center', width: 32 },
  gmailIconReady: { backgroundColor: '#E1F5E9' },
  gmailIconIdle: { backgroundColor: '#FFF1CC' },
  gmailText: { flex: 1 },
  gmailTitle: { color: theme.colors.text, fontSize: 13, fontWeight: '800' },
  gmailDetail: { color: theme.colors.textMuted, fontSize: 12, marginTop: 1 },
  gmailStatus: { fontSize: 12, fontWeight: '800' },
  pressed: { opacity: 0.72 },
});

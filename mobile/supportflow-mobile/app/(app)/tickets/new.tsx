import { AppButton } from '@/components/AppButton';
import { AppTextInput } from '@/components/AppTextInput';
import { ErrorMessage } from '@/components/ErrorMessage';
import { useAuth } from '@/context/AuthContext';
import { ticketService } from '@/services/ticketService';
import { theme } from '@/theme/theme';
import type { TicketPriority } from '@/types/tickets';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

const priorities: { value: TicketPriority; label: string }[] = [
  { value: 'Low', label: 'Düşük' }, { value: 'Normal', label: 'Normal' },
  { value: 'High', label: 'Yüksek' }, { value: 'Critical', label: 'Kritik' },
];

export default function NewTicketScreen() {
  const { token } = useAuth();
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [priority, setPriority] = useState<TicketPriority>('Normal');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    const normalizedTitle = title.trim();
    const normalizedDescription = description.trim();
    if (!normalizedTitle || !normalizedDescription) {
      setError('Başlık ve açıklama zorunludur.');
      return;
    }
    if (normalizedTitle.length > 250) {
      setError('Başlık en fazla 250 karakter olabilir.');
      return;
    }
    if (!token) return;

    setSaving(true);
    setError(null);
    try {
      const ticket = await ticketService.createTicket(token, {
        title: normalizedTitle,
        description: normalizedDescription,
        priority,
      });
      router.replace({ pathname: '/(app)/tickets/[id]', params: { id: String(ticket.id) } });
    } catch {
      setError('Talep oluşturulamadı. Lütfen tekrar deneyin.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.screen}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <View style={styles.header}>
          <Pressable accessibilityLabel="Geri dön" onPress={() => router.back()}><Ionicons color={theme.colors.text} name="arrow-back" size={24} /></Pressable>
          <Text style={styles.headerTitle}>Yeni Talep</Text>
        </View>
        <Text style={styles.intro}>Yaşadığınız sorunu açıklayın, ekibimiz sizinle iletişime geçsin.</Text>
        {error ? <ErrorMessage message={error} /> : null}
        <AppTextInput label="Başlık" maxLength={250} onChangeText={setTitle} placeholder="Örn. VPN bağlantısı kurulamıyor" value={title} />
        <AppTextInput label="Açıklama" multiline maxLength={5000} numberOfLines={7} onChangeText={setDescription} placeholder="Sorunu ve denediğiniz adımları açıklayın." style={styles.description} textAlignVertical="top" value={description} />
        <Text style={styles.label}>Öncelik</Text>
        <View style={styles.priorities}>{priorities.map(item => <Pressable key={item.value} onPress={() => setPriority(item.value)} style={[styles.priority, priority === item.value && styles.prioritySelected]}><Text style={[styles.priorityText, priority === item.value && styles.priorityTextSelected]}>{item.label}</Text></Pressable>)}</View>
        <AppButton disabled={saving} label="Talebi Oluştur" loading={saving} onPress={() => void submit()} />
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  screen: { backgroundColor: theme.colors.background, flex: 1 },
  content: { gap: theme.spacing.md, padding: theme.spacing.lg, paddingBottom: theme.spacing.xl },
  header: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.md },
  headerTitle: { color: theme.colors.text, fontSize: 21, fontWeight: '800' },
  intro: { color: theme.colors.textMuted, lineHeight: 22 },
  description: { minHeight: 144, paddingTop: theme.spacing.md },
  label: { color: theme.colors.text, fontSize: 14, fontWeight: '700' },
  priorities: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  priority: { borderColor: theme.colors.border, borderRadius: 999, borderWidth: 1, paddingHorizontal: 13, paddingVertical: 9 },
  prioritySelected: { backgroundColor: theme.colors.primarySoft, borderColor: theme.colors.primary },
  priorityText: { color: theme.colors.textMuted, fontWeight: '700' },
  priorityTextSelected: { color: theme.colors.primary },
});

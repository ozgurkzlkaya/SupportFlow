import { AppButton } from '@/components/AppButton';
import { AppCard } from '@/components/AppCard';
import { AppTextInput } from '@/components/AppTextInput';
import { BrandMark } from '@/components/BrandMark';
import { ErrorMessage } from '@/components/ErrorMessage';
import { ScreenContainer } from '@/components/ScreenContainer';
import { useAuth } from '@/context/AuthContext';
import { ApiError } from '@/services/apiClient';
import { theme } from '@/theme/theme';
import { type Href, Link, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native';

export default function LoginScreen() {
  const { login, isSubmitting, sessionExpired } = useAuth();
  const { registered } = useLocalSearchParams<{ registered?: string }>();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [submitted, setSubmitted] = useState(false);

  const emailError = submitted && !email.trim() ? 'E-posta alanı zorunludur.' : '';
  const passwordError = submitted && !password ? 'Şifre alanı zorunludur.' : '';

  async function handleLogin() {
    setSubmitted(true);
    setError('');
    if (!email.trim() || !password) {
      return;
    }

    try {
      await login({ email: email.trim(), password });
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : 'Giriş yapılamadı. Lütfen tekrar deneyin.');
    }
  }

  return (
    <ScreenContainer>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.flex}>
        <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
          <View style={styles.hero}>
            <View style={styles.brandRow}><BrandMark size={50} /><View><Text style={styles.brand}>SupportFlow</Text><Text style={styles.product}>DESTEK OPERASYON PLATFORMU</Text></View></View>
            <Text style={styles.title}>Destek süreçleriniz her zaman kontrol altında.</Text>
            <Text style={styles.subtitle}>Taleplerinize, güncel konuşmalarınıza ve hesabınıza güvenle erişin.</Text>
          </View>

          <AppCard style={styles.card}>
            <View style={styles.cardHeading}><Text style={styles.cardEyebrow}>GÜVENLİ ERİŞİM</Text><Text style={styles.cardTitle}>Hoş geldiniz</Text><Text style={styles.cardSubtitle}>SupportFlow hesabınıza giriş yapın.</Text></View>
            {registered === '1' ? <Text style={styles.success}>Kaydınız oluşturuldu. Giriş yapabilirsiniz.</Text> : null}
            {sessionExpired ? <ErrorMessage message="Oturumunuz sona erdi. Lütfen tekrar giriş yapın." /> : null}
            {error ? <ErrorMessage message={error} /> : null}
            <View style={styles.form}>
              <AppTextInput autoCapitalize="none" autoComplete="email" error={emailError} keyboardType="email-address" label="E-posta" onChangeText={setEmail} placeholder="ornek@firma.com" value={email} />
              <AppTextInput autoComplete="password" error={passwordError} label="Şifre" onChangeText={setPassword} placeholder="Şifreniz" secureTextEntry value={password} />
              <AppButton disabled={Boolean(emailError || passwordError)} label="Giriş Yap" loading={isSubmitting} onPress={() => void handleLogin()} />
            </View>
            <View style={styles.footerRow}>
              <Text style={styles.footerText}>Hesabınız yok mu?</Text>
              <Link href={'/(auth)/register' as Href} style={styles.link}>Kayıt Ol</Link>
            </View>
          </AppCard>
          <Text style={styles.version}>SupportFlow Mobile · Sürüm 1.0.0</Text>
        </ScrollView>
      </KeyboardAvoidingView>
    </ScreenContainer>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { flexGrow: 1, justifyContent: 'center', padding: theme.spacing.lg, paddingVertical: theme.spacing.xl },
  hero: { marginBottom: theme.spacing.lg },
  brandRow: { alignItems: 'center', flexDirection: 'row', gap: theme.spacing.sm },
  brand: { color: theme.colors.text, fontSize: 22, fontWeight: '800', letterSpacing: -0.4 },
  product: { color: theme.colors.primary, fontSize: 10, fontWeight: '800', letterSpacing: 1, marginTop: 2 },
  title: { color: theme.colors.text, fontSize: 29, fontWeight: '800', letterSpacing: -0.8, lineHeight: 36, marginTop: theme.spacing.md },
  subtitle: { color: theme.colors.textMuted, fontSize: 15, lineHeight: 23, marginTop: theme.spacing.sm },
  card: { gap: theme.spacing.sm, padding: theme.spacing.md },
  cardHeading: { gap: 4 },
  cardEyebrow: { color: theme.colors.primary, fontSize: 11, fontWeight: '800', letterSpacing: 1 },
  cardTitle: { color: theme.colors.text, fontSize: 22, fontWeight: '800' },
  cardSubtitle: { color: theme.colors.textMuted, fontSize: 14 },
  success: { backgroundColor: '#F0FDF4', borderColor: '#BBF7D0', borderRadius: theme.radius.sm, borderWidth: 1, color: theme.colors.success, padding: theme.spacing.sm },
  form: { gap: theme.spacing.sm, marginTop: theme.spacing.xs },
  footerRow: { alignItems: 'center', flexDirection: 'row', gap: 6, justifyContent: 'center', marginTop: theme.spacing.sm },
  footerText: { color: theme.colors.textMuted },
  link: { color: theme.colors.primary, fontWeight: '800' },
  version: { color: theme.colors.textMuted, fontSize: 12, marginTop: theme.spacing.lg, textAlign: 'center' },
});

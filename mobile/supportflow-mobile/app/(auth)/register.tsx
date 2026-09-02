import { AppButton } from '@/components/AppButton';
import { AppTextInput } from '@/components/AppTextInput';
import { ErrorMessage } from '@/components/ErrorMessage';
import { ScreenContainer } from '@/components/ScreenContainer';
import { useAuth } from '@/context/AuthContext';
import { ApiError } from '@/services/apiClient';
import { theme } from '@/theme/theme';
import { type Href, Link, router } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native';

export default function RegisterScreen() {
  const { register, isSubmitting } = useAuth();
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [submitted, setSubmitted] = useState(false);

  const errors = {
    firstName: submitted && !firstName.trim() ? 'Ad alanı zorunludur.' : '',
    lastName: submitted && !lastName.trim() ? 'Soyad alanı zorunludur.' : '',
    email: submitted && !email.trim() ? 'E-posta alanı zorunludur.' : '',
    password: submitted && password.length < 8 ? 'Şifre en az 8 karakter olmalıdır.' : '',
  };

  async function handleRegister() {
    setSubmitted(true);
    setError('');
    if (Object.values(errors).some(Boolean)) {
      return;
    }

    try {
      await register({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        email: email.trim(),
        password,
      });
      router.replace('/(auth)/login?registered=1' as Href);
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : 'Kayıt oluşturulamadı. Lütfen tekrar deneyin.');
    }
  }

  return (
    <ScreenContainer>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.flex}>
        <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
          <Text style={styles.brand}>SupportFlow</Text>
          <Text style={styles.title}>Müşteri hesabı oluşturun</Text>
          <Text style={styles.subtitle}>Kayıtlar yalnızca Müşteri rolüyle oluşturulur.</Text>
          <View style={styles.card}>
            {error ? <ErrorMessage message={error} /> : null}
            <AppTextInput autoComplete="given-name" error={errors.firstName} label="Ad" onChangeText={setFirstName} placeholder="Adınız" value={firstName} />
            <AppTextInput autoComplete="family-name" error={errors.lastName} label="Soyad" onChangeText={setLastName} placeholder="Soyadınız" value={lastName} />
            <AppTextInput autoCapitalize="none" autoComplete="email" error={errors.email} keyboardType="email-address" label="E-posta" onChangeText={setEmail} placeholder="ornek@firma.com" value={email} />
            <AppTextInput autoComplete="new-password" error={errors.password} label="Şifre" onChangeText={setPassword} placeholder="En az 8 karakter" secureTextEntry value={password} />
            <AppButton label="Kayıt Ol" loading={isSubmitting} onPress={() => void handleRegister()} />
          </View>
          <View style={styles.footerRow}>
            <Text style={styles.footerText}>Zaten hesabınız var mı?</Text>
            <Link href={'/(auth)/login' as Href} style={styles.link}>Giriş Yap</Link>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </ScreenContainer>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { gap: theme.spacing.md, padding: theme.spacing.lg, paddingTop: theme.spacing.xl },
  brand: { color: theme.colors.primary, fontSize: 22, fontWeight: '800', marginTop: theme.spacing.lg },
  title: { color: theme.colors.text, fontSize: 28, fontWeight: '800', letterSpacing: -0.5, marginTop: theme.spacing.sm },
  subtitle: { color: theme.colors.textMuted, fontSize: 15, lineHeight: 22 },
  card: { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, borderRadius: theme.radius.lg, borderWidth: 1, gap: theme.spacing.md, marginTop: theme.spacing.md, padding: theme.spacing.lg },
  footerRow: { alignItems: 'center', flexDirection: 'row', gap: 6, justifyContent: 'center', marginBottom: theme.spacing.lg },
  footerText: { color: theme.colors.textMuted },
  link: { color: theme.colors.primary, fontWeight: '800' },
});

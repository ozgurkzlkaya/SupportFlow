import { type Href, Redirect, useLocalSearchParams } from 'expo-router';

export default function GmailAuthorizationReturnRoute() {
  const { status } = useLocalSearchParams<{ status?: string }>();
  const oauthStatus = status === 'success' ? 'success' : 'error';

  return <Redirect href={`/(app)/gmail?oauth=${oauthStatus}` as Href} />;
}

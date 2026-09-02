import { type Href, Redirect } from 'expo-router';

export default function IndexRoute() {
  return <Redirect href={'/(app)/(tabs)' as Href} />;
}

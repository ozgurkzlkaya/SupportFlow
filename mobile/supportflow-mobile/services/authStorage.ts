import * as SecureStore from 'expo-secure-store';

const accessTokenKey = 'supportflow.access-token';

export const authStorage = {
  getAccessToken: () => SecureStore.getItemAsync(accessTokenKey),
  saveAccessToken: (token: string) => SecureStore.setItemAsync(accessTokenKey, token),
  clearAccessToken: () => SecureStore.deleteItemAsync(accessTokenKey),
};

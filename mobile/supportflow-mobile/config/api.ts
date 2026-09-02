const configuredApiUrl = process.env.EXPO_PUBLIC_API_URL?.trim();

export const apiConfig = {
  // A physical-device build must receive its reachable API address from the environment.
  baseUrl: configuredApiUrl?.replace(/\/$/, '') ?? '',
};

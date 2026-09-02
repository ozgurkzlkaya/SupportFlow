# SupportFlow Mobile

React Native and Expo Router mobile client for SupportFlow. Mobile V1 includes secure authentication, role-aware navigation, customer ticket lifecycle, staff/admin ticket and conversation workflows, notifications, profile, attachments, and Admin Gmail integration.

## Project

- Path: `C:\Users\Costa\Desktop\SupportFlow\mobile\supportflow-mobile`
- Expo SDK: 54
- Runtime: Expo SDK 54; Android custom development client required for mobile Gmail OAuth
- API authentication: JWT stored with `expo-secure-store`

## Start for Development Client

```powershell
cd C:\Users\Costa\Desktop\SupportFlow\mobile\supportflow-mobile
npx expo start --dev-client --clear
```

Use a custom Android development client for the `supportflowmobile` OAuth return route. Expo Go remains suitable for ordinary development checks but cannot complete the custom-scheme OAuth path.

## Start the API for a Physical Device

```powershell
cd C:\Users\Costa\Desktop\SupportFlow\backend\SupportFlow.Api
dotnet run --launch-profile http --urls "http://0.0.0.0:5135"
```

Set a reachable backend URL through `EXPO_PUBLIC_API_URL`. The mobile app has no hard-coded LAN address:

```powershell
$env:EXPO_PUBLIC_API_URL = "http://YOUR-LAN-IP:5135"
npx expo start --dev-client --clear
```

The computer's LAN IP can change after reconnecting to a network or restarting its network adapter. Update `EXPO_PUBLIC_API_URL` and restart Expo when that happens.

## Validation

```powershell
npx tsc --noEmit
npm run lint
npx expo config --type public
```

Do not commit access tokens, passwords, Gmail credentials, or API secrets. Gmail controls use backend APIs only; the mobile client stores no Gmail secrets.

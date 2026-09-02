# SupportFlow Release and Delivery Guide

## Scope

Web V1 is frozen. Mobile V1 is delivered from `mobile/supportflow-mobile` with Expo SDK 54. This guide distinguishes controlled local-demo delivery from production release requirements.

## Local Configuration

Keep secrets outside source control. The API requires local `ConnectionStrings:DefaultConnection` and `Jwt:Key`. The web application requires `VITE_API_BASE_URL`. The physical mobile client requires `EXPO_PUBLIC_API_URL` to be a reachable API URL. Gmail OAuth credentials, token storage, and redirect settings remain backend-only.

Do not commit JWT keys, OAuth client credentials, Gmail token files, real passwords, environment files containing secrets, or Android signing keys.

## Development and Demo Commands

API: `dotnet restore backend/SupportFlow.Api/SupportFlow.Api.csproj`, then `dotnet run --project backend/SupportFlow.Api --launch-profile http`. The development API profile listens on `http://localhost:5135`.

Web: in `frontend/supportflow-web`, run `npm install` then `npm run dev`.

Mobile: in `mobile/supportflow-mobile`, run `npm install`, set `EXPO_PUBLIC_API_URL` to a reachable URL, then run `npx expo start --dev-client --clear`. The mobile custom scheme is `supportflowmobile`.

## Gmail Safety Mode

- `GmailBackgroundSync.Enabled`: `false`
- `GmailBackgroundSync.IntervalSeconds`: `600`
- `GmailBackgroundSync.InitialDelaySeconds`: `30`
- Manual Admin action **Gelen Mailleri Isleme**: available

The Gmail path includes shared rate-limit state, 429 cooldown/backoff, incremental one-day-overlap discovery after a successful cycle, batch processed-message lookup, durable deduplication, lazy attachment retrieval, and diagnostics separated from background processing. Safe restart behavior relies on processed-message deduplication.

For demo and release readiness, keep automatic synchronization disabled and use the Admin manual-processing fallback. Enable automatic sync only after validating the Gmail quota for the deployed environment.

## OAuth Deployment

Production needs a stable HTTPS callback registered in Google Cloud and routed to the backend. The backend owns OAuth state/tokens and redirects the app to `supportflowmobile://gmail-auth`. A temporary tunnel can be used for a controlled demo but is not a production callback solution.

## Android Delivery

Current metadata is display name `SupportFlow`, version `1.0.0`, version code `1`, and package `com.anonymous.supportflowmobile`.

Local Android builds require JDK 17 and an installed Android SDK. Set `JAVA_HOME` and `ANDROID_HOME` (or create an ignored local `android/local.properties` containing `sdk.dir`) on the build workstation. On Windows, use a short workspace path such as `C:\src\SupportFlow`; the current long workspace path exceeds the native CMake/Ninja object-file path limit for a full release APK build.

For a local demo APK, run in `mobile/supportflow-mobile/android`: `.\gradlew.bat :app:assembleRelease --no-daemon`.

The current release variant is debug-signed. Its APK is for controlled local demo/testing only, not Google Play distribution.

Before production Android delivery:

1. Reserve an organization-owned package identifier.
2. Use a production signing key held in CI/secret management, never in source control.
3. Configure release signing and increment `versionCode` for every distribution.
4. Use a stable HTTPS API endpoint and stable Google OAuth callback.
5. Build a signed App Bundle with `:app:bundleRelease`.
6. Complete physical-device acceptance against the production-like API.

## Quality Gates

API: `dotnet build backend/SupportFlow.Api/SupportFlow.Api.csproj` and `dotnet test backend/SupportFlow.Api.Tests/SupportFlow.Api.Tests.csproj`.

Web: in `frontend/supportflow-web`, run `npm run lint` and `npm run build`.

Mobile: in `mobile/supportflow-mobile`, run `npx tsc --noEmit`, `npm run lint`, `npx expo config --type public`, and `npx expo export --platform android`.

## Delivery Assessment

Local demo delivery is ready when quality gates and the physical checklist pass. Production distribution remains blocked until stable hosting, secret management, stable OAuth registration, organization-owned Android identity, and production signing are complete.

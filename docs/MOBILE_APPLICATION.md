# SupportFlow Mobile Application

## Purpose

SupportFlow Mobile is the Android-focused companion application for the SupportFlow ticketing system. It gives customers a mobile ticket lifecycle and gives TechnicalStaff and Admin users mobile access to the ticket queue, conversations, notifications, profile functions, and authorized operational controls.

## Technology Stack

- React Native with Expo SDK 54
- TypeScript
- Expo Router for file-based navigation and role-aware route groups
- Expo SecureStore for the local JWT session
- Expo FileSystem and Expo Sharing for authenticated attachment downloads
- Android native project configuration for local development and demo builds

## Application Architecture

The `mobile/supportflow-mobile` project is organized by responsibility:

- `app/`: Expo Router routes, tabs, login, ticket detail, and Gmail OAuth return route.
- `components/`: shared visual and interaction components.
- `services/`: API client and domain services for tickets, conversations, notifications, profile, and Gmail status.
- `context/`: authenticated session and application-level state.
- `types/`: API-facing TypeScript contracts.
- `theme/`, `constants/`, and `utils/`: visual tokens, shared constants, and formatting helpers.
- `android/`: generated/native Android configuration used for local Android development builds. Generated build output is not a submission artifact.

## Authentication

The application uses the existing backend JWT authentication contract. After a normal login, the session is retained in SecureStore and the API client sends the bearer token only to the configured backend API. Passwords, OAuth tokens, Gmail client secrets, and refresh tokens are never logged or stored in application source.

Expired or invalid sessions are handled through the existing authenticated API flow and return the user to login. Mobile route guards provide a UI boundary; backend authorization remains the authoritative security control.

## Roles

### Customer

Customers can view their own tickets, create tickets, add public messages, use attachments, view notifications, manage their profile, and log out. Customer navigation does not expose Conversations or Gmail Integration.

### TechnicalStaff

TechnicalStaff can work the ticket queue, open ticket details, update available ticket operations, send public replies or internal notes, review conversations, view notifications, manage profile data, and log out.

### Admin

Admins have the TechnicalStaff operational surface plus administrator-only Gmail Integration controls. Gmail OAuth, mailbox readiness, and manual inbox processing remain backend-owned and Admin-only.

## Main Screens

- **Login:** normal backend authentication and role-aware routing.
- **Home:** role-specific summary entry point.
- **Tickets:** server-backed ticket list, search/filter/sort controls where authorized.
- **Ticket Detail:** ticket metadata, messages, activity timeline, attachments, and permitted operations.
- **Conversations:** TechnicalStaff/Admin requester communication center.
- **Notifications:** backend-provided notification list with read/unread states.
- **Profile:** identity, role, password-change form where supported, and logout.
- **Gmail Integration:** Admin-only Gmail readiness, OAuth flow, and manual processing controls.

## Customer Workflow

1. Login as a Customer.
2. Review owned tickets or create a new ticket.
3. Open ticket detail, add a public message, and attach a supported file if needed.
4. Open an attachment through the authenticated local download and Android share/open flow.
5. Review notifications and profile, then log out.

## TechnicalStaff Workflow

1. Login as TechnicalStaff.
2. Open the ticket queue and select a ticket.
3. Use available assignment, status, priority, reply, or internal-note actions.
4. Open Conversations to review requester activity across tickets.
5. Review notifications/profile and log out.

## Admin Workflow

1. Login as Admin.
2. Use ticket and conversation operations according to the same backend authorization rules.
3. Review notifications and profile.
4. Open Gmail Integration to check readiness, complete OAuth when needed, or invoke the supported manual inbox-processing action.

## Attachments

Attachment metadata comes from the existing ticket API. Binary files are downloaded with the authenticated bearer token to a unique sanitized cache filename. The mobile app validates a non-empty local file and uses Expo Sharing for an Android-compatible visible share/open chooser. This supports images, PDFs, and other supported files without exposing backend storage paths.

## Notifications

The application renders the existing backend notification feed, including loading, empty, error, and read/unread states. It does not introduce push notifications or a separate mobile notification backend.

## Gmail OAuth Mobile Flow

The authorized mobile flow is:

```text
Mobile -> backend OAuth start -> Google -> HTTPS backend callback
       -> backend stores tokens -> supportflowmobile://gmail-auth
       -> application refreshes Gmail status
```

Gmail credentials, authorization codes, access tokens, and refresh tokens remain on the backend. They are never returned to the mobile application.

## API Configuration

The app receives its API base URL through `EXPO_PUBLIC_API_URL`. For a local physical-device LAN demo, use a reachable machine address, for example:

```text
EXPO_PUBLIC_API_URL=http://<PC_LAN_IP>:5135
```

The phone and backend machine must be reachable on the same network. A private LAN IP is a development/demo configuration, not a production endpoint.

## Android Demo APK

The accepted M8.2 artifact is a local demo APK:

- versionName: `1.0.0`
- versionCode: `1`
- package ID: `com.anonymous.supportflowmobile`
- signing: Android debug/demo signing
- network: requires a reachable LAN backend API

It is not a Play Store production release.

## Validation

Run from `mobile/supportflow-mobile`:

```powershell
npx tsc --noEmit
npm run lint
npx expo config --type public
npx expo export --platform android
```

## Known Limitations

- The demo APK depends on a local/LAN API endpoint.
- Gmail background synchronization is intentionally disabled by default; Admin manual processing remains available.
- The APK uses demo/debug signing and is not distributed through Play Store signing.

## Production Requirements

Production delivery requires a stable HTTPS API and OAuth callback, organization-owned package identity, release signing and AAB/Play App Signing, managed secrets, observability, backups, deployment automation, and provider-quota operations.

# SupportFlow PC / Web Application

## Purpose

SupportFlow PC / Web is the browser application and ASP.NET Core API that provide the main operational ticketing experience. It supports Customer, TechnicalStaff, and Admin workflows using one role-authorized API and SQL Server data layer.

## Architecture

```text
Web Browser -> React + TypeScript -> ASP.NET Core API -> SQL Server
                                      |
                                      +-> Gmail API / OAuth 2.0
```

The mobile client uses the same backend contracts. Authorization decisions remain in the API rather than being trusted to a particular user interface.

## Backend Technology

- ASP.NET Core on .NET 10
- Entity Framework Core
- SQL Server
- JWT authentication
- role-based authorization
- Gmail API/OAuth integration held on the server
- xUnit-style automated backend test project

## Frontend Technology

- React
- TypeScript
- Vite build pipeline
- route protection and role-aware navigation
- API service layer and Turkish application UI

## Authentication and Authorization

The API issues JWT sessions through the established authentication endpoints. Role-based authorization protects server endpoints and the web UI mirrors permitted navigation. Browser routing does not replace backend authorization.

## User Roles

- **Customer:** owns and follows customer tickets, messages, attachments, notifications, and profile settings.
- **TechnicalStaff:** works operational tickets, messages, conversations, queue views, and authorized ticket changes.
- **Admin:** has administrative ticket/user/taxonomy/Gmail controls in addition to operational access.

## Ticket Management

SupportFlow includes ticket creation, assignment, status and priority changes, public replies, internal notes, attachments, history/activity information, server-side pagination, filtering, classification, queue aging, SLA projection, and agent workload views where authorized.

## Conversations

The Conversations Center groups requester communication across tickets for TechnicalStaff and Admin users. It distinguishes customer/external requester messages, staff public replies, and internal notes according to backend permissions and visibility rules.

## Notifications

The web application renders backend-created in-app notifications with read-state behavior and ticket deep links. Notification eligibility and content stay on the API side.

## Attachments

Ticket attachments use authenticated metadata and binary endpoints. The UI renders safe previews/download behavior without exposing physical storage paths.

## Gmail Integration

Gmail Integration is Admin-only. It supports backend-held OAuth, inbound email discovery, requester/correlation handling, attachment import, outbound public-reply delivery handling, and a supported manual inbox-processing action.

Automatic background synchronization is disabled by default. This is an intentional quota-safety mode: manual Admin processing remains available, and Gmail rate-limit/cooldown handling is kept server-side. Inbound Gmail can create a ticket or correlate an email reply to an existing ticket/conversation according to the existing backend rules.

## Database

SQL Server is accessed through EF Core. The backend contains ordered migrations and a model snapshot. Migration application is an explicit operational step; application startup and documentation do not treat it as a silent schema-management mechanism.

## Backend Startup

From the repository root:

```powershell
cd backend/SupportFlow.Api
dotnet run --launch-profile http --urls "http://0.0.0.0:5135"
```

## Frontend Startup

From `frontend/supportflow-web`, the current package command is:

```powershell
npm run dev
```

## Configuration

Connection strings, JWT settings, Gmail OAuth client settings, token storage, and environment-specific API URLs are local configuration concerns. Do not commit secret values, Gmail credential files, token stores, or local OAuth state.

## Testing

Backend:

```powershell
dotnet build
dotnet test
```

Web:

```powershell
npm run lint
npm run build
```

## Security

JWT sessions, role-based endpoint authorization, protected attachment access, backend-only Gmail credentials, ignored local secret/token paths, and server-side requester correlation are core controls. UI guards improve usability but do not replace API authorization.

## Known Limitations

- The accepted demo runs locally and can use LAN HTTP during demonstration.
- Gmail background synchronization is intentionally disabled by default because provider quota operations need production monitoring and policy controls.
- The demo APK is not a production-signed distribution.

## Production Requirements

Production operation requires stable HTTPS hosting, managed secrets, production OAuth callbacks, SQL backup/recovery, monitoring and alerting, rate-limit operations, organization release signing, and deployment controls.

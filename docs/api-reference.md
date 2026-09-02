# REST API Reference

This reference is based on the current controller implementations in `backend/SupportFlow.Api/Controllers`.

Notes:

- Routes are shown in lowercase for readability. ASP.NET Core routing is case-insensitive.
- JSON enum values are serialized as strings.
- Authorized endpoints require a bearer JWT unless otherwise noted.
- Validation errors are returned as `400 Bad Request` with standard ASP.NET Core validation details or a `{ message }` payload, depending on the endpoint.

## Auth

| Method | Route | Roles | Request | Response | Important status codes |
| --- | --- | --- | --- | --- | --- |
| `POST` | `/api/auth/register` | Anonymous | `RegisterRequest { firstName, lastName, email, password, companyId?, role }` | `201 AuthUserResponse` | `201`, `400`, `409` |
| `POST` | `/api/auth/login` | Anonymous | `LoginRequest { email, password }` | `200 LoginResponse { accessToken, expiresAt, user }` | `200`, `400`, `401` |
| `GET` | `/api/auth/me` | Any authenticated user | none | `200 AuthUserResponse` | `200`, `401` |

### DTOs

- `RegisterRequest`
  - `firstName: string`
  - `lastName: string`
  - `email: string`
  - `password: string`
  - `companyId?: number`
  - `role: Customer | TechnicalStaff | Admin`
- `LoginRequest`
  - `email: string`
  - `password: string`
- `AuthUserResponse`
  - `id`
  - `firstName`
  - `lastName`
  - `email`
  - `role`
  - `companyId`
  - `createdAt`
- `LoginResponse`
  - `accessToken`
  - `expiresAt`
  - `user: AuthUserResponse`

## Users / Profile

| Method | Route | Roles | Request | Response | Important status codes |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/api/users/me` | Customer, TechnicalStaff, Admin | none | `200 UserProfileResponse` | `200`, `401` |
| `PATCH` | `/api/users/me` | Customer, TechnicalStaff, Admin | `UpdateUserProfileRequest { firstName, lastName }` | `200 UserProfileResponse` | `200`, `400`, `401` |
| `POST` | `/api/users/me/change-password` | Customer, TechnicalStaff, Admin | `ChangePasswordRequest { currentPassword, newPassword, confirmNewPassword }` | `204 No Content` | `204`, `400`, `401` |
| `GET` | `/api/users/technical-staff` | TechnicalStaff, Admin | none | `200 TechnicalStaffResponse[]` | `200`, `401`, `403` |

### DTOs

- `UserProfileResponse`
  - `id`
  - `firstName`
  - `lastName`
  - `email`
  - `role`
  - `companyId`
  - `isActive`
  - `createdAt`
- `TechnicalStaffResponse`
  - `id`
  - `firstName`
  - `lastName`
  - `email`

## Tickets

| Method | Route | Roles | Request | Response | Important status codes |
| --- | --- | --- | --- | --- | --- |
| `POST` | `/api/tickets` | Customer | `CreateTicketRequest { title, description, priority }` | `201 TicketDetailResponse` | `201`, `400`, `401`, `403` |
| `GET` | `/api/tickets` | Customer, TechnicalStaff, Admin | none | `200 TicketListItemResponse[]` | `200`, `401` |
| `GET` | `/api/tickets/{id}` | Customer, TechnicalStaff, Admin | none | `200 TicketDetailResponse` | `200`, `401`, `404` |
| `GET` | `/api/tickets/{id}/messages` | Customer, TechnicalStaff, Admin | none | `200 TicketMessageResponse[]` | `200`, `401`, `404` |
| `POST` | `/api/tickets/{id}/messages` | Customer, TechnicalStaff, Admin | `CreateTicketMessageRequest { message, isInternal }` | `201 TicketMessageResponse` | `201`, `400`, `401`, `403`, `404` |
| `PATCH` | `/api/tickets/{id}/assign` | TechnicalStaff, Admin | `AssignTicketRequest { assignedUserId }` | `200 TicketDetailResponse` | `200`, `400`, `401`, `403`, `404` |
| `PATCH` | `/api/tickets/{id}/status` | TechnicalStaff, Admin | `UpdateTicketStatusRequest { status, note? }` | `200 TicketDetailResponse` | `200`, `400`, `401`, `403`, `404` |
| `GET` | `/api/tickets/{id}/history` | Customer, TechnicalStaff, Admin | none | `200 TicketStatusHistoryResponse[]` | `200`, `401`, `404` |

### Ticket behaviors

- Customers only see their own tickets.
- Customers only see non-internal ticket messages.
- Customers cannot create internal messages.
- Assigning a `New` ticket to technical staff also changes status to `Assigned`.
- Valid status transitions are enforced in `TicketService`.
- Customer replies to `Resolved` or `Closed` tickets automatically reopen the ticket to `Reopened`.

### DTOs

- `CreateTicketRequest`
  - `title`
  - `description`
  - `priority: Low | Normal | High | Critical`
- `TicketListItemResponse`
  - `id`
  - `ticketNumber`
  - `title`
  - `status`
  - `priority`
  - `createdAt`
  - `updatedAt`
  - `customerId`
  - `customerName`
  - `assignedUserId`
  - `assignedUserName`
- `TicketDetailResponse`
  - `id`
  - `ticketNumber`
  - `title`
  - `description`
  - `status`
  - `priority`
  - `createdAt`
  - `updatedAt`
  - `closedAt`
  - `customerId`
  - `customerName`
  - `customerEmail`
  - `assignedUserId`
  - `assignedUserName`
- `CreateTicketMessageRequest`
  - `message`
  - `isInternal`
- `TicketMessageResponse`
  - `id`
  - `ticketId`
  - `senderId`
  - `senderName`
  - `senderRole`
  - `message`
  - `isInternal`
  - `createdAt`
- `AssignTicketRequest`
  - `assignedUserId`
- `UpdateTicketStatusRequest`
  - `status: New | Assigned | InProgress | WaitingForCustomer | Resolved | Closed | Reopened`
  - `note?`
- `TicketStatusHistoryResponse`
  - `id`
  - `oldStatus`
  - `newStatus`
  - `changedByUserId`
  - `changedByUserName`
  - `note`
  - `changedAt`

## Attachments

| Method | Route | Roles | Request | Response | Important status codes |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/api/tickets/{id}/attachments` | Customer, TechnicalStaff, Admin | none | `200 TicketAttachmentResponse[]` | `200`, `401`, `404` |
| `POST` | `/api/tickets/{id}/attachments` | Customer, TechnicalStaff, Admin | `multipart/form-data` with field name exactly `file` | `201 TicketAttachmentResponse` | `201`, `400`, `401`, `404` |
| `GET` | `/api/tickets/{id}/attachments/{attachmentId}` | Customer, TechnicalStaff, Admin | none | binary file download/preview | `200`, `401`, `404` |

### Attachment behaviors

- The upload endpoint declares `Consumes("multipart/form-data")`.
- The multipart field must be named exactly `file`.
- The current attachment endpoints operate on ticket-level attachments; uploaded rows are stored with `MessageId = null`, and the list endpoint returns only rows where `MessageId == null`.
- Current limits in `AttachmentService`:
  - max 5 MB per file
  - max 5 ticket-level attachments per ticket
  - allowed types: PNG, JPG, JPEG, WEBP, PDF
- File signatures are validated against expected content types.

### DTO

- `TicketAttachmentResponse`
  - `id`
  - `ticketId`
  - `messageId`
  - `fileName`
  - `contentType`
  - `fileSize`
  - `isImage`
  - `createdAt`
  - `uploadedByUserId`
  - `uploadedByUserName`

## Notifications

| Method | Route | Roles | Request | Response | Important status codes |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/api/notifications` | Customer, TechnicalStaff, Admin | query `unreadOnly=false` | `200 NotificationResponse[]` | `200`, `401` |
| `GET` | `/api/notifications/unread-count` | Customer, TechnicalStaff, Admin | none | `200 UnreadNotificationCountResponse` | `200`, `401` |
| `PATCH` | `/api/notifications/{id}/read` | Customer, TechnicalStaff, Admin | none | `204 No Content` | `204`, `401`, `404` |
| `PATCH` | `/api/notifications/read-all` | Customer, TechnicalStaff, Admin | none | `200 MarkAllNotificationsReadResponse` | `200`, `401` |

### DTOs

- `NotificationResponse`
  - `id`
  - `ticketId`
  - `title`
  - `message`
  - `type: TicketCreated | CustomerReply | TechnicalReply | Assignment | StatusChange | GmailInbound | GmailOutboundSuccess | GmailOutboundFailure`
  - `isRead`
  - `createdAt`
- `UnreadNotificationCountResponse`
  - `count`
- `MarkAllNotificationsReadResponse`
  - `updatedCount`

## Gmail / Integrations

All integration endpoints are Admin-only except the OAuth callback, which is anonymous by design.

| Method | Route | Roles | Request | Response | Important status codes |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/api/integrations/gmail/oauth/start` | Admin | none | `200 GmailOAuthStartResponse { authorizationUrl }` | `200`, `401`, `403`, `503` |
| `GET` | `/api/integrations/gmail/oauth/callback` | Anonymous | query `code?`, `state?`, `error?` | `200 { message }` | `200`, `400`, `503` |
| `GET` | `/api/integrations/gmail/oauth/status` | Admin | none | `200 GmailOAuthStatusResponse` | `200`, `401`, `403` |
| `POST` | `/api/integrations/gmail/process` | Admin | none | `200 GmailMailboxProcessingSummary` | `200`, `401`, `403`, `409`, `503` |

### Gmail behaviors

- OAuth start generates a Google URL with Gmail read and send scopes.
- OAuth callback validates the state and stores the resulting token.
- OAuth status reports:
  - `configured`
  - `authorized`
  - `canRead`
  - `canSend`
  - `mailbox`
- Gmail processing can fail with:
  - `409 Conflict` when authorization is required
  - `503 Service Unavailable` when Gmail integration is unavailable

### DTOs

- `GmailOAuthStartResponse`
  - `authorizationUrl`
- `GmailOAuthStatusResponse`
  - `configured`
  - `authorized`
  - `canRead`
  - `canSend`
  - `mailbox`
- `GmailMailboxProcessingSummary`
  - `processed`
  - `newTickets`
  - `messagesAdded`
  - `alreadyProcessed`
  - `unknownCustomers`
  - `senderMismatches`
  - `selfMessagesIgnored`
  - `failed`

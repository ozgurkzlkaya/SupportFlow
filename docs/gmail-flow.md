# Gmail Integration Flow

This document describes the Gmail integration that is currently implemented and verified in SupportFlow.

## Scope and Authorization Model

SupportFlow requests exactly these Gmail scopes:

- `https://www.googleapis.com/auth/gmail.readonly`
- `https://www.googleapis.com/auth/gmail.send`

OAuth behavior implemented in `GmailAuthorizationService`:

- `GET /api/integrations/gmail/oauth/start` creates a new authorization URL
- `GET /api/integrations/gmail/oauth/callback` completes authorization
- `access_type=offline`
- `prompt=consent`
- state lifetime: 15 minutes
- state storage: file-backed `gmail-oauth-state` directory under the API content root
- state usage: single-use
- multiple valid states can coexist until consumed or expired

Stored Gmail tokens are kept in the configured protected token store, currently rooted at `gmail-token`.

## Mailbox Discovery

Inbox discovery is handled by `GmailInboxService`.

Manual Admin fallback query:

```text
in:inbox newer_than:7d -from:support11flow@gmail.com
```

Background synchronization uses a quota-conscious discovery strategy:

- after an API restart, the first successful cycle uses the bounded catch-up query
  `in:inbox newer_than:1d -from:support11flow@gmail.com`
- later successful cycles use `after:yyyy/MM/dd` with a one-day overlap and the
  same mailbox exclusion
- the incremental cursor is in memory; a restart intentionally returns to the
  one-day catch-up query rather than persisting synchronization state
- candidate Gmail message IDs are compared with `ProcessedEmailMessages` in one
  batch before full Gmail message or attachment reads
- full attachment bytes are resolved only for an unseen message after its ticket
  persistence succeeds

Diagnostics remains separate from automatic processing. It is never invoked by
the background cycle and uses its existing 90-second in-memory cache. Gmail 429
responses stop the current cycle immediately; all Gmail reads observe the shared
provider cooldown.

Behavior:

- only inbox mail is considered
- only recent mail from the last 7 days is considered
- spam and trash are excluded
- self-sent messages from the SupportFlow mailbox are excluded at query level and also rechecked in processing

## New Email Flow

When an active customer sends a new email to the SupportFlow mailbox:

1. Gmail receives the message in `support11flow@gmail.com`.
2. An Admin calls `POST /api/integrations/gmail/process`.
3. `GmailMailboxProcessor` lists recent inbox message IDs, batch-skips IDs that
   are already in `ProcessedEmailMessages`, and reads only unseen messages.
4. `InboundEmailProcessor` validates that the message has:
   - `GmailMessageId`
   - `GmailThreadId`
   - sender email
   - usable body
5. Self-message protection rejects any message whose normalized sender email equals the configured mailbox address.
6. The sender email must map to an active `Customer` user.
7. The processor checks whether `ProcessedEmailMessages.GmailMessageId` already exists.
8. If the message is genuinely new and no existing ticket matches, SupportFlow:
   - creates a new `Ticket`
   - creates a `TicketEmailThread`
   - creates a `ProcessedEmailMessage`
   - leaves `ProcessedEmailMessage.TicketMessageId = null`
9. The transaction commits first.
10. After commit, `TicketAcknowledgmentService` attempts to send the Gmail acknowledgment email.
11. Technical staff/admin notifications are created for the new Gmail inbound ticket.

Acknowledgment subject format:

```text
[{TicketNumber}] {Ticket.Title}
```

Example:

```text
[SUP-2026-001009] Konu: Faz 4 ACK Testi - 1308
```

## Reply Correlation Flow

When the customer replies to the SupportFlow acknowledgment email, the inbound Gmail message is processed in this order:

1. `GmailMessageId` deduplication
2. `GmailThreadId`
3. `InReplyTo`
4. `References`
5. `SubjectTicketNumber`
6. New ticket fallback

Implementation details:

- Deduplication is performed first by looking up `ProcessedEmailMessages.GmailMessageId`.
- Exact Gmail thread matches use `TicketEmailThreads.GmailThreadId`.
- If `InReplyTo` exists, SupportFlow tries to find an unambiguous prior processed message by `InternetMessageId`.
- If `References` contains multiple IDs, SupportFlow walks them in reverse order and accepts the first unambiguous ticket match.
- If no header-based correlation succeeds, SupportFlow extracts a ticket number such as `SUP-2026-001009` from the subject.
- If correlation succeeds but the sender does not own the matched ticket, the message is rejected as `SenderMismatch`.

## Reply Persistence Rules

If the reply correlates to an existing ticket:

- no new ticket is created
- one `TicketMessage` is created
- one `ProcessedEmailMessage` is created
- `ProcessedEmailMessage.TicketMessageId` points to the created message
- if the Gmail thread ID is new, SupportFlow creates a `TicketEmailThread` alias for the existing ticket instead of creating another ticket

## Automatic Reopen Rule

If the replying sender is the ticket customer and the current ticket status is `Resolved` or `Closed`:

- the new customer message is added
- ticket status automatically becomes `Reopened`
- `ClosedAt` is cleared to `null`
- a `TicketStatusHistory` row is written with the transition to `Reopened`

## Self-Message Protection

SupportFlow protects itself from turning its own outbound Gmail mail into inbound tickets in two places:

1. discovery query excludes `-from:<mailbox>`
2. `InboundEmailProcessor` rejects any message whose sender equals the configured mailbox

Processing status for those messages is `SelfMessageIgnored`.

## Deduplication

Deduplication is based on the unique `ProcessedEmailMessages.GmailMessageId` index plus an explicit pre-check in `InboundEmailProcessor`.

Results:

- the same Gmail message is not processed twice
- the same Gmail reply does not create duplicate `TicketMessage` rows
- the same inbound message does not create duplicate tickets
- concurrent processing collisions fall back to `AlreadyProcessed` when the unique index proves the message was stored by another execution

## Processing Summary

`POST /api/integrations/gmail/process` returns `GmailMailboxProcessingSummary` with counts for:

- `processed`
- `newTickets`
- `messagesAdded`
- `alreadyProcessed`
- `unknownCustomers`
- `senderMismatches`
- `selfMessagesIgnored`
- `failed`

## Mermaid Flow

```mermaid
flowchart TD
    CustomerMail[Customer Gmail] --> Mailbox[support11flow@gmail.com]
    Mailbox --> GmailApi[Google Gmail API]
    GmailApi --> ProcessEndpoint[POST /api/integrations/gmail/process]
    ProcessEndpoint --> ListRead[List and read inbox messages]
    ListRead --> Validate[Validate sender/body/ids]
    Validate --> Dedupe[GmailMessageId dedupe]
    Dedupe --> Correlate[Thread / headers / subject correlation]
    Correlate -->|No match| NewTicket[Create Ticket + TicketEmailThread + ProcessedEmailMessage]
    Correlate -->|Match| AddReply[Create TicketMessage + ProcessedEmailMessage]
    AddReply --> Reopen{Resolved or Closed?}
    Reopen -->|Yes| Reopened[Status -> Reopened]
    Reopen -->|No| DoneReply[Reply stored]
    NewTicket --> Commit[DB commit]
    Commit --> Ack[Send Gmail acknowledgment]
    Ack --> Sent[Sent from support11flow@gmail.com]
```

# Demo Scenario

This presentation flow stays aligned with the current SupportFlow implementation.

## End-to-End Demo

1. A `Customer` signs in and opens the prepared ticket dashboard.
2. The customer opens a ticket with a PNG attachment and follows the public conversation.
3. An `Admin` opens the dashboard, then filters the ticket list by priority.
4. The Admin opens ticket detail, verifies assignment and status/priority chips, and adds a public reply or internal note when a live demonstration is appropriate.
5. Staff opens **Sohbetler** to show registered and external requesters, multi-ticket separation, pinning, unread state, and **Yanıt Bekleyenler**.
6. A public staff reply is delivered through Gmail; failed delivery remains visible to staff with a safe retry action.
7. The Admin opens Gmail Integration to show OAuth capability status, automatic synchronization, safe rate-limit backoff, and read-only diagnostics.
8. An inbound email from a registered customer or external requester is handled by the shared Gmail pipeline, with deduplication and attachment import.
9. A reply is correlated to its existing ticket; a reply to a resolved or closed ticket reopens it when applicable.
10. The customer view demonstrates ticket ownership, public-only messages, attachments, and notifications.

## What This Demonstrates

- JWT-based authentication and role-based access control
- Customer ownership isolation
- Ticket assignment and status workflow
- Public replies versus staff-only internal notes
- Authenticated attachment upload, listing, and preview
- In-app notifications
- Gmail OAuth, inbound processing, acknowledgment send, correlation, deduplication, and automatic reopen behavior
- External requester intake without automatic account creation
- Conversation pinning, unread state, awaiting-staff-reply state, and periodic summary refresh
- Priority management and safe public-reply delivery retry

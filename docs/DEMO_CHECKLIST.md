# SupportFlow Demo Checklist

## Preflight

- Confirm the API, web, and mobile clients point to the intended demo environment.
- Confirm `GmailBackgroundSync.Enabled` remains `false`.
- Keep all credentials, OAuth tokens, local paths, and signing material out of the demo.

## Customer

- Sign in, list/search/filter tickets, and open a ticket.
- Create one ticket only when required by the demo script.
- Add a public message and verify timeline order.
- Upload and open an attachment through the Android share/open sheet.
- Confirm Conversations and Gmail Integration are not visible.
- Open notifications/profile and sign out.

## TechnicalStaff

- Sign in and open the ticket queue.
- Verify ticket detail, assignment, status, priority, classification, SLA, and timeline as applicable.
- Send one public reply or internal note according to the demo script.
- Open Sohbetler and verify requester grouping and ticket navigation.
- Sign out.

## Admin

- Sign in and open dashboard metrics.
- Verify ticket, conversation, user, taxonomy, SLA, queue-aging, and workload views required by the demo.
- Open Gmail Integration and verify readiness only.
- When a Gmail demonstration is planned, use **Gelen Mailleri Isleme** once. Do not enable background synchronization.
- Verify outbound delivery only when Gmail authorization and quota are healthy.
- Sign out.

## Final Check

- Confirm no unexpected duplicate ticket, message, attachment, or processed-Gmail row was created.
- Confirm no browser/mobile console error or failed API request is visible.
- Record provider rate-limit events as a demo note; do not retry repeatedly.

# Final Presentation Screenshot Checklist

Use a clean `1280x900` or `1440x900` browser window at 100% zoom. Keep browser
password prompts, DevTools, error banners, raw IDs, and credentials out of frame.

| Screen | Role and route | Recommended target | Keep visible | Avoid |
| --- | --- | --- | --- | --- |
| Login | Public `/login` | None | Brand panel, Turkish form labels, login card | Autofill prompts and validation errors |
| Admin dashboard | Admin `/admin` | Tickets `2018-2021` | Metrics, professional recent ticket titles, status mix | Obvious test records |
| Ticket list | Admin `/admin/tickets` | Tickets `2018-2021` | Search/filter bar, ticket number, requester, status and priority chips | Empty or error state |
| Ticket detail | Admin `/admin/tickets/2018` | `VPN bağlantısı kurulamıyor` | Assignment, `Devam Ediyor`, public thread, visible PNG attachment | Internal-only content when demonstrating customer-facing work |
| Conversations | Admin `/admin/conversations` | `Privora Exclusive` or another active external requester | Requester chip, pin, unread marker, awaiting-reply chip, separate ticket sections | Empty selection and raw requester types |
| Customer dashboard | Customer `/customer` | Customer with prepared demo tickets | Summary cards and professional ticket titles | Empty dashboard |
| Customer ticket detail | Customer `/customer/tickets/2018` | Ticket with the presentation PNG | Status/priority, attachment thumbnail, public conversation | Internal notes and failed staff deliveries |
| Gmail integration | Admin `/admin/gmail` | Authorized development mailbox | Capability cards, automatic-sync status, latest successful check | OAuth URL, tokens, secrets, or rate-limit error unless intentionally demonstrated |
| Notifications | Admin or TechnicalStaff `/{role}/notifications` | Role with current operational notifications | Unread indicator, notification cards, ticket navigation | Empty state |

## Capture Order

1. Sign in and capture the Admin dashboard.
2. Capture the ticket list with priority filtering, then ticket `2018` detail.
3. Capture Conversations with an external requester and the filter-count toolbar.
4. Capture Gmail Integration without triggering mailbox processing.
5. Switch to Customer and capture dashboard plus the attachment ticket detail.
6. Capture Notifications only when realistic notifications are available.

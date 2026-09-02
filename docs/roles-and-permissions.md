# Roles and Permissions

This matrix reflects the currently implemented backend authorization and service behavior.

| Capability | Customer | TechnicalStaff | Admin |
| --- | --- | --- | --- |
| Register/login | Yes | Yes | Yes |
| View own profile | Yes | Yes | Yes |
| Update own profile | Yes | Yes | Yes |
| Change own password | Yes | Yes | Yes |
| List technical staff | No | Yes | Yes |
| Create ticket | Yes | No | No |
| View own tickets | Yes | Yes | Yes |
| View all tickets | No | Yes | Yes |
| View ticket details | Own tickets only | Any ticket | Any ticket |
| Add public reply | Own tickets only | Any ticket | Any ticket |
| Add internal note | No | Yes | Yes |
| View internal notes | No | Yes | Yes |
| Assign ticket | No | Yes | Yes |
| Update ticket status | No | Yes | Yes |
| View ticket history | Own tickets only | Any ticket | Any ticket |
| Upload ticket attachment | Own tickets only | Any ticket | Any ticket |
| List ticket attachments | Own tickets only | Any ticket | Any ticket |
| Download/preview attachments | Own tickets only | Any ticket | Any ticket |
| View notifications | Own notifications only | Own notifications only | Own notifications only |
| Mark notifications read | Own notifications only | Own notifications only | Own notifications only |
| Use Gmail OAuth UI | No | No | Yes |
| Trigger Gmail inbox processing | No | No | Yes |
| Receive Gmail acknowledgment success/failure notifications | No | No | Yes |

## Important Access Rules

- Customer ticket access is ownership-based. Customers only see their own tickets, their own attachments, and only non-internal ticket messages.
- Technical staff and Admin users can access all tickets through the current backend implementation.
- Internal notes are created via the normal message endpoint using `isInternal = true`, but customers cannot create or view them.
- Attachment access follows the same ownership/visibility rule as ticket access.
- Notifications are always user-scoped. A user can only read and update their own notification rows.

## Gmail-Specific Role Notes

- Gmail OAuth start, status, and mailbox processing endpoints are restricted to `Admin`.
- OAuth callback is anonymous because Google redirects back to the backend, not to an authenticated browser API call.
- Gmail inbound processing still enforces customer ownership after correlation. Even Admin-triggered processing cannot attach a reply from the wrong sender to another customer's ticket.

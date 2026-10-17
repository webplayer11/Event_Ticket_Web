# Events & approval

An event always belongs to an organization. Only active `Owner`/`Manager`
members of an active organization create, edit, submit, publish or cancel its
events. A Platform Admin approves or rejects. Every status change is recorded
in `EventApprovalHistories`.

## Status flow

```text
Draft ──submit──► PendingApproval ──approve──► Approved ──publish──► Published ──► Completed
  ▲                    │                                                      
  │                    └──reject──► Rejected ──(edit, submit)──► PendingApproval
Draft / Approved / Published ──cancel──► Cancelled
```

The table lives in `EventStatusTransitions` (Application layer); every service
checks it. A disallowed transition returns `409`.

| Status | Edit (`PUT`) | Public | Tickets on sale |
|---|---|---|---|
| `Draft`, `Rejected` | yes | no (Owner/Manager can preview) | no |
| `PendingApproval` | no | no | no |
| `Approved` | no | no | no |
| `Published` | no | yes | yes |
| `Cancelled` | no | no | no |

Policies chosen because none existed:

- **No edits after submission.** While pending, the admin is reviewing it.
  After approval, tickets may be sold and there is no change policy. Ticket
  types keep their own rules in [ticket-types.md](ticket-types.md).
- **Cancel is blocked (`409`) when any ticket type has reserved or sold
  tickets**, because there is no refund policy.
- **`Completed` has no endpoint or background job yet.** Ended events simply
  drop out of the public list.
- **There is no "pause" state.** The status enum (and its DB check
  constraint) has no such value, and none was added.

## Endpoints

| Method | Path | Who | Result |
|---|---|---|---|
| `POST` | `/api/organizations/{organizationId}/events` | Owner/Manager | `201` `Draft` event |
| `GET` | `/api/organizations/{organizationId}/events?status=` | Owner/Manager | `200` array, newest first |
| `PUT` | `/api/events/{eventId}` | Owner/Manager | `200`; `Draft`/`Rejected` only, status unchanged |
| `POST` | `/api/events/{eventId}/submit` | Owner/Manager | `Draft`/`Rejected` → `PendingApproval`, admins notified |
| `POST` | `/api/events/{eventId}/publish` | Owner/Manager | `Approved` → `Published` |
| `POST` | `/api/events/{eventId}/cancel` | Owner/Manager | → `Cancelled`; optional body `{ "reason": "..." }` |
| `GET` | `/api/events` | anyone | `200` published, not ended; filters and paging below |
| `GET` | `/api/events/{slug}` | anyone | `200` if `Published`; Owner/Manager also see other statuses; else `404` |
| `GET` | `/api/events/{eventId}` | anyone | Same as by slug; kept for existing clients |
| `GET` | `/api/admin/events?status=` | PlatformAdmin | Default `PendingApproval`, oldest first |
| `GET` | `/api/admin/events/pending-count` | PlatformAdmin | `{ "count": n }` |
| `POST` | `/api/admin/events/{eventId}/approve` | PlatformAdmin | `PendingApproval` → `Approved`; optional `{ "note": "..." }` |
| `POST` | `/api/admin/events/{eventId}/reject` | PlatformAdmin | `PendingApproval` → `Rejected`; `{ "note": "reason" }` required |
| `POST` | `/api/events` | signed in | Create and submit in one step (see below) |
| `GET` | `/api/events/mine` | signed in | Events of every org where caller is Owner/Manager |

`/api/platform-admin/events/...` still answers the same four admin routes. It
is an alias kept for clients built before `/api/admin/events`; new code should
use `/api/admin`.

`?status=` takes a status name (case-insensitive, numbers not accepted).
Anything else returns `400 { message, errors }`.

### Quick create: `POST /api/events`

This route serves the "any user can create an event" flow. It creates the
event and submits it right away (history: `Draft → PendingApproval`).

- Without `organizationId`, the event goes to the caller's **personal
  organization**, created on the first event and named after `organizerName`
  (or the user's full name). The caller becomes its `Owner`.
- With `organizationId`, the caller must be `Owner`/`Manager` there.
- At least one ticket type is required.

Creating organizations explicitly (`POST /api/organizations`) still requires
the `Organizer` system role.

## Request bodies

`POST /api/organizations/{id}/events` and `PUT /api/events/{id}`:

```json
{
  "title": "Đêm nhạc Hà Nội",
  "description": "<p>HTML từ trình soạn thảo</p>",
  "categoryId": "0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b01",
  "venueName": "Nhà hát Lớn",
  "address": "1 Tràng Tiền",
  "city": "Hà Nội",
  "startsAt": "2026-12-01T19:00:00+07:00",
  "endsAt": "2026-12-01T22:00:00+07:00",
  "ticketTypes": [
    {
      "name": "VIP",
      "description": "",
      "price": 500000,
      "totalQuantity": 50,
      "maxQuantityPerOrder": 4,
      "saleStartsAt": "2026-11-01T00:00:00+07:00",
      "saleEndsAt": "2026-12-01T18:00:00+07:00"
    }
  ]
}
```

- `ticketTypes`: `null` (or omitted) keeps the current ticket types, which can
  be managed separately via `/api/events/{id}/ticket-types`. An array replaces
  them all. Replacing is safe because `Draft`/`Rejected` events were never on
  sale.
- `POST /api/events` takes the same body plus `organizationId`,
  `organizerName` and `organizerInfo`.
- For online events, the frontend maps the meeting link to `address` and
  uses `"Online"` for `venueName`/`city`.

### Validation

These rules return `400 { message, errors[] }`:

- start in the future;
- end after start;
- unique ticket names;
- valid sale window that closes before the event ends;
- `maxQuantityPerOrder ≤ totalQuantity`.

When `ticketTypes` is `null`, the kept ticket types are rechecked against the
new dates.

Submitting additionally requires:

- at least one active ticket type;
- a start in the future;
- an active category (`400` if it was deactivated after the draft was made).

Publishing requires:

- an event that has not ended;
- at least one active ticket type.

## Public search: `GET /api/events`

| Query | Meaning |
|---|---|
| `q` | Title contains (case-insensitive) |
| `categoryId` | Category id |
| `city` | City equals (case-insensitive) |
| `from`, `to` | `startsAt` within range (ISO 8601) |
| `minPrice`, `maxPrice` | At least one active ticket type priced in range |
| `page` (default 1), `pageSize` (default 20, max 100) | Paging |

The response body is a plain array of `EventResponse`, ordered by `startsAt`.
The total match count is in the `X-Total-Count` header, which CORS exposes.

Invalid parameters return `400` `ValidationProblemDetails`. Examples:
`pageSize=0`, `minPrice > maxPrice`, `from > to`.

Query names are bound case-insensitively. Swagger lists them in PascalCase
(`PageSize`), and validation error keys are PascalCase too, as everywhere else
in this API.

Only `Published` events of active organizations that have not ended are
listed.

## Responses

- **Organization, admin and lifecycle endpoints:** return
  `ManagedEventResponse`, which is the public `EventResponse` plus
  `createdByUserId`, `createdByName`, `createdByEmail`, `lastReviewNote` and
  `lastReviewedAt`. `lastReview*` is the latest decision taken on a pending
  event (approve or reject).
- **Public endpoints:** return `EventResponse` and never include creator or
  review data.
- **Field formats:** `status` is the enum name, dates are ISO 8601 and nulls
  are written.

| Case | Status | Body |
|---|---|---|
| DataAnnotations failure | `400` | `application/problem+json` (`ValidationProblemDetails`) |
| Business validation, inactive category, bad `?status=` | `400` | `{ "message", "errors"? }` |
| No / invalid token | `401` | empty |
| Not PlatformAdmin on `/api/admin/...` | `403` | empty |
| Organization `Staff` | `403` | `{ "message" }` |
| Not a member of the organization, unknown event | `404` | `{ "message" }` |
| Disallowed transition, concurrent change (row version), cancel with tickets sold | `409` | `{ "message" }` |

Error conventions:
[event-categories.md#response-conventions-used-here](event-categories.md#response-conventions-used-here).

Error conventions that differ across modules:

- Ticket types use `{ code, message }` with `422`.
- Reservations and orders use `{ error: { code } }`.

The event API keeps the `{ message, errors? }` shape it already used. The team
should still pick one style for all modules.

## Known limitation

Reservation creation reads the event status before it locks inventory. A
reservation racing with a `cancel` can therefore slip through. This is
pre-existing behaviour of the reservation flow and is not changed here.

## Admin notification

On each submission and resubmission:

1. **In-app:** the event shows up in `pending-count` and the pending list.
2. **Email:** one BCC message goes to every active Platform Admin.
   - It reuses the `PasswordResetEmail` SMTP settings.
   - Set `EventApproval__AdminReviewUrl` to include a link to the admin review
     page.
   - If SMTP is not configured, no email is sent, but the event still appears
     in the pending list.
   - Email failures are logged and never fail the request.

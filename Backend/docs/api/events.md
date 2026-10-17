# Events & approval

Any signed-in user can create an event. Every event, including those created
by an `Organizer`, starts as `PendingApproval` and becomes public (and
saleable) only after a Platform Admin approves it.

```
create ──► PendingApproval ──approve──► Published (public, tickets on sale)
                 │   ▲
           reject│   │edit & resubmit
                 ▼   │
               Rejected
```

Every transition is recorded in `EventApprovalHistories`.

## Ownership: personal organizations

An event always belongs to an organization.

- `organizationId` omitted: the event goes to the caller's **personal
  organization**, created automatically on their first event. It is named after
  `organizerName` (falling back to the user's full name), and the caller is its
  `Owner`. Each user has at most one personal organization (unique
  `Organizations.PersonalOwnerUserId`).
- `organizationId` set: the caller must be `Owner` or `Manager` there.

Creating organizations explicitly (`POST /api/organizations`) still requires
the `Organizer` system role.

## Endpoints

| Method | Path | Who | Notes |
|---|---|---|---|
| `GET` | `/api/event-categories` | anyone | Active categories; management in [event-categories.md](event-categories.md) |
| `GET` | `/api/events` | anyone | Published, not yet ended |
| `GET` | `/api/events/{id}` | anyone | Published only; `404` otherwise |
| `POST` | `/api/events` | signed in | Creates as `PendingApproval`, notifies admins |
| `GET` | `/api/events/mine` | signed in | Events of orgs where caller is Owner/Manager, incl. `status` and `lastReviewNote` |
| `PUT` | `/api/events/{id}` | Owner/Manager | Only while `PendingApproval` or `Rejected`; a `Rejected` event goes back to `PendingApproval` and admins are notified again |
| `GET` | `/api/platform-admin/events?status=` | PlatformAdmin | Default `PendingApproval`, oldest first; unknown status name → `400` |
| `GET` | `/api/platform-admin/events/pending-count` | PlatformAdmin | `{ "count": n }` for a badge |
| `POST` | `/api/platform-admin/events/{id}/approve` | PlatformAdmin | Body optional `{ "note": "..." }` → `Published` |
| `POST` | `/api/platform-admin/events/{id}/reject` | PlatformAdmin | Body `{ "note": "reason" }` (required) → `Rejected` |

Public responses never include the creator's email or review notes.

### Create / update body

```json
{
  "organizationId": null,
  "organizerName": "Ban nhạc ABC",
  "organizerInfo": "Giới thiệu ban tổ chức",
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

For online events, the frontend maps the meeting link to `address` and uses
`"Online"` for `venueName`/`city`.

Validation (`400` with `errors[]`): start in the future, end after start, at
least one ticket type, unique ticket names, sale window valid and closing
before the event ends, `maxQuantityPerOrder ≤ totalQuantity`. On update,
ticket types are replaced entirely (safe because unpublished events have no
sales).

| Case | Response |
|---|---|
| Edit a `Published` event, approve/reject a non-pending event | `409` |
| Concurrent review/edit (row version) | `409`, reload and retry |
| Not a member of the event's organization | `404` |
| Member but `Staff` | `403` |
| Category missing or deactivated | `400` |

Error bodies follow [event-categories.md#response-conventions-used-here](event-categories.md#response-conventions-used-here):
`{ "message", "errors"? }`, or `ValidationProblemDetails` for DataAnnotations.

## Admin notification

1. **In-app:** `pending-count` and the pending list.
2. **Email:** one message (BCC) to every active Platform Admin on each new
   submission and resubmission. It reuses the `PasswordResetEmail` SMTP
   settings; set `EventApproval__AdminReviewUrl` to include a link to the
   admin review page. Without SMTP configured, nothing is emailed and the
   event still appears in the pending list. Email failures are logged and
   never fail the request.

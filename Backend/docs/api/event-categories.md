# Event categories

| Method | Path | Who | Success |
|---|---|---|---|
| `GET` | `/api/event-categories` | anyone | `200` array of active categories |
| `GET` | `/api/event-categories?includeInactive=true` | PlatformAdmin | `200` all categories (others: `403`) |
| `POST` | `/api/event-categories` | PlatformAdmin | `201` category |
| `PUT` | `/api/event-categories/{id}` | PlatformAdmin | `200` category |
| `PATCH` | `/api/event-categories/{id}/status` | PlatformAdmin | `200` category |

```json
{
  "id": "0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b01",
  "name": "Âm nhạc",
  "slug": "am-nhac",
  "description": "",
  "isActive": true
}
```

## Requests

`POST`: `name` (2–100), optional `slug`, optional `description` (≤ 1000).
When `slug` is empty it is generated from the name (`"Âm nhạc dân tộc"` →
`"am-nhac-dan-toc"`).

`PUT`: `name`, `slug` (required) and `description`.

`PATCH .../status`: `{ "isActive": false }` (`isActive` is required).
Sending the current state again returns `200`.

`slug` must match `^[a-z0-9]+(?:-[a-z0-9]+)*$`, max 150 characters, and be
unique across all categories (active or not).

## Deactivation policy

- **Blocked (`409`)** while the category has `Published` events that have not
  ended yet (tickets may still be on sale). The `errors` array states how many.
- Events that are pending, rejected or already ended do not block it.
- A deactivated category is hidden from the public list and cannot be chosen
  when creating or editing an event (`400`). Events already using it keep it,
  and pending events can still be approved.

## Errors

| Case | Status | Body |
|---|---|---|
| Body fails DataAnnotations (name length, slug format, missing `isActive`) | `400` | `application/problem+json` (`ValidationProblemDetails`) |
| Name yields no slug and none was given | `400` | `{ "message", "errors" }` |
| No / invalid token | `401` | empty |
| Not PlatformAdmin | `403` | empty (`{ "message" }` for `includeInactive`) |
| Unknown id | `404` | `{ "message" }` |
| Duplicate slug, deactivation blocked | `409` | `{ "message" }` / `{ "message", "errors" }` |

## Response conventions used here

No `AGENTS.md` or rule file is committed in this repository (`RULES_CODEX.md`
and `docs/project-context/*` are git-ignored, local only). These conventions
are taken from the existing controllers and `docs/api/*.md`, not newly
introduced:

- No response wrapper, middleware or exception filter: success returns the DTO
  itself; lists are plain JSON arrays. There is no pagination DTO in the
  codebase, so none is used. The one paged list (`GET /api/events`) keeps the
  plain array body and returns the total in the `X-Total-Count` header.
- camelCase properties (`Program.cs`); enum values in responses are their
  string names; dates are ISO 8601 `DateTimeOffset`; nulls are written.
- Business errors in admin/account/organization/event APIs:
  `{ "message": "..." }`, plus `"errors": ["..."]` when there are several
  reasons (same as `POST /api/auth/register`). The C# type `MessageResponse`
  names exactly this shape for Swagger.
- Transaction APIs (reservations, orders, check-in) use
  `{ "error": { "code": "UPPER_SNAKE" } }` instead. Two styles coexist; the
  team should pick one before the next public API.
- Automatic validation errors keep ASP.NET Core's `ValidationProblemDetails`;
  keys in `errors` are PascalCase property names (framework default).

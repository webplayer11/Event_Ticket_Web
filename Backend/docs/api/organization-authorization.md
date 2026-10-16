# Organization & check-in authorization

Organization roles live in `OrganizationMembers` and are checked per request
through `IOrganizationAuthorizer` (never embedded in the JWT). A membership
counts only when both the member and the organization are active. Callers
outside the organization get `404`, not `403`, so resource existence is not
leaked.

## Creating an organization

`POST /api/organizations` requires the `Organizer` **system** role (policy
`Organizer`), granted by a Platform Admin:

```http
PUT /api/platform-admin/users/{userId}/system-role
{ "role": "Organizer" }
```

The role change rotates the user's `SecurityStamp`, so they must log in again
to receive the new claim. `Customer` and `PlatformAdmin` get `403`.
The creator becomes the organization's `Owner`. Revoking `Organizer` later
only blocks creating *new* organizations; existing memberships are untouched.
Buying tickets only needs an authenticated account, so Organizers can still
book.

## Permission matrix

Defined once in `OrganizationPermissions`.

| Action | Owner | Manager | Staff |
|---|:-:|:-:|:-:|
| View organization | ✔ | ✔ | ✔ |
| Update organization, list members | ✔ | ✔ | |
| Add member, change role, activate/deactivate, transfer ownership | ✔ | | |
| Assign / revoke event check-in staff | ✔ | ✔ | |
| Validate / check in tickets | needs assignment | needs assignment | needs assignment |

Exactly one active `Owner` per organization is enforced by the filtered unique
index `IX_OrganizationMembers_SingleActiveOwner`
(`[Role] = 1 AND [IsActive] = 1`).

## Member management (Owner only)

### `PUT /api/organizations/{organizationId}/members/{memberId}/role`

```json
{ "role": 2 }
```

`2` = Manager, `3` = Staff. The Owner cannot be changed here (`409`); use
ownership transfer.

### `PUT /api/organizations/{organizationId}/members/{memberId}/status`

```json
{ "isActive": false }
```

Deactivating a member also revokes all of their event check-in assignments.
Reactivating does not restore old assignments. The Owner cannot be
deactivated (`409`).

### `POST /api/organizations/{organizationId}/transfer-ownership`

```json
{ "newOwnerMemberId": "<member id>" }
```

The target must be another active member whose account is active. In one
transaction the current Owner becomes `Manager` and the target becomes
`Owner`. A concurrent transfer that loses the race returns `409`.

## Event check-in staff (Owner or Manager)

Base route: `/api/events/{eventId}/staff`

| Method | Path | Body |
|---|---|---|
| `GET` | `/` | |
| `POST` | `/` | `{ "organizationMemberId": "<member id>" }` |
| `DELETE` | `/{assignmentId}` | |

The member must be active and belong to the organization that owns the event.
Revoking sets `IsActive = false` (rows are kept because `CheckIns` reference
them); assigning the same member again reactivates the row.

## Check-in

Base route: `/api/events/{eventId}/check-in`. The caller must hold an active
assignment for **this** event. That applies to Owners and Managers too; they
assign themselves first.

| Method | Path | Body |
|---|---|---|
| `POST` | `/validate` | `{ "qrToken": "..." }` |
| `POST` | `/` | `{ "qrToken": "...", "gateName": "Gate A", "note": null }` |

| Case | Response | Code |
|---|---|---|
| No active assignment for the event | `403` | `CHECK_IN_NOT_ASSIGNED` |
| QR unknown or belongs to another event | `404` | `TICKET_NOT_FOUND` |
| Ticket already checked in (including concurrent scans) | `409` | `TICKET_ALREADY_CHECKED_IN` |
| Ticket cancelled | `422` | `TICKET_NOT_VALID` |

The customer email in the response is masked (`cu***@example.com`).

## My tickets

`GET /api/tickets/mine`: tickets from the caller's paid orders. Raw QR tokens
are never returned in this list.

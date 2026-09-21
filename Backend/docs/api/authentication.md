# Authentication and account lifecycle

Base route: `/api/auth`.

The API uses ASP.NET Core Identity with bearer JWTs. Access tokens currently
expire after the configured `Jwt:AccessTokenMinutes` (15 minutes by default).
Every authenticated request reloads the user and rejects the token when the
account is inactive, locked, missing, or its `SecurityStamp` no longer matches.

## Public endpoints

### `POST /api/auth/register`

```json
{
  "fullName": "Example User",
  "email": "user@example.com",
  "password": "Strong!Pass123",
  "confirmPassword": "Strong!Pass123"
}
```

Creates an active Identity account and atomically assigns the `Customer`
system role. Identity normalizes the email, hashes the password, and enforces
the configured password policy. The API does not require email confirmation.

Success: `201 Created`. Duplicate and invalid requests return `400` without
confirming that a particular email already exists.

### `POST /api/auth/login`

```json
{
  "email": "user@example.com",
  "password": "Strong!Pass123"
}
```

Success: `200 OK` with `accessToken`, `tokenType`, `expiresAt`, and `user`.
The JWT contains `sub`, `jti`, `iat`, email, system role claims, and a
`security_stamp` claim. Wrong credentials, unknown users, inactive users, and
locked users all receive the same `401 Unauthorized` response.

### `POST /api/auth/forgot-password`

```json
{
  "email": "user@example.com"
}
```

Success: `202 Accepted` with the same neutral message for known and unknown
accounts. For an active account, Identity generates its password-reset token
and the SMTP adapter sends a reset link. Tokens and recipient addresses are
not logged. Delivery failures are recorded without the token or address and
the public response remains neutral to prevent account enumeration.

### `POST /api/auth/reset-password`

```json
{
  "email": "user@example.com",
  "token": "identity-reset-token",
  "newPassword": "New!Strong123",
  "confirmPassword": "New!Strong123"
}
```

Identity validates the one-hour reset token and password policy. A successful
password hash change rotates `SecurityStamp`, invalidating existing JWTs and
preventing reset-token reuse. Invalid/expired/used tokens return a generic
`400 Bad Request`.

## Authenticated account endpoints

All endpoints below require `Authorization: Bearer <accessToken>`.

### `GET /api/auth/me`

Returns only public account data:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "fullName": "Example User",
  "email": "user@example.com",
  "phoneNumber": "+84901234567",
  "systemRoles": ["Customer"],
  "createdAt": "2026-09-20T00:00:00+00:00"
}
```

Password hashes, security/concurrency stamps, lockout data, and internal
tokens are never returned.

### `PUT /api/auth/me`

```json
{
  "fullName": "Updated Name",
  "phoneNumber": "+84901234567"
}
```

Only `fullName` and `phoneNumber` are allowlisted. The client cannot update
IDs, email confirmation, activity status, roles, lockout fields, password
metadata, or organization membership through this contract.

### `POST /api/auth/change-password`

```json
{
  "currentPassword": "Strong!Pass123",
  "newPassword": "New!Strong123",
  "confirmPassword": "New!Strong123"
}
```

Requires the current password and the existing Identity password policy.
Success rotates `SecurityStamp`; all existing JWTs must be replaced by a new
login.

### `POST /api/auth/logout`

Success: `204 No Content`.

The current architecture has no refresh token or per-device session store.
Logout therefore rotates `SecurityStamp` and revokes all JWTs for the account.
It is intentionally not described as a current-device-only logout. There is
no in-memory blacklist.

## Platform Admin endpoints

Base route: `/api/platform-admin/users`. These endpoints require the
`PlatformAdmin` policy, backed by the Identity `PlatformAdmin` system role.

### `PUT /api/platform-admin/users/{userId}/system-role`

```json
{
  "role": "PlatformAdmin"
}
```

Allowed values are `Customer` and `PlatformAdmin`. A Platform Admin cannot
change their own system role. A successful role change rotates the target
user's `SecurityStamp`, so stale role claims stop working immediately.

### `PUT /api/platform-admin/users/{userId}/status`

```json
{
  "isActive": false
}
```

`IsActive=false` is long-term administrative deactivation. It is distinct
from Identity lockout, which remains the temporary brute-force protection.
The caller cannot change their own status. Every status change rotates the
target user's `SecurityStamp`.

## Role boundaries

- System roles: `Customer`, `PlatformAdmin` in `AspNetRoles` / `AspNetUserRoles`.
- Organization roles: `Owner`, `Manager`, `Staff` in `OrganizationMembers`.

Organization roles never produce a `PlatformAdmin` JWT claim and never grant
cross-organization access. Organization authorization continues to query
current membership at resource scope rather than embedding memberships in JWTs.

Migration `20260920150000_SeedSystemRoles` inserts the two system roles. It
does not create an initial Platform Admin account. The first Platform Admin
must be provisioned through an approved deployment/operations process; the
public API never self-promotes a user.

## Email configuration

The SMTP adapter reads `PasswordResetEmail` configuration. Environment
variables use the standard double-underscore form:

```text
PasswordResetEmail__Host
PasswordResetEmail__Port
PasswordResetEmail__EnableSsl
PasswordResetEmail__UserName
PasswordResetEmail__Password
PasswordResetEmail__FromAddress
PasswordResetEmail__FromName
PasswordResetEmail__ResetUrl
```

`ResetUrl` must be an absolute frontend URL. Secrets belong in environment or
a secret manager, not JSON configuration or source control. Multi-instance
deployments must also share the ASP.NET Core Data Protection key ring so a
reset token generated by one instance can be consumed by another.

## Deferred product decisions

- Avatar storage is deferred because the backlog names the feature but does
  not define whether avatars use `StoredFile`, an external URL, privacy rules,
  upload validation, or lifecycle cleanup.
- Event preferences are deferred because no user-preference entity or category
  selection contract exists yet.
- Auth/admin audit persistence is deferred because the backend has no general
  append-only audit infrastructure. A one-off auth-only audit table was not
  introduced.

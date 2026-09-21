# EventGO Create Reservation idempotency

`POST /api/reservations` requires the HTTP header:

```http
Idempotency-Key: <unique-logical-request-id>
```

The key is trimmed, must not be empty, and may contain at most 128 characters.
Its scope is the authenticated user, so the database identity of an operation is
`(UserId, IdempotencyKey)`.

- A retry with the same key and the same `EventId`/items returns the existing
  Reservation and does not reserve inventory again.
- Reusing the same key with a different `EventId`, `TicketTypeId`, or quantity
  returns HTTP `409` with error code `IDEMPOTENCY_KEY_REUSED`.
- A missing, empty, or oversized key returns HTTP `400` with error code
  `INVALID_IDEMPOTENCY_KEY`.

Item order does not affect request identity. The backend canonicalizes items by
`TicketTypeId` and stores a SHA-256 request fingerprint. Inventory availability
for different logical requests remains controlled by `dbo.ReserveInventory`.

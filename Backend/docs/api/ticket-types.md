# Ticket types

Routes follow the existing unwrapped DTO / `{ code, message }` API conventions.
POST/PUT/PATCH require authentication and a current active Owner/Manager
membership in the active organization owning the event. System roles alone
never grant access. Cross-organization resources return 404; Staff gets 403.

| Method | Route | Success |
|---|---|---|
| POST | /api/events/{eventId}/ticket-types | 201, TicketTypeResponse |
| GET | /api/events/{eventId}/ticket-types | 200, TicketTypeResponse[] |
| PUT | /api/ticket-types/{ticketTypeId} | 200, TicketTypeResponse |
| PATCH | /api/ticket-types/{ticketTypeId}/status | 200, TicketTypeResponse |

POST and PUT body (no inventory counters or ownership fields accepted):

```json
{
  "name": "VIP", "description": "VIP admission", "price": 250000,
  "currency": "VND", "totalQuantity": 100, "maxQuantityPerOrder": 4,
  "saleStartsAt": "2026-11-01T00:00:00Z",
  "saleEndsAt": "2026-11-10T00:00:00Z"
}
```

PATCH body: `{ "isActive": false }`.
Response contains id, eventId, name, description, price, currency,
totalQuantity, availableQuantity, maxQuantityPerOrder, saleStartsAt,
saleEndsAt, isActive. ReservedQuantity, SoldQuantity and RowVersion are private.

GET is public only for Published events in active organizations; anonymous
callers see only active ticket types. Owner/Manager can also view unpublished
events and inactive ticket types. No draft data leaks to other callers.

Policy decisions: sale end must be after sale start and no later than event
start (no late admission sales policy exists). Event must not have started.
Draft/Rejected/Approved/Published events support ticket management; pending
review, cancelled and completed events are immutable. New types are active,
but reservations still enforce Published status and the sale window.
Activation is allowed before sale start, never after sale end.
Price/currency changes are allowed only before the original AND proposed sale
start, with no reservation/order history or committed inventory. No snapshots
are rewritten. Currency is uniform across an event's ticket types.
TotalQuantity > 0, 0 < MaxQuantityPerOrder <= TotalQuantity, nonnegative price
with at most 2 decimals and decimal(18,2) range. Empty/oversize names are rejected.

Inventory comes from TicketTypes.TotalQuantity - ReservedQuantity - SoldQuantity.
Expired holds remain conservatively reserved until the existing expiration
worker releases them; GET never invents stock by subtracting expired holds.
SQL Server UPDLOCK locks on ticket types, acquired in ID order by reservation
creation, serialize mutations and snapshot reads with the existing inventory
procedures. Quantity cannot drop below sold + reserved. Disabling a type does
not alter existing holds, orders or payments.

Errors: 400 binding validation; 401 invalid authentication; 403 insufficient
membership permission; 404 TICKET_TYPE_NOT_FOUND/EVENT_NOT_FOUND;
409 TICKET_TYPE_CONFLICT (duplicate name/concurrent write), PRICE_LOCKED,
INVENTORY_COMMITTED, EVENT_NOT_EDITABLE; 422 INVALID_TICKET_TYPE,
CURRENCY_MISMATCH, SALE_WINDOW_CLOSED. Errors contain code and message.

FE_CONTRACT_IMPACT: four additive routes. Existing reservation/order/payment
contracts remain unchanged. Clients use availableQuantity from the response.

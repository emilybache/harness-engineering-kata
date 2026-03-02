# Warehouse Desk Kata (Java)

This is a small starter codebase for a code kata.  
It models a simple warehouse desk flow with command-style input (`RECV`, `SELL`, `CANCEL`, `COUNT`, `DUMP`).

## Why this is good for a kata

- Procedural style with mixed responsibilities in one class.
- Mutable state spread across multiple maps and lists.
- Read/write dependencies between inventory, orders, and cash updates.
- Credible domain behavior but intentionally not high quality.

## Run

```bash
mvn -q compile
java -cp target/classes com.kata.warehouse.Main
```

## Missing feature to implement in the kata

### Feature: Stock reservation with expiry

Today, the app tracks a `reservedBySku` map but never actually reserves stock.  
Implement a full reservation flow:

1. Add command: `RESERVE;<customer>;<sku>;<qty>;<minutes>`
2. Reserve only if enough available stock exists.
3. Add command: `CONFIRM;<reservationId>` to convert reservation into a shipped order.
4. Add command: `RELEASE;<reservationId>` to release stock manually.
5. Reservations should expire automatically based on the configured minutes, returning stock to availability.

### Why it is not trivial

- Affects availability checks in `SELL` and `COUNT`.
- Needs consistent updates across shared mutable state.
- Requires introducing reservation records with status and time handling.
- Must avoid corrupting cash and order state when confirm/release/expire interleave.
